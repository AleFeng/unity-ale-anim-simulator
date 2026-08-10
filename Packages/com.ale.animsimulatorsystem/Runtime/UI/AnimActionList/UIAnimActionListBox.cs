using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画动作 UI列表项。显示单个动画动作的信息。
    /// <para>由 <see cref="UIAnimActionScrollList"/> 作为虚拟滚动的格子复用：
    /// 滚入视口时 <see cref="Bind"/>，滚出视口时 <see cref="Clear"/> 并隐藏。
    /// 因此本组件不持有任何跨复用的状态，显示内容完全由传入的数据决定。</para>
    /// </summary>
    public class UIAnimActionListBox : MonoBehaviour, IUiAnimListCell<UIAnimActionListBoxContent>
    {
        [Header("UI组件")]
        [Tooltip("文本：动作名称。内容取自动作配置的「UI中显示的动作名称」。")]
        [SerializeField] private TMP_Text textActionName;
        [Tooltip("动作图标")]
        [SerializeField] private Image imageActionIcon;
        // 这里原本还有一个序列化的「按钮：播放动作」字段，但全类无人引用——点击并操作
        // AnimActionPlayer 的行为由 AnimSimulatorManager 处理，按钮纯粹是预制体上的视觉元素，
        // 不需要在脚本里持有。已删除。

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
            RefreshDisplayName();
            // 设置 动作图标图片
            if (imageActionIcon)
                imageActionIcon.sprite = _uiAnimActionListBoxContent.ActionIcon;
        }

        private void OnEnable()  { AnimLocale.OnLocaleChanged += RefreshDisplayName; }
        private void OnDisable() { AnimLocale.OnLocaleChanged -= RefreshDisplayName; }

        /// <summary>按当前语言重取一次动作名。语言切换时由 <see cref="AnimLocale"/> 触发。</summary>
        private void RefreshDisplayName()
        {
            if (!textActionName) return;
            var value = _uiAnimActionListBoxContent != null ? _uiAnimActionListBoxContent.UIDisplayActionName : null;
            textActionName.text = value != null ? value.ResolveText() : string.Empty;
        }

        /// <summary>
        /// 清空 显示内容。格子被回收出视口时调用。
        /// </summary>
        public void Clear()
        {
            // 断开数据引用，避免回收后的格子仍通过 AnimAction 属性暴露旧数据
            _uiAnimActionListBoxContent = null;

            // 设置 UI动作名称 为空
            RefreshDisplayName();
            if (imageActionIcon)
                imageActionIcon.sprite = null;
        }
    }
}
