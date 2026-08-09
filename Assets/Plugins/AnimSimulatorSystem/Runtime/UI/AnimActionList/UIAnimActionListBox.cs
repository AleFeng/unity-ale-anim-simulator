using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if ATK_LOCALIZATION
using UnityEngine.Localization.Components;
#endif

namespace Fs.GameFramework.Gameplay.AnimSimulatorSystem
{
    /// <summary>
    /// 动画动作 UI列表项。显示单个动画动作的信息。
    /// <para>由 <see cref="UIAnimActionScrollList"/> 作为虚拟滚动的格子复用：
    /// 滚入视口时 <see cref="Bind"/>，滚出视口时 <see cref="Clear"/> 并隐藏。
    /// 因此本组件不持有任何跨复用的状态，显示内容完全由传入的数据决定。</para>
    /// </summary>
    public class UIAnimActionListBox : MonoBehaviour
    {
        [Header("UI组件")] 
#if ATK_LOCALIZATION
        [Tooltip("本地化文字：动作名称")]
        [SerializeField] private LocalizeStringEvent localizeTxtActionName;
#else
        [Tooltip("文本：动作名称")] 
        [SerializeField] private Text textActionName;
#endif
        [Tooltip("动作图标")] 
        [SerializeField] private Image imageActionIcon;
        [Tooltip("按钮：播放动作")] 
        // 按钮仅作为视觉元素使用。点击并操作 AnimActionPlayer组件 的行为由 AnimSimulatorManager 处理。
        [SerializeField] private Button btnPlayAction;
        
        // 当前列表内容（动画动作数据）
        private UIAnimActionListBoxContent _uiAnimActionListBoxContent;
        
        /// <summary>
        /// 动画动作
        /// </summary>
        public AnimAction AnimAction => _uiAnimActionListBoxContent?.AnimAction;
        
        /// <summary>
        /// 绑定 显示内容。传入 <c>null</c> 等同于 <see cref="Clear"/>。
        /// </summary>
        /// <param name="content">动画动作数据</param>
        public void Bind(UIAnimActionListBoxContent content)
        {
            // 记录 当前列表内容
            _uiAnimActionListBoxContent = content;
            if (_uiAnimActionListBoxContent == null)
            {
                Clear();
                return;
            }

            // 更新UI显示内容
#if ATK_LOCALIZATION
            // 设置 UI动作名称 多语言Key
            if (localizeTxtActionName)
                localizeTxtActionName.StringReference = _uiAnimActionListBoxContent.UIDisplayActionName;
#else
            // 设置 UI动作名称
            if (textActionName)
                textActionName.text = _uiAnimActionListBoxContent.UIDisplayActionName;
#endif
            // 设置 动作图标图片
            if (imageActionIcon)
                imageActionIcon.sprite = _uiAnimActionListBoxContent.ActionIcon;
        }

        /// <summary>
        /// 清空 显示内容。格子被回收出视口时调用。
        /// </summary>
        public void Clear()
        {
            // 断开数据引用，避免回收后的格子仍通过 AnimAction 属性暴露旧数据
            _uiAnimActionListBoxContent = null;

#if ATK_LOCALIZATION
            // 设置 UI动作名称 多语言Key 为空
            if (localizeTxtActionName)
                localizeTxtActionName.StringReference = null;
#else
            // 设置 UI动作名称 为空
            if (textActionName)
                textActionName.text = string.Empty;
#endif
            if (imageActionIcon)
                imageActionIcon.sprite = null;
        }
    }
}
