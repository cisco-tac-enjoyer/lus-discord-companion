using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace LusDiscordCompanion
{
    public sealed class PostedLedger
    {
        const int MaxEntries = 5000;

        sealed class Entry
        {
            public string Name;
            public long Size;
            public DateTimeOffset PostedAt;
        }

        readonly string _path;
        readonly object _gate = new object();
        readonly List<Entry> _posted = new List<Entry>();
        DateTimeOffset? _baselineAt;
        string _folder;
        string _format;

        PostedLedger(string path)
        {
            _path = path;
        }

        public static string DefaultPath
        {
            get { return Path.Combine(AppSettings.DataDir, "posted.json"); }
        }

        public static PostedLedger Load(string path = null)
        {
            var ledger = new PostedLedger(path ?? DefaultPath);
            try
            {
                if (File.Exists(ledger._path))
                    ledger.Read(AppSettings.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(ledger._path, Encoding.UTF8)));
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (IOException)
            {
            }
            return ledger;
        }

        void Read(Dictionary<string, object> values)
        {
            if (values == null)
                return;
            object value;
            if (values.TryGetValue("baselineAt", out value))
                _baselineAt = ParseTime(value as string);
            if (values.TryGetValue("folder", out value))
                _folder = value as string;
            if (values.TryGetValue("format", out value))
                _format = value as string;
            if (values.TryGetValue("posted", out value) && value is IEnumerable)
            {
                foreach (var item in ((IEnumerable)value).OfType<Dictionary<string, object>>())
                {
                    object name, size, postedAt;
                    if (!item.TryGetValue("name", out name) || !(name is string))
                        continue;
                    item.TryGetValue("size", out size);
                    item.TryGetValue("postedAt", out postedAt);
                    _posted.Add(new Entry
                    {
                        Name = (string)name,
                        Size = size == null ? 0 : Convert.ToInt64(size, CultureInfo.InvariantCulture),
                        PostedAt = ParseTime(postedAt as string) ?? DateTimeOffset.MinValue,
                    });
                }
            }
        }

        static DateTimeOffset? ParseTime(string text)
        {
            DateTimeOffset result;
            if (text != null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return result;
            return null;
        }

        public DateTimeOffset? BaselineAt
        {
            get { lock (_gate) return _baselineAt; }
        }

        public void EnsureBaseline(string folder, string format, DateTimeOffset now)
        {
            folder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
            lock (_gate)
            {
                if (_baselineAt != null
                    && string.Equals(_folder, folder, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(_format, format, StringComparison.OrdinalIgnoreCase))
                    return;
                _baselineAt = now;
                _folder = folder;
                _format = format;
                SaveLocked();
            }
        }

        public bool Contains(string name)
        {
            lock (_gate)
                return _posted.Exists(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public void Add(string name, long size, DateTimeOffset postedAt)
        {
            lock (_gate)
            {
                _posted.Add(new Entry { Name = name, Size = size, PostedAt = postedAt });
                if (_posted.Count > MaxEntries)
                    _posted.RemoveRange(0, _posted.Count - MaxEntries);
                SaveLocked();
            }
        }

        void SaveLocked()
        {
            var values = new Dictionary<string, object>
            {
                { "baselineAt", _baselineAt == null ? null : _baselineAt.Value.ToString("o", CultureInfo.InvariantCulture) },
                { "folder", _folder },
                { "format", _format },
                { "posted", _posted.Select(e => new Dictionary<string, object>
                    {
                        { "name", e.Name },
                        { "size", e.Size },
                        { "postedAt", e.PostedAt.ToString("o", CultureInfo.InvariantCulture) },
                    }).ToList() },
            };
            AtomicFile.WriteAllText(_path, AppSettings.Json.Serialize(values));
        }
    }
}
