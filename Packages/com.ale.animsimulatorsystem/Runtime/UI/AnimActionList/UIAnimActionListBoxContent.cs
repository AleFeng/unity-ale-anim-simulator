using Ale.Toolkit.Runtime;
using UnityEngine;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画动作 UI列表项目的内容。一条动画动作在列表里所需的全部展示数据。
    /// <para>由 <see cref="UIAnimActionList"/> 从 <see cref="AnimActionPlayer"/> 组装，
    /// 喂给 <see cref="UIAnimActionScrollList"/> 作为虚拟滚动的数据源。</para>
    /// </summary>
    public class UIAnimActionListBoxContent
    {
        /// <summary>UI中显示的动作名称。直接引用动作上那一份，不复制。</summary>
        public readonly TextValue uiDisplayActionName;

        /// <summary>
        /// 动画动作 图标
        /// </summary>
        public readonly Sprite actionIcon;

        /// <summary>
        /// 动画动作
        /// </summary>
        public AnimAction AnimAction => _animAction;
        private AnimAction _animAction;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="animAction">动画动作</param>
        public UIAnimActionListBoxContent(AnimAction animAction)
        {
            // 设置 关联的 动画动作
            _animAction = animAction;

            // 设置 动画动作 名称
            uiDisplayActionName = animAction.uiDisplayActionName;
            // 设置 动画动作 图标
            actionIcon = _animAction.actionIcon;
        }
    }
}
