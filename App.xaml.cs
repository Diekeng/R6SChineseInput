using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace MouseHookApp;

/// <summary>
/// 应用程序入口：
/// 1. Mutex 单实例检测
/// 2. 全局低级键盘钩子 + 可配置热键
/// </summary>
public partial class App : System.Windows.Application
{
    // ========== Win32 API ==========

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104; // Alt 组合键触发此消息

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // ========== 实例字段 ==========

    private static Mutex? _mutex;
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;
    private WindowManager? _windowManager;
    private InputOverlay? _inputOverlay;
    private AppConfig _config = new();

    /// <summary>
    /// 外部获取当前配置（MainWindow 设置热键时使用）。
    /// </summary>
    public AppConfig Config => _config;

    // ========== 生命周期 ==========

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ===== 单实例检测 =====
        const string mutexName = "Global\\MouseHookApp_SingleInstance_9F3A";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "MouseHookApp 已在运行中！\n本实例将自动关闭。",
                "多开检测",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        // ===== 加载配置 =====
        _config = AppConfig.Load();

        // ===== 初始化 UI =====
        _inputOverlay = new InputOverlay();
        _windowManager = new WindowManager(_inputOverlay);
        _windowManager.InputSubmitted += text =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var msg = $"[{timestamp}] 用户输入: {text}";
            Debug.WriteLine(msg);
            Console.WriteLine(msg);
            if (MainWindow is MainWindow mw) mw.AppendLog(msg);
        };

        // ===== 安装键盘钩子 =====
        _hookProc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _hookProc,
            GetModuleHandle(curModule.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
        {
            var errorCode = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[KeyboardHook] SetWindowsHookEx 失败，错误码: {errorCode}");
        }
        else
        {
            Debug.WriteLine($"[KeyboardHook] 全局钩子已安装，热键: {_config.HotkeyDisplayText}");
        }
    }

    /// <summary>
    /// 热键配置变更后重新加载（不需要重装钩子，只需更新 _config 引用）。
    /// </summary>
    public void ReloadConfig()
    {
        _config = AppConfig.Load();
        Debug.WriteLine($"[App] 配置已重新加载，热键: {_config.HotkeyDisplayText}");
    }

    private bool IsModifierPressed()
    {
        if (_config.ModifierVk == 0) return true; // 无修饰键
        return (GetAsyncKeyState(_config.ModifierVk) & 0x8000) != 0;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            if (hookStruct.vkCode == (uint)_config.HotkeyVk && IsModifierPressed())
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var message = $"[{timestamp}] 热键触发: {_config.HotkeyDisplayText}";

                Debug.WriteLine(message);
                Console.WriteLine(message);

                Dispatcher.BeginInvoke(() =>
                {
                    if (MainWindow is MainWindow mw)
                        mw.AppendLog(message);

                    _windowManager?.ShowOverlay();
                });
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        base.OnExit(e);
    }
}
