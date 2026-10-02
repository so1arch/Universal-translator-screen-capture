using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ScreenTranslator;

public sealed record WinInfo(IntPtr Handle, string Title, string Proc)
{
    public override string ToString() => $"{Proc}  —  {Title}";
}

static class Native
{
    delegate bool EnumProc(IntPtr h, IntPtr l);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int L, T, R, B; }

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);

    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);

    public static uint Pid(IntPtr h)
    {
        GetWindowThreadProcessId(h, out uint pid);
        return pid;
    }

    public static Rectangle Rect(IntPtr h)
    {
        GetWindowRect(h, out var r);
        return Rectangle.FromLTRB(r.L, r.T, r.R, r.B);
    }

    /// <summary>Список обычных окон приложений (без служебных и скрытых).</summary>
    public static List<WinInfo> Windows()
    {
        var list = new List<WinInfo>();
        uint me = (uint)Environment.ProcessId;

        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            int len = GetWindowTextLength(h);
            if (len == 0) return true;
            DwmGetWindowAttribute(h, 14 /*DWMWA_CLOAKED*/, out int cloaked, 4);
            if (cloaked != 0) return true;
            if ((GetWindowLong(h, -20) & 0x80) != 0) return true; // WS_EX_TOOLWINDOW
            uint pid = Pid(h);
            if (pid == me) return true;
            if (!IsIconic(h))
            {
                var r = Rect(h);
                if (r.Width < 100 || r.Height < 60) return true;
            }

            var sb = new StringBuilder(len + 1);
            GetWindowText(h, sb, sb.Capacity);
            string proc;
            try { proc = Process.GetProcessById((int)pid).ProcessName; } catch { proc = "?"; }
            list.Add(new WinInfo(h, sb.ToString(), proc));
            return true;
        }, IntPtr.Zero);

        return list.OrderBy(w => w.Proc, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
