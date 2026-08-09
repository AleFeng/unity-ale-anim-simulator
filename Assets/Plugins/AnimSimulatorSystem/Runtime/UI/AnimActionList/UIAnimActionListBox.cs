using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AirFishLab.ScrollingList;
using AirFishLab.ScrollingList.ContentManagement;

#if HAS_LOCALIZATION
using UnityEngine.Localization.Components;
#endif

namespace Fs.GameFramework.Gameplay.AnimSimulatorSystem
{
    /// <summary>
    /// 动画动作 UI列表项。
    /// 显示单个动画动作的信息，并处理用户交互。
    /// </summary>
    public class UIAnimActionListBox : ListBox
    {
        [Header("UI组件")] 
#if HAS_LOCALIZATION
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
        /// 更新 显示内容
        /// </summary>
        /// <param name="listContent"></param>
        protected override void UpdateDisplayContent(IListContent listContent)
        {
            // 将列表内容转换为 AnimActionListContent 类型
            _uiAnimActionListBoxContent = listContent as UIAnimActionListBoxContent;
            if (_uiAnimActionListBoxContent == null)
            {
                // 无效内容时，清空显示
#if HAS_LOCALIZATION
                // 设置 UI皮肤名称 多语言Key 为空
                if (localizeTxtActionName)
                    localizeTxtActionName.StringReference = null;
#else
                // 设置 UI皮肤名称 为空
                if (textActionName)
                    textActionName.text = string.Empty;
#endif
                imageActionIcon.sprite = null;
            }
            else
            {
                // 更新UI显示内容
#if HAS_LOCALIZATION
                // 设置 UI皮肤名称 多语言Key 为空
                if (localizeTxtActionName)
                    localizeTxtActionName.StringReference = _uiAnimActionListBoxContent.UIDisplayActionName;
#else
                // 设置 UI皮肤名称 为空
                if (textActionName)
                    textActionName.text = _uiAnimActionListBoxContent.UIDisplayActionName;
#endif
                // 设置 动作图标图片
                imageActionIcon.sprite = _uiAnimActionListBoxContent.ActionIcon;
            }
        }
    }
}
