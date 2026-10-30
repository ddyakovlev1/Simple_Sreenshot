using System;
using System.Runtime.InteropServices;

namespace SimpleScreenshot
{
    /// <summary>
    /// Глобальный перехват клавиатуры (low-level hook).
    /// Обработчик возвращает true, если нажатие надо «съесть» и не пускать дальше в систему.
    /// </summary>
    internal sealed class KeyboardHook : IDisposable
    {
        public delegate bool KeyHandler(int vkCode, bool isDown);

        private readonly KeyHandler _handler;
        // Держим ссылку на делегат, иначе сборщик мусора его удалит и приложение упадёт.
        private readonly NativeMethods.LowLevelKeyboardProc _proc;
        private IntPtr _hook = IntPtr.Zero;

        public KeyboardHook(KeyHandler handler)
        {
            _handler = handler;
            _proc = HookProc;
            _hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);

            if (_hook == IntPtr.Zero)
                throw new InvalidOperationException(
                    "Не удалось перехватить клавиатуру, код ошибки " + Marshal.GetLastWin32Error());
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var data = (NativeMethods.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(
                    lParam, typeof(NativeMethods.KBDLLHOOKSTRUCT));
                int msg = wParam.ToInt32();
                bool isDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;

                try
                {
                    if (_handler(data.vkCode, isDown))
                        return new IntPtr(1);
                }
                catch
                {
                    // Ошибка в обработчике не должна ломать клавиатуру всей системы.
                }
            }

            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}
