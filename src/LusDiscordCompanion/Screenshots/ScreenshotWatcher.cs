using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LusDiscordCompanion
{
    public sealed class WatcherTimings
    {
        public TimeSpan Rescan = TimeSpan.FromSeconds(10);
        public TimeSpan StableFor = TimeSpan.FromSeconds(1);
        public TimeSpan GiveUpAfter = TimeSpan.FromSeconds(30);
        public TimeSpan Poll = TimeSpan.FromMilliseconds(250);
    }

    public sealed class ScreenshotWatcher : IDisposable
    {
        readonly string _folder;
        readonly string _extension;
        readonly string _format;
        readonly PostedLedger _ledger;
        readonly Func<string, CancellationToken, Task> _upload;
        readonly WatcherTimings _timings;
        readonly HashSet<string> _queued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> _failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        FileSystemWatcher _fsw;
        volatile bool _running;

        public event Action<string> Posted;
        public event Action<string, Exception> Failed;
        public event Action WebhookInvalid;

        public ScreenshotWatcher(string folder, string format, PostedLedger ledger,
            Func<string, CancellationToken, Task> upload, WatcherTimings timings = null)
        {
            _folder = folder;
            _format = format.ToLowerInvariant();
            _extension = "." + _format;
            _ledger = ledger;
            _upload = upload;
            _timings = timings ?? new WatcherTimings();
        }

        public bool IsRunning
        {
            get { return _running; }
        }

        public void Start()
        {
            _ledger.EnsureBaseline(_folder, _format, DateTimeOffset.UtcNow);

            _fsw = new FileSystemWatcher(_folder, "*" + _extension) { IncludeSubdirectories = false };
            _fsw.Created += (s, e) => Enqueue(e.FullPath);
            _fsw.Renamed += (s, e) => Enqueue(e.FullPath);
            _fsw.EnableRaisingEvents = true;
            _running = true;

            Scan();
            var token = _cts.Token;
            Task.Run(() => WorkAsync(token));
            Task.Run(() => RescanAsync(token));
        }

        public void Stop()
        {
            if (!_running)
                return;
            _running = false;
            _cts.Cancel();
            if (_fsw != null)
            {
                _fsw.Dispose();
                _fsw = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        void Scan()
        {
            var baseline = _ledger.BaselineAt ?? DateTimeOffset.MinValue;
            List<FileInfo> files;
            try
            {
                files = new DirectoryInfo(_folder).EnumerateFiles("*" + _extension)
                    .Where(f => new DateTimeOffset(f.LastWriteTimeUtc) > baseline)
                    .OrderBy(f => f.LastWriteTimeUtc)
                    .ToList();
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            foreach (var file in files)
                Enqueue(file.FullName);
        }

        void Enqueue(string path)
        {
            if (!_running || !string.Equals(Path.GetExtension(path), _extension, StringComparison.OrdinalIgnoreCase))
                return;
            var name = Path.GetFileName(path);
            lock (_queued)
            {
                if (_failed.Contains(name) || _ledger.Contains(name) || !_queued.Add(name))
                    return;
            }
            _queue.Enqueue(path);
            _signal.Release();
        }

        async Task RescanAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(_timings.Rescan, ct).ConfigureAwait(false);
                    Scan();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task WorkAsync(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    await _signal.WaitAsync(ct).ConfigureAwait(false);
                    string path;
                    if (!_queue.TryDequeue(out path))
                        continue;
                    if (!await ProcessAsync(path, ct).ConfigureAwait(false))
                        return;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task<bool> ProcessAsync(string path, CancellationToken ct)
        {
            var name = Path.GetFileName(path);
            Exception failure = null;
            bool invalid = false;
            try
            {
                if (_ledger.Contains(name))
                    return true;
                var size = await WaitUntilReadyAsync(path, ct).ConfigureAwait(false);
                await _upload(path, ct).ConfigureAwait(false);
                _ledger.Add(name, size, DateTimeOffset.UtcNow);
                var posted = Posted;
                if (posted != null)
                    posted(name);
            }
            catch (WebhookInvalidException)
            {
                invalid = true;
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                    throw;
                failure = new TimeoutException("The upload timed out.");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                lock (_queued)
                    _queued.Remove(name);
            }

            if (invalid)
            {
                Stop();
                var handler = WebhookInvalid;
                if (handler != null)
                    handler();
                return false;
            }

            if (failure != null)
            {
                lock (_queued)
                    _failed.Add(name);
                var failed = Failed;
                if (failed != null)
                    failed(name, failure);
            }
            return true;
        }

        async Task<long> WaitUntilReadyAsync(string path, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow + _timings.GiveUpAfter;
            long lastSize = -1;
            var stableSince = DateTime.UtcNow;

            while (true)
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException("The file disappeared before it could be posted.", path);

                long size;
                try
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                        size = stream.Length;
                }
                catch (IOException)
                {
                    size = -1;
                }

                var now = DateTime.UtcNow;
                if (size > 0 && size == lastSize)
                {
                    if (now - stableSince >= _timings.StableFor)
                        return size;
                }
                else
                {
                    lastSize = size;
                    stableSince = now;
                }

                if (now > deadline)
                    throw new TimeoutException("The file was still being written after 30 seconds.");

                await Task.Delay(_timings.Poll, ct).ConfigureAwait(false);
            }
        }
    }
}
