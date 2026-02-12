using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseHookApp;

/// <summary>
/// 管理 InputOverlay 窗口的显示/隐藏，并处理前台窗口焦点的保存与恢复。
/// 确保 Overlay 关闭后焦点能正确归还给之前的窗口（如全屏游戏）。
/// </summary>
public class WindowManager
{
    // ========== Win32 API ==========

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private const int SW_SHOW = 5;

    // ========== 实例字段 ==========

    private readonly InputOverlay _overlay;
    private IntPtr _previousForegroundWindow = IntPtr.Zero;

    public WindowManager(InputOverlay overlay)
    {
        _overlay = overlay;

        // 当用户按 Enter 提交时，处理文本发送流程
        _overlay.InputSubmitted += OnInputSubmitted;

        // 当用户按 Esc 取消时（仅隐藏，不提交），归还焦点
        _overlay.IsVisibleChanged += (_, e) =>
        {
            // 仅在非提交场景下（Esc 取消）归还焦点
            // 提交场景的焦点归还在 OnInputSubmitted 中手动处理
            if (e.NewValue is false && !_isSubmitting)
            {
                RestorePreviousWindow();
            }
        };
    }

    /// <summary>
    /// 当输入提交时触发，外部可订阅此事件获取用户输入。
    /// </summary>
    public event Action<string>? InputSubmitted;

    private bool _isSubmitting;

    /// <summary>
    /// 记录当前前台窗口，然后显示 Overlay 并强制获取焦点。
    /// </summary>
    public void ShowOverlay()
    {
        // 记录当前前台窗口（如游戏窗口）
        _previousForegroundWindow = GetForegroundWindow();
        Debug.WriteLine($"[WindowManager] 记录前台窗口句柄: 0x{_previousForegroundWindow:X}");

        // 显示 Overlay 并强制获取焦点
        _overlay.ShowAndFocus();
        ForceForeground(_overlay);

        Debug.WriteLine("[WindowManager] Overlay 已显示");
    }

    /// <summary>
    /// 隐藏 Overlay 窗口。焦点归还由 IsVisibleChanged 事件自动处理。
    /// </summary>
    public void HideOverlay()
    {
        _overlay.Hide();
    }

    /// <summary>
    /// 切换 Overlay 显示状态：如果已显示则隐藏（模拟 Esc），否则显示。
    /// </summary>
    public void ToggleOverlay()
    {
        if (_overlay.IsVisible)
        {
            Debug.WriteLine("[WindowManager] Overlay 已显示，执行隐藏（模拟 Esc）");
            HideOverlay();
            // HideOverlay 触发 IsVisibleChanged -> 自动 RestorePreviousWindow
        }
        else
        {
            ShowOverlay();
        }
    }

    // ========== 私有方法 ==========

    private async void OnInputSubmitted(string text)
    {
        Debug.WriteLine($"[WindowManager] 用户输入: {text}");

        // 标记正在提交，防止 IsVisibleChanged 重复归还焦点
        _isSubmitting = true;

        try
        {
            InputSubmitted?.Invoke(text);

            // 1. 隐藏 Overlay (InputOverlay 的 KeyDown 中也会 Hide，这里确保顺序)
            _overlay.Hide();

            // 2. 归还焦点给游戏窗口
            RestorePreviousWindow();

            // 3. 等待焦点切换完全生效后再发送文本
            Debug.WriteLine("[WindowManager] 等待 200ms 后发送文本...");
            await Task.Delay(200);

            // 4. 发送文本到游戏窗口
            await TextSender.SendTextAsync(text, delayMs: 0); // 延迟已在上面处理
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowManager] 发送文本异常: {ex.Message}");
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    /// <summary>
    /// 将焦点归还给之前记录的前台窗口。
    /// </summary>
    private void RestorePreviousWindow()
    {
        if (_previousForegroundWindow == IntPtr.Zero)
            return;

        Debug.WriteLine($"[WindowManager] 归还焦点至窗口句柄: 0x{_previousForegroundWindow:X}");

        // 使用 AttachThreadInput 技巧绕过 Windows 前台窗口限制
        var targetThreadId = GetWindowThreadProcessId(_previousForegroundWindow, out _);
        var currentThreadId = GetCurrentThreadId();

        if (targetThreadId != currentThreadId)
        {
            AttachThreadInput(currentThreadId, targetThreadId, true);
            SetForegroundWindow(_previousForegroundWindow);
            AttachThreadInput(currentThreadId, targetThreadId, false);
        }
        else
        {
            SetForegroundWindow(_previousForegroundWindow);
        }

        _previousForegroundWindow = IntPtr.Zero;
    }

    /// <summary>
    /// 强制将 WPF 窗口设为前台窗口（绕过 Windows 前台窗口切换限制）。
    /// </summary>
    private static void ForceForeground(System.Windows.Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var currentThreadId = GetCurrentThreadId();

        if (foregroundThreadId != currentThreadId)
        {
            AttachThreadInput(currentThreadId, foregroundThreadId, true);
            ShowWindow(hwnd, SW_SHOW);
            SetForegroundWindow(hwnd);
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
        else
        {
            ShowWindow(hwnd, SW_SHOW);
            SetForegroundWindow(hwnd);
        }
    }
}
