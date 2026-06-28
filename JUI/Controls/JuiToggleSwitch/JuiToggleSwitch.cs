using System.Windows;
using System.Windows.Controls.Primitives;

namespace JUI.Controls
{
    /// <summary>JUI 开关: 方形轨道 + 方形滑块, 滑块在开 / 关之间平移。</summary>
    public class JuiToggleSwitch : ToggleButton
    {
        static JuiToggleSwitch()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JuiToggleSwitch),
                new FrameworkPropertyMetadata(typeof(JuiToggleSwitch)));
        }
    }
}
