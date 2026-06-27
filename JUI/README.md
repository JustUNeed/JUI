# JUI

一个 WPF (.NET 8) 自用控件库。核心是类资源管理器风格的图片网格控件 `JuiGrid`,配套轻量主题系统与设置持久化。

- 目标框架:`net8.0-windows`
- 依赖:[VirtualizingWrapPanel](https://www.nuget.org/packages/VirtualizingWrapPanel) `2.5.2`
- 主要能力:多列自适应布局、UI 虚拟化、拖动排序、外部拖入 / 拖出、"移入某一项"、单选、左右键点击信号、明暗主题切换。

---

## 快速开始

### 1. 接入主题(必做一次)

在 `App.xaml.cs` 的 `OnStartup` 里调用一次 `ThemeManager.Install()`,注入全部控件样式并应用主题:

```csharp
using JUI.Theming;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Install();          // 用上次保存的主题
        // ThemeManager.Install(JuiTheme.Dark);  // 或指定初始主题
    }
}
```

> 不调用 `Install()`,`JuiGrid` 会缺少默认样式,无法正常显示。

### 2. 在 XAML 中放置控件

```xml
<Window ...
        xmlns:jui="clr-namespace:JUI.Controls;assembly=JUI">

    <jui:JuiGrid x:Name="Grid"
                 ItemWidth="80" ItemHeight="80"
                 ItemSpacing="5" MaxRows="3"
                 AllowDropOnItem="True">
        <jui:JuiGrid.ItemTemplate>
            <DataTemplate>
                <Image Source="{Binding Thumbnail}" Stretch="Fill"
                       Width="{Binding ItemWidth, RelativeSource={RelativeSource AncestorType=jui:JuiGrid}}"
                       Height="{Binding ItemHeight, RelativeSource={RelativeSource AncestorType=jui:JuiGrid}}"/>
            </DataTemplate>
        </jui:JuiGrid.ItemTemplate>
    </jui:JuiGrid>
</Window>
```

### 3. 在代码后台接线

`JuiGrid` 不在 XAML 里绑定行为,所有契约通过 `Action` / `Func` 属性赋值(精简、直观,适合自用):

```csharp
// 数据源:用 ObservableCollection,增删才能自动刷新
public ObservableCollection<Photo> Items { get; } = new();

Grid.ItemsSource = Items;

// 拖出到外部(资源管理器等)所需:如何从数据项取文件路径
Grid.FilePathSelector = item => (item as Photo)?.Path;

// 外部拖入:控件不解析格式,把原始数据交给你,返回要插入的数据项
Grid.ExternalDropHandler = data =>
{
    if (data.GetDataPresent(DataFormats.FileDrop))
    {
        var paths = (string[])data.GetData(DataFormats.FileDrop);
        return paths.Select(p => new Photo { Path = p });
    }
    return null;   // 不接受本次拖入
};

// 左键 / 右键点击(已无双击概念)
Grid.LeftClick  = item => OpenPhoto((Photo)item);
Grid.RightClick = item => ShowContextMenu((Photo)item);

// 把一项拖到另一项主体上(需 AllowDropOnItem=True),不改变列表
Grid.ItemDropped = (dragged, rawData, target) =>
{
    // dragged: 内部拖动时是被拖的数据项;外部拖入时为 null
    // rawData: 外部拖入时为原始 IDataObject;内部拖动时为 null
    // target : 被放入的目标项
};

// 列表自身发生增删改(插入/排序/添加)时回调,供持久化
Grid.ContentChanged = () => SaveOrder();
```

---

## 控件:`JuiGrid`

`public class JuiGrid : ListBox` —— 命名空间 `JUI.Controls`。

继承自 `ListBox`,因此 `ItemsSource`、`ItemTemplate`、`Items` 等基类成员照常可用。控件始终为**单选**(构造时强制 `SelectionMode = Single`)。

### 依赖属性(可在 XAML 设置 / 绑定)

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `ItemWidth` | `double` | `128` | 每项容器的完整宽度(不含间隔)。 |
| `ItemHeight` | `double` | `148` | 每项容器的完整高度(不含间隔)。 |
| `ItemSpacing` | `double` | `0` | 单边间隔;项与项之间实际间隙为此值的 2 倍。 |
| `MaxRows` | `int` | `3` | 最多显示几行,超过则出现垂直滚动条。 |
| `EmptyContent` | `object` | `null` | 列表为空时居中显示的内容(任意 UI)。 |

> 控件会根据可用宽度估算列数与行数,自动设置 `MaxHeight`,使其最多显示 `MaxRows` 行。布局假设所有项等宽等高。

### 行为契约属性(代码后台赋值)

| 成员 | 签名 | 说明 |
|---|---|---|
| `FilePathSelector` | `Func<object, string?>?` | 如何从数据项取出磁盘文件路径,用于拖出到外部程序。 |
| `ExternalDropHandler` | `Func<IDataObject, IEnumerable<object>?>?` | 外部数据拖入时调用。控件不判断格式,把原始 `IDataObject` 全权交给你;返回要插入的数据项集合,返回 `null` 或空表示不接受。控件负责按落点插入。 |
| `ContentChanged` | `Action?` | 列表自身增删改(插入 / 排序 / 添加)后回调,供持久化。 |
| `ItemDropped` | `Action<object?, IDataObject?, object>?` | 把东西"放进某一项"时调用(**不改变当前列表**)。参数依次为:被拖数据(内部拖动=项数据,外部=`null`)、原始拖放数据(外部=有值,内部=`null`)、目标项。需 `AllowDropOnItem=true`。 |
| `LeftClick` | `Action<object>?` | 项被左键点击时调用,参数为数据项。刚发生过拖动的抬起不算点击;项内控件标记 `Handled` 则不触发。 |
| `RightClick` | `Action<object>?` | 项被右键点击时调用,参数为数据项。 |
| `AllowDropOnItem` | `bool`(默认 `false`) | 是否允许"放进某一项"。关闭时一律按插入 / 添加处理。 |

### 公开方法

| 方法 | 说明 |
|---|---|
| `void AddItem(object item)` | 追加到末尾,触发 `ContentChanged`。 |
| `void InsertItem(int index, object item)` | 插入到指定位置(越界自动夹取),触发 `ContentChanged`。 |
| `void RemoveItem(object item)` | 按引用移除,触发 `ContentChanged`。 |
| `void MoveItem(int oldIndex, int newIndex)` | 移动项位置(内部拖动排序即用此方法),触发 `ContentChanged`。 |
| `object? GetItemFromElement(DependencyObject? element)` | 由可视树元素反查其所属数据项;常用于项内按钮的 `Click` 事件中定位当前项。 |

> 以上写操作要求 `ItemsSource` 是**可写、非固定大小**的列表(如 `ObservableCollection<T>`)。绑定到只读视图时这些方法会安全地不执行,不会抛异常。

### 附加属性

| 成员 | 说明 |
|---|---|
| `JuiGrid.IsDropTarget`(attached `bool`) | 拖动悬停到某项主体上时,控件自动把该项设为 `true`,供 `ItemContainerStyle` 的 `Trigger` 做高亮。一般无需手动设置。 |

---

## 落点判定规则

拖动放下时,落点决定行为:

- **落在间隙 / 空白处** → 插入 / 排序。拖动过程中显示一条插入位置竖线(`InsertionAdorner`)。
  - 内部拖动:调用 `MoveItem` 重排。
  - 外部拖入:调用 `ExternalDropHandler` 取数据项,控件按落点插入。
- **落在某一项主体上**(需 `AllowDropOnItem=true`)→ "移入该项",高亮目标项并隐藏插入线,调用 `ItemDropped`,**不修改当前列表**。

外部拖入的数据格式(文件 / 浏览器 URL / 文本 / 任意私有格式)一律不由控件判断,全部交给 `ExternalDropHandler` 解析。

---

## 点击信号说明

控件不提供双击。左键抬起触发 `LeftClick`,右键抬起触发 `RightClick`,两者独立、互不干扰:

- 刚刚发生过拖动的那次鼠标抬起**不会**被当作点击。
- 点在项内的子控件(如删除按钮)上,只要该控件把事件标记为 `Handled`,就不会冒泡触发 `LeftClick` / `RightClick`。

项内按钮里定位当前项的典型写法:

```csharp
private void DeleteBtn_Click(object sender, RoutedEventArgs e)
{
    var item = Grid.GetItemFromElement(sender as DependencyObject);
    if (item != null)
        Grid.RemoveItem(item);   // 内部会触发 ContentChanged
}
```

---

## 主题:`ThemeManager`

`public static class ThemeManager` —— 命名空间 `JUI.Theming`。

| 成员 | 签名 | 说明 |
|---|---|---|
| `Install` | `void Install(JuiTheme? theme = null)` | 一行接入:注入全部样式并应用主题。在 `OnStartup` 里调用一次。不传 `theme` 则用上次保存的设置。 |
| `Initialize` | `void Initialize()` | 兼容旧调用,等价于 `Install()`。 |
| `Apply` | `void Apply(JuiTheme theme, bool save = true)` | 切换并应用颜色主题,默认持久化。 |
| `Toggle` | `void Toggle()` | 在浅色 / 深色间一键切换并自动保存。 |
| `Current` | `JuiTheme { get; }` | 当前主题。 |
| `ThemeChanged` | `event Action<JuiTheme>` | 主题切换时触发。 |

```csharp
ThemeManager.Toggle();                 // 明暗一键切换
ThemeManager.Apply(JuiTheme.Dark);     // 切到暗色并保存
ThemeManager.ThemeChanged += t => { /* 主题变了 */ };
```

### 主题资源键(供自定义样式引用)

通过 `DynamicResource` 引用,切主题时自动更新:

| 键 | 用途 |
|---|---|
| `Jui.Surface.Window` | 窗口背景 |
| `Jui.Surface.Default` / `.Hover` / `.Active` | 表面态:默认 / 悬停 / 激活 |
| `Jui.Text.Primary` / `.Secondary` / `.Disabled` | 文字:主 / 次 / 禁用 |
| `Jui.Accent` / `Jui.Accent.Hover` | 强调色 / 悬停 |
| `Jui.Border` | 边框 / 分隔线 |

---

## 设置持久化

| 类型 | 说明 |
|---|---|
| `JuiSettings` | 全部持久化设置的数据类(目前含 `Theme`)。新增设置只需加属性,序列化自动处理,旧文件向后兼容。 |
| `SettingsStore` | 负责 `JuiSettings` 的 JSON 读写。`Current` 首次访问自动加载,`Save()` 写回磁盘,`Load()` 读取(损坏 / 缺失时回退默认)。 |

配置文件路径:`%AppData%\JUI\settings.json`。`ThemeManager` 的主题切换会自动落盘,通常无需手动调用 `SettingsStore`。

---

## 数据项约定

数据项可以是任意类型,控件不做约束。配套提供 `JuiImageItem`(`FilePath` / `Thumbnail` / `DisplayName`)作为图片项的参考模型。实际使用中,推荐自定义类并提供按需加载、解码到缩略图尺寸、`Freeze()` 的 `BitmapImage`,以节省内存(参见 demo 的 `Photo` 类)。
