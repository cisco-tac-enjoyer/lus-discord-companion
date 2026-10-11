using System;
using System.IO;

namespace LusDiscordCompanion
{
    public sealed class ActivityLog
    {
        const long MaxFileBytes = 1024 * 1024;
        readonly object _gate = new object();
        readonly string _path;

        public event Action<string> LineAdded;

        public ActivityLog(string path)
        {
            _path = path;
        }

        public void Write(string message)
        {
            var line = DateTime.Now.ToString("HH:mm:ss") + "  " + AppSettings.Redact(message);
            lock (_gate)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path));
                    var info = new FileInfo(_path);
                    if (info.Exists && info.Length > MaxFileBytes)
                    {
                        var old = Path.ChangeExtension(_path, ".old.txt");
                        File.Delete(old);
                        File.Move(_path, old);
                    }
                    File.AppendAllText(_path, DateTime.Now.ToString("yyyy-MM-dd") + " " + line + Environment.NewLine);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            var handler = LineAdded;
            if (handler != null)
                handler(line);
        }
    }
}
