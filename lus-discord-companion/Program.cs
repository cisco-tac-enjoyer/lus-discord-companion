using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("LUS Discord Companion")]
[assembly: AssemblyProduct("LUS Discord Companion")]
[assembly: AssemblyCompany("cisco-tac-enjoyer")]
[assembly: AssemblyCopyright("Copyright (c) 2026 cisco-tac-enjoyer, MIT License")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace LusDiscordCompanion
{
    static class Program
    {
        const string MutexName = "lus-discord-companion";
        const string ShowEventName = "lus-discord-companion.Show";

        [STAThread]
        static void Main(string[] args)
        {
            bool isFirstInstance;
            using (var mutex = new Mutex(true, MutexName, out isFirstInstance))
            {
                if (!isFirstInstance)
                {
                    EventWaitHandle existing;
                    if (EventWaitHandle.TryOpenExisting(ShowEventName, out existing))
                    {
                        existing.Set();
                        existing.Dispose();
                    }
                    return;
                }

                using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    var startMinimized = args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));
                    using (var form = new MainForm(startMinimized))
                    {
                        var listener = new Thread(() =>
                        {
                            while (showEvent.WaitOne())
                            {
                                if (form.IsDisposed)
                                    return;
                                try
                                {
                                    form.BeginInvoke(new Action(form.ShowFromTray));
                                }
                                catch (InvalidOperationException)
                                {
                                    return;
                                }
                            }
                        });
                        listener.IsBackground = true;
                        listener.Start();

                        Application.Run(form);
                    }
                }
                GC.KeepAlive(mutex);
            }
        }
    }
}
