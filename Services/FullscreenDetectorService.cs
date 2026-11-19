using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace LucidTune.Services
{
    public struct FullscreenInfo
    {
        public bool IsFullscreen { get; set; }
        public bool HasBottomLetterbox { get; set; }
        public double AspectRatio { get; set; }
    }

    public class FullscreenDetectorService : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private readonly DispatcherTimer _pollTimer;
        private IntPtr _ownHwnd;
        private FullscreenInfo _currentInfo;
        private bool _disposed;

        public event Action<FullscreenInfo>? FullscreenStateChanged;

        public FullscreenDetectorService()
        {
            _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1.0)
            };
            _pollTimer.Tick += OnPollTick;
        }

        public void Start(IntPtr ownHwnd)
        {
            _ownHwnd = ownHwnd;
            _pollTimer.Start();
        }

        public void Stop()
        {
            _pollTimer.Stop();
        }

        private void OnPollTick(object? sender, EventArgs e)
        {
            var info = CheckIsForegroundFullscreen();
            if (info.IsFullscreen != _currentInfo.IsFullscreen ||
                info.HasBottomLetterbox != _currentInfo.HasBottomLetterbox)
            {
                _currentInfo = info;
                FullscreenStateChanged?.Invoke(_currentInfo);
            }
        }

        private FullscreenInfo CheckIsForegroundFullscreen()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == _ownHwnd)
                return new FullscreenInfo { IsFullscreen = false };

            IntPtr shell = GetShellWindow();
            IntPtr desktop = GetDesktopWindow();
            if (foreground == shell || foreground == desktop)
                return new FullscreenInfo { IsFullscreen = false };

            var className = new StringBuilder(256);
            GetClassName(foreground, className, className.Capacity);
            var cls = className.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd")
                return new FullscreenInfo { IsFullscreen = false };

            if (GetWindowRect(foreground, out RECT rect))
            {
                IntPtr hMonitor = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero)
                {
                    var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        int monWidth = mi.rcMonitor.Right - mi.rcMonitor.Left;
                        int monHeight = mi.rcMonitor.Bottom - mi.rcMonitor.Top;

                        // Check if window covers the entire monitor (allowing small margin for multi-monitor borders)
                        if (rect.Left <= mi.rcMonitor.Left + 2 &&
                            rect.Top <= mi.rcMonitor.Top + 2 &&
                            rect.Right >= mi.rcMonitor.Right - 2 &&
                            rect.Bottom >= mi.rcMonitor.Bottom - 2)
                        {
                            double aspectRatio = monHeight > 0 ? (double)monWidth / monHeight : 1.777;
                            // On 16:10 or 3:2 displays (aspectRatio < 1.70), 16:9 video leaves vertical letterboxing
                            // with a bottom black bar of ~50-100px where the taskbar widget safely fits.
                            // On 16:9 or 21:9 ultrawide (aspectRatio >= 1.70), video touches the bottom of the screen.
                            bool hasBottomLetterbox = aspectRatio < 1.70;

                            return new FullscreenInfo
                            {
                                IsFullscreen = true,
                                HasBottomLetterbox = hasBottomLetterbox,
                                AspectRatio = aspectRatio
                            };
                        }
                    }
                }
            }

            return new FullscreenInfo { IsFullscreen = false };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pollTimer.Stop();
        }
    }
}
