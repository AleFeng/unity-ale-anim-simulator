using UnityEngine;
using UnityEngine.Events;
using AirFishLab.ScrollingList;
using Fs.GameFramework.Main.UI;

namespace Fs.GameFramework.Gameplay.AnimSimulatorSystem
{
    /// <summary>
    /// 动画动作 UI列表。
    /// 显示所有的动画动作，允许玩家选择播放。
    /// </summary>
    public class UIAnimActionList : MonoBehaviour
    {
        [Header("基础设置")]
        [Tooltip("Animator 组件")]
        [SerializeField] private Animator animator;
        [Tooltip("RectTransform")]
        [SerializeField] private RectTransform rectTrans;
        
        [Header("点击提示")]
        [Tooltip("点击提示 CanvasGroup组件")]
        [SerializeField] private CanvasGroup clickTipCanvasGroup;
        
        [Header("动画动作")]
        [Tooltip("动画动作 列表 CanvasGroup组件")]
        [SerializeField] private CanvasGroup animActionListCanvasGroup;
        [Tooltip("动画动作 列表数据组件")]
        [SerializeField] private UIAnimActionListBank uiAnimActionListBank;
        [Tooltip("动画动作 列表组件")]
        [SerializeField] private CircularScrollingList animActionList;
        
        /// <summary>
        /// 获取 UI Canvas 组件
        /// </summary>
        public Canvas UICanvas { get; set; }
        
        // 当前关联的 AnimActionPlayer 组件
        private AnimActionPlayer _animActionPlayerCurrent;
        
        private void Awake()
        {
            // 隐藏 动画动作 列表
            if (animActionListCanvasGroup)
                animActionListCanvasGroup.alpha = 0f;
            // 隐藏 点击提示
            if (clickTipCanvasGroup)
                clickTipCanvasGroup.alpha = 0f;
        }

        /// <summary>
        /// 设置 AnimActionPlayer
        /// </summary>
        /// <param name="animActionPlayer"></param>
        public void SetAnimActionPlayer(AnimActionPlayer animActionPlayer)
        {
            // 检查并初始化 列表数据组件
            if (animActionList.ListBank == null && uiAnimActionListBank != null)
            {
                // 关联 列表数据组件
                animActionList.SetListBank(uiAnimActionListBank);
                // 初始化 列表组件
                animActionList.Initialize();
            }
            
            // animActionPlayer为空时，隐藏列表 并清空数据
            _animActionPlayerCurrent = animActionPlayer;
            if (_animActionPlayerCurrent == null)
            {
                // 动画机 淡出
                FadeAnimActionList(false);
                // 清空数据
                uiAnimActionListBank.SetListContents(null, null);
                // 退订焦点监听，清理状态
                UnsubscribeFocusListener();
            }
            else
            {
                // 动画机 淡入。
                FadeAnimActionList(true);
                // 设置 UI空间的位置
                rectTrans.localPosition = UIUtility.WorldPosToUILocalPos(_animActionPlayerCurrent.transform.position, UICanvas);
                // 填充 列表的数据内容
                uiAnimActionListBank.SetListContents(_animActionPlayerCurrent, animActionList);
                // 订阅 CircularScrollingList焦点变化监听
                EnsureSubscribeFocusListener();
            }
        }
        
        #region UI操作
        // 当前的淡入淡出状态
        private bool _isFadeIn;
        // 当前的打开关闭状态
        private bool _isOpen;
        
        // Animator 参数 Hash 值
        private static readonly int AnimatorTriggerFadeIn = Animator.StringToHash("TriggerFadeIn"); // 触发 淡入
        private static readonly int AnimatorTriggerFadeOut = Animator.StringToHash("TriggerFadeOut"); // 触发 淡出
        private static readonly int AnimatorTriggerListOpen = Animator.StringToHash("TriggerListOpen"); // 触发 列表打开
        private static readonly int AnimatorTriggerListClose = Animator.StringToHash("TriggerListClose"); // 触发 列表关闭
        
        /// <summary>
        /// 淡入或淡出 动画动作列表
        /// </summary>
        /// <param name="isFadeIn"></param>
        /// <param name="isForceFadeIn">强制淡入。不判断AnimActionListPlayer的设定。</param>
        public void FadeAnimActionList(bool isFadeIn, bool isForceFadeIn = false)
        {
            // 淡入
            if (isFadeIn)
            {
                // 检查是否已经淡入。不重复淡入。
                if (!_isFadeIn)
                {
                    // 只有 Operate手动操作类型 或 强制操作 时，才淡入。
                    if (_animActionPlayerCurrent.AnimActionPlayerType != EAnimActionPlayerType.Operate && 
                        isForceFadeIn == false) return;
                    
                    animator.SetTrigger(AnimatorTriggerFadeIn);
                    _isFadeIn = true;
                    // 重置 打开状态
                    _isOpen = false;
                    // 清空所有Animator参数，防止状态冲突
                    animator.ResetTrigger(AnimatorTriggerFadeOut);
                    animator.ResetTrigger(AnimatorTriggerListOpen);
                    animator.ResetTrigger(AnimatorTriggerListClose);
                }
            }
            // 淡出
            else
            {
                // 检查是否已经淡出。不重复淡出。
                if (_isFadeIn)
                {
                    animator.SetTrigger(AnimatorTriggerFadeOut);
                    _isFadeIn = false;
                    // 重置 打开状态
                    _isOpen = false;
                    // 清空所有Animator参数，防止状态冲突
                    animator.ResetTrigger(AnimatorTriggerFadeIn);
                    animator.ResetTrigger(AnimatorTriggerListOpen);
                    animator.ResetTrigger(AnimatorTriggerListClose);
                }
            }
        }
        
