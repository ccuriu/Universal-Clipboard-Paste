using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

[assembly: AssemblyTitle("Universal Clipboard Paste")]
[assembly: AssemblyProduct("Universal Clipboard Paste")]
[assembly: AssemblyDescription("Smart clipboard hotkey: text as file, files and images passthrough")]
[assembly: AssemblyCompany("Universal Clipboard Paste")]
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const string AppVersion = "1.2.1";
    const int WM_HOTKEY = 0x0312;
    const int HOTKEY_ID = 0x5347;
    const uint MOD_CONTROL = 0x0002;
    const uint MOD_SHIFT = 0x0004;
    const uint MOD_NOREPEAT = 0x4000;

    const int VK_CONTROL = 0x11;
    const int VK_SHIFT = 0x10;
    const int VK_LSHIFT = 0xA0;
    const int VK_RSHIFT = 0xA1;
    const int VK_LCONTROL = 0xA2;
    const int VK_RCONTROL = 0xA3;
    const int VK_V = 0x56;
    const uint INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint CF_UNICODETEXT = 13;
    const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    static extern uint SendInput(uint nInputs, INPUT[] inputs, int cbSize);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll")]
    static extern bool CloseClipboard();

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll")]
    static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalFree(IntPtr memory);
    [DllImport("ole32.dll")]
    static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    static extern void OleUninitialize();

    [DllImport("ole32.dll")]
    static extern int OleSetClipboard([MarshalAs(UnmanagedType.Interface)] ComIDataObject dataObject);

    [DllImport("ole32.dll")]
    static extern int OleFlushClipboard();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHParseDisplayName(string name, IntPtr bindContext, out IntPtr pidl,
        uint attributesIn, out uint attributesOut);

    [DllImport("shell32.dll")]
    static extern IntPtr ILClone(IntPtr pidl);

    [DllImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ILRemoveLastID(IntPtr pidl);

    [DllImport("shell32.dll")]
    static extern IntPtr ILFindLastID(IntPtr pidl);

    [DllImport("shell32.dll")]
    static extern int SHCreateDataObject(IntPtr pidlFolder, uint count, IntPtr[] childPidls,
        IntPtr innerDataObject, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out ComIDataObject dataObject);

    [DllImport("ole32.dll")]
    static extern void CoTaskMemFree(IntPtr pointer);
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }
    static readonly string BaseDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniversalClipboardPaste");
    static readonly string PayloadDir =
        Path.Combine(Path.GetTempPath(), "UniversalClipboardPaste");
    static readonly string LogPath = Path.Combine(BaseDir, "hotkey.log");
    static int Busy;
    static int PayloadSequence;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
        Directory.CreateDirectory(PayloadDir);
        CleanupPayloads(TimeSpan.Zero);

        if (!RegisterHotKey(Handle, HOTKEY_ID,
            MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_V))
            throw new InvalidOperationException("Cannot register Ctrl+Shift+V.");

        Log("REGISTER_OK VERSION=" + AppVersion);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            if (Interlocked.CompareExchange(ref Busy, 1, 0) != 0)
            {
                Log("HOTKEY_SKIPPED_BUSY");
                return;
            }
            var worker = new Thread(() =>
            {
                try { SmartPaste(); }
                catch (Exception ex) { Log("SMART_PASTE_ERROR " + ex.GetType().Name + ": " + ex.Message); }
                finally { Interlocked.Exchange(ref Busy, 0); }
            });

            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            return;
        }

        base.WndProc(ref m);
    }

    static void SmartPaste()
    {
        string kind = DetectClipboardKind();

        if (kind == "text")
        {
            AttachClipboardTextAsFile();
            return;
        }

        if (kind == "empty")
        {
            Log("CLIPBOARD_EMPTY");
            return;
        }

        PassthroughClipboard(kind);
    }

    static string DetectClipboardKind()
    {
        for (int i = 0; i < 8; i++)
        {
            try
            {
                IDataObject data = Clipboard.GetDataObject();
                if (data == null) return "empty";

                if (data.GetDataPresent(DataFormats.FileDrop, false))
                    return "files";

                if (Clipboard.ContainsImage() ||
                    data.GetDataPresent(DataFormats.Bitmap, false))
                    return "image";

                if (data.GetDataPresent(DataFormats.UnicodeText, false) ||
                    data.GetDataPresent(DataFormats.Text, false))
                    return "text";

                return "other";
            }
            catch (ExternalException)
            {
                Thread.Sleep(10);
            }
        }

        return "other";
    }

    static void PassthroughClipboard(string kind)
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 20; i++)
        {
            if (!IsModifierDown()) break;
            Thread.Sleep(5);
        }

        ReleaseCtrlShift();
        Thread.Sleep(5);

        uint sent = SendPaste();
        bool fallback = sent != 4;
        if (fallback) LegacyPaste();

        sw.Stop();
        Log("PASSTHROUGH_SENT kind=" + kind +
            " count=" + sent +
            " fallback=" + fallback +
            " total_ms=" + sw.ElapsedMilliseconds);
    }

    static void AttachClipboardTextAsFile()
    {
        var total = Stopwatch.StartNew();

        string text;
        if (!TryGetClipboardText(out text) || String.IsNullOrEmpty(text))
        {
            Log("CLIPBOARD_NO_TEXT");
            return;
        }

        CleanupPayloads(TimeSpan.FromSeconds(15));
        string filePath = CreatePayload(text);
        for (int i = 0; i < 20; i++)
        {
            if (!IsModifierDown()) break;
            Thread.Sleep(5);
        }

        ReleaseCtrlShift();
        Thread.Sleep(5);

        int oleHr = OleInitialize(IntPtr.Zero);
        bool oleInitialized = oleHr >= 0;
        ComIDataObject shellObject = null;

        try
        {
            var shellWatch = Stopwatch.StartNew();
            shellObject = CreateShellDataObject(filePath);
            int setHr = OleSetClipboard(shellObject);
            if (setHr < 0) Marshal.ThrowExceptionForHR(setHr);
            int flushHr = OleFlushClipboard();
            if (flushHr < 0) Marshal.ThrowExceptionForHR(flushHr);
            shellWatch.Stop();

            Thread.Sleep(25);
            uint sent = SendPaste();
            bool fallback = sent != 4;
            if (fallback) LegacyPaste();

            Thread.Sleep(120);
            bool restored = TryRestoreClipboardTextFast(text);
            total.Stop();
            Log("ATTACH_SENT file=" + Path.GetFileName(filePath) +
                " bytes=" + new FileInfo(filePath).Length +
                " shell_ms=" + shellWatch.ElapsedMilliseconds +
                " count=" + sent +
                " fallback=" + fallback +
                " restored=" + restored +
                " total_ms=" + total.ElapsedMilliseconds);

            ScheduleDelete(filePath);
        }
        finally
        {
            GC.KeepAlive(shellObject);
            if (shellObject != null && Marshal.IsComObject(shellObject))
            {
                try { Marshal.FinalReleaseComObject(shellObject); }
                catch { }
            }

            if (oleInitialized) OleUninitialize();
        }
    }

    static string CreatePayload(string text)
    {
        Directory.CreateDirectory(PayloadDir);
        int seq = Interlocked.Increment(ref PayloadSequence);
        string name = "clipboard_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") +
            "_" + seq.ToString("D3") + ".txt";
        string path = Path.Combine(PayloadDir, name);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }
    static ComIDataObject CreateShellDataObject(string filePath)
    {
        IntPtr fullPidl = IntPtr.Zero;
        IntPtr parentPidl = IntPtr.Zero;

        try
        {
            uint attrs;
            int hr = SHParseDisplayName(filePath, IntPtr.Zero, out fullPidl, 0, out attrs);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);

            parentPidl = ILClone(fullPidl);
            if (parentPidl == IntPtr.Zero)
                throw new InvalidOperationException("ILClone failed.");

            if (!ILRemoveLastID(parentPidl))
                throw new InvalidOperationException("ILRemoveLastID failed.");

            IntPtr childPidl = ILFindLastID(fullPidl);
            Guid iid = new Guid("0000010e-0000-0000-C000-000000000046");
            ComIDataObject dataObject;

            hr = SHCreateDataObject(parentPidl, 1, new IntPtr[] { childPidl },
                IntPtr.Zero, ref iid, out dataObject);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);

            return dataObject;
        }
        finally
        {
            if (parentPidl != IntPtr.Zero) CoTaskMemFree(parentPidl);
            if (fullPidl != IntPtr.Zero) CoTaskMemFree(fullPidl);
        }
    }

    static bool TryGetClipboardText(out string text)
    {
        text = null;

        for (int i = 0; i < 8; i++)
        {
            try
            {
                if (!Clipboard.ContainsText(TextDataFormat.UnicodeText)) return false;
                text = Clipboard.GetText(TextDataFormat.UnicodeText);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(10);
            }
        }

        return false;
    }

    static bool TryRestoreClipboardTextFast(string text)
    {
        for (int i = 0; i < 40; i++)
        {
            if (TrySetUnicodeClipboard(text)) return true;
            Thread.Sleep(10);
        }

        return false;
    }

    static bool TrySetUnicodeClipboard(string text)
    {
        int bytes = (text.Length + 1) * 2;
        IntPtr memory = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
        if (memory == IntPtr.Zero) return false;

        IntPtr target = GlobalLock(memory);
        if (target == IntPtr.Zero)
        {
            GlobalFree(memory);
            return false;
        }

        try
        {
            byte[] data = Encoding.Unicode.GetBytes(text + "\0");
            Marshal.Copy(data, 0, target, data.Length);
        }
        finally
        {
            GlobalUnlock(memory);
        }

        if (!OpenClipboard(IntPtr.Zero))
        {
            GlobalFree(memory);
            return false;
        }

        try
        {
            if (!EmptyClipboard())
                return false;

            if (SetClipboardData(CF_UNICODETEXT, memory) == IntPtr.Zero)
                return false;

            memory = IntPtr.Zero;
            return true;
        }
        finally
        {
            CloseClipboard();
            if (memory != IntPtr.Zero) GlobalFree(memory);
        }
    }

    static void CleanupPayloads(TimeSpan minimumAge)
    {
        try
        {
            Directory.CreateDirectory(PayloadDir);
            DateTime cutoff = DateTime.UtcNow - minimumAge;

            foreach (string file in Directory.GetFiles(PayloadDir, "clipboard_*.txt"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) <= cutoff)
                        File.Delete(file);
                }
                catch { }
            }
        }
        catch { }
    }

    static void ScheduleDelete(string filePath)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            Thread.Sleep(60000);
            try { File.Delete(filePath); }
            catch { }
        });
    }
    static bool IsModifierDown()
    {
        return IsDown(VK_CONTROL) || IsDown(VK_SHIFT) ||
               IsDown(VK_LCONTROL) || IsDown(VK_RCONTROL) ||
               IsDown(VK_LSHIFT) || IsDown(VK_RSHIFT);
    }

    static bool IsDown(int vk)
    {
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    static void ReleaseCtrlShift()
    {
        INPUT[] release = new INPUT[]
        {
            Key(VK_SHIFT, true),
            Key(VK_LSHIFT, true),
            Key(VK_RSHIFT, true),
            Key(VK_CONTROL, true),
            Key(VK_LCONTROL, true),
            Key(VK_RCONTROL, true)
        };

        uint sent = SendInput((uint)release.Length, release, Marshal.SizeOf(typeof(INPUT)));
        if (sent != release.Length)
        {
            keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_LSHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_RSHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_LCONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_RCONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    static uint SendPaste()
    {
        INPUT[] paste = new INPUT[]
        {
            Key(VK_CONTROL, false),
            Key(VK_V, false),
            Key(VK_V, true),
            Key(VK_CONTROL, true)
        };

        return SendInput((uint)paste.Length, paste, Marshal.SizeOf(typeof(INPUT)));
    }

    static void LegacyPaste()
    {
        keybd_event((byte)VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event((byte)VK_V, 0, 0, UIntPtr.Zero);
        keybd_event((byte)VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    static INPUT Key(int vk, bool keyUp)
    {
        INPUT input = new INPUT();
        input.type = INPUT_KEYBOARD;
        input.U.ki.wVk = (ushort)vk;
        input.U.ki.dwFlags = keyUp ? KEYEVENTF_KEYUP : 0;
        return input;
    }

    static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);
            File.AppendAllText(LogPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") +
                text + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }

    public void Dispose()
    {
        UnregisterHotKey(Handle, HOTKEY_ID);
        DestroyHandle();
    }
}
static class Program
{
    [STAThread]
    static void Main()
    {
        Directory.CreateDirectory(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UniversalClipboardPaste"));

        bool created;
        using (var mutex = new Mutex(true, "Local\\UniversalClipboardPaste", out created))
        {
            if (!created) return;

            Application.EnableVisualStyles();
            using (var window = new HotkeyWindow())
                Application.Run();
        }
    }
}