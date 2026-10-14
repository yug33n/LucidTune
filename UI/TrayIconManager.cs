using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using LucidTune.Config;
using LucidTune.Services;

namespace LucidTune.UI
{
    public class TrayIconManager : IDisposable
    {
        private const int WM_APP = 0x8000;
        public const int WM_TRAYICON = WM_APP + 101;

        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;

        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public uint uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hinst, IntPtr uId, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        private IntPtr _hWnd;
        private HwndSource? _hwndSource;
        private IntPtr _hIcon = IntPtr.Zero;
        private bool _isCreated;
        private bool _disposed;

        private readonly ConfigManager _configManager;
        private readonly Action _toggleVisibilityAction;
        private readonly Action _hotkeySettingsAction;
        private readonly Func<bool>? _isWidgetVisibleGetter;
        private ContextMenu? _trayContextMenu;
        private MenuItem? _toggleItem;
        private MenuItem? _autoHideItem;
        private MenuItem? _autoStartItem;

        public TrayIconManager(ConfigManager configManager, Action toggleVisibilityAction, Action hotkeySettingsAction, Func<bool>? isWidgetVisibleGetter = null)
        {
            _configManager = configManager;
            _toggleVisibilityAction = toggleVisibilityAction;
            _hotkeySettingsAction = hotkeySettingsAction;
            _isWidgetVisibleGetter = isWidgetVisibleGetter;
        }

        public void Initialize(IntPtr windowHandle)
        {
            _hWnd = windowHandle;
            _hwndSource = HwndSource.FromHwnd(_hWnd);
            _hwndSource?.AddHook(HwndHook);

            _hIcon = LoadAppIcon();
            CreateTrayIcon();
            BuildContextMenu();
        }

        private IntPtr LoadAppIcon()
        {
            try
            {
                // 1. Try loading from file next to exe (portable / unpacked mode)
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                if (File.Exists(iconPath))
                {
                    IntPtr hLoaded = LoadImage(IntPtr.Zero, iconPath, 1, 16, 16, 0x00000010);
                    if (hLoaded != IntPtr.Zero) return hLoaded;
                }

                // 2. Try extracting from current executable (works in standalone single-file mode)
                var procPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(procPath) && File.Exists(procPath))
                {
                    if (ExtractIconEx(procPath, 0, out _, out var hSmall, 1) > 0 && hSmall != IntPtr.Zero)
                    {
                        return hSmall;
                    }
                }

                // 3. Try loading icon resource from the current module
                IntPtr hModule = GetModuleHandle(null);
                if (hModule != IntPtr.Zero)
                {
                    IntPtr hModIcon = LoadImage(hModule, (IntPtr)1, 1, 16, 16, 0);
                    if (hModIcon != IntPtr.Zero) return hModIcon;

                    IntPtr hModIcon32512 = LoadImage(hModule, (IntPtr)32512, 1, 16, 16, 0);
                    if (hModIcon32512 != IntPtr.Zero) return hModIcon32512;
                }
            }
            catch
            {
                // Fallback to application icon
            }

            return LoadIcon(IntPtr.Zero, (IntPtr)32512);
        }

        private void CreateTrayIcon()
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1001,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "LucidTune"
            };

            _isCreated = Shell_NotifyIcon(NIM_ADD, ref nid);
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenu();

            _toggleItem = new MenuItem
            {
                Header = _isWidgetVisibleGetter != null && _isWidgetVisibleGetter() ? "Скрыть" : "Показать"
            };
            _toggleItem.Click += (s, e) =>
            {
                _toggleVisibilityAction();
                UpdateState();
            };

            var hotkeyItem = new MenuItem
            {
                Header = "Настройки виджета"
            };
            hotkeyItem.Click += (s, e) => _hotkeySettingsAction();

            _autoHideItem = new MenuItem
            {
                Header = "Скрывать в полноэкранных играх",
                IsCheckable = true,
                IsChecked = _configManager.CurrentConfig.AutoHideInFullscreen
            };
            _autoHideItem.Click += (s, e) =>
            {
                var cfg = _configManager.CurrentConfig;
                cfg.AutoHideInFullscreen = !cfg.AutoHideInFullscreen;
                _autoHideItem.IsChecked = cfg.AutoHideInFullscreen;
                _configManager.Save(cfg);
            };

            _autoStartItem = new MenuItem
            {
                Header = "Автозагрузка",
                IsCheckable = true,
                IsChecked = AutoStartupService.IsAutoStartEnabled()
            };
            _autoStartItem.Click += (s, e) =>
            {
                bool newState = !AutoStartupService.IsAutoStartEnabled();
                AutoStartupService.SetAutoStart(newState);
                _autoStartItem.IsChecked = newState;

                var cfg = _configManager.CurrentConfig;
                cfg.AutoStart = newState;
                _configManager.Save(cfg);
            };

            var exitItem = new MenuItem
            {
                Header = "Выход",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B))
            };
            exitItem.Click += (s, e) =>
            {
                Application.Current.Shutdown();
            };

            menu.Items.Add(_toggleItem);
            menu.Items.Add(hotkeyItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(_autoHideItem);
            menu.Items.Add(_autoStartItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);

            _trayContextMenu = menu;
        }

        private void UpdateState()
        {
            if (_toggleItem != null && _isWidgetVisibleGetter != null)
            {
                _toggleItem.Header = _isWidgetVisibleGetter() ? "Скрыть" : "Показать";
            }

            if (_autoHideItem != null)
            {
                _autoHideItem.IsChecked = _configManager.CurrentConfig.AutoHideInFullscreen;
            }

            if (_autoStartItem != null)
            {
                _autoStartItem.IsChecked = AutoStartupService.IsAutoStartEnabled();
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                int mouseEvent = lParam.ToInt32();
                if (mouseEvent == WM_LBUTTONUP)
                {
                    _toggleVisibilityAction();
                    handled = true;
                }
                else if (mouseEvent == WM_RBUTTONUP)
                {
                    ShowContextMenu();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        private void ShowContextMenu()
        {
            if (_trayContextMenu == null) return;

            UpdateState();
            SetForegroundWindow(_hWnd);
            _trayContextMenu.Placement = PlacementMode.MousePoint;
            _trayContextMenu.IsOpen = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_isCreated)
            {
                var nid = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _hWnd,
                    uID = 1001
                };
                Shell_NotifyIcon(NIM_DELETE, ref nid);
                _isCreated = false;
            }

            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

            _hwndSource?.RemoveHook(HwndHook);
        }
    }
}
