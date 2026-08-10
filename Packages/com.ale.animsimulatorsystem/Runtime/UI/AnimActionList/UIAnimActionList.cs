using System.Collections.Generic;
using UnityEngine;
using Ale.Toolkit.Runtime.UI;

namespace Ale.AnimSimulatorSystem
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
        [Tooltip("动画动作 列表组件")]
        [SerializeField] private UIAnimActionScrollList animActionList;
        [Tooltip("倒序显示动作。列表以焦点居中呈现时，倒序更符合从下往上翻的直觉。")]
        [SerializeField] private bool reverseContentOrder = true;

        /// <summary>
        /// 获取 UI Canvas 组件
        /// </summary>
        public Canvas UICanvas { get; set; }

        // 当前关联的 AnimActionPlayer 组件
        private AnimActionPlayer _animActionPlayerCurrent;
        // 动画动作数据 列表。作为滚动列表的数据源，索引与列表条目一一对应——
        // 焦点索引即可直接查到对应的 AnimAction，无需依赖格子实例。
        private readonly List<UIAnimActionListBoxContent> _animActionContentList =
            new List<UIAnimActionListBoxContent>();

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
            // animActionPlayer为空时，隐藏列表 并清空数据
            _animActionPlayerCurrent = animActionPlayer;
            if (_animActionPlayerCurrent == null)
            {
                // 动画机 淡出
                FadeAnimActionList(false);
                // 清空数据
                RebuildListContents();
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
                RebuildListContents();
                // 订阅 焦点变化监听
                EnsureSubscribeFocusListener();
            }
        }

        #region 列表 数据构建
        /// <summary>
        /// 依据当前 AnimActionPlayer 重建列表数据并推给列表组件。
        /// 当前播放器为空时即为清空。
        /// </summary>
        private void RebuildListContents()
        {
            // 清空 现有数据
            _animActionContentList.Clear();

            // 检查 角色动作播放器
            if (_animActionPlayerCurrent)
            {
                // 获取 所有满足条件的动画动作。传入回调，条件在之后才满足的动作会增量补进来。
                var allAnimActions = _animActionPlayerCurrent.GetAnimActionsMeetConditions(OnUnmetAnimActionIsMet);
                if (allAnimActions != null)
                {
                    // 填充 动画动作 数据列表
                    for (int i = 0; i < allAnimActions.Count; i++)
                        _animActionContentList.Add(new UIAnimActionListBoxContent(allAnimActions[i]));

                    // 倒序显示：焦点居中的呈现方式下，倒序更贴合从下往上翻的直觉
                    if (reverseContentOrder)
                        _animActionContentList.Reverse();
                }
            }

            // 推给 列表组件。SetItems 会回到起点并重算焦点。
            if (animActionList)
                animActionList.SetItems(_animActionContentList);
        }

        /// <summary>
        /// 当 不满足的动画动作，条件被满足时 调用
        /// </summary>
        /// <param name="animAction"></param>
        private void OnUnmetAnimActionIsMet(AnimAction animAction)
        {
            if (animAction == null) return;

            // 将 新满足条件的 动画动作，添加到 列表数据中。
            // 倒序显示时新条目应出现在列表头部，与重建时的整体倒序保持一致。
            var content = new UIAnimActionListBoxContent(animAction);
            if (reverseContentOrder) _animActionContentList.Insert(0, content);
            else                     _animActionContentList.Add(content);

            // 刷新 列表显示。用 UpdateItems 而非 SetItems：保留当前滚动位置，
            // 不因为解锁了一个新动作就把玩家正在浏览的位置弹回起点。
            if (animActionList)
                animActionList.UpdateItems(_animActionContentList);
        }
        #endregion
        
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
            // 状态未变化，不重复触发
            if (_isFadeIn == isFadeIn) return;

            // 只有 Operate手动操作类型 或 强制操作 时，才淡入。淡出不设此限制。
            if (isFadeIn && !IsOperateMode && !isForceFadeIn) return;

            // 触发对应的触发器，并复位其余三个——四个触发器互斥，留着旧的会让状态机跳错
            SetTriggerExclusive(isFadeIn ? AnimatorTriggerFadeIn : AnimatorTriggerFadeOut);
            _isFadeIn = isFadeIn;
            // 重置 打开状态
            _isOpen = false;
        }

        /// <summary>
        /// 当前播放器是否为「手动操作」类型。没有播放器时视为否。
        /// <para>原先三处直接对 <c>_animActionPlayerCurrent</c> 取 <c>AnimActionPlayerType</c>，
        /// 而本类的这几个方法都是 <c>public</c>——外部在没有播放器时调用即空引用。</para>
        /// </summary>
        private bool IsOperateMode =>
            _animActionPlayerCurrent && _animActionPlayerCurrent.AnimActionPlayerType == EAnimActionPlayerType.Operate;

        /// <summary>
        /// 触发一个 Animator 触发器，并把其余三个复位。
        /// <para>仅用于淡入淡出这一对：打开 / 关闭那一对必须<b>保留</b>已置位的淡入触发器
        /// （打开流程会先淡入再打开），故不走这里。</para>
        /// </summary>
        private void SetTriggerExclusive(int trigger)
        {
            if (!animator) return;

            animator.SetTrigger(trigger);
            foreach (var other in AnimatorTriggersAll)
                if (other != trigger) animator.ResetTrigger(other);
        }

        // 四个互斥触发器，供上面的复位遍历使用
        private static readonly int[] AnimatorTriggersAll =
            { AnimatorTriggerFadeIn, AnimatorTriggerFadeOut, AnimatorTriggerListOpen, AnimatorTriggerListClose };
        
        /// <summary>
        /// 打开或关闭 动画动作列表
        /// </summary>
        /// <param name="isOpen"></param>
        /// <param name="isForceOpen">强制打开。不判断AnimActionListPlayer的设定。</param>
        public void OpenCloseAnimActionList(bool isOpen, bool isForceOpen = false)
        {
            // 状态未变化，不重复触发
            if (_isOpen == isOpen) return;
            if (!animator) return;

            // 打开列表
            if (isOpen)
            {
                // 只有 Operate手动操作类型 或 强制操作 时，才打开。
                if (!IsOperateMode && !isForceOpen) return;

                // 若未淡入，则淡入。注意这里不能用 SetTriggerExclusive——
                // 淡入刚置位的触发器必须留着，两个触发器是叠加生效的。
                if (!_isFadeIn)
                    FadeAnimActionList(true, isForceOpen);

                animator.SetTrigger(AnimatorTriggerListOpen);
                _isOpen = true;
            }
            // 关闭列表
            else
            {
                animator.SetTrigger(AnimatorTriggerListClose);
                _isOpen = false;

                // 不是 Operate手动操作类型（含没有播放器的情况）时，一并淡出。
                if (!IsOperateMode)
                    FadeAnimActionList(false);
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

        /// <summary>
        /// 订阅 列表焦点变化监听
        /// </summary>
        private void EnsureSubscribeFocusListener()
        {
            // 已订阅则跳过
            if (_isFocusListenerSubscribed) return;
            if (!animActionList) return;

            // 订阅 焦点变化 事件
            animActionList.OnFocusChanged += OnFocusedItemChanged;
            // 记录状态
            _isFocusListenerSubscribed = true;

            // 初始化 选中的 动画动作。数据是在订阅之前推给列表的，那一次焦点事件订阅方收不到，
            // 故此处按当前焦点索引主动同步一次。
            ApplySelectedAnimAction(animActionList.FocusedIndex);
        }

        /// <summary>
        /// 退订 列表焦点变化监听
        /// </summary>
        private void UnsubscribeFocusListener()
        {
            // 未订阅则跳过
            if (!_isFocusListenerSubscribed) return;

            // 退订 焦点变化 事件
            if (animActionList)
                animActionList.OnFocusChanged -= OnFocusedItemChanged;

            // 清理状态
            _isFocusListenerSubscribed = false;
        }

        /// <summary>
        /// 焦点变化 处理函数
        /// </summary>
        /// <param name="previousIndex">上一个焦点索引</param>
        /// <param name="currentIndex">当前焦点索引</param>
        private void OnFocusedItemChanged(int previousIndex, int currentIndex)
        {
            ApplySelectedAnimAction(currentIndex);
        }

        /// <summary>
        /// 把指定索引的动画动作同步为 AnimActionPlayer 的选中项。
        /// <para>按<b>数据索引</b>而非格子实例取动作：格子是限速逐帧生成的，
        /// 数据推入后当帧未必已有对应实例，按索引查数据则始终可靠。</para>
        /// </summary>
        /// <param name="index">数据索引</param>
        private void ApplySelectedAnimAction(int index)
        {
            // 无关联的播放器时跳过
            if (!_animActionPlayerCurrent) return;
            // 索引越界（含列表为空时的 -1）时跳过
            if (index < 0 || index >= _animActionContentList.Count) return;

            // 设置 AnimActionPlayer 的当前动画动作
            _animActionPlayerCurrent.SetSelectedAnimAction(_animActionContentList[index].AnimAction);
        }
        #endregion
    }
}

