#if ATK_LOCALIZATION
using UnityEngine.Localization;
#endif

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
#if ATK_LOCALIZATION
        [Tooltip("UI中显示的动作名称：多语言Key。")]
        public LocalizedString UIDisplayActionName;
#else
        [Tooltip("UI中显示的动作名称")]
        public string UIDisplayActionName;
#endif

        /// <summary>
        /// 动画动作 图标
        /// </summary>
        public readonly Sprite ActionIcon;

        /// <summary>
        /// 动画动作
        /// </summary>
        public AnimAction AnimAction => _animAction;
        private AnimAction _animAction;

        /// <summary>
        /// 构造函数
        /// </summary>
        public UIAnimActionListBoxContent()
        {

        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="animAction">动画动作</param>
        public UIAnimActionListBoxContent(AnimAction animAction)
        {
            // 设置 关联的 动画动作
            _animAction = animAction;

            // 设置 动画动作 名称
            UIDisplayActionName = animAction.uiDisplayActionName;
            // 设置 动画动作 图标
            ActionIcon = _animAction.actionIcon;
        }
    }
}
