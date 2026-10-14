using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using LucidTune.Config;
using LucidTune.Services;

namespace LucidTune
{
    public partial class App : Application
    {
        private static EventWaitHandle? _singleInstanceSignal;
        private static bool _ownsSignal;

        public static bool IsAutoStartLaunch { get; private set; }

        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                Debug.WriteLine($"[DispatcherUnhandledException] {e.Exception}");
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Determine if launched automatically on Windows startup
            if (e.Args != null)
            {
                foreach (var arg in e.Args)
                {
                    if (arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("-autostart", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("--startup", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
                    {
                        IsAutoStartLaunch = true;
                        break;
                    }
                }
            }

            // Single instance management
            try
            {
                _singleInstanceSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "LucidTune_SingleInstance_Signal", out _ownsSignal);
                if (!_ownsSignal)
                {
                    // Another instance is already running
                    if (!IsAutoStartLaunch)
                    {
                        // Signal running instance to show its settings window
                        _singleInstanceSignal.Set();
                    }
                    Shutdown();
                    return;
                }

                StartSignalListener();
            }
            catch
            {
                // Proceed if named event cannot be created
            }

            var config = new ConfigManager().CurrentConfig;
            ThemeManager.ApplyTheme(config.Theme, config.AccentColor);

            base.OnStartup(e);
        }

        private void StartSignalListener()
        {
            var thread = new Thread(() =>
            {
                while (_ownsSignal)
                {
                    try
                    {
                        if (_singleInstanceSignal != null && _singleInstanceSignal.WaitOne())
                        {
                            Dispatcher.BeginInvoke(() =>
                            {
                                if (MainWindow is MainWindow mw)
                                {
                                    mw.OpenSettingsWindow();
                                }
                            });
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            })
            {
                IsBackground = true,
                Name = "LucidTune_SingleInstanceListener"
            };
            thread.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _singleInstanceSignal?.Dispose();
            }
            catch { }
            base.OnExit(e);
        }
    }
}
