using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AirFishLab.ScrollingList;
using AirFishLab.ScrollingList.ContentManagement;
#if ATK_LOCALIZATION
using UnityEngine.Localization.Components;
#endif

namespace Fs.GameFramework.Gameplay.AnimSimulatorSystem
{
    /// <summary>
    /// 动画角色 UI皮肤项目。
    /// 显示单个 角色皮肤 信息，可选择 添加或移除 该皮肤。
    /// </summary>
    public class UIAnimActorSkinBox : ListBox
    {
        [Header("UI组件")]
        [Tooltip("文本：皮肤名称")]
#if ATK_LOCALIZATION
        [SerializeField] private LocalizeStringEvent localizeTxtSkinName;
#else
        [SerializeField] private Text txtSkinName;
#endif
        [Tooltip("图片：皮肤图标")]
        [SerializeField] private Image imgSkin;
        [Tooltip("按钮：选择皮肤按钮")]
        [SerializeField] private Button btnSkin;
        [Tooltip("对象：已选择 标记")]
        [SerializeField] private GameObject goSelectedTip;
        [Tooltip("对象：未选择 标记")]
        [SerializeField] private GameObject goUnselectedTip;
        
        // 角色皮肤项目 数据
        private UIAnimActorSkinBoxContent _uiAnimActorSkinBoxContent;
        // 是否已选择
        private bool _isSelected;
        
        private void Awake()
        {
            // 监听 按钮点击 事件
            if (btnSkin != null)
                btnSkin.onClick.AddListener(OnBtnClickSkin);
            // 初始化 选择状态
            if (goSelectedTip)
                goSelectedTip.SetActive(false);
            if (goUnselectedTip)
                goUnselectedTip.SetActive(false);
        }
        
        /// <summary>
        /// 更新 数据内容 事件。
        /// </summary>
        /// <param name="listContent"></param>
        protected override void UpdateDisplayContent(IListContent listContent)
        {
            if (_uiAnimActorSkinBoxContent != null)
            {
                if (_uiAnimActorSkinBoxContent.AnimActor)
                {
                    // 移除 角色皮肤添加或移除 事件监听
                    _uiAnimActorSkinBoxContent.AnimActor.OnSkinAddOrRemove -= OnSkinAddOrRemove;
                }
            }
            
            // 记录 当前角色皮肤 数据
            _uiAnimActorSkinBoxContent = listContent as UIAnimActorSkinBoxContent;
            if (_uiAnimActorSkinBoxContent == null)
            {
                // 无效内容时，清空显示
#if ATK_LOCALIZATION
                // 设置 UI皮肤名称 多语言Key 为空
                if (localizeTxtSkinName)
                    localizeTxtSkinName.StringReference = null;
#else
                // 设置 UI皮肤名称 为空
                if (txtSkinName != null)
                    txtSkinName.text = string.Empty;
#endif
                // 设置 UI皮肤图片 为空
                if (imgSkin)
                    imgSkin.sprite = null;
                return;
            }
            
            if (_uiAnimActorSkinBoxContent.AnimActor)
            {
                // 监听 角色皮肤添加或移除 事件
                _uiAnimActorSkinBoxContent.AnimActor.OnSkinAddOrRemove += OnSkinAddOrRemove;
                // 设置 选择提示 显示状态
                SetSelectedTipDisplay(_uiAnimActorSkinBoxContent.AnimActor.CheckIsSelectedSkin(
                    _uiAnimActorSkinBoxContent.AnimActorSkinGroup, _uiAnimActorSkinBoxContent.AnimActorSkin));
            }
            
#if ATK_LOCALIZATION
            // 设置 UI皮肤名称 多语言Key
            if (localizeTxtSkinName)
            {
                localizeTxtSkinName.StringReference = _uiAnimActorSkinBoxContent.AnimActorSkin.uiDisplaySkinName;
                localizeTxtSkinName.RefreshString();
            }
#else
            // 设置 UI皮肤名称
            if (txtSkinName != null)
                txtSkinName.text = _uiAnimActorSkinBoxContent.AnimActorSkin.uiDisplaySkinName;
#endif
            // 设置 UI皮肤图片
            if (imgSkin)
            {
                imgSkin.sprite = _uiAnimActorSkinBoxContent.AnimActorSkin.skinImage;
            }
        }
        
        /// <summary>
        /// 点击 选择皮肤 按钮
        /// </summary>
        private void OnBtnClickSkin()
        {
            // 反选择状态
            SelectSkin(!_isSelected);
            // 清除当前选中的物体。避免按钮一直处于选中状态，导致光标悬停时 无法进入HighLight状态。
            EventSystem.current.SetSelectedGameObject(null);
        }
        
        /// <summary>
        /// 角色皮肤添加或移除 事件
        /// </summary>
        /// <param name="animActorSkinGroup"></param>
        /// <param name="animActorSkin"></param>
        /// <param name="isAdd"></param>
        private void OnSkinAddOrRemove(AnimActorSkinGroup animActorSkinGroup, AnimActorSkin animActorSkin, bool isAdd)
        {
            // 仅当 角色皮肤数据 匹配时，更新 选择状态
            if (_uiAnimActorSkinBoxContent != null
                && _uiAnimActorSkinBoxContent.AnimActorSkinGroup == animActorSkinGroup
                && _uiAnimActorSkinBoxContent.AnimActorSkin == animActorSkin)
            {
                // 皮肤的添加或移除，是由AnimActor或其他系统发起，此处仅更新UI显示状态。
                // 设置 选择提示 显示状态
                SetSelectedTipDisplay(isAdd);
            }
        }
        
        /// <summary>
        /// 选择皮肤
        /// </summary>
        /// <param name="isSelect">是否 选择</param>
        /// <param name="isForce">强制</param>
        public void SelectSkin(bool isSelect, bool isForce = false)
        {
            // 检查 状态是否变化
            if (isForce == false && _isSelected == isSelect) return;
            // 无数据内容时，跳过
            if (_uiAnimActorSkinBoxContent == null) return;

            // 添加或移除 角色皮肤
            bool isSuccess = true;
            if (_uiAnimActorSkinBoxContent.AnimActor)
            {
                if (isSelect)
                {
                    isSuccess = _uiAnimActorSkinBoxContent.AnimActor.AddSkin(
                        _uiAnimActorSkinBoxContent.AnimActorSkinGroup, _uiAnimActorSkinBoxContent.AnimActorSkin);
                }
                else
                {
                    isSuccess = _uiAnimActorSkinBoxContent.AnimActor.RemoveSkin(
                        _uiAnimActorSkinBoxContent.AnimActorSkinGroup, _uiAnimActorSkinBoxContent.AnimActorSkin);
                }
            }
            
            // 操作成功时，更新 选择状态
            if (isSuccess)
            {
                // 更新 选择提示 显示状态
                SetSelectedTipDisplay(_isSelected);
            }
        }
        
        /// <summary>
        /// 设置 选择提示 显示状态
        /// </summary>
        /// <param name="isSelected"></param>
        private void SetSelectedTipDisplay(bool isSelected)
        {
            // 状态无变化时，跳过
            if (_isSelected == isSelected) return;
            // 记录状态
            _isSelected = isSelected;
            
            // 设置 选择提示 显示状态
            if (goSelectedTip)
                goSelectedTip.SetActive(isSelected);
            // 设置 未选择提示 显示状态
            if (goUnselectedTip)
                goUnselectedTip.SetActive(!isSelected);
        }
    }
}