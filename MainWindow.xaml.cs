using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace MouseHookApp;

/// <summary>
/// MainWindow：粉色小窗口，系统托盘支持，热键配置 UI。
/// </summary>
public partial class MainWindow : Window
{
    private const int MaxLogItems = 200;
    private NotifyIcon? _trayIcon;
    private bool _hotkeyInitialized; // 防止初始化时触发 SelectionChanged

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
        InitializeHotkeyUI();
        ShowTutorial();
    }

    // ========== 热键配置 UI ==========

    private void InitializeHotkeyUI()
    {
        var config = ((App)Application.Current).Config;

        // 填充修饰键下拉框
        foreach (var (name, _) in AppConfig.AvailableModifiers)
            ModifierCombo.Items.Add(name);

        // 填充主键下拉框
        foreach (var (name, _) in AppConfig.AvailableKeys)
            KeyCombo.Items.Add(name);

        // 选中当前配置
        SelectComboByName(ModifierCombo, config.ModifierName);
        SelectComboByName(KeyCombo, config.HotkeyName);

        _hotkeyInitialized = true;
        UpdateStatusText(config);
    }

    private static void SelectComboByName(System.Windows.Controls.ComboBox combo, string name)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i]?.ToString() == name)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void HotkeyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_hotkeyInitialized) return;

        var modIdx = ModifierCombo.SelectedIndex;
        var keyIdx = KeyCombo.SelectedIndex;
        if (modIdx < 0 || keyIdx < 0) return;

        var (modName, modVk) = AppConfig.AvailableModifiers[modIdx];
        var (keyName, keyVk) = AppConfig.AvailableKeys[keyIdx];

        var config = ((App)Application.Current).Config;
        config.ModifierVk = modVk;
        config.ModifierName = modName;
        config.HotkeyVk = keyVk;
        config.HotkeyName = keyName;
        config.Save();

        ((App)Application.Current).ReloadConfig();
        UpdateStatusText(config);

        AppendLog($"🔑 热键已更改为: {config.HotkeyDisplayText}");
    }

    private void UpdateStatusText(AppConfig config)
    {
        StatusText.Text = $"运行中  |  {config.HotkeyDisplayText}";
    }

    // ========== 教程 ==========

    private void ShowTutorial()
    {
        var hotkey = ((App)Application.Current).Config.HotkeyDisplayText;
        string[] lines =
        [
            "══════ 使用教程 ══════",
            $"🔑 按 {hotkey} 呼出输入框",
            "⌨️ 输入文本后按 Enter 发送",
            "❌ 按 Esc 取消输入",
            "━  点击标题栏 ━ 最小化到托盘",
            "🖱️ 双击托盘图标恢复窗口",
            "══════════════════════",
        ];
        foreach (var line in lines)
            AppendLog(line);
    }

    // ========== 系统托盘 ==========

    private void InitializeTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = new Icon("icon.ico"),
            Text = "R6SChineseInput",
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ToggleWindow();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("显示窗口", null, (_, _) => ShowWindow());
        contextMenu.Items.Add("退出", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = contextMenu;
    }

    private void ToggleWindow()
    {
        if (IsVisible) Hide(); else ShowWindow();
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    // ========== 日志 ==========

    public void AppendLog(string message)
    {
        LogListBox.Items.Add(message);
        while (LogListBox.Items.Count > MaxLogItems)
            LogListBox.Items.RemoveAt(0);
        LogListBox.ScrollIntoView(LogListBox.Items[^1]);
    }

    // ========== 事件处理 ==========

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => Hide();
    private void ClearButton_Click(object sender, RoutedEventArgs e) => LogListBox.Items.Clear();
    private void ExitButton_Click(object sender, RoutedEventArgs e) => ExitApp();

    private void ExitApp()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        Application.Current.Shutdown();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
