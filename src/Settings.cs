using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace WindowSweaters {
internal sealed class Settings {
    internal int Width = 24, Stitch = 100;
    internal string Pattern = "by-app", Palette = "by-app";
    internal bool Paused, Maximized;
    internal readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    internal readonly string DirectoryPath;
    internal Settings(string directory) { DirectoryPath = directory; }
    internal string FilePath { get { return Path.Combine(DirectoryPath, "settings.ini"); } }
    internal void Load() {
        if (!File.Exists(FilePath)) return;
        foreach (string line in File.ReadAllLines(FilePath)) {
            int eq = line.IndexOf('='); if (eq < 0) continue;
            string key = line.Substring(0, eq), value = line.Substring(eq + 1); int n;
            if (key == "width" && int.TryParse(value, out n)) Width = Math.Max(12, Math.Min(48, n));
            if (key == "stitch" && int.TryParse(value, out n)) Stitch = Math.Max(75, Math.Min(150, n));
            if (key == "pattern" && (value == "by-app" || value == "plain" || Collection.Charts.ContainsKey(value))) Pattern = value;
            if (key == "palette" && Yarn.Palettes.ContainsKey(value)) Palette = value;
            if (key == "paused") Paused = value == "true";
            if (key == "maximized") Maximized = value == "true";
            if (key == "exclude" && value.Length > 0) Excluded.Add(value);
        }
    }
    internal void Save() {
        Directory.CreateDirectory(DirectoryPath);
        var lines = new List<string> { "# Window Window Sweaters - portable settings", "width=" + Width.ToString(CultureInfo.InvariantCulture), "stitch=" + Stitch.ToString(CultureInfo.InvariantCulture), "pattern=" + Pattern, "palette=" + Palette, "paused=" + Paused.ToString().ToLowerInvariant(), "maximized=" + Maximized.ToString().ToLowerInvariant() };
        foreach (string name in Excluded) lines.Add("exclude=" + name);
        string temp = FilePath + ".tmp";
        File.WriteAllLines(temp, lines.ToArray());
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
    }
}
}
