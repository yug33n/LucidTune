using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace LucidTune.Services
{
    public class HotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private const int HotkeyId = 9001;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private IntPtr _hWnd;
        private HwndSource? _source;
        private bool _isRegistered;
        private bool _disposed;

        public event Action? HotkeyPressed;

        public void Initialize(IntPtr windowHandle)
        {
            _hWnd = windowHandle;
            _source = HwndSource.FromHwnd(_hWnd);
            _source?.AddHook(HwndHook);
        }

        public bool Register(string modifierStr, string keyStr)
        {
            if (_hWnd == IntPtr.Zero) return false;

            if (_isRegistered)
            {
                UnregisterHotKey(_hWnd, HotkeyId);
                _isRegistered = false;
            }

            uint modifiers = ParseModifiers(modifierStr) | MOD_NOREPEAT;
            uint vk = ParseVirtualKey(keyStr);

            if (vk == 0) return false;

            _isRegistered = RegisterHotKey(_hWnd, HotkeyId, modifiers, vk);
            return _isRegistered;
        }

        private static uint ParseModifiers(string modifierStr)
        {
            uint result = 0;
            if (string.IsNullOrWhiteSpace(modifierStr)) return MOD_ALT;

            var parts = modifierStr.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) result |= MOD_ALT;
                else if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) result |= MOD_CONTROL;
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) result |= MOD_SHIFT;
                else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase)) result |= MOD_WIN;
            }

            return result == 0 ? MOD_ALT : result;
        }

        private static uint ParseVirtualKey(string keyStr)
        {
            if (string.IsNullOrWhiteSpace(keyStr)) return (uint)KeyInterop.VirtualKeyFromKey(Key.M);

            if (Enum.TryParse<Key>(keyStr, true, out var key))
            {
                return (uint)KeyInterop.VirtualKeyFromKey(key);
            }

            return (uint)KeyInterop.VirtualKeyFromKey(Key.M);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_isRegistered && _hWnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hWnd, HotkeyId);
                _isRegistered = false;
            }

            _source?.RemoveHook(HwndHook);
        }
    }
}
