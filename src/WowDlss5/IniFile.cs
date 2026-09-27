using System.Text;
using System.Text.RegularExpressions;

namespace WowDlss5;

/// <summary>
/// Правка ini/cfg построчно: чужие строки, комментарии, порядок и выравнивание сохраняются.
/// Section = null — ключи до первой секции (dlss5-feed.cfg, dxvk.conf без секций).
/// </summary>
sealed class IniFile
{
    readonly string _path;
    readonly List<string> _lines;
    readonly string _nl;
    readonly bool _bom;

    IniFile(string path, List<string> lines, string nl, bool bom) { _path = path; _lines = lines; _nl = nl; _bom = bom; }

    public static IniFile Load(string path)
    {
        if (!File.Exists(path)) return new IniFile(path, new List<string>(), "\r\n", false);
        var bytes = File.ReadAllBytes(path);
        bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
        var nl = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return new IniFile(path, lines, nl, bom);
    }

    static bool IsSection(string line, out string name)
    {
        var t = line.Trim();
        if (t.StartsWith('[') && t.EndsWith(']')) { name = t[1..^1].Trim(); return true; }
        name = null;
        return false;
    }

    // [start, end) строк секции (без заголовка). Для section == null — всё до первой секции.
    bool FindSection(string section, out int start, out int end)
    {
        start = end = -1;
        if (section == null)
        {
            start = 0;
            end = _lines.FindIndex(l => IsSection(l, out _));
            if (end < 0) end = _lines.Count;
            return true;
        }
        for (int i = 0; i < _lines.Count; i++)
        {
            if (IsSection(_lines[i], out var n) && n.Equals(section, StringComparison.OrdinalIgnoreCase))
            {
                start = i + 1;
                end = _lines.FindIndex(start, l => IsSection(l, out _));
                if (end < 0) end = _lines.Count;
                return true;
            }
        }
        return false;
    }

    static Regex KeyRx(string key) => new(@"^\s*" + Regex.Escape(key) + @"\s*=(\s*)(.*)$", RegexOptions.IgnoreCase);

    public string Get(string section, string key)
    {
        if (!FindSection(section, out var s, out var e)) return null;
        var rx = KeyRx(key);
        for (int i = s; i < e; i++)
        {
            var m = rx.Match(_lines[i]);
            if (m.Success) return m.Groups[2].Value.Trim();
        }
        return null;
    }

    /// <summary>value = null удаляет ключ.</summary>
    public void Set(string section, string key, string value)
    {
        if (!FindSection(section, out var s, out var e))
        {
            if (value == null) return;
            if (_lines.Count > 0 && _lines[^1].Trim().Length > 0) _lines.Add("");
            _lines.Add("[" + section + "]");
            _lines.Add(key + "=" + value);
            return;
        }
        var rx = KeyRx(key);
        for (int i = s; i < e; i++)
        {
            var m = rx.Match(_lines[i]);
            if (!m.Success) continue;
            if (value == null) _lines.RemoveAt(i);
            else _lines[i] = _lines[i].Substring(0, m.Groups[2].Index) + value;
            return;
        }
        if (value == null) return;
        // Новый ключ — после последней непустой строки секции.
        int at = e;
        while (at > s && _lines[at - 1].Trim().Length == 0) at--;
        _lines.Insert(at, key + "=" + value);
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path));
        var text = string.Join(_nl, _lines) + _nl;
        File.WriteAllText(_path, text, new UTF8Encoding(_bom));
    }
}

/// <summary>WTF\Config.wtf: строки вида SET key "value". Клиент пишет его с LF — формат сохраняется.</summary>
static class WtfConfig
{
    static Regex Rx(string key) => new(@"^SET\s+" + Regex.Escape(key) + @"\s+""(.*)""\s*$", RegexOptions.IgnoreCase);

    public static string Get(string path, string key)
    {
        if (!File.Exists(path)) return null;
        foreach (var l in File.ReadAllLines(path))
        {
            var m = Rx(key).Match(l);
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    /// <summary>value = null удаляет ключ.</summary>
    public static void Set(string path, IDictionary<string, string> values)
    {
        string nl = "\n";
        var lines = new List<string>();
        if (File.Exists(path))
        {
            var text = File.ReadAllText(path);
            if (text.Contains("\r\n")) nl = "\r\n";
            lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        }
        foreach (var (key, value) in values)
        {
            var rx = new Regex(@"^SET\s+" + Regex.Escape(key) + @"\s+", RegexOptions.IgnoreCase);
            int idx = lines.FindIndex(l => rx.IsMatch(l));
            if (value == null) { if (idx >= 0) lines.RemoveAt(idx); }
            else if (idx >= 0) lines[idx] = $"SET {key} \"{value}\"";
            else lines.Add($"SET {key} \"{value}\"");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, string.Join(nl, lines) + nl, new UTF8Encoding(false));
    }
}
