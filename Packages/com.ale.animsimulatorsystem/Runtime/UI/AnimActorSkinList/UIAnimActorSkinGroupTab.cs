using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画角色 皮肤组 页签。
    /// 用于不同 皮肤组 之间的切换。点击页签后，切换到此 皮肤组。
    /// </summary>
    public class UIAnimActorSkinGroupTab : MonoBehaviour
    {
        [FormerlySerializedAs("imgSkinGroup")]
        [Header("UI组件")]
        [Tooltip("图片：皮肤组图标")]
        [SerializeField] private Image imgSkinGroupIcon;
        [Tooltip("图片：皮肤组背景")]
        [SerializeField] private Image imgSkinGroupBackground;
        [Tooltip("颜色：已选择 状态")]
        [SerializeField] private Color colorSelected = Color.white;
        [Tooltip("颜色：未选择 状态")]
        [SerializeField] private Color colorUnselected = Color.gray;
        [Tooltip("对象：已选择 标记")]
        [SerializeField] private GameObject goIsSelected;
        [Tooltip("对象：未选择 标记")]
        [SerializeField] private GameObject goIsUnselected;
        [Tooltip("按钮：选择皮肤按钮")]
        [SerializeField] private Button btnSkinGroupTab;
        
        /// <summary>
        /// 点击 皮肤组页签 事件
        /// </summary>
        public Action<UIAnimActorSkinGroupTab> OnClickSkinGroupTab;
        
        /// <summary>
        /// 动画角色 皮肤组
        /// </summary>
        public AnimActorSkinGroup AnimActorSkinGroup => _animActorSkinGroup;
        private AnimActorSkinGroup _animActorSkinGroup = null;
        
        private void Awake()
        {
            // 监听 按钮点击 事件
            if (btnSkinGroupTab != null)
                btnSkinGroupTab.onClick.AddListener(OnBtnClickSkinGroupTab);
        }
        
        /// <summary>
        /// 设置 动画角色 皮肤组
        /// </summary>
        /// <param name="animActorSkinGroup"></param>
        public void SetAnimActorSkinGroup(AnimActorSkinGroup animActorSkinGroup)
        {
            if (_animActorSkinGroup == animActorSkinGroup) return;
            _animActorSkinGroup = animActorSkinGroup;
            
            // 设置 皮肤组数据
            if (_animActorSkinGroup != null)
            {
                // 设置 皮肤组图标
                if (imgSkinGroupIcon)
                    imgSkinGroupIcon.sprite = _animActorSkinGroup.skinGroupIcon;
            }
            else
            {
                // 清空 皮肤组图标
                if (imgSkinGroupIcon)
                    imgSkinGroupIcon.sprite = null;
            }
            
            // 初始化 选择状态 为 未选择
            SetIsSelected(false);
        }
        
        /// <summary>
        /// 点击 选择皮肤组 页签
        /// </summary>
        private void OnBtnClickSkinGroupTab()
        {
            // 触发 点击事件
            OnClickSkinGroupTab?.Invoke(this);
        }
        
        /// <summary>
        /// 设置 是否已选择
        /// </summary>
        /// <param name="isSelected"></param>
        public void SetIsSelected(bool isSelected)
        {
            // 原先是完整复制的 if/else 两段，逐行相同、只差 true/false 与两个颜色。
            Color color = isSelected ? colorSelected : colorUnselected;

            // 设置 状态颜色
            // 皮肤组 图标
            if (imgSkinGroupIcon)
                imgSkinGroupIcon.color = color;
            // 皮肤组 背景
            if (imgSkinGroupBackground)
                imgSkinGroupBackground.color = color;

            // 设置 选中/未选中 标记 显示状态
            if (goIsSelected)
                goIsSelected.SetActive(isSelected);
            if (goIsUnselected)
                goIsUnselected.SetActive(!isSelected);
        }
    }
}