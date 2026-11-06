using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WindowSweaters {
internal static class Tests {
    static int checks;
    [DllImport("user32.dll")] static extern int GetGuiResources(IntPtr process, int flags);
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
    [STAThread] static int Main() {
        Native.EnableDpi(); Application.EnableVisualStyles();
        string directory = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "test-results");
        Directory.CreateDirectory(directory);
        try {
            Check(Collection.Charts.Count == 47, "all upstream charts imported");
            foreach (Chart chart in Collection.Charts.Values) foreach (string row in chart.Rows) Check(row.Length == chart.Rows[0].Length, "rectangular chart");
            var settings = new Settings(directory);
            Check(Yarn.Resolve("WINWORD", settings).Pattern == "atelier-word", "Word executable alias");
            Check(Yarn.Resolve("Code", settings).Pattern == "atelier-vscode", "VS Code mapping");
            Check(Yarn.Resolve("unknown", settings).Color == Yarn.Resolve("UNKNOWN", settings).Color, "stable case-independent fallback");
            File.WriteAllText(settings.FilePath, "width=999\nstitch=-1\npattern=invalid\npalette=invalid\nexclude=테스트\n"); settings.Load();
            Check(settings.Width == 48 && settings.Stitch == 75 && settings.Pattern == "by-app" && settings.Palette == "by-app", "invalid settings clamped");
            settings.Paused = true; settings.Save();
            var loaded = new Settings(directory); loaded.Load(); Check(loaded.Paused && loaded.Excluded.Contains("테스트"), "portable settings roundtrip");
            settings = new Settings(directory);
            using (var knit = new Knit()) {
                foreach (string name in Collection.Charts.Keys) using (Bitmap image = knit.Render(740, 450, 24, 8, new AppRule("test", 0xff4e8098, name), 100)) {
                    Check(image.GetPixel(370, 225).A == 0, "transparent center: " + name);
                    Check(image.GetPixel(370, 12).A > 240, "opaque yarn: " + name);
                    Check(image.GetPixel(0, 0).A == 0, "rounded outer corner: " + name);
                }
                using (Bitmap image = knit.Render(740, 450, 24, 8, new AppRule("test", 0xffd58561, "atelier-claude"), 100)) image.Save(Path.Combine(directory, "border.png"), ImageFormat.Png);
            }
            using (var context = new SweaterContext(settings, false)) {
                context.ShowDemo(); Application.DoEvents(); Thread.Sleep(100); Application.DoEvents();
                Form form = null; foreach (Form candidate in Application.OpenForms) if (candidate is DemoWindow) form = candidate;
                Check(form != null, "demo window exists");
                IntPtr before = Native.GetForegroundWindow();
                context.RefreshTarget(form.Handle, true); Application.DoEvents();
                Check(Native.IsWindowVisible(context.Border.Handle), "live overlay visible");
                Check(Native.GetForegroundWindow() == before, "overlay does not steal focus");
                int style = Native.GetWindowLong(context.Border.Handle, -20);
                Check((style & Native.Transparent) != 0 && (style & Native.Layered) != 0 && (style & Native.NoActivate) != 0, "click-through layered no-activate styles");
                Rectangle old = context.LastBounds;
                form.Location = new Point(form.Left + 23, form.Top + 17); Application.DoEvents(); Thread.Sleep(150); Application.DoEvents(); context.RefreshTarget(form.Handle, true);
                Check(context.LastBounds.Left == old.Left + 23 && context.LastBounds.Top == old.Top + 17, "moving follows visible frame bounds: " + old + " -> " + context.LastBounds);
                form.Size = new Size(form.Width + 41, form.Height + 29); Application.DoEvents(); Thread.Sleep(150); Application.DoEvents(); context.RefreshTarget(form.Handle, true);
                Check(context.LastBounds.Width == old.Width + 41 && context.LastBounds.Height == old.Height + 29, "resizing redraws to the new frame");
                settings.Paused = true; context.RefreshTarget(form.Handle, true); Check(!Native.IsWindowVisible(context.Border.Handle), "pause hides overlay");
                settings.Paused = false;
                settings.Excluded.Add(Process.GetCurrentProcess().ProcessName); context.RefreshTarget(form.Handle, true); Check(!Native.IsWindowVisible(context.Border.Handle), "process exclusion hides overlay");
                settings.Excluded.Clear();
                form.WindowState = FormWindowState.Minimized; Application.DoEvents(); context.RefreshTarget(form.Handle, true); Check(!Native.IsWindowVisible(context.Border.Handle), "minimize hides overlay");
                form.WindowState = FormWindowState.Maximized; Application.DoEvents(); Thread.Sleep(200); Application.DoEvents(); context.RefreshTarget(form.Handle, true); Check(!Native.IsWindowVisible(context.Border.Handle), "maximize hidden by default");
                settings.Maximized = true; context.RefreshTarget(form.Handle, true); Check(Native.IsWindowVisible(context.Border.Handle), "optional inset border on maximized window");
                form.WindowState = FormWindowState.Normal; Application.DoEvents(); context.RefreshTarget(form.Handle, true);
                int handlesBefore = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                for (int i = 0; i < 40; i++) { settings.Width = i % 2 == 0 ? 24 : 32; context.RefreshTarget(form.Handle, true); }
                int handlesAfter = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                Check(handlesAfter - handlesBefore < 8, "GDI handles stay bounded across redraws");
                settings.Width = 24; context.RefreshTarget(form.Handle, true);
                context.RefreshTarget(IntPtr.Zero, true); Check(!Native.IsWindowVisible(context.Border.Handle), "no foreground hides overlay");
            }
            // Publish a rendered sample with no desktop or personal content.
            using (var knit = new Knit()) using (var sample = new Bitmap(1000, 650)) using (Graphics g = Graphics.FromImage(sample)) {
                g.Clear(Color.FromArgb(232, 226, 214));
                using (Bitmap border = knit.Render(796, 496, 28, 8, new AppRule("Claude", 0xffd58561, "atelier-claude"), 100)) g.DrawImageUnscaled(border, 102, 77);
                using (var fill = new SolidBrush(Color.FromArgb(250, 247, 239))) g.FillRectangle(fill, 130, 105, 740, 440);
                using (var title = new Font("Georgia", 38)) using (var body = new Font("Segoe UI", 14)) using (var small = new Font("Segoe UI", 11)) {
                    g.DrawString("A cosier window.", title, Brushes.Black, 175, 188);
                    g.DrawString("Knitted borders for Windows.", body, Brushes.DimGray, 180, 273);
                    g.DrawString("Window Window Sweaters", body, Brushes.Black, 180, 365);
                    g.DrawString("Unofficial Windows port of saragordic/window-sweaters", small, Brushes.DimGray, 180, 405);
                    g.DrawString("Rendered sample using the app's renderer · GPL-3.0", small, Brushes.DimGray, 132, 595);
                }
                sample.Save(Path.Combine(directory, "preview.png"), ImageFormat.Png);
            }
            File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS: " + checks + " checks\nWindows: " + Environment.OSVersion + "\nRuntime: " + Environment.Version + "\n");
            Console.WriteLine("PASS: " + checks + " checks (collection, rendering, settings, live window lifecycle, GDI resources)"); return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
}
