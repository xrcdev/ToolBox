using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace AlwaysOnTop
{
    public class GlobalHotkey
    {
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
            IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpmsg);

        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public System.Drawing.Point pt;
        }

        // 修饰符常量
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        // 虚拟键码
        private static readonly Dictionary<string, uint> VirtualKeyCodes = new()
        {
            { "A", 0x41 }, { "B", 0x42 }, { "C", 0x43 }, { "D", 0x44 }, { "E", 0x45 },
            { "F", 0x46 }, { "G", 0x47 }, { "H", 0x48 }, { "I", 0x49 }, { "J", 0x4A },
            { "K", 0x4B }, { "L", 0x4C }, { "M", 0x4D }, { "N", 0x4E }, { "O", 0x4F },
            { "P", 0x50 }, { "Q", 0x51 }, { "R", 0x52 }, { "S", 0x53 }, { "T", 0x54 },
            { "U", 0x55 }, { "V", 0x56 }, { "W", 0x57 }, { "X", 0x58 }, { "Y", 0x59 },
            { "Z", 0x5A },
            { "0", 0x30 }, { "1", 0x31 }, { "2", 0x32 }, { "3", 0x33 }, { "4", 0x34 },
            { "5", 0x35 }, { "6", 0x36 }, { "7", 0x37 }, { "8", 0x38 }, { "9", 0x39 },
            { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
            { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
            { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },
            { "SPACE", 0x20 }, { "RETURN", 0x0D }, { "ESCAPE", 0x1B }
        };

        private IntPtr _hotkeyWindow;
        private int _pinHotkeyId = 1;
        private int _unpinHotkeyId = 2;
        private Dictionary<int, Action> _hotkeyActions;

        public event Action OnPinHotkey;
        public event Action OnUnpinHotkey;

        public GlobalHotkey()
        {
            _hotkeyActions = new Dictionary<int, Action>();
        }

        public bool Register(HotkeyConfig pinConfig, HotkeyConfig unpinConfig)
        {
            try
            {
                if (!CreateMessageWindow())
                    return false;

                if (!RegisterHotkey(_pinHotkeyId, pinConfig))
                {
                    Console.WriteLine("✗ 注册置顶热键失败");
                    return false;
                }

                if (!RegisterHotkey(_unpinHotkeyId, unpinConfig))
                {
                    Console.WriteLine("✗ 注册取消置顶热键失败");
                    UnregisterHotKey(_hotkeyWindow, _pinHotkeyId);
                    return false;
                }

                _hotkeyActions[_pinHotkeyId] = () => OnPinHotkey?.Invoke();
                _hotkeyActions[_unpinHotkeyId] = () => OnUnpinHotkey?.Invoke();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 注册热键异常: {ex.Message}");
                return false;
            }
        }

        private bool CreateMessageWindow()
        {
            try
            {
                const string className = "GlobalHotkeyWindow";
                _hotkeyWindow = CreateWindowEx(0, className, "HotkeyWindow", 0, 0, 0, 0, 0,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                return _hotkeyWindow != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        private bool RegisterHotkey(int hotkeyId, HotkeyConfig config)
        {
            uint modifiers = ParseModifiers(config.Modifiers);
            uint keyCode = ParseKeyCode(config.Key);

            if (keyCode == 0)
            {
                Console.WriteLine($"✗ 不支持的快捷键: {config.Key}");
                return false;
            }

            return RegisterHotKey(_hotkeyWindow, hotkeyId, modifiers, keyCode);
        }

        private uint ParseModifiers(string modifiers)
        {
            uint result = 0;
            var parts = modifiers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmed = part.Trim().ToLower();
                result |= trimmed switch
                {
                    "alt" => MOD_ALT,
                    "control" or "ctrl" => MOD_CONTROL,
                    "shift" => MOD_SHIFT,
                    "win" or "windows" => MOD_WIN,
                    _ => 0
                };
            }

            return result;
        }

        private uint ParseKeyCode(string keyName)
        {
            var upperKey = keyName.ToUpper();
            return VirtualKeyCodes.TryGetValue(upperKey, out var code) ? code : 0;
        }

        public void StartListening()
        {
            if (_hotkeyWindow == IntPtr.Zero)
                return;

            while (GetMessage(out MSG msg, _hotkeyWindow, 0, 0))
            {
                if (msg.message == WM_HOTKEY)
                {
                    int hotkeyId = (int)msg.wParam;
                    if (_hotkeyActions.TryGetValue(hotkeyId, out var action))
                    {
                        action?.Invoke();
                    }
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        public void Unregister()
        {
            if (_hotkeyWindow != IntPtr.Zero)
            {
                UnregisterHotKey(_hotkeyWindow, _pinHotkeyId);
                UnregisterHotKey(_hotkeyWindow, _unpinHotkeyId);
                DestroyWindow(_hotkeyWindow);
                _hotkeyWindow = IntPtr.Zero;
            }
        }

        ~GlobalHotkey()
        {
            Unregister();
        }
    }
}
