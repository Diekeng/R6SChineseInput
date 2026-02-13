using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace MouseHookApp;

/// <summary>
/// 应用配置管理。热键设置保存在 config.json 中。
/// </summary>
public class AppConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MouseHookApp");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    /// <summary>
    /// 热键的修饰键 (VK code)。0 = 无修饰键。
    /// 默认: VK_LCONTROL (0xA2)
    /// </summary>
    public int ModifierVk { get; set; } = 0xA2; // Ctrl

    /// <summary>
    /// 热键的主键 (VK code)。
    /// 默认: VK_OEM_3 (0xC0) = ` 反引号
    /// </summary>
    public int HotkeyVk { get; set; } = 0xC0; // `

    /// <summary>
    /// 修饰键显示名称。
    /// </summary>
    public string ModifierName { get; set; } = "Ctrl";

    /// <summary>
    /// 主键显示名称。
    /// </summary>
    public string HotkeyName { get; set; } = "`";

    /// <summary>
    /// 文本发送失败时的重试次数（默认 3 次）。
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 每次重试之间的等待时间（毫秒，默认 100ms）。
    /// </summary>
    public int RetryDelayMs { get; set; } = 100;

    /// <summary>
    /// 焦点归还后、发送文本前的等待时间（毫秒，默认 300ms）。
    /// </summary>
    public int FocusRestoreDelayMs { get; set; } = 300;

    /// <summary>
    /// 获取热键显示文本，如 "Ctrl + `"
    /// </summary>
    public string HotkeyDisplayText =>
        string.IsNullOrEmpty(ModifierName) ? HotkeyName : $"{ModifierName} + {HotkeyName}";

    // ========== 持久化 ==========

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
            Debug.WriteLine($"[Config] 配置已保存至: {ConfigPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Config] 保存失败: {ex.Message}");
        }
    }

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    Debug.WriteLine($"[Config] 配置已加载: {config.HotkeyDisplayText}");
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Config] 加载失败，使用默认值: {ex.Message}");
        }

        return new AppConfig();
    }

    // ========== 常用按键映射 ==========

    /// <summary>
    /// 可选的修饰键列表 (显示名 -> VK code)。
    /// </summary>
    public static readonly (string Name, int Vk)[] AvailableModifiers =
    [
        ("无", 0),
        ("Ctrl", 0xA2),
        ("Alt", 0xA4),
        ("Shift", 0xA0),
    ];

    /// <summary>
    /// 可选的主键列表 (显示名 -> VK code)。
    /// </summary>
    public static readonly (string Name, int Vk)[] AvailableKeys =
    [
        ("`", 0xC0),
        ("F1", 0x70), ("F2", 0x71), ("F3", 0x72), ("F4", 0x73),
        ("F5", 0x74), ("F6", 0x75), ("F7", 0x76), ("F8", 0x77),
        ("F9", 0x78), ("F10", 0x79), ("F11", 0x7A), ("F12", 0x7B),
        ("Insert", 0x2D), ("Delete", 0x2E),
        ("Home", 0x24), ("End", 0x23),
        ("PageUp", 0x21), ("PageDown", 0x22),
        ("Pause", 0x13), ("ScrollLock", 0x91),
    ];
}
