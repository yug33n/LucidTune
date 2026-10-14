using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using LucidTune.Config;
using LucidTune.Services;

namespace LucidTune
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;
        private static EventWaitHandle? _showSettingsSignal;
        private static bool _ownsMutex;

        public static bool IsAutoStartLaunch { get; private set; }

        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                Debug.WriteLine($"[DispatcherUnhandledException] {e.Exception}");
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Debug.WriteLine($"[UnhandledException] {e.ExceptionObject}");
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

            // Single instance management via Mutex
            try
            {
                _singleInstanceMutex = new Mutex(true, @"Local\LucidTune_SingleInstance_Mutex_v102", out _ownsMutex);
                if (!_ownsMutex)
                {
                    // Another instance is already running
                    if (!IsAutoStartLaunch)
                    {
                        // Signal running instance to show its settings window
                        try
                        {
                            if (EventWaitHandle.TryOpenExisting(@"Local\LucidTune_ShowSettings_Signal_v102", out var signal))
                            {
                                signal.Set();
                                signal.Dispose();
                            }
                        }
                        catch { }
                    }
                    Shutdown();
                    return;
                }

                _showSettingsSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\LucidTune_ShowSettings_Signal_v102");
                StartSignalListener();
            }
            catch
            {
                // Proceed if named mutex cannot be created
            }

            try
            {
                var config = new ConfigManager().CurrentConfig;
                ThemeManager.ApplyTheme(config.Theme, config.AccentColor);
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OnStartup Exception] {ex}");
            }
        }

        private void StartSignalListener()
        {
            var thread = new Thread(() =>
            {
                while (_ownsMutex)
                {
                    try
                    {
                        if (_showSettingsSignal != null && _showSettingsSignal.WaitOne())
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
                _showSettingsSignal?.Dispose();
                if (_ownsMutex)
                {
                    _singleInstanceMutex?.ReleaseMutex();
                }
                _singleInstanceMutex?.Dispose();
            }
            catch { }
            base.OnExit(e);
        }
    }
}
