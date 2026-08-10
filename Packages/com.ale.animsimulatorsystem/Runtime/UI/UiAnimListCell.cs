using UnityEngine;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 虚拟滚动列表里「可清空的格子」。
    /// </summary>
    public interface IUiAnimListCell
    {
        /// <summary>清空显示内容。格子滚出视口被回收时调用。</summary>
        void Clear();
    }

    /// <summary>
    /// 虚拟滚动列表里「可绑定数据的格子」。
    /// </summary>
    /// <typeparam name="TData">该格子显示的数据类型。</typeparam>
    public interface IUiAnimListCell<in TData> : IUiAnimListCell
    {
        /// <summary>把一条数据显示到本格子上。</summary>
        void Bind(TData data);
    }

    /// <summary>
    /// 格子绑定 / 清空的统一实现。
    ///
    /// <para>本包有两个滚动列表适配器（动作列表与皮肤列表），它们的 <c>BindCell</c> / <c>ClearCell</c>
    /// 原本逐行相同，只有类型名不同。但两者继承的是 toolkit 的<b>两个不同基类</b>
    /// （<c>UiwFocusOrderList</c> 与 <c>UiwVirtualOrderList</c>，前者是后者的子类），
    /// C# 单继承下没法再塞一个共同的中间基类进去，故把「绑定 / 清空到底要做哪几步」
    /// 收在这里，两个适配器各自调用——改顺序时只有一处要动，不会漏掉另一个。</para>
    /// </summary>
    public static class UiAnimListCell
    {
        /// <summary>激活格子并绑定数据。</summary>
        public static void Bind<TCell, TData>(TCell cell, TData data)
            where TCell : Component, IUiAnimListCell<TData>
        {
            if (!cell) return;

            cell.gameObject.SetActive(true);
            cell.Bind(data);
        }

        /// <summary>清空格子并隐藏。<b>先清后隐</b>——清空里通常带着事件退订，隐藏在前会读不到该做的收尾。</summary>
        public static void Clear<TCell>(TCell cell)
            where TCell : Component, IUiAnimListCell
        {
            if (!cell) return;

            cell.Clear();
            cell.gameObject.SetActive(false);
        }
    }
}
