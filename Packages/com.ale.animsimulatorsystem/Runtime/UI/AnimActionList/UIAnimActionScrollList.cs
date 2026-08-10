using Ale.Toolkit.Runtime.UI;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画动作 滚动列表。把动画动作数据显示为一列可滚动的 <see cref="UIAnimActionListBox"/>。
    ///
    /// <para>继承自 <see cref="UiwFocusOrderList{TData,TCell}"/>，因此「焦点条目 = 当前选中的动作」：
    /// 玩家滚动列表时，落在焦点线上的那一条即为选中项，无需点击。选中的同步由
    /// <see cref="UIAnimActionList"/> 订阅 <c>OnFocusChanged</c> 完成。</para>
    ///
    /// <para>焦点位置、缩放曲线、横向偏移曲线等外观参数在 Inspector 上配置（见基类的「焦点」分组）。</para>
    /// </summary>
    public class UIAnimActionScrollList : UiwFocusOrderList<UIAnimActionListBoxContent, UIAnimActionListBox>
    {
        /// <summary>把一条动画动作数据显示到一个格子上。</summary>
        protected override void BindCell(UIAnimActionListBox cell, UIAnimActionListBoxContent data)
            => UiAnimListCell.Bind(cell, data);

        /// <summary>清空并隐藏一个格子。格子滚出视口被回收时调用。</summary>
        protected override void ClearCell(UIAnimActionListBox cell)
            => UiAnimListCell.Clear(cell);
    }
}
