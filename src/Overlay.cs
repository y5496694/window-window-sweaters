using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WindowSweaters {
internal sealed class Overlay : Form {
    internal Overlay() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; Text = "Window Window Sweaters Overlay"; }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams {
        get { var cp = base.CreateParams; cp.ExStyle |= Native.Layered | Native.Transparent | Native.ToolWindow | Native.NoActivate; return cp; }
    }
    protected override void WndProc(ref Message m) {
        if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; } // HTTRANSPARENT
        if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
        base.WndProc(ref m);
    }
    internal void Present(Bitmap bitmap, int x, int y) {
        IntPtr screen = Native.GetDC(IntPtr.Zero), memory = Native.CreateCompatibleDC(screen), hbitmap = IntPtr.Zero, previous = IntPtr.Zero;
        try {
            hbitmap = bitmap.GetHbitmap(Color.FromArgb(0)); previous = Native.SelectObject(memory, hbitmap);
            var destination = new Native.Point(x, y); var origin = new Native.Point(0, 0);
            var size = new Native.Size(bitmap.Width, bitmap.Height);
            var blend = new Native.Blend { Op = 0, Flags = 0, Alpha = 255, Format = 1 };
            if (!Native.UpdateLayeredWindow(Handle, screen, ref destination, ref size, memory, ref origin, 0, ref blend, 2)) throw new Win32Exception();
            Place(x, y);
        } finally {
            if (previous != IntPtr.Zero) Native.SelectObject(memory, previous);
            if (hbitmap != IntPtr.Zero) Native.DeleteObject(hbitmap);
            Native.DeleteDC(memory); Native.ReleaseDC(IntPtr.Zero, screen);
        }
    }
    internal void Place(int x, int y) {
        Native.SetWindowPos(Handle, Native.Topmost, x, y, 0, 0, Native.NoSize | Native.NoActivation | Native.Show);
    }
    internal void Conceal() { if (IsHandleCreated) Native.ShowWindow(Handle, 0); }
}
}
