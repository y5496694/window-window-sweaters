using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WindowSweaters {
internal static class Program {
    internal const string QuitEvent = @"Local\WindowWindowSweaters.Quit.v1";
    [STAThread] static int Main(string[] args) {
        Native.EnableDpi(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if (Array.IndexOf(args, "--quit") >= 0) {
            try { using (var signal = EventWaitHandle.OpenExisting(QuitEvent)) signal.Set(); } catch (WaitHandleCannotBeOpenedException) { }
            return 0;
        }
        bool created;
        using (var instance = new Mutex(true, @"Local\WindowWindowSweaters.Instance.v1", out created)) {
            if (!created) { MessageBox.Show("이미 실행 중입니다. 알림 영역의 털실 아이콘을 확인하세요.", "Window Window Sweaters"); return 0; }
            var settings = new Settings(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"));
            try {
                settings.Load();
                using (var context = new SweaterContext(settings, true)) {
                    Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { context.Report(e.Exception); };
                    if (Array.IndexOf(args, "--demo") >= 0) context.ShowDemo();
                    Application.Run(context);
                }
                return 0;
            } catch (Exception e) {
                MessageBox.Show("실행할 수 없습니다. 쓰기 가능한 폴더에서 실행해 주세요.\n\n" + e.Message, "Window Window Sweaters"); return 1;
            }
        }
    }
}
internal sealed class SweaterContext : ApplicationContext {
    readonly Settings settings;
    internal readonly Overlay Border = new Overlay();
    readonly Knit renderer = new Knit();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly Native.WinEvent callback;
    readonly IntPtr foregroundHook, locationHook;
    readonly EventWaitHandle quit;
    readonly uint ownPid = (uint)Process.GetCurrentProcess().Id;
    NotifyIcon tray;
    ContextMenuStrip menu;
    Icon icon;
    DemoWindow demo;
    string appName = "", renderKey = "";
    uint lastPid;
    Size pendingSize;
    DateTime sizeChanged;
    bool menuOpen, disposed;
    internal Rectangle LastBounds { get; private set; }
    internal IntPtr LastTarget { get; private set; }
    internal SweaterContext(Settings state, bool showTray) {
        settings = state;
        if (showTray) quit = new EventWaitHandle(false, EventResetMode.AutoReset, Program.QuitEvent);
        callback = delegate(IntPtr hook, uint evt, IntPtr hwnd, int obj, int child, uint thread, uint time) {
            if (disposed || menuOpen || (evt == 0x800B && (obj != 0 || hwnd != LastTarget))) return;
            RefreshTarget(Native.GetForegroundWindow(), false);
        };
        foregroundHook = Native.SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 0);
        locationHook = Native.SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, callback, 0, 0, 0);
        if (showTray) {
            icon = Knit.MakeIcon();
            menu = new ContextMenuStrip();
            menu.Opening += delegate { menuOpen = true; Border.Conceal(); RebuildMenu(); };
            menu.Closed += delegate { menuOpen = false; };
            tray = new NotifyIcon { Icon = icon, Text = "Window Window Sweaters", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { ShowDemo(); };
            tray.ShowBalloonTip(3500, "Window Window Sweaters", "활성 창에 뜨개 테두리를 입힙니다. 털실 아이콘을 오른쪽 클릭하면 설정할 수 있어요.", ToolTipIcon.Info);
        }
        timer.Interval = 33;
        timer.Tick += delegate { if (quit != null && quit.WaitOne(0)) { ExitThread(); return; } RefreshTarget(Native.GetForegroundWindow(), false); };
        timer.Start();
    }
    internal void ShowDemo() {
        if (demo == null || demo.IsDisposed) demo = new DemoWindow();
        demo.Show(); demo.Activate();
    }
    string ProcessName(uint pid) {
        if (pid == lastPid && appName.Length > 0) return appName;
        try { using (Process process = Process.GetProcessById((int)pid)) { lastPid = pid; appName = process.ProcessName; return appName; } }
        catch (ArgumentException) { return ""; }
        catch (System.ComponentModel.Win32Exception) { return ""; }
    }
    internal void RefreshTarget(IntPtr hwnd, bool immediate) {
        if (disposed || menuOpen || settings.Paused || hwnd == IntPtr.Zero || !Native.IsWindowVisible(hwnd) || Native.IsIconic(hwnd)) { Border.Conceal(); return; }
        uint pid; Native.GetWindowThreadProcessId(hwnd, out pid);
        if (pid == ownPid && (demo == null || !demo.IsHandleCreated || hwnd != demo.Handle)) { Border.Conceal(); return; }
        var cls = new System.Text.StringBuilder(256); Native.GetClassName(hwnd, cls, cls.Capacity);
        string className = cls.ToString();
        if (className == "Progman" || className == "WorkerW" || className.StartsWith("Shell_") || className == "#32768" || className == "Windows.UI.Core.CoreWindow" || (Native.GetWindowLong(hwnd, -20) & Native.ToolWindow) != 0) { Border.Conceal(); return; }
        int cloaked;
        if (Native.WindowAttribute(hwnd, 14, out cloaked, 4) == 0 && cloaked != 0) { Border.Conceal(); return; }
        Native.Rect frame;
        if (Native.FrameBounds(hwnd, 9, out frame, 16) != 0 && !Native.GetWindowRect(hwnd, out frame)) { Border.Conceal(); return; }
        var target = Rectangle.FromLTRB(frame.Left, frame.Top, frame.Right, frame.Bottom);
        if (target.Width < 80 || target.Height < 60) { Border.Conceal(); return; }
        Screen monitor = Screen.FromHandle(hwnd);
        bool maximized = Native.IsZoomed(hwnd);
        bool fullscreen = target.Left <= monitor.Bounds.Left && target.Top <= monitor.Bounds.Top && target.Right >= monitor.Bounds.Right && target.Bottom >= monitor.Bounds.Bottom;
        if (fullscreen || (maximized && !settings.Maximized)) { Border.Conceal(); return; }
        string processName = ProcessName(pid);
        if (processName.Length == 0 || settings.Excluded.Contains(processName)) { Border.Conceal(); return; }
        int band = Math.Max(8, (int)Math.Round(settings.Width * Native.DpiScale(hwnd)));
        Rectangle outer;
        if (maximized) { outer = Rectangle.Intersect(target, monitor.WorkingArea); band = Math.Min(band, 12); }
        else { outer = target; outer.Inflate(band, band); }
        if (outer.Width <= band * 2 || outer.Height <= band * 2 || outer.Width > 10000 || outer.Height > 10000 || (long)outer.Width * outer.Height > 40000000 || !SystemInformation.VirtualScreen.IntersectsWith(outer)) { Border.Conceal(); return; }
        if (hwnd != LastTarget) { LastTarget = hwnd; pendingSize = outer.Size; sizeChanged = DateTime.MinValue; renderKey = ""; }
        if (pendingSize != outer.Size) { pendingSize = outer.Size; sizeChanged = DateTime.UtcNow; Border.Conceal(); }
        // Avoid allocating large bitmaps continuously during interactive resize.
        if (!immediate && (DateTime.UtcNow - sizeChanged).TotalMilliseconds < 100) return;
        AppRule rule = Yarn.Resolve(processName, settings);
        string key = outer.Width + ":" + outer.Height + ":" + band + ":" + rule.Color + ":" + rule.Pattern + ":" + settings.Stitch + ":" + maximized;
        if (key != renderKey) {
            using (Bitmap bitmap = renderer.Render(outer.Width, outer.Height, band, maximized ? 0 : 8 * Native.DpiScale(hwnd), rule, settings.Stitch)) Border.Present(bitmap, outer.X, outer.Y);
            renderKey = key;
        } else Border.Place(outer.X, outer.Y);
        LastBounds = outer;
    }
    ToolStripMenuItem Item(string label, bool selected, Action action) {
        var item = new ToolStripMenuItem(label) { Checked = selected };
        item.Click += delegate { action(); Changed(); }; return item;
    }
    void Changed() {
        renderKey = "";
        try { settings.Save(); } catch (IOException e) { ReportSaveError(e.Message); } catch (UnauthorizedAccessException e) { ReportSaveError(e.Message); }
    }
    void ReportSaveError(string message) { if (tray != null) tray.ShowBalloonTip(3000, "설정을 저장하지 못했습니다", message, ToolTipIcon.Warning); }
    void RebuildMenu() {
        // Dispose detached items so repeated openings do not accumulate resources.
        while (menu.Items.Count > 0) { var old = menu.Items[0]; menu.Items.RemoveAt(0); old.Dispose(); }
        menu.Items.Add(new ToolStripMenuItem("Window Window Sweaters · 0.1.0") { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem("활성 창에 적용 · Windows 시제품") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("일시 정지", settings.Paused, delegate { settings.Paused = !settings.Paused; }));
        var patterns = new ToolStripMenuItem("패턴 / Pattern");
        string[,] choices = { { "by-app", "앱별 / By app" }, { "zigzag", "지그재그 / Zigzag" }, { "plain", "기본 뜨개 / Plain" }, { "picnic", "체크 / Picnic" }, { "twinkle", "별 / Twinkle" }, { "braid", "땋기 / Braid" }, { "ribbon", "리본 / Ribbon" }, { "posy", "꽃 / Posy" }, { "seedling", "새싹 / Seedling" }, { "candy-stripe", "캔디 / Candy stripe" }, { "checker", "바둑판 / Checker" } };
        for (int i = 0; i < choices.GetLength(0); i++) { string value = choices[i, 0]; patterns.DropDownItems.Add(Item(choices[i, 1], settings.Pattern == value, delegate { settings.Pattern = value; })); }
        menu.Items.Add(patterns);
        var colors = new ToolStripMenuItem("색상 / Colours");
        string[,] palettes = { { "by-app", "앱별 / By app" }, { "sorbet", "파스텔 / Sorbet" }, { "forest", "숲 / Forest" }, { "mono", "회색 / Mono" } };
        for (int i = 0; i < palettes.GetLength(0); i++) { string value = palettes[i, 0]; colors.DropDownItems.Add(Item(palettes[i, 1], settings.Palette == value, delegate { settings.Palette = value; })); }
        menu.Items.Add(colors);
        var widths = new ToolStripMenuItem("테두리 굵기 / Width");
        foreach (int value in new[] { 16, 24, 32, 40 }) { int v = value; widths.DropDownItems.Add(Item(v + " px", settings.Width == v, delegate { settings.Width = v; })); }
        menu.Items.Add(widths);
        var stitches = new ToolStripMenuItem("코 크기 / Stitch size");
        foreach (int value in new[] { 75, 100, 125, 150 }) { int v = value; stitches.DropDownItems.Add(Item(v + "%", settings.Stitch == v, delegate { settings.Stitch = v; })); }
        menu.Items.Add(stitches);
        menu.Items.Add(Item("최대화 창에도 표시 (안쪽 12 px)", settings.Maximized, delegate { settings.Maximized = !settings.Maximized; }));
        if (appName.Length > 0) { string name = appName; menu.Items.Add(Item(name + " 제외 / Exclude", settings.Excluded.Contains(name), delegate { if (!settings.Excluded.Remove(name)) settings.Excluded.Add(name); })); }
        if (settings.Excluded.Count > 0) menu.Items.Add(Item("제외 목록 초기화 / Reset exclusions", false, delegate { settings.Excluded.Clear(); }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("미리보기 / Preview", null, delegate { ShowDemo(); }));
        menu.Items.Add(new ToolStripMenuItem("정보 및 출처 / About", null, delegate {
            MessageBox.Show("Window Window Sweaters 0.1.0\n비공식 Windows 포트 / Unofficial Windows port\n\n원작: Window Sweaters by saragordic\nhttps://github.com/saragordic/window-sweaters\n기반 프로젝트: JankyBorders by Felix Kratz\nhttps://github.com/FelixKratz/JankyBorders\n\nGPL-3.0 · 패턴과 색상 원작 출처 유지\n트레이 아이콘 우클릭: 설정 · 더블클릭: 미리보기", "출처 / Attribution");
        }));
        menu.Items.Add(new ToolStripMenuItem("종료 / Quit", null, delegate { ExitThread(); }));
    }
    internal void Report(Exception error) {
        settings.Paused = true; Border.Conceal();
        try { Directory.CreateDirectory(settings.DirectoryPath); File.AppendAllText(Path.Combine(settings.DirectoryPath, "error.log"), DateTime.UtcNow.ToString("o") + " " + error + Environment.NewLine); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (tray != null) tray.ShowBalloonTip(4000, "테두리를 일시 정지했습니다", error.Message, ToolTipIcon.Warning);
    }
    protected override void ExitThreadCore() { Dispose(); base.ExitThreadCore(); }
    protected override void Dispose(bool disposing) {
        if (disposing && !disposed) {
            disposed = true; timer.Stop(); timer.Dispose();
            if (foregroundHook != IntPtr.Zero) Native.UnhookWinEvent(foregroundHook);
            if (locationHook != IntPtr.Zero) Native.UnhookWinEvent(locationHook);
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            if (menu != null) menu.Dispose(); if (icon != null) icon.Dispose();
            if (demo != null) demo.Dispose(); Border.Dispose(); renderer.Dispose(); if (quit != null) quit.Dispose();
        }
        base.Dispose(disposing);
    }
}
internal sealed class DemoWindow : Form {
    internal DemoWindow() {
        Text = "Window Window Sweaters — Preview"; ClientSize = new Size(680, 400);
        StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(249, 246, 239);
        Font = new Font("Segoe UI", 11); MinimumSize = new Size(460, 300);
        var title = new Label { Text = "A cosier window.", Font = new Font("Georgia", 34), AutoSize = true, Location = new Point(42, 45) };
        var detail = new Label { Text = "A little wool for your Windows.\n창을 움직이거나 크기를 바꿔 보세요.", AutoSize = true, Location = new Point(47, 120) };
        var button = new Button { Text = "클릭 테스트 / Click me", Location = new Point(47, 209), Size = new Size(260, 44), FlatStyle = FlatStyle.Flat, BackColor = Color.White };
        int clicks = 0; button.Click += delegate { clicks++; button.Text = "클릭 통과 확인 / Clicked " + clicks; };
        var attribution = new Label { Text = "Windows port · GPL-3.0\nOriginal: saragordic/window-sweaters · based on JankyBorders", AutoSize = true, Location = new Point(47, 298), ForeColor = Color.FromArgb(110, 106, 99) };
        Controls.AddRange(new Control[] { title, detail, button, attribution });
    }
}
}
