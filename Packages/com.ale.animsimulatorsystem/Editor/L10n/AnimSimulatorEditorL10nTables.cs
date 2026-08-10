using UnityEditor;

namespace Ale.AnimSimulatorSystem.Editor
{
    /// <summary>
    /// 动画模拟器系统的编辑器 UI 译表登记入口。三语本地化<b>引擎</b>在 toolkit 的
    /// <see cref="Ale.Toolkit.Editor.ToolkitEditorL10n"/>；本类留在本包内，按区域把领域译文登记进
    /// 引擎的同一张表——各 <c>AnimSimulatorEditorL10n.Table.*.cs</c> 通过实现对应的
    /// <c>RegisterXxx()</c> 分部方法登记本区域译文，未实现的分部方法在编译期被消除，
    /// 可分步增量补充译表而无需改动本文件。
    ///
    /// <para>以 <c>[InitializeOnLoad]</c> 在编辑器加载时即登记（早于任何窗口打开）。分部方法体内通过
    /// <c>using static Ale.Toolkit.Editor.ToolkitEditorL10n;</c> 使裸的 <c>Add(...)</c> 直接解析到 toolkit 引擎。</para>
    ///
    /// <para>通用面板文案（警告 / 确定 / 取消 / 已安装 / 等待重新编译 / 启动时自动显示 / 查看文档 /
    /// 插件支持（编译宏）/ 文档未找到）已由 toolkit 自己的译表登记，本包不重复。</para>
    /// </summary>
    [InitializeOnLoad]
    internal static partial class AnimSimulatorEditorL10nTables
    {
        static AnimSimulatorEditorL10nTables()
        {
            RegisterWelcome();
        }

        // 各区域译表在对应的分部文件中实现；未实现者编译期消除，可增量补充。
        static partial void RegisterWelcome();
    }
}
