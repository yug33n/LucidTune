using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LucidTune.Config;
using LucidTune.Models;
using LucidTune.Services;
using Microsoft.Win32;

namespace LucidTune.UI
{
    public partial class TaskbarWidgetWindow : Window
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const double MAX_WIDGET_WIDTH = 150.0;
        private const double MIN_WIDGET_WIDTH = 54.0;

        private readonly SmtcMediaService _mediaService;
        private readonly ConfigManager _configManager;
        private readonly AudioVolumeService _volumeService;
        private readonly MainWindow _mainWindow;

        private readonly Geometry _playGeometry;
        private readonly Geometry _pauseGeometry;
        private readonly DispatcherTimer _repositionTimer;

        private double _currentProgress;
        private TimeSpan _lastDuration = TimeSpan.Zero;
        private bool _isDragging;
        private bool _isFullscreenHidden;
        private MediaTrackInfo? _lastTrack;

        private double _detectedAppsRightEdge = 0;
        private double _detectedTrayLeftEdge = 0;
        private double _detectedWidgetsRightEdge = 0;
        private bool _isDetectingBoundaries;
        private DateTime _lastBoundariesDetectionUtc = DateTime.MinValue;

        public TaskbarWidgetWindow(
            SmtcMediaService mediaService,
            ConfigManager configManager,
            AudioVolumeService volumeService,
            MainWindow mainWindow)
        {
            InitializeComponent();

            _mediaService = mediaService;
            _configManager = configManager;
            _volumeService = volumeService;
            _mainWindow = mainWindow;

            _playGeometry = (Geometry)FindResource("PlayGeometry");
            _pauseGeometry = (Geometry)FindResource("PauseGeometry");

            _repositionTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(1200)
            };
            _repositionTimer.Tick += OnRepositionTick;

            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
            ProgressBarTrack.SizeChanged += (s, e) => UpdateProgressBarWidth();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            RepositionOnTaskbar();
            _repositionTimer.Start();
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _repositionTimer.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(RepositionOnTaskbar);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            }
        }

        private void OnRepositionTick(object? sender, EventArgs e)
        {
            RepositionOnTaskbar();

            // Run taskbar boundary detection in background to prevent UI lag
            if (!_isDetectingBoundaries && (DateTime.UtcNow - _lastBoundariesDetectionUtc).TotalSeconds >= 1.2)
            {
                _isDetectingBoundaries = true;
                _lastBoundariesDetectionUtc = DateTime.UtcNow;

                IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
                var dpi = VisualTreeHelper.GetDpi(this);
                double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;

                _ = Task.Run(() =>
                {
                    try
                    {
                        var (appsRight, trayLeft, widgetsRight) = DetectTaskbarBoundaries(taskbarHwnd, dpiX);
                        Dispatcher.Invoke(() =>
                        {
                            bool changed = Math.Abs(_detectedAppsRightEdge - appsRight) > 2 ||
                                           Math.Abs(_detectedTrayLeftEdge - trayLeft) > 2 ||
                                           Math.Abs(_detectedWidgetsRightEdge - widgetsRight) > 2;

                            if (changed)
                            {
                                _detectedAppsRightEdge = appsRight;
                                _detectedTrayLeftEdge = trayLeft;
                                _detectedWidgetsRightEdge = widgetsRight;
                                RepositionOnTaskbar();
                            }
                        });
                    }
                    catch { }
                    finally
                    {
                        _isDetectingBoundaries = false;
                    }
                });
            }
        }

        private static (double appsRight, double trayLeft, double widgetsRight) DetectTaskbarBoundaries(IntPtr taskbarHwnd, double dpiX)
        {
            if (taskbarHwnd == IntPtr.Zero) return (0, 0, 0);

            try
            {
                var tbElement = AutomationElement.FromHandle(taskbarHwnd);
                if (tbElement == null) return (0, 0, 0);

                var condition = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Group)
                );

                var allItems = tbElement.FindAll(TreeScope.Descendants, condition);
                if (allItems == null || allItems.Count == 0) return (0, 0, 0);

                double tbLeft = tbElement.Current.BoundingRectangle.Left;
                double tbRight = tbElement.Current.BoundingRectangle.Right;

                // Step 1: Detect tray left boundary (Chevron or leftmost tray element)
                double minTrayLeft = double.MaxValue;
                for (int i = 0; i < allItems.Count; i++)
                {
                    try
                    {
                        var it = allItems[i];
                        var autoId = it.Current.AutomationId ?? "";
                        var name = it.Current.Name ?? "";
                        var cls = it.Current.ClassName ?? "";
                        var rect = it.Current.BoundingRectangle;

                        if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) continue;

                        bool isTray = autoId.IndexOf("Tray", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     autoId.IndexOf("Overflow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     autoId.IndexOf("Clock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     autoId.IndexOf("Notification", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     cls.IndexOf("Tray", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("значки", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("hidden icons", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("overflow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     name.IndexOf("трей", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isTray && rect.Left > 0 && rect.Left < minTrayLeft)
                        {
                            minTrayLeft = rect.Left;
                        }
                    }
                    catch { }
                }

                double effectiveTrayLeft = minTrayLeft < double.MaxValue ? minTrayLeft : (tbRight - (240 * dpiX));

                // Step 2: Detect Windows 11 Weather / Widgets button on the left
                double maxWidgetsRight = 0;
                for (int i = 0; i < allItems.Count; i++)
                {
                    try
                    {
                        var it = allItems[i];
                        var rect = it.Current.BoundingRectangle;
                        if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) continue;

                        if (rect.Left >= tbLeft - 10 && rect.Left < tbLeft + 250 && rect.Width > 20 && rect.Width < 300)
                        {
                            var autoId = it.Current.AutomationId ?? "";
                            var name = it.Current.Name ?? "";
                            var cls = it.Current.ClassName ?? "";

                            bool isWidgetsOrWeather =
                                autoId.IndexOf("Widget", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                autoId.IndexOf("Feeds", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                cls.IndexOf("Widget", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Погода", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Weather", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Мини-прилож", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.IndexOf("Виджет", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                name.Contains('°');

                            if (isWidgetsOrWeather && rect.Right > maxWidgetsRight)
                            {
                                maxWidgetsRight = rect.Right;
                            }
                        }
                    }
                    catch { }
                }

                // Step 3: Detect rightmost taskbar app button (buttons strictly to the left of the tray)
                double maxAppRight = 0;
                for (int i = 0; i < allItems.Count; i++)
                {
                    try
                    {
                        var it = allItems[i];
                        var ctrlType = it.Current.ControlType;
                        if (ctrlType != ControlType.Button && ctrlType != ControlType.ListItem) continue;

                        var rect = it.Current.BoundingRectangle;
                        if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) continue;
                        if (rect.Width > 500) continue; // skip whole bars/containers

                        // Skip Start button if on left
                        var autoId = it.Current.AutomationId ?? "";
                        if (autoId.Equals("StartButton", StringComparison.OrdinalIgnoreCase) ||
                            autoId.Equals("Start", StringComparison.OrdinalIgnoreCase)) continue;

                        // Must be to the left of the tray area by at least 12px
                        if (rect.Right < effectiveTrayLeft - 12)
                        {
                            if (rect.Right > maxAppRight)
                            {
                                maxAppRight = rect.Right;
                            }
                        }
                    }
                    catch { }
                }

                return (maxAppRight, effectiveTrayLeft, maxWidgetsRight);
            }
            catch
            {
                return (0, 0, 0);
            }
        }

        public void RepositionOnTaskbar()
        {
            if (_isDragging) return;

            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

                IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
                double targetLeft;
                double targetTop;
                double trayLeftDip;
                double appsRightDip = _detectedAppsRightEdge > 0 ? (_detectedAppsRightEdge / dpiX) : 0;

                if (taskbarHwnd != IntPtr.Zero && GetWindowRect(taskbarHwnd, out RECT tbRect))
                {
                    double tbTopDip = tbRect.Top / dpiY;
                    double tbHeightDip = (tbRect.Bottom - tbRect.Top) / dpiY;
                    double tbRightDip = tbRect.Right / dpiX;

                    targetTop = tbTopDip + (tbHeightDip - ActualHeight) / 2;

                    if (_detectedTrayLeftEdge > 0)
                    {
                        trayLeftDip = _detectedTrayLeftEdge / dpiX;
                    }
                    else
                    {
                        trayLeftDip = tbRightDip - 240;
                    }
                }
                else
                {
                    double tbHeight = Math.Max(40, SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Height);
                    double tbTop = SystemParameters.WorkArea.Bottom;
                    targetTop = tbTop + (tbHeight - ActualHeight) / 2;
                    trayLeftDip = SystemParameters.PrimaryScreenWidth - 240;
                }

                // Check placement mode: Left (expanded) vs Right (compact & responsive)
                bool isLeftMode = string.Equals(_configManager.CurrentConfig.TaskbarPosition, "Left", StringComparison.OrdinalIgnoreCase);

                if (isLeftMode)
                {
                    RootCard.Visibility = Visibility.Visible;
                    ArtistText.Visibility = Visibility.Visible;
                    ArtworkBorder.Visibility = Visibility.Visible;
                    ArtworkBorder.Width = 24;
                    ArtworkBorder.Height = 24;
                    Width = 225;

                    double tbLeftDip = (taskbarHwnd != IntPtr.Zero && GetWindowRect(taskbarHwnd, out RECT rectTb))
                        ? rectTb.Left / dpiX
                        : SystemParameters.WorkArea.Left;

                    double widgetsRightDip = _detectedWidgetsRightEdge > 0 ? (_detectedWidgetsRightEdge / dpiX) : 0;

                    IntPtr startHwnd = FindWindowEx(taskbarHwnd, IntPtr.Zero, "Start", null);
                    if (startHwnd != IntPtr.Zero && GetWindowRect(startHwnd, out RECT startRect) && startRect.Left < 100)
                    {
                        // Left-aligned taskbar: dock next to the Start button
                        targetLeft = (startRect.Right / dpiX) + 10;
                    }
                    else if (widgetsRightDip > tbLeftDip + 10)
                    {
                        // Windows 11 Weather / Widgets button detected on left
                        targetLeft = widgetsRightDip + 10;
                    }
                    else
                    {
                        // Check if Windows 11 Widgets button is enabled in registry
                        bool widgetsEnabledInRegistry = false;
                        try
                        {
                            var val = Microsoft.Win32.Registry.GetValue(
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                                "TaskbarDa", 0);
                            if (val is int intVal && intVal == 1) widgetsEnabledInRegistry = true;
                        }
                        catch { }

                        if (widgetsEnabledInRegistry)
                        {
                            // Standard weather widget on left occupies ~160px
                            targetLeft = tbLeftDip + 170;
                        }
                        else
                        {
                            // Widgets disabled -> dock right at the left edge
                            targetLeft = tbLeftDip + 12;
                        }
                    }
                }
                else
                {
                    ArtistText.Visibility = Visibility.Collapsed;
                    ArtworkBorder.Width = 20;
                    ArtworkBorder.Height = 20;

                    // Margin between app buttons and widget = 8px; margin from tray = 6px
                    double maxAllowedRight = trayLeftDip - 6;
                    double minAllowedLeft = appsRightDip > 0 ? (appsRightDip + 8) : 0;
                    double availableSpace = maxAllowedRight - minAllowedLeft;

                    RootCard.Visibility = Visibility.Visible;

                    // Clamp width between MIN_WIDGET_WIDTH and MAX_WIDGET_WIDTH
                    double targetWidth = MAX_WIDGET_WIDTH;
                    if (appsRightDip > 0 && availableSpace < MAX_WIDGET_WIDTH)
                    {
                        targetWidth = Math.Clamp(availableSpace, MIN_WIDGET_WIDTH, MAX_WIDGET_WIDTH);
                    }
                    Width = targetWidth;

                    // Responsive degradation:
                    // If width is constrained (< 115px), hide the cover art to preserve space for title & controls
                    if (targetWidth < 115)
                    {
                        ArtworkBorder.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ArtworkBorder.Visibility = Visibility.Visible;
                    }

                    // If width is very constrained (< 85px), hide title and time so playback buttons remain
                    if (targetWidth < 85)
                    {
                        TitleText.Visibility = Visibility.Collapsed;
                        TimeText.Visibility = Visibility.Collapsed;
                        ProgressBarTrack.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        TitleText.Visibility = Visibility.Visible;
                        TimeText.Visibility = Visibility.Visible;
                        ProgressBarTrack.Visibility = Visibility.Visible;
                    }

                    // Dock right before the tray chevron
                    targetLeft = maxAllowedRight - targetWidth;
                }

                Left = targetLeft;
                Top = targetTop;

                // Ensure it stays above taskbar without stealing focus
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TaskbarWidgetWindow Reposition] {ex.Message}");
            }
        }

        public void UpdateTrackInfo(MediaTrackInfo track)
        {
            _lastTrack = track;

            Dispatcher.Invoke(() =>
            {
                TitleText.Text = string.IsNullOrWhiteSpace(track.Title) || track.Title == "Нет активного трека"
                    ? LocalizationService.T("NoTrack")
                    : track.Title;
                TitleText.ToolTip = track.Title;

                ArtistText.Text = string.IsNullOrWhiteSpace(track.Artist) || track.Artist == "Запустите музыку"
                    ? LocalizationService.T("PlayMusic")
                    : track.Artist;
                ArtistText.ToolTip = track.Artist;

                if (track.Thumbnail != null)
                {
                    ArtworkImage.Source = track.Thumbnail;
                    ArtworkImage.Visibility = Visibility.Visible;
                    FallbackIcon.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ArtworkImage.Source = null;
                    ArtworkImage.Visibility = Visibility.Collapsed;
                    FallbackIcon.Visibility = Visibility.Visible;
                }

                PlayPauseIcon.Data = track.IsPlaying ? _pauseGeometry : _playGeometry;
                PlayPauseIcon.Margin = track.IsPlaying ? new Thickness(0) : new Thickness(1, 0, 0, 0);

                bool hasSession = track.HasActiveSession;
                PrevButton.IsEnabled = hasSession;
                PlayPauseButton.IsEnabled = hasSession;
                NextButton.IsEnabled = hasSession;

                PrevButton.ToolTip = LocalizationService.T("PrevTrack");
                PlayPauseButton.ToolTip = LocalizationService.T("PlayPause");
                NextButton.ToolTip = LocalizationService.T("NextTrack");

                _volumeService.SetTargetApp(track.SourceAppUserModelId);
            });
        }

        public void UpdateLocalization()
        {
            Dispatcher.Invoke(() =>
            {
                if (RootCard.ContextMenu != null && RootCard.ContextMenu.Items.Count >= 4)
                {
                    if (RootCard.ContextMenu.Items[0] is MenuItem item0) item0.Header = LocalizationService.T("Settings");
                    if (RootCard.ContextMenu.Items[1] is MenuItem item1) item1.Header = LocalizationService.T("TraySwitchToDesktop");
                    if (RootCard.ContextMenu.Items[3] is MenuItem item3) item3.Header = LocalizationService.T("Exit");
                }
                if (_lastTrack != null) UpdateTrackInfo(_lastTrack);
            });
        }

        public void UpdateProgress(TimeSpan position, TimeSpan duration, double progress)
        {
            _lastDuration = duration;
            _currentProgress = progress;

            Dispatcher.Invoke(() =>
            {
                TimeText.Text = position > TimeSpan.Zero ? FormatTime(position) : "0:00";
                UpdateProgressBarWidth();
            });
        }

        public void UpdatePalette(ExtractedPalette? palette)
        {
            Dispatcher.Invoke(() =>
            {
                if (_configManager.CurrentConfig.AdaptiveColorEnabled && palette != null)
                {
                    ProgressBarFill.Background = new SolidColorBrush(palette.PrimaryAccent);
                }
                else
                {
                    ProgressBarFill.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
                }
            });
        }

        public void OnFullscreenChanged(FullscreenInfo info)
        {
            bool shouldHide = false;
            if (info.IsFullscreen)
            {
                if (_configManager.CurrentConfig.AutoHideTaskbarOnFullscreenVideo)
                {
                    // Widescreen & Ultrawide (aspectRatio >= 1.70, e.g. 16:9, 21:9): video covers bottom -> hide!
                    // Letterboxed laptop screens (aspectRatio < 1.70, e.g. 16:10): black bar exists -> keep visible!
                    shouldHide = !info.HasBottomLetterbox;
                }
                else if (_configManager.CurrentConfig.AutoHideInFullscreen)
                {
                    shouldHide = true;
                }
            }

            _isFullscreenHidden = shouldHide;
            Dispatcher.Invoke(() =>
            {
                if (_isFullscreenHidden)
                {
                    Visibility = Visibility.Collapsed;
                }
                else if (_configManager.CurrentConfig.TaskbarModeEnabled)
                {
                    Visibility = Visibility.Visible;
                    RepositionOnTaskbar();
                }
            });
        }

        private void UpdateProgressBarWidth()
        {
            double trackWidth = ProgressBarTrack.ActualWidth;
            if (trackWidth > 0)
            {
                ProgressBarFill.Width = Math.Clamp(trackWidth * _currentProgress, 0, trackWidth);
            }
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            return time.Hours > 0
                ? time.ToString(@"h\:mm\:ss")
                : time.ToString(@"m\:ss");
        }

        private async void OnPrevButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await _mediaService.SkipPreviousAsync();
        }

        private async void OnPlayPauseButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await _mediaService.TogglePlayPauseAsync();
        }

        private async void OnNextButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await _mediaService.SkipNextAsync();
        }

        private async void OnProgressBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_lastDuration <= TimeSpan.Zero) return;

            var pos = e.GetPosition(ProgressBarTrack);
            double fraction = Math.Clamp(pos.X / ProgressBarTrack.ActualWidth, 0.0, 1.0);
            var targetPosition = TimeSpan.FromMilliseconds(_lastDuration.TotalMilliseconds * fraction);

            await _mediaService.ChangePlaybackPositionAsync(targetPosition);
        }

        private void OnRootCardMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (IsOrHasParent<Button>(dep) || IsOrHasParent<Border>(dep, ProgressBarTrack))
                    return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                try
                {
                    DragMove();
                }
                catch { }
                finally
                {
                    _isDragging = false;
                    RepositionOnTaskbar();
                }
            }
        }

        private void OnRootCardMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            float currentVol = _volumeService.GetVolume();
            float delta = e.Delta > 0 ? 0.03f : -0.03f;
            _volumeService.SetVolume(Math.Clamp(currentVol + delta, 0f, 1f));
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.OpenSettingsWindow();
        }

        private void OnSwitchToDesktopClick(object sender, RoutedEventArgs e)
        {
            _configManager.CurrentConfig.TaskbarModeEnabled = false;
            _configManager.Save(_configManager.CurrentConfig);
            _mainWindow.ApplyTaskbarMode(false);
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private static bool IsOrHasParent<T>(DependencyObject? element, DependencyObject? stopAt = null) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T) return true;
                if (element == stopAt) return false;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }
    }
}
