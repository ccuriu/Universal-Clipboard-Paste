using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const string AppVersion = "0.3.0";
    const int WM_HOTKEY = 0x0312, HOTKEY_ID = 0x5347;
    const uint MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_NOREPEAT = 0x4000;
    const int VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_V = 0x56;
    const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 0x0002;
    const int BM_CLICK = 0x00F5;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd,int id,uint modifiers,uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd,int id);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] p, int cb);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd,StringBuilder s,int n);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd,StringBuilder s,int n);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool SetWindowText(IntPtr hWnd,string text);
    [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr hDlg,int id);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd,int msg,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent,EnumProc cb,IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb,IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd,out uint processId);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd,int cmd);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int X,int Y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool SetPhysicalCursorPos(int X,int Y);
    [DllImport("user32.dll")] static extern bool GetPhysicalCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd,out RECT r);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd,out RECT r);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPhysicalPoint(POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hWnd,uint flags);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
    delegate bool EnumProc(IntPtr hWnd,IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk,wScan; public uint dwFlags,time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left,Top,Right,Bottom; }

    static readonly string BaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPTClipboardFilePaste");
    static readonly string LogPath = Path.Combine(BaseDir,"hotkey.log");
    static int Busy=0;

    static void Log(string s)
    {
        try { File.AppendAllText(LogPath,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ")+s+Environment.NewLine,Encoding.UTF8); } catch {}
    }

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
        if(!RegisterHotKey(Handle,HOTKEY_ID,MOD_CONTROL|MOD_SHIFT|MOD_NOREPEAT,VK_V))
            throw new InvalidOperationException("Cannot register Ctrl+Shift+V.");
        Log("REGISTER_OK VERSION="+AppVersion+" HWND="+Handle.ToInt64());
    }

    protected override void WndProc(ref Message m)
    {
        if(m.Msg==WM_HOTKEY && m.WParam.ToInt32()==HOTKEY_ID)
        {
            Log("HOTKEY");
            if(Interlocked.CompareExchange(ref Busy,1,0)!=0)
            {
                Log("HOTKEY_SKIPPED_BUSY");
                return;
            }
            var t=new Thread(() =>
            {
                try { PasteClipboardAsFile(); }
                finally { Interlocked.Exchange(ref Busy,0); }
            });
            t.IsBackground=true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            return;
        }
        base.WndProc(ref m);
    }

    static bool IsDown(int vk) { return (GetAsyncKeyState(vk)&0x8000)!=0; }

    static void ReleaseModifiers()
    {
        INPUT[] a=new INPUT[] {
            Key(VK_SHIFT,true), Key(VK_LSHIFT,true), Key(VK_RSHIFT,true),
            Key(VK_CONTROL,true), Key(VK_LCONTROL,true), Key(VK_RCONTROL,true)
        };
        SendInput((uint)a.Length,a,Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(35);
        SendInput((uint)a.Length,a,Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(35);
        Log("MODIFIERS_RELEASED SHIFT="+IsDown(VK_SHIFT)+" CTRL="+IsDown(VK_CONTROL));
    }

    static void NormalizeModifiersHard()
    {
        ReleaseModifiers();
        if(IsDown(VK_SHIFT) || IsDown(VK_CONTROL))
        {
            INPUT[] cycle=new INPUT[] {
                Key(VK_SHIFT,false), Key(VK_SHIFT,true),
                Key(VK_CONTROL,false), Key(VK_CONTROL,true),
                Key(VK_LSHIFT,true), Key(VK_RSHIFT,true),
                Key(VK_LCONTROL,true), Key(VK_RCONTROL,true)
            };
            SendInput((uint)cycle.Length,cycle,Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(70);
            ReleaseModifiers();
        }
        Log("MODIFIERS_NORMALIZED SHIFT="+IsDown(VK_SHIFT)+" CTRL="+IsDown(VK_CONTROL));
    }

    static IntPtr HideWindowsInputHost()
    {
        IntPtr found=IntPtr.Zero;
        EnumProc cb=(h,l) =>
        {
            if(!IsWindowVisible(h)) return true;
            uint pid;
            GetWindowThreadProcessId(h,out pid);
            if(pid==0) return true;
            try
            {
                var p=Process.GetProcessById((int)pid);
                string pn=p.ProcessName ?? "";
                if(String.Equals(pn,"TextInputHost",StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(pn,"TabTip",StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(pn,"osk",StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(pn,"InputApp",StringComparison.OrdinalIgnoreCase))
                {
                    found=h;
                    Log("INPUT_HOST_FOUND "+pn+" HWND="+h.ToInt64());
                    return false;
                }

                IntPtr child;
                if(HasTextInputHostChild(h,out child))
                {
                    found=h;
                    Log("INPUT_HOST_FRAME_FOUND "+pn+" HWND="+h.ToInt64()+" child="+child.ToInt64());
                    return false;
                }
            }
            catch {}
            return true;
        };
        EnumWindows(cb,IntPtr.Zero);
        GC.KeepAlive(cb);

        if(found!=IntPtr.Zero)
        {
            ShowWindow(found,0);
            Thread.Sleep(180);
            Log("INPUT_HOST_HIDDEN HWND="+found.ToInt64());
        }
        return found;
    }

    static void RestoreWindowsInputHost(IntPtr h)
    {
        if(h==IntPtr.Zero) return;
        try
        {
            ShowWindow(h,8);
            Log("INPUT_HOST_RESTORED HWND="+h.ToInt64());
        }
        catch {}
    }

    static void PasteClipboardAsFile()
    {
        Log("RUN");
        IntPtr inputHost=IntPtr.Zero;
        try
        {
            for(int i=0;i<8 && (IsDown(VK_CONTROL)||IsDown(VK_SHIFT));i++) Thread.Sleep(20);
            NormalizeModifiersHard();

            var focused=AutomationElement.FocusedElement;
            var focusName=focused==null ? "" : focused.Current.Name;
            var focusClass=focused==null ? "" : focused.Current.ClassName;
            Log("FOCUS "+focusName+" | "+focusClass);

            AutomationElement chatWindow=null;
            AutomationElement attachCheck=null;
            AutomationElement composer=null;

            for(int i=0;i<20 && chatWindow==null;i++)
            {
                var fg=ForegroundAutomation();
                if(TryGetChatContext(fg,out composer,out attachCheck))
                {
                    chatWindow=fg;
                    break;
                }

                chatWindow=FindAnyChatWindow(out composer,out attachCheck);
                if(chatWindow==null) Thread.Sleep(75);
            }

            if(chatWindow==null || attachCheck==null || composer==null)
            {
                Log("WRONG_CONTEXT");
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            try
            {
                if(focused==null || !AutomationElement.Equals(focused,composer))
                {
                    composer.SetFocus();
                    Thread.Sleep(80);
                    Log("COMPOSER_REFOCUSED "+SafeName(composer));
                }
            }
            catch {}

            inputHost=HideWindowsInputHost();

            string text=null;
            for(int i=0;i<15;i++)
            {
                try { text=Clipboard.GetText(TextDataFormat.UnicodeText); if(!String.IsNullOrEmpty(text)) break; }
                catch {}
                Thread.Sleep(50);
            }
            if(String.IsNullOrEmpty(text)) { Log("NO_TEXT"); System.Media.SystemSounds.Beep.Play(); return; }

            string tempDir=Path.Combine(Path.GetTempPath(),"ChatGPT_Paste");
            Directory.CreateDirectory(tempDir);
            try
            {
                foreach(string oldFile in Directory.GetFiles(tempDir,"clipboard_*.txt"))
                    try { File.Delete(oldFile); } catch {}
            }
            catch {}
            string file=Path.Combine(tempDir,"clipboard_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".txt");
            File.WriteAllText(file,text,new UTF8Encoding(false));
            Log("FILE "+file+" LEN="+text.Length);

            var fgWin=ForegroundAutomation();
            var attach=FindByName(fgWin,"Добавить файлы и другое",ControlType.Button);
            if(attach==null)
            {
                Log("ATTACH_BUTTON_NOT_FOUND");
                System.Media.SystemSounds.Hand.Play();
                return;
            }

            try
            {
                var ep=(ExpandCollapsePattern)attach.GetCurrentPattern(ExpandCollapsePattern.Pattern);
                if(ep.Current.ExpandCollapseState!=ExpandCollapseState.Expanded) ep.Expand();
                Log("ATTACH_MENU_OPEN");
            }
            catch(Exception ex)
            {
                Log("ATTACH_MENU_ERROR "+ex.GetType().Name+" "+ex.Message);
                System.Media.SystemSounds.Hand.Play();
                return;
            }

            Thread.Sleep(250);

            fgWin=ForegroundAutomation();
            AutomationElement upload=null;
            for(int i=0;i<8 && upload==null;i++)
            {
                upload=FindByName(fgWin,"Добавить фото и файлы",ControlType.Button);
                if(upload==null)
                    upload=FindByName(AutomationElement.RootElement,"Добавить фото и файлы",ControlType.Button);

                if(upload==null) Thread.Sleep(80);
            }
            if(upload!=null)
            {
                Log("UPLOAD_ITEM_FOUND");
                if(!RealClick(upload,ref inputHost))
                {
                    Log("UPLOAD_REAL_CLICK_FAILED");
                    System.Media.SystemSounds.Hand.Play();
                    return;
                }
                Log("UPLOAD_REAL_CLICK");
            }
            else
            {
                Log("UPLOAD_ITEM_NOT_FOUND");
                System.Media.SystemSounds.Hand.Play();
                return;
            }

            IntPtr dlg=WaitForFileDialog(3500);
            if(dlg==IntPtr.Zero)
            {
                Log("NO_FILE_DIALOG");
                System.Media.SystemSounds.Hand.Play();
                return;
            }
            Log("FILE_DIALOG");

            IntPtr edit=FindChildEdit1148(dlg);
            IntPtr open=GetDlgItem(dlg,1);
            if(edit==IntPtr.Zero || open==IntPtr.Zero)
            {
                Log("DIALOG_CONTROLS_MISSING edit="+edit.ToInt64()+" open="+open.ToInt64());
                System.Media.SystemSounds.Hand.Play();
                return;
            }

            string fileName=Path.GetFileName(file);
            bool pathSet=SetWindowText(edit,file);
            Log("DIALOG_PATH_SET native="+pathSet+" "+file);

            Thread.Sleep(40);
            try
            {
                SendMessage(open,BM_CLICK,IntPtr.Zero,IntPtr.Zero);
                Log("OPEN_BUTTON_BM_CLICK");
            }
            catch(Exception ex)
            {
                Log("OPEN_BUTTON_BM_CLICK_ERROR "+ex.GetType().Name+" "+ex.Message);
            }

            int closeWait=0;
            while(IsWindow(dlg) && closeWait<400)
            {
                Thread.Sleep(40);
                closeWait+=40;
            }

            if(IsWindow(dlg))
            {
                try
                {
                    var editElement=AutomationElement.FromHandle(edit);
                    var vp=(ValuePattern)editElement.GetCurrentPattern(ValuePattern.Pattern);
                    vp.SetValue(file);
                    editElement.SetFocus();
                    Thread.Sleep(60);
                    Log("DIALOG_VALUE_FALLBACK");
                }
                catch(Exception ex)
                {
                    Log("DIALOG_VALUE_FALLBACK_ERROR "+ex.GetType().Name+" "+ex.Message);
                }

                if(!ClickNativeButton(open))
                {
                    Log("OPEN_BUTTON_CLICK_FAILED");
                    System.Media.SystemSounds.Hand.Play();
                    return;
                }

                closeWait=0;
                while(IsWindow(dlg) && closeWait<1000)
                {
                    Thread.Sleep(50);
                    closeWait+=50;
                }
            }

            if(IsWindow(dlg))
            {
                Log("DIALOG_STILL_OPEN");
                System.Media.SystemSounds.Hand.Play();
                return;
            }

            Log("UPLOAD_SUBMITTED "+fileName);

            AutomationElement attached=null;
            for(int i=0;i<40 && attached==null;i++)
            {
                var chat=ForegroundAutomation();
                attached=FindByName(chat,fileName,ControlType.Text);
                if(attached==null) attached=FindByName(chat,fileName,ControlType.Button);
                if(attached==null) Thread.Sleep(100);
            }

            if(attached!=null)
                Log("ATTACHMENT_CONFIRMED "+fileName);
            else
                Log("ATTACHMENT_NOT_CONFIRMED "+fileName);
        }
        catch(Exception ex)
        {
            Log("ERROR "+ex.GetType().Name+" "+ex.Message);
            System.Media.SystemSounds.Hand.Play();
        }
        finally
        {
            NormalizeModifiersHard();
            RestoreWindowsInputHost(inputHost);
            Thread.Sleep(120);
            NormalizeModifiersHard();
        }
    }

    static string SafeName(AutomationElement e)
    {
        try { return e==null ? "<null>" : e.Current.Name+" | "+e.Current.ClassName; } catch { return "<stale>"; }
    }

    static AutomationElement ForegroundAutomation()
    {
        try
        {
            var h=GetForegroundWindow();
            return h==IntPtr.Zero ? null : AutomationElement.FromHandle(h);
        }
        catch { return null; }
    }

    static AutomationElement FindByName(AutomationElement root,string name,ControlType type)
    {
        if(root==null) return null;
        try
        {
            var c1=new PropertyCondition(AutomationElement.NameProperty,name);
            var c2=new PropertyCondition(AutomationElement.ControlTypeProperty,type);
            var cond=new AndCondition(c1,c2);
            return root.FindFirst(TreeScope.Descendants,cond);
        }
        catch { return null; }
    }

    static AutomationElement FindAttachButton(AutomationElement root)
    {
        if(root==null) return null;
        string[] names = new string[] {
            "Добавить файлы и другое",
            "Добавить файлы",
            "Add files and more",
            "Attach files"
        };
        for(int i=0;i<names.Length;i++)
        {
            var b=FindByName(root,names[i],ControlType.Button);
            if(b!=null) return b;
        }
        return null;
    }

    static bool TryGetChatContext(AutomationElement root,out AutomationElement composer,out AutomationElement attach)
    {
        composer=null;
        attach=null;
        if(root==null) return false;
        composer=FindChatComposer(root);
        if(composer==null) return false;
        attach=FindAttachButton(root);
        return attach!=null;
    }

    static AutomationElement FindAnyChatWindow(out AutomationElement composer,out AutomationElement attach)
    {
        composer=null;
        attach=null;
        try
        {
            var root=AutomationElement.RootElement;
            var wins=root.FindAll(TreeScope.Children,Condition.TrueCondition);
            for(int i=0;i<wins.Count;i++)
            {
                var w=wins[i];
                string cls="";
                try { cls=w.Current.ClassName ?? ""; } catch {}
                if(!String.Equals(cls,"Chrome_WidgetWin_1",StringComparison.OrdinalIgnoreCase)) continue;

                AutomationElement c,a;
                if(TryGetChatContext(w,out c,out a))
                {
                    composer=c;
                    attach=a;
                    return w;
                }
            }
        }
        catch {}
        return null;
    }

    static AutomationElement FindChatComposer(AutomationElement root)
    {
        if(root==null) return null;
        try
        {
            var edits=root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit)
            );
            for(int i=0;i<edits.Count;i++)
            {
                var e=edits[i];
                string cls="";
                string name="";
                try { cls=e.Current.ClassName ?? ""; } catch {}
                try { name=e.Current.Name ?? ""; } catch {}

                if(cls.IndexOf("ProseMirror",StringComparison.OrdinalIgnoreCase)>=0)
                    return e;

                if(String.Equals(name,"Сообщение ChatGPT",StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(name,"Спросить ChatGPT",StringComparison.OrdinalIgnoreCase))
                    return e;
            }
        }
        catch {}
        return null;
    }

    static bool HasTextInputHostChild(IntPtr hWnd,out IntPtr childFound)
    {
        childFound=IntPtr.Zero;
        if(hWnd==IntPtr.Zero) return false;

        IntPtr found=IntPtr.Zero;
        EnumProc cb=(ch,l) =>
        {
            uint pid=0;
            GetWindowThreadProcessId(ch,out pid);
            if(pid==0) return true;
            try
            {
                var p=Process.GetProcessById((int)pid);
                string pn=p.ProcessName ?? "";
                if(String.Equals(pn,"TextInputHost",StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(pn,"TabTip",StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(pn,"InputApp",StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(pn,"osk",StringComparison.OrdinalIgnoreCase))
                {
                    found=ch;
                    return false;
                }
            }
            catch {}
            return true;
        };
        EnumChildWindows(hWnd,cb,IntPtr.Zero);
        GC.KeepAlive(cb);
        childFound=found;
        return found!=IntPtr.Zero;
    }

    static bool IsLikelyKeyboardWindow(IntPtr hWnd,out string description)
    {
        description="";
        if(hWnd==IntPtr.Zero) return false;

        uint pid=0;
        GetWindowThreadProcessId(hWnd,out pid);
        string pn="";
        try { if(pid!=0) pn=Process.GetProcessById((int)pid).ProcessName ?? ""; } catch {}

        var cls=new StringBuilder(256);
        var title=new StringBuilder(512);
        try { GetClassName(hWnd,cls,cls.Capacity); } catch {}
        try { GetWindowText(hWnd,title,title.Capacity); } catch {}

        description=pn+" | "+cls.ToString()+" | "+title.ToString();

        if(String.Equals(pn,"TextInputHost",StringComparison.OrdinalIgnoreCase) ||
           String.Equals(pn,"TabTip",StringComparison.OrdinalIgnoreCase) ||
           String.Equals(pn,"osk",StringComparison.OrdinalIgnoreCase) ||
           String.Equals(pn,"InputApp",StringComparison.OrdinalIgnoreCase))
            return true;

        IntPtr inputChild;
        if(HasTextInputHostChild(hWnd,out inputChild))
        {
            description += " | child TextInputHost HWND="+inputChild.ToInt64();
            return true;
        }

        string c=cls.ToString();
        string t=title.ToString();
        if(c.IndexOf("Windows.UI.Core.CoreWindow",StringComparison.OrdinalIgnoreCase)>=0 ||
           c.IndexOf("Xaml",StringComparison.OrdinalIgnoreCase)>=0 ||
           t.IndexOf("Интерфейс ввода Windows",StringComparison.OrdinalIgnoreCase)>=0 ||
           t.IndexOf("keyboard",StringComparison.OrdinalIgnoreCase)>=0 ||
           t.IndexOf("клавиат",StringComparison.OrdinalIgnoreCase)>=0)
            return true;

        return false;
    }

    static bool RealClick(AutomationElement element,ref IntPtr keyboardWindow)
    {
        POINT old;
        GetPhysicalCursorPos(out old);
        try
        {
            try
            {
                var scroll=(ScrollItemPattern)element.GetCurrentPattern(ScrollItemPattern.Pattern);
                scroll.ScrollIntoView();
                Thread.Sleep(80);
            }
            catch {}

            var pt=element.GetClickablePoint();
            int x=(int)Math.Round(pt.X);
            int y=(int)Math.Round(pt.Y);
            Log("REAL_CLICK_PHYSICAL "+x+","+y);

            POINT probe=new POINT();
            probe.X=x; probe.Y=y;
            IntPtr under=WindowFromPhysicalPoint(probe);
            IntPtr root=under==IntPtr.Zero ? IntPtr.Zero : GetAncestor(under,2);
            if(root!=IntPtr.Zero)
            {
                string desc;
                if(IsLikelyKeyboardWindow(root,out desc))
                {
                    Log("CLICK_OCCLUDED_BY_KEYBOARD "+root.ToInt64()+" "+desc);
                    ShowWindow(root,0);
                    keyboardWindow=root;
                    Thread.Sleep(220);
                    NormalizeModifiersHard();

                    try
                    {
                        string itemName=element.Current.Name ?? "";
                        var refreshedRoot=ForegroundAutomation();
                        var refreshed=FindByName(refreshedRoot,itemName,ControlType.Button);
                        if(refreshed!=null) element=refreshed;

                        var pt2=element.GetClickablePoint();
                        x=(int)Math.Round(pt2.X);
                        y=(int)Math.Round(pt2.Y);
                        Log("REAL_CLICK_RECALCULATED "+x+","+y);
                    }
                    catch(Exception rex)
                    {
                        Log("REAL_CLICK_RECALC_ERROR "+rex.GetType().Name+" "+rex.Message);
                    }
                }
                else
                {
                    Log("CLICK_UNDER_WINDOW "+root.ToInt64()+" "+desc);
                }
            }

            if(!SetPhysicalCursorPos(x,y)) return false;
            Thread.Sleep(60);
            mouse_event(0x0002,0,0,0,UIntPtr.Zero);
            Thread.Sleep(30);
            mouse_event(0x0004,0,0,0,UIntPtr.Zero);
            Thread.Sleep(150);
            return true;
        }
        catch(Exception ex)
        {
            Log("REAL_CLICK_ERROR "+ex.GetType().Name+" "+ex.Message);
            return false;
        }
        finally
        {
            SetPhysicalCursorPos(old.X,old.Y);
        }
    }

    static bool ClickNativeButton(IntPtr hWnd)
    {
        if(hWnd==IntPtr.Zero) return false;
        RECT r;
        if(!GetClientRect(hWnd,out r)) return false;

        int x=(r.Right-r.Left)/2;
        int y=(r.Bottom-r.Top)/2;
        IntPtr lp=(IntPtr)(((y & 0xffff)<<16) | (x & 0xffff));

        try
        {
            Log("BUTTON_CLIENT_CLICK "+x+","+y);
            SendMessage(hWnd,0x0201,(IntPtr)1,lp);
            Thread.Sleep(35);
            SendMessage(hWnd,0x0202,IntPtr.Zero,lp);
            Thread.Sleep(180);
            return true;
        }
        catch(Exception ex)
        {
            Log("BUTTON_CLIENT_CLICK_ERROR "+ex.GetType().Name+" "+ex.Message);
            return false;
        }
    }

    static IntPtr WaitForFileDialog(int timeoutMs)
    {
        int waited=0;
        var sb=new StringBuilder(64);
        while(waited<timeoutMs)
        {
            var h=GetForegroundWindow();
            if(h!=IntPtr.Zero)
            {
                sb.Clear();
                GetClassName(h,sb,sb.Capacity);
                if(sb.ToString()=="#32770") return h;
            }
            Thread.Sleep(50);
            waited+=50;
        }
        return IntPtr.Zero;
    }

    static IntPtr FindChildEdit1148(IntPtr dlg)
    {
        IntPtr found=IntPtr.Zero;
        var sb=new StringBuilder(64);
        EnumProc cb=(h,l) =>
        {
            sb.Clear();
            GetClassName(h,sb,sb.Capacity);
            if(sb.ToString()=="Edit" && GetDlgCtrlID(h)==1148)
            {
                found=h;
                return false;
            }
            return true;
        };
        EnumChildWindows(dlg,cb,IntPtr.Zero);
        GC.KeepAlive(cb);
        return found;
    }

    static void SendChord(int mod,int key)
    {
        INPUT[] a=new INPUT[4];
        a[0]=Key(mod,false); a[1]=Key(key,false); a[2]=Key(key,true); a[3]=Key(mod,true);
        SendInput((uint)a.Length,a,Marshal.SizeOf(typeof(INPUT)));
    }

    static void SendKey(int key)
    {
        INPUT[] a=new INPUT[2];
        a[0]=Key(key,false); a[1]=Key(key,true);
        SendInput((uint)a.Length,a,Marshal.SizeOf(typeof(INPUT)));
    }

    static INPUT Key(int vk,bool up)
    {
        INPUT i=new INPUT();
        i.type=INPUT_KEYBOARD;
        i.U.ki.wVk=(ushort)vk;
        i.U.ki.dwFlags=up?KEYEVENTF_KEYUP:0;
        return i;
    }

    public void Dispose()
    {
        UnregisterHotKey(Handle,HOTKEY_ID);
        DestroyHandle();
    }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPTClipboardFilePaste"));
        bool created;
        using(var mutex=new Mutex(true,"Local\\ChatGPTClipboardFilePaste",out created))
        {
            if(!created) return;
            try
            {
                string d=Path.Combine(Path.GetTempPath(),"ChatGPT_Paste");
                if(Directory.Exists(d))
                    foreach(string f in Directory.GetFiles(d,"clipboard_*.txt"))
                        try { if(File.GetCreationTime(f)<DateTime.Now.AddDays(-2)) File.Delete(f); } catch {}
            }
            catch {}

            Application.EnableVisualStyles();
            using(var w=new HotkeyWindow()) Application.Run();
        }
    }
}