        /// <summary>
        /// 打开或关闭 动画动作列表
        /// </summary>
        /// <param name="isOpen"></param>
        /// <param name="isForceOpen">强制打开。不判断AnimActionListPlayer的设定。</param>
        public void OpenCloseAnimActionList(bool isOpen, bool isForceOpen = false)
        {
            // 打开列表
            if (isOpen)
            {
                // 检查是否已经打开。不重复打开。
                if (!_isOpen)
                {
                    // 只有 Operate手动操作类型 或 强制操作 时，才打开关闭。
                    if (_animActionPlayerCurrent.AnimActionPlayerType != EAnimActionPlayerType.Operate && 
                        isForceOpen == false) return;
                    
                    // 若未淡入，则淡入
                    if (!_isFadeIn)
                        FadeAnimActionList(true, isForceOpen);
                    
                    animator.SetTrigger(AnimatorTriggerListOpen);
                    _isOpen = true;
                }
            }
            // 关闭列表
            else
            {
                // 检查是否已经关闭。不重复关闭。
                if (_isOpen)
                {
                    animator.SetTrigger(AnimatorTriggerListClose);
                    _isOpen = false;

                    // 若不是 Operate手动操作类型，则淡出。
                    if (_animActionPlayerCurrent.AnimActionPlayerType != EAnimActionPlayerType.Operate)
                        FadeAnimActionList(false);
                }
            }
        }
        
        /// <summary>
        /// 设置 UI空间的位置。
        /// </summary>
        /// <param name="worldPos"></param>
        public void SetToCanvasSpacePosition(Vector3 worldPos)
        {
            rectTrans.localPosition = UIUtility.WorldPosToUILocalPos(worldPos, UICanvas);
        }
        #endregion
        
        #region 列表 选中项目监听
        // 是否 已订阅焦点变化事件
        private bool _isFocusListenerSubscribed;
        // 回调函数
        private UnityAction<ListBox, ListBox> _onFocusingChangedAction;
        
        /// <summary>
        /// 订阅 CircularScrollingList焦点变化监听
        /// </summary>
        private void EnsureSubscribeFocusListener()
        {
            // 已订阅则跳过
            if (_isFocusListenerSubscribed) return;
            if (animActionList == null) return;
            
            // 订阅 焦点变化 事件
            _onFocusingChangedAction = (prevBox, currBox) =>
            {
                // 调用 焦点变化 处理函数
                if (currBox != prevBox)
                    OnFocusedItemChanged(currBox as UIAnimActionListBox);
            };
            if (animActionList != null && animActionList.ListSetting != null)
                animActionList.ListSetting.AddOnFocusingBoxChangedCallback(_onFocusingChangedAction);
            
            // 初始化 选中的 动画动作
            OnFocusedItemChanged(animActionList.GetFocusingBox() as UIAnimActionListBox);
            
            // 记录状态
            _isFocusListenerSubscribed = true;
        }

        /// <summary>
        /// 退订 CircularScrollingList焦点变化监听
        /// </summary>
        private void UnsubscribeFocusListener()
        {
            // 未订阅则跳过
            if (!_isFocusListenerSubscribed) return;
            
            // 退订 焦点变化 事件
            if (animActionList != null && animActionList.ListSetting != null && _onFocusingChangedAction != null)
                animActionList.ListSetting.RemoveOnFocusingBoxChangedCallback(_onFocusingChangedAction);
            _onFocusingChangedAction = null;
            
            // 清理状态
            _isFocusListenerSubscribed = false;
        }

        /// <summary>
        /// 焦点变化 处理函数
        /// </summary>
        /// <param name="listBoxNew"></param>
        private void OnFocusedItemChanged(UIAnimActionListBox listBoxNew)
        {
            if (listBoxNew == null) return;
            
            // 同步到 AnimActionPlayer 组件
            if (_animActionPlayerCurrent != null)
            {
                // 设置 AnimActionPlayer 的当前动画动作索引
                _animActionPlayerCurrent.SetSelectedAnimAction(listBoxNew.AnimAction);
            }
        }
        #endregion
    }
}

