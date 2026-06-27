using JUI.Models;

namespace JUI.Configuration
{
    /// <summary>
    /// JUI 库的全部持久化设置。
    /// 未来需要保存新数据时, 只需在此类中添加新属性即可,
    /// 序列化/反序列化会自动处理, 旧配置文件也能向后兼容。
    /// </summary>
    public class JuiSettings
    {
        // 主题
        public JuiTheme Theme { get; set; } = JuiTheme.Light;

        // ↓↓↓ 以后要加的设置往这里加, 例如:
        // public double LastWindowWidth { get; set; } = 800;
        // public string LastFolder { get; set; } = "";
        // public bool EnableAnimation { get; set; } = true;
    }
}
