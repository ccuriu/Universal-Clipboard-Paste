using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const string AppVersion = "1.0.0";
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
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

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
            "ChatGPTClipboardFilePaste");

    static readonly string LogPath = Path.Combine(BaseDir, "hotkey.log");
    static int Busy;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());

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
                try { PasteToActiveField(); }
                finally { Interlocked.Exchange(ref Busy, 0); }
            });

            worker.IsBackground = true;
            worker.Start();
            return;
        }

        base.WndProc(ref m);
    }

    static void PasteToActiveField()
    {
        var sw = Stopwatch.StartNew();
        IntPtr target = GetForegroundWindow();

        for (int i = 0; i < 20; i++)
        {
            if (!IsModifierDown()) break;
            Thread.Sleep(5);
        }

        ReleaseCtrlShift();
        Thread.Sleep(10);

        INPUT[] paste = new INPUT[]
        {
            Key(VK_CONTROL, false),
            Key(VK_V, false),
            Key(VK_V, true),
            Key(VK_CONTROL, true)
        };

        uint sent = SendInput((uint)paste.Length, paste, Marshal.SizeOf(typeof(INPUT)));
        bool fallback = false;

        if (sent != paste.Length)
        {
            fallback = true;
            LegacyPaste();
        }

        sw.Stop();

        string title = WindowTitle(target);
        Log("PASTE_SENT count=" + sent + " fallback=" + fallback +
            " elapsed_ms=" + sw.ElapsedMilliseconds + " target=" + title);
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

        Thread.Sleep(5);
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

    static string WindowTitle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return "<none>";

        var sb = new StringBuilder(256);
        try { GetWindowText(hWnd, sb, sb.Capacity); }
        catch { return "<error>"; }

        string s = sb.ToString().Replace("\r", " ").Replace("\n", " ");
        return s.Length <= 120 ? s : s.Substring(0, 120);
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
                "ChatGPTClipboardFilePaste"));

        bool created;
        using (var mutex = new Mutex(true, "Local\\ChatGPTClipboardFilePaste", out created))
        {
            if (!created) return;

            Application.EnableVisualStyles();
            using (var window = new HotkeyWindow())
                Application.Run();
        }
    }
}