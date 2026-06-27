namespace JUI.Controls
{
    /// <summary>JuiWindow 的窗口外观模式。</summary>
    public enum JuiTitleBarMode
    {
        /// <summary>常规: 标题栏 + 标题文字 + 按钮 + 边框, 可拖边改尺寸。</summary>
        Normal,

        /// <summary>无标题栏: 隐藏标题栏与按钮, 保留边框且可拖边改尺寸。</summary>
        NoTitleBar,

        /// <summary>侵入式: 标题栏透明、内容铺满、保留按钮, 可拖边改尺寸。</summary>
        Immersive,

        /// <summary>完全无边框: 无标题、无按钮、无边框、无背景, 不可拖边改尺寸。</summary>
        Borderless
    }
}
