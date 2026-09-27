using System.IO.Compression;
using System.Text;

namespace WowDlss5;

/// <summary>
/// Файлы DLSS-стека, вшитые в exe. build.ps1 дописывает zip в конец собранного exe, а за ним —
/// 16 байт: смещение zip (Int64) и сигнатуру. Пока программа запущена из-под отладки
/// (сигнатуры нет), файлы берутся из папки vendor рядом с исходниками.
/// </summary>
static class Payload
{
    const string Magic = "WDLSS5P1";

    static readonly object Sync = new();
    static ZipArchive _zip;
    static string _dir;
    static bool _opened;

    public static string Source { get; private set; } = "";

    static void EnsureOpen()
    {
        lock (Sync)
        {
            if (_opened) return;
            _opened = true;

            var exe = Environment.ProcessPath;
            if (exe != null && File.Exists(exe))
            {
                var fs = new FileStream(exe, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length > 16)
                {
                    fs.Seek(-16, SeekOrigin.End);
                    var tail = new byte[16];
                    fs.ReadExactly(tail);
                    if (Encoding.ASCII.GetString(tail, 8, 8) == Magic)
                    {
                        long offset = BitConverter.ToInt64(tail, 0);
                        _zip = new ZipArchive(new SubStream(fs, offset, fs.Length - 16 - offset), ZipArchiveMode.Read);
                        Source = "встроенные файлы";
                        return;
                    }
                }
                fs.Dispose();
            }

            // Отладка: ищем vendor\ вверх по дереву от exe.
            var d = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && d != null; i++)
            {
                var v = Path.Combine(d, "vendor");
                if (Directory.Exists(Path.Combine(v, "common"))) { _dir = v; Source = v; return; }
                d = Path.GetDirectoryName(d.TrimEnd('\\'));
            }
        }
    }

    public static bool Available { get { EnsureOpen(); return _zip != null || _dir != null; } }

    /// <summary>Файлы под префиксом ("common/"): путь относительно префикса, размер, открытие.</summary>
    public static List<(string Rel, long Size, Func<Stream> Open)> List(string prefix)
    {
        EnsureOpen();
        var result = new List<(string, long, Func<Stream>)>();
        if (_zip != null)
        {
            foreach (var e in _zip.Entries)
            {
                // ZipFile из .NET Framework (PowerShell 5.1) пишет пути с '\' — приводим к '/'.
                var full = e.FullName.Replace('\\', '/');
                if (full.EndsWith('/') || !full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var entry = e;
                result.Add((full.Substring(prefix.Length).Replace('/', '\\'), e.Length, () => entry.Open()));
            }
        }
        else if (_dir != null)
        {
            var root = Path.Combine(_dir, prefix.TrimEnd('/').Replace('/', '\\'));
            if (Directory.Exists(root))
                foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    var path = f;
                    result.Add((Path.GetRelativePath(root, f), new FileInfo(f).Length, () => File.OpenRead(path)));
                }
        }
        return result;
    }

    /// <summary>Распаковать всё под префиксом в папку. Zip читается под замком: ZipArchive не потокобезопасен.</summary>
    public static void ExtractTo(string prefix, string destDir, Action<long> onBytes = null)
    {
        var files = List(prefix);
        if (files.Count == 0) throw new InvalidDataException("Во встроенных файлах нет раздела " + prefix + " — exe собран неправильно.");
        foreach (var (rel, _, open) in files)
        {
            var dest = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            lock (Sync)
            {
                using var src = open();
                using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                var buf = new byte[1 << 20];
                int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    onBytes?.Invoke(n);
                }
            }
        }
    }

    public static long Size(string prefix) => List(prefix).Sum(x => x.Size);

    /// <summary>Имена верхнего уровня под префиксом: файлы и папки, которые окажутся в корне игры.</summary>
    public static IEnumerable<string> TopLevel(string prefix) =>
        List(prefix).Select(x => x.Rel.Split('\\')[0]).Distinct(StringComparer.OrdinalIgnoreCase);

    public static void ExtractFile(string entry, string dest)
    {
        var slash = entry.LastIndexOf('/');
        var prefix = entry.Substring(0, slash + 1);
        var name = entry.Substring(slash + 1);
        var item = List(prefix).FirstOrDefault(x => x.Rel.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (item.Open == null) throw new FileNotFoundException("Нет во встроенных файлах: " + entry);
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        lock (Sync)
        {
            using var src = item.Open();
            using var dst = File.Create(dest);
            src.CopyTo(dst);
        }
    }
}

/// <summary>Окно только для чтения в середину другого потока (zip в хвосте exe).</summary>
sealed class SubStream : Stream
{
    readonly Stream _base;
    readonly long _start, _length;
    long _pos;

    public SubStream(Stream baseStream, long start, long length) { _base = baseStream; _start = start; _length = length; }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _pos; set => _pos = value; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_pos >= _length) return 0;
        count = (int)Math.Min(count, _length - _pos);
        _base.Position = _start + _pos;
        int n = _base.Read(buffer, offset, count);
        _pos += n;
        return n;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _pos = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => _pos + offset, _ => _length + offset };
        return _pos;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
