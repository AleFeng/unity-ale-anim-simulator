using Ale.Toolkit.Runtime.UI;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画角色皮肤 滚动列表。把当前皮肤组内的皮肤显示为一列可滚动的 <see cref="UIAnimActorSkinBox"/>。
    ///
    /// <para>与动作列表不同，这里<b>不需要焦点语义</b>：皮肤的选中与否由格子内的按钮点击驱动，
    /// 与滚动位置无关，故直接继承 <see cref="UiwVirtualOrderList{TData,TCell}"/> 即可。</para>
    /// </summary>
    public class UIAnimActorSkinScrollList : UiwVirtualOrderList<UIAnimActorSkinBoxContent, UIAnimActorSkinBox>
    {
        /// <summary>把一条皮肤数据显示到一个格子上。</summary>
        protected override void BindCell(UIAnimActorSkinBox cell, UIAnimActorSkinBoxContent data)
        {
            if (!cell) return;

            cell.gameObject.SetActive(true);
            cell.Bind(data);
        }

        /// <summary>清空并隐藏一个格子。格子滚出视口被回收时调用，其中会退订角色换装事件。</summary>
        protected override void ClearCell(UIAnimActorSkinBox cell)
        {
            if (!cell) return;

            cell.Clear();
            cell.gameObject.SetActive(false);
        }
    }
}
