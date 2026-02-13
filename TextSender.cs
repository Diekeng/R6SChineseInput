using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseHookApp;

/// <summary>
/// 使用 Win32 SendInput API 将 Unicode 文本逐字符发送到当前前台窗口。
/// 每个字符生成一对 KEYDOWN + KEYUP 事件，通过 KEYEVENTF_UNICODE 标志直接发送 Unicode 码点。
/// </summary>
public static class TextSender
{
    // ========== Win32 SendInput P/Invoke ==========

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    // INPUT 结构体：type(4) + pad(4) + union(32) = 40 bytes on x64
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public INPUTUNION u;
    }

    // Union 大小必须为 32 字节（匹配最大成员 MOUSEINPUT），否则 SendInput 会静默失败
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // ========== 公开方法 ==========

    /// <summary>
    /// 等待指定延迟后，将文本以 Unicode 字符序列发送到当前前台窗口。
    /// 支持自动重试：如果 SendInput 未能完整发送所有事件，将自动重试。
    /// </summary>
    /// <param name="text">要发送的文本</param>
    /// <param name="delayMs">发送前等待的毫秒数</param>
    /// <param name="retryCount">最大重试次数（默认 3）</param>
    /// <param name="retryDelayMs">重试间隔毫秒数（默认 100）</param>
    public static async Task SendTextAsync(string text, int delayMs = 50, int retryCount = 3, int retryDelayMs = 100)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }

        var fgWindow = GetForegroundWindow();
        Console.WriteLine($"[TextSender] 当前前台窗口句柄: 0x{fgWindow:X}");
        Console.WriteLine($"[TextSender] INPUT 结构体大小: {Marshal.SizeOf<INPUT>()} 字节");

        uint expectedEvents = (uint)(text.Length * 2); // 每个字符 KEYDOWN + KEYUP

        for (int attempt = 1; attempt <= retryCount + 1; attempt++)
        {
            Console.WriteLine($"[TextSender] 第 {attempt} 次尝试发送文本 ({text.Length} 个字符): {text}");

            var sent = SendUnicodeString(text);

            if (sent == expectedEvents)
            {
                Console.WriteLine($"[TextSender] ✅ 第 {attempt} 次发送成功: {sent}/{expectedEvents} 个事件");
                return;
            }

            Console.WriteLine($"[TextSender] ⚠️ 第 {attempt} 次发送不完整: {sent}/{expectedEvents} 个事件");

            if (attempt <= retryCount)
            {
                Console.WriteLine($"[TextSender] 等待 {retryDelayMs}ms 后重试...");
                await Task.Delay(retryDelayMs);
            }
        }

        Console.WriteLine($"[TextSender] ❌ 所有 {retryCount + 1} 次尝试均未完整发送");
    }

    /// <summary>
    /// 通过 SendInput 逐字符发送 Unicode 文本。
    /// 返回实际发送的事件数量。
    /// </summary>
    public static uint SendUnicodeString(string text)
    {
        var inputs = new List<INPUT>();

        foreach (var ch in text)
        {
            // KEYDOWN
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            });

            // KEYUP
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            });
        }

        var inputArray = inputs.ToArray();
        var cbSize = Marshal.SizeOf<INPUT>();
        var sent = SendInput((uint)inputArray.Length, inputArray, cbSize);

        if (sent != inputArray.Length)
        {
            var error = Marshal.GetLastWin32Error();
            Console.WriteLine($"[TextSender] SendInput 失败: 发送 {sent}/{inputArray.Length}，错误码: {error}");
        }
        else
        {
            Console.WriteLine($"[TextSender] SendInput 成功: {sent}/{inputArray.Length} 个事件已发送");
        }

        return sent;
    }
}
