using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace LucidTune.Services
{
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
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private readonly DispatcherTimer _pollTimer;
        private IntPtr _ownHwnd;
        private bool _isCurrentlyFullscreen;
        private bool _disposed;

        public event Action<bool>? FullscreenStateChanged;

        public FullscreenDetectorService()
        {
            _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1.5)
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
            bool isFullscreen = CheckIsForegroundFullscreen();
            if (isFullscreen != _isCurrentlyFullscreen)
            {
                _isCurrentlyFullscreen = isFullscreen;
                FullscreenStateChanged?.Invoke(_isCurrentlyFullscreen);
            }
        }

        private bool CheckIsForegroundFullscreen()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == _ownHwnd) return false;

            IntPtr shell = GetShellWindow();
            IntPtr desktop = GetDesktopWindow();
            if (foreground == shell || foreground == desktop) return false;

            var className = new StringBuilder(256);
            GetClassName(foreground, className, className.Capacity);
            var cls = className.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;

            if (GetWindowRect(foreground, out RECT rect))
            {
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                if (width >= screenWidth && height >= screenHeight && rect.Left <= 0 && rect.Top <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pollTimer.Stop();
        }
    }
}
