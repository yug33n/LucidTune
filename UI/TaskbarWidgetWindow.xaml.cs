using System;
using System.Runtime.InteropServices;
using System.Windows;
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
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _repositionTimer.Tick += (s, e) => RepositionOnTaskbar();

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

        public void RepositionOnTaskbar()
        {
            if (_isDragging) return;

            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

                IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
                IntPtr trayHwnd = taskbarHwnd != IntPtr.Zero
                    ? FindWindowEx(taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null)
                    : IntPtr.Zero;

                double targetLeft;
                double targetTop;

                if (taskbarHwnd != IntPtr.Zero && GetWindowRect(taskbarHwnd, out RECT tbRect))
                {
                    double tbTopDip = tbRect.Top / dpiY;
                    double tbHeightDip = (tbRect.Bottom - tbRect.Top) / dpiY;
                    double tbRightDip = tbRect.Right / dpiX;

                    targetTop = tbTopDip + (tbHeightDip - ActualHeight) / 2;

                    if (trayHwnd != IntPtr.Zero && GetWindowRect(trayHwnd, out RECT trayRect) && trayRect.Left > 0)
                    {
                        double trayLeftDip = trayRect.Left / dpiX;
                        targetLeft = trayLeftDip - ActualWidth - 8 + _configManager.CurrentConfig.TaskbarOffsetX;
                    }
                    else
                    {
                        // Fallback estimate for tray width in Windows 11
                        targetLeft = tbRightDip - 240 - ActualWidth + _configManager.CurrentConfig.TaskbarOffsetX;
                    }
                }
                else
                {
                    // Fallback using SystemParameters
                    double tbHeight = Math.Max(40, SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Height);
                    double tbTop = SystemParameters.WorkArea.Bottom;
                    targetTop = tbTop + (tbHeight - ActualHeight) / 2;
                    targetLeft = SystemParameters.PrimaryScreenWidth - 260 - ActualWidth + _configManager.CurrentConfig.TaskbarOffsetX;
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
                    ? "Нет трека"
                    : track.Title;
                TitleText.ToolTip = track.Title;

                ArtistText.Text = string.IsNullOrWhiteSpace(track.Artist) || track.Artist == "Запустите музыку"
                    ? "Запустите музыку"
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

                _volumeService.SetTargetApp(track.SourceAppUserModelId);
            });
        }

        public void UpdateProgress(TimeSpan position, TimeSpan duration, double progress)
        {
            _lastDuration = duration;
            _currentProgress = progress;

            Dispatcher.Invoke(() =>
            {
                if (duration > TimeSpan.Zero)
                {
                    string posStr = FormatTime(position);
                    string durStr = FormatTime(duration);
                    TimeText.Text = $"{posStr} / {durStr}";
                }
                else
                {
                    TimeText.Text = position > TimeSpan.Zero ? FormatTime(position) : "0:00";
                }

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

        public void OnFullscreenChanged(bool isFullscreen)
        {
            _isFullscreenHidden = _configManager.CurrentConfig.AutoHideInFullscreen && isFullscreen;
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
                    SaveDraggedOffset();
                }
            }
        }

        private void SaveDraggedOffset()
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
                IntPtr trayHwnd = taskbarHwnd != IntPtr.Zero
                    ? FindWindowEx(taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null)
                    : IntPtr.Zero;

                double defaultLeft;
                if (trayHwnd != IntPtr.Zero && GetWindowRect(trayHwnd, out RECT trayRect) && trayRect.Left > 0)
                {
                    defaultLeft = (trayRect.Left / dpiX) - ActualWidth - 8;
                }
                else
                {
                    defaultLeft = SystemParameters.PrimaryScreenWidth - 260 - ActualWidth;
                }

                double offset = Left - defaultLeft;
                _configManager.CurrentConfig.TaskbarOffsetX = offset;
                _configManager.Save(_configManager.CurrentConfig);

                RepositionOnTaskbar();
            }
            catch { }
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
