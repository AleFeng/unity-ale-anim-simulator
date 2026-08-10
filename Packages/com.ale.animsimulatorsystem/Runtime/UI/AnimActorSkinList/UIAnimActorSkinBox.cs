using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ATK_LOCALIZATION
using UnityEngine.Localization.Components;
#endif

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画角色 UI皮肤项目。
    /// 显示单个 角色皮肤 信息，可选择 添加或移除 该皮肤。
    ///
    /// <para>由 <see cref="UIAnimActorSkinScrollList"/> 作为虚拟滚动的格子复用：
    /// 滚入视口时 <see cref="Bind"/>，滚出视口时 <see cref="Clear"/> 并隐藏。
    /// 因为格子会被反复复用，<b>对角色换装事件的订阅必须与绑定严格配对</b>——
    /// 绑定时订阅、清空时退订，否则被回收的格子仍会响应事件、按早已不属于自己的皮肤刷新显示。</para>
    /// </summary>
    public class UIAnimActorSkinBox : MonoBehaviour, IUiAnimListCell<UIAnimActorSkinBoxContent>
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
        /// 绑定 显示内容。传入 <c>null</c> 等同于 <see cref="Clear"/>。
        /// </summary>
        /// <param name="content">角色皮肤数据</param>
        public void Bind(UIAnimActorSkinBoxContent content)
        {
            // 先退订上一条数据的事件，再记录新数据——订阅与绑定严格配对
            UnsubscribeSkinEvent();

            // 记录 当前角色皮肤 数据
            _uiAnimActorSkinBoxContent = content;
            if (_uiAnimActorSkinBoxContent == null)
            {
                Clear();
                return;
            }

            if (_uiAnimActorSkinBoxContent.AnimActor)
            {
                // 监听 角色皮肤添加或移除 事件
                _uiAnimActorSkinBoxContent.AnimActor.OnSkinAddOrRemove += OnSkinAddOrRemove;
                // 设置 选择提示 显示状态。
                // 强制写入：格子是复用来的，_isSelected 保留着上一条数据的状态，
                // 走增量判断会在「新旧状态恰好相同」时跳过，而此时提示对象的显示状态未必与之一致。
                SetSelectedTipDisplay(_uiAnimActorSkinBoxContent.AnimActor.CheckIsSelectedSkin(
                    _uiAnimActorSkinBoxContent.AnimActorSkinGroup, _uiAnimActorSkinBoxContent.AnimActorSkin), true);
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
        /// 清空 显示内容。格子被回收出视口时调用。
        /// </summary>
        public void Clear()
        {
            // 退订事件并断开数据引用
            UnsubscribeSkinEvent();
            _uiAnimActorSkinBoxContent = null;

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

            // 提示对象一并复位，与 _isSelected 保持一致，避免复用时状态错位
            SetSelectedTipDisplay(false, true);
        }

        /// <summary>
        /// 退订 当前数据对应角色的 换装事件。无数据或角色已销毁时为空操作。
        /// </summary>
        private void UnsubscribeSkinEvent()
        {
            if (_uiAnimActorSkinBoxContent == null) return;
            if (!_uiAnimActorSkinBoxContent.AnimActor) return;

            _uiAnimActorSkinBoxContent.AnimActor.OnSkinAddOrRemove -= OnSkinAddOrRemove;
        }

        private void OnDestroy()
        {
            // 列表销毁时会连同格子一并销毁。角色可能比列表活得更久，
            // 不退订就会把委托留在角色上，指向已销毁的格子。
            UnsubscribeSkinEvent();
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
                // 更新 选择提示 显示状态。
                // 这里必须传新值 isSelect：原先传的是 _isSelected（旧值），
                // 于是 SetSelectedTipDisplay 的判等守卫直接早退，这个分支成了空操作
                // ——一直靠 AnimActor.OnSkinAddOrRemove 走另一条路兜住，
                // 而角色引用为空时（上面 isSuccess 仍为 true）标记就永远不更新了。
                SetSelectedTipDisplay(isSelect);
            }
        }
        
        /// <summary>
        /// 设置 选择提示 显示状态
        /// </summary>
        /// <param name="isSelected">是否已选择</param>
        /// <param name="isForce">强制写入。绑定 / 清空时必须为 true——格子复用后 <c>_isSelected</c>
        /// 残留的是上一条数据的状态，增量判断会误跳过。</param>
        private void SetSelectedTipDisplay(bool isSelected, bool isForce = false)
        {
            // 状态无变化时，跳过
            if (!isForce && _isSelected == isSelected) return;
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