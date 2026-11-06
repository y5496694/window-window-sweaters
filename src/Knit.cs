// Collection data and stitch geometry adapted from saragordic/window-sweaters.
// Windows GDI+ renderer: GPL-3.0. See NOTICE.md for attribution.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WindowSweaters {
internal sealed class Chart {
    internal readonly string[] Rows;
    internal readonly uint[] Colors;
    internal Chart(string[] rows, uint[] colors) { Rows = rows; Colors = colors; }
}
internal sealed class AppRule {
    internal readonly string Name, Pattern;
    internal readonly uint Color;
    internal AppRule(string name, uint color, string pattern) { Name = name; Color = color; Pattern = pattern; }
}
internal static class Yarn {
    internal static readonly Dictionary<string, uint[]> Palettes = new Dictionary<string, uint[]> {
        { "by-app", new uint[] { 0xffd1495b, 0xff4e8098, 0xffedae49, 0xff7c9885, 0xff9b6a8f, 0xffe8846b, 0xff3f7d6e, 0xffc46a4e, 0xff6d7ba8, 0xffb8935f } },
        { "sorbet", new uint[] { 0xfff08ca4, 0xff7fc6c4, 0xffffc978, 0xffb08ed4, 0xff8fce90, 0xffff9f7a, 0xff8ab6f0, 0xffe5a3d0 } },
        { "forest", new uint[] { 0xff4a6b52, 0xff7d8f5c, 0xff3f6b6e, 0xff8a7248, 0xff5c6b8a, 0xff6b5344, 0xff2f5e4a } },
        { "mono", new uint[] { 0xff9aa0a6, 0xff7c8288, 0xffb4bac0, 0xff686e74, 0xff8d939a } }
    };
    internal static uint Hash(string text) {
        uint hash = 2166136261;
        foreach (char c in text.ToLowerInvariant()) hash = unchecked((hash ^ c) * 16777619);
        hash ^= hash >> 16; hash = unchecked(hash * 0x85ebca6b); hash ^= hash >> 13;
        hash = unchecked(hash * 0xc2b2ae35); return hash ^ (hash >> 16);
    }
    internal static AppRule Resolve(string process, Settings settings) {
        string match = process;
        // Windows executable aliases for the original app-name collection.
        switch (process.ToLowerInvariant()) {
            case "explorer": match = "Finder"; break;
            case "winword": match = "Microsoft Word"; break;
            case "excel": match = "Microsoft Excel"; break;
            case "powerpnt": match = "Microsoft PowerPoint"; break;
            case "outlook": case "olk": match = "Microsoft Outlook"; break;
            case "msedge": match = "Safari"; break;
            case "windowsterminal": case "powershell": case "pwsh": case "cmd": match = "Terminal"; break;
            case "photoshop": match = "Adobe Photoshop"; break;
            case "illustrator": match = "Adobe Illustrator"; break;
        }
        AppRule rule = null;
        foreach (AppRule candidate in Collection.Apps)
            if (match.StartsWith(candidate.Name, StringComparison.OrdinalIgnoreCase) && (rule == null || candidate.Name.Length > rule.Name.Length)) rule = candidate;
        uint hash = Hash(process);
        uint[] palette = Palettes[settings.Palette];
        uint color = settings.Palette == "by-app" && rule != null ? rule.Color : palette[hash % palette.Length];
        string[] fallback = { "zigzag", "picnic", "twinkle" };
        string pattern = settings.Pattern == "by-app" ? (rule == null ? fallback[hash % 3] : rule.Pattern) : settings.Pattern;
        return new AppRule(process, color, pattern);
    }
}
internal sealed class Knit : IDisposable {
    Bitmap tile;
    string tileKey;
    internal static Color Shade(Color c, float factor) { return Color.FromArgb(255, Math.Min(255, (int)(c.R * factor)), Math.Min(255, (int)(c.G * factor)), Math.Min(255, (int)(c.B * factor))); }
    internal static GraphicsPath Rounded(RectangleF rect, float radius) {
        GraphicsPath path = new GraphicsPath();
        if (radius <= 0) { path.AddRectangle(rect); return path; }
        float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
    Bitmap MakeTile(int band, AppRule rule, int stitch) {
        const int scale = 3;
        Chart chart;
        Collection.Charts.TryGetValue(rule.Pattern, out chart);
        int rows = chart == null ? 6 : chart.Rows.Length;
        int columns = chart == null ? 8 : chart.Rows[0].Length;
        float rowStep = band / (float)rows * stitch / 100f;
        float sw = rowStep * 1.35f;
        int tw = Math.Max(8, (int)Math.Round(sw * columns)), th = Math.Max(8, (int)Math.Round(rowStep * rows));
        sw = tw / (float)columns; rowStep = th / (float)rows;
        Bitmap image = new Bitmap(tw * scale, th * scale, PixelFormat.Format32bppPArgb);
        Color baseColor = Color.FromArgb(unchecked((int)rule.Color));
        using (Graphics g = Graphics.FromImage(image)) {
            g.ScaleTransform(scale, scale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Shade(baseColor, 0.72f));
            for (int row = -1; row <= rows; row++) for (int col = -1; col <= columns; col++) {
                Color color = baseColor;
                if (chart != null) {
                    char yarn = chart.Rows[(row + rows) % rows][(col + columns) % columns];
                    if (yarn >= 'a' && yarn - 'a' < chart.Colors.Length) color = Color.FromArgb(unchecked((int)chart.Colors[yarn - 'a']));
                    // Cream motifs retain contrast on pale application yarns.
                    if (rule.Pattern == "zigzag" && yarn == 'a' && baseColor.GetBrightness() > 0.70f) color = Shade(baseColor, 0.57f);
                }
                float x = col * sw, y = row * rowStep, sh = rowStep * 1.10f;
                using (var ground = new SolidBrush(Shade(color, 0.77f))) g.FillRectangle(ground, x, y, sw + 0.1f, rowStep + 0.1f);
                using (var path = new GraphicsPath()) {
                    // Cubic equivalent of the upstream nested, bowed V stitch.
                    path.AddBezier(x + sw * 0.05f, y, x + sw * 0.10f, y + sh * 0.48f, x + sw * 0.34f, y + sh * 0.88f, x + sw * 0.5f, y + sh);
                    path.StartFigure();
                    path.AddBezier(x + sw * 0.95f, y, x + sw * 0.90f, y + sh * 0.48f, x + sw * 0.66f, y + sh * 0.88f, x + sw * 0.5f, y + sh);
                    float[] widths = { 0.53f, 0.40f, 0.23f, 0.08f };
                    float[] shades = { 0.63f, 0.88f, 1.03f, 1.12f };
                    for (int layer = 0; layer < widths.Length; layer++) using (var pen = new Pen(Shade(color, shades[layer]), Math.Max(0.25f, sw * widths[layer]))) {
                        pen.StartCap = pen.EndCap = LineCap.Round; g.DrawPath(pen, path);
                    }
                }
            }
        }
        return image;
    }
    internal Bitmap Render(int w, int h, int band, float radius, AppRule rule, int stitch) {
        string key = band + ":" + rule.Color + ":" + rule.Pattern + ":" + stitch;
        if (key != tileKey) { if (tile != null) tile.Dispose(); tile = MakeTile(band, rule, stitch); tileKey = key; }
        var result = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(result))
        using (GraphicsPath ring = Rounded(new RectangleF(0, 0, w, h), radius + band))
        using (GraphicsPath hole = Rounded(new RectangleF(band, band, w - band * 2, h - band * 2), radius)) {
            ring.AddPath(hole, false); ring.FillMode = FillMode.Alternate;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Draw antialiased outer/inner edges once; side clips handle mitres.
            using (var mask = new Bitmap(w, h, PixelFormat.Format32bppPArgb)) {
                using (Graphics fabric = Graphics.FromImage(mask)) {
                    PointF[][] sides = {
                        new[] { new PointF(0, 0), new PointF(w, 0), new PointF(w-band, band), new PointF(band, band) },
                        new[] { new PointF(0, h), new PointF(w, h), new PointF(w-band, h-band), new PointF(band, h-band) },
                        new[] { new PointF(0, 0), new PointF(band, band), new PointF(band, h-band), new PointF(0, h) },
                        new[] { new PointF(w, 0), new PointF(w-band, band), new PointF(w-band, h-band), new PointF(w, h) }
                    };
                    Matrix[] transforms = { new Matrix(1f/3, 0, 0, 1f/3, 0, 0), new Matrix(1f/3, 0, 0, -1f/3, 0, h), new Matrix(0, 1f/3, 1f/3, 0, 0, 0), new Matrix(0, 1f/3, -1f/3, 0, w, 0) };
                    for (int side = 0; side < 4; side++) {
                        using (transforms[side]) using (var brush = new TextureBrush(tile, WrapMode.Tile)) {
                            brush.Transform = transforms[side]; fabric.FillPolygon(brush, sides[side]);
                        }
                    }
                }
                using (var brush = new TextureBrush(mask)) g.FillPath(brush, ring);
            }
        }
        return result;
    }
    internal static Icon MakeIcon() {
        using (var b = new Bitmap(32, 32)) using (Graphics g = Graphics.FromImage(b)) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Color.FromArgb(210, 135, 139))) g.FillEllipse(brush, 3, 3, 25, 25);
            using (var pen = new Pen(Color.FromArgb(250, 232, 210), 1.6f)) {
                g.DrawArc(pen, 3, 7, 27, 14, 20, 250); g.DrawArc(pen, 4, 12, 24, 12, 0, 200);
                g.DrawArc(pen, 9, 1, 14, 29, 80, 220); g.DrawArc(pen, 4, 2, 14, 30, 80, 200);
            }
            IntPtr h = b.GetHicon(); try { using (Icon temporary = Icon.FromHandle(h)) return (Icon)temporary.Clone(); } finally { Native.DestroyIcon(h); }
        }
    }
    public void Dispose() { if (tile != null) { tile.Dispose(); tile = null; } }
}
}
