using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JUI.Controls
{
  
    /// <summary>JTWindow 标题栏显示模式。</summary>
    public enum JTTitleBarMode
    {
        /// <summary>常规模式:不透明标题栏 + 标题文字 + 右上角三按钮,内容在标题栏下方。</summary>
        Normal,

        /// <summary>无标题栏模式:完全去掉标题栏和三个按钮,内容占满整窗。</summary>
        NoTitleBar,

        /// <summary>沉浸模式:内容侵入标题栏区域,标题栏背景透明、无标题文字,
        /// 仅保留右上角三按钮,透明区域仍可拖动窗口。</summary>
        Immersive
    }
}
