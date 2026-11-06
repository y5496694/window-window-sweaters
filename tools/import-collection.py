"""Regenerate the attributed, embedded collection from an upstream checkout."""
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1])
dest = pathlib.Path(__file__).resolve().parents[1] / 'src' / 'Collection.cs'
charts = re.findall(r'\{ "([\w-]+)", \{(.*?)\},\s*\{(.*?)\} \}', (root / 'src/chart.c').read_text(encoding='utf-8'), re.S)
apps_block = (root / 'src/apps.c').read_text(encoding='utf-8').split('static const struct app_rule k_collection[] = {')[1].split('\n};')[0]
apps = re.findall(r'\{ "([^"]+)",\s*(0x[0-9a-f]+)u, "([\w-]+)" \}', apps_block)
out = ['// Adapted from saragordic/window-sweaters src/chart.c and src/apps.c.',
       '// Upstream commit: 4c9376bd774b09c9653c108c1961219edb16c33c; GPL-3.0.',
       'using System.Collections.Generic;', 'namespace WindowSweaters {',
       'internal static class Collection {',
       'internal static readonly Dictionary<string, Chart> Charts = new Dictionary<string, Chart> {']
for name, rows, colors in charts:
    rs = re.findall(r'"([.a-f]+)"', rows)
    cs = re.findall(r'(0x[0-9a-f]+)u', colors)
    assert rs and len(set(map(len, rs))) == 1
    out.append('    { "%s", new Chart(new string[] { %s }, new uint[] { %s }) },' % (name, ', '.join('"' + r + '"' for r in rs), ', '.join(c + 'u' for c in cs)))
out += ['};', 'internal static readonly AppRule[] Apps = new AppRule[] {']
for name, color, chart in apps:
    out.append('    new AppRule("%s", %su, "%s"),' % (name, color, chart))
out += ['};', '}', '}']
dest.parent.mkdir(parents=True, exist_ok=True)
dest.write_text('\n'.join(out) + '\n', encoding='utf-8')
print(f'Imported {len(charts)} charts and {len(apps)} app rules.')
