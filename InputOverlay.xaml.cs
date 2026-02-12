using System.Windows;
using System.Windows.Input;

namespace MouseHookApp;

/// <summary>
/// 浮动输入叠加层窗口。
/// 无边框、半透明、置顶显示。
/// Enter 提交输入并触发事件，Esc 取消并清空。
/// </summary>
public partial class InputOverlay : Window
{
    /// <summary>
    /// 用户按下 Enter 时触发，参数为输入框中的文本。
    /// </summary>
    public event Action<string>? InputSubmitted;

    public InputOverlay()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 显示叠加层并聚焦输入框。
    /// </summary>
    public void ShowAndFocus()
    {
        Show();
        Activate();
        InputBox.Focus();
        Keyboard.Focus(InputBox);
    }

    private void InputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                var text = InputBox.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    InputSubmitted?.Invoke(text);
                }
                InputBox.Clear();
                Hide();
                e.Handled = true;
                break;

            case Key.Escape:
                InputBox.Clear();
                Hide();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// 重写关闭行为：隐藏而非销毁，以便复用。
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        InputBox.Clear();
        Hide();
    }
}
