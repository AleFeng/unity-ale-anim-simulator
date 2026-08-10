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
        private void RebuildListContents(bool preserveScroll = false)
        {
            // 清空 现有数据
            _animActionContentList.Clear();

            // 检查 角色动作播放器
            if (_animActionPlayerCurrent)
            {
                // 获取 所有满足条件的动画动作
                var allAnimActions = _animActionPlayerCurrent.GetAnimActionsMeetConditions();
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

            if (!animActionList) return;

            // preserveScroll=true 时用 UpdateItems：保留当前滚动位置，
            // 不因为解锁了一个新动作就把玩家正在浏览的位置弹回起点。
            // 否则用 SetItems——换了播放器就该回到起点并重算焦点。
            if (preserveScroll) animActionList.UpdateItems(_animActionContentList);
            else                animActionList.SetItems(_animActionContentList);
        }

        private void OnEnable()  { AnimSimulatorManager.OnConditionInputsChanged += OnConditionInputsChanged; }
        private void OnDisable() { AnimSimulatorManager.OnConditionInputsChanged -= OnConditionInputsChanged; }

        /// <summary>
        /// 条件所依赖的输入（进度条读数 / 等级）发生变化：重新求值一遍，条数变了就刷新列表。
        ///
        /// <para>取代了旧的「条件不满足时登记回调、满足后把该条增量插进来」——那套以活着的 UI 组件的
        /// <c>GetHashCode()</c> 为字典键，且每次检查都会把回调置空再只留最新一个订阅者。
        /// 改为统一广播 + 重新求值后，多个列表 UI 同时在用也各自正确。</para>
        /// </summary>
        private void OnConditionInputsChanged()
        {
            if (!_animActionPlayerCurrent) return;

            // 只有条数变化才动列表，避免每次进度条跳动都重建一遍
            int countNow = _animActionPlayerCurrent.GetAnimActionsMeetConditions().Count;
            if (countNow == _animActionContentList.Count) return;

            RebuildListContents(preserveScroll: true);
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

            // 只有 接受玩家点击的播放器 或 强制操作 时，才淡入。淡出不设此限制。
            if (isFadeIn && !CanFadeIn && !isForceFadeIn) return;

            // 触发对应的触发器，并复位其余三个——四个触发器互斥，留着旧的会让状态机跳错
            SetTriggerExclusive(isFadeIn ? AnimatorTriggerFadeIn : AnimatorTriggerFadeOut);
            _isFadeIn = isFadeIn;
            // 重置 打开状态
            _isOpen = false;
        }

        //
        // 本界面有两级状态：**淡入**（显示「这里可以点」的提示）与**展开**（铺开动画动作列表让玩家挑）。
        // 三种播放器类型各取所需，故两级各有各的判定，不能共用一个「是不是 Operate」：
        //
        //   Operate      淡入 + 展开   —— 玩家滚动列表选中一条再点
        //   Random       只淡入        —— 点了就随机播，没什么可挑的，铺开列表反而误导
        //   ProgressBar  两者皆无      —— 根本不接受点击
        //
        // 判定一律先判播放器非空：本类的这几个方法都是 public，外部在没有播放器时调用即空引用。
        //

        /// <summary>当前播放器是否需要显示「可点击」的提示（即是否接受玩家点击）。没有播放器时视为否。</summary>
        private bool CanFadeIn => _animActionPlayerCurrent && _animActionPlayerCurrent.IsPlayerOperable;

        /// <summary>当前播放器是否需要铺开 动画动作列表 让玩家挑一条。没有播放器时视为否。</summary>
        private bool CanExpandList => _animActionPlayerCurrent && _animActionPlayerCurrent.IsAnimActionSelectable;

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
            if (!animator) return;

            // 打开列表
            if (isOpen)
            {
                // 【第一级：淡入】只看「是否接受玩家点击」。必须走在下面那个 _isOpen 守卫之前——
                // Random 类型永远不展开、_isOpen 恒为 false，把守卫提到方法开头会把它的淡入一起挡掉。
                // 注意这里不能用 SetTriggerExclusive——淡入刚置位的触发器必须留着，两个触发器是叠加生效的。
                if ((CanFadeIn || isForceOpen) && !_isFadeIn)
                    FadeAnimActionList(true, isForceOpen);

                // 【第二级：展开】只有需要玩家挑一条的播放器（或强制打开）才铺开列表
                if (!CanExpandList && !isForceOpen) return;
                // 状态未变化，不重复触发
                if (_isOpen) return;

                animator.SetTrigger(AnimatorTriggerListOpen);
                _isOpen = true;
            }
            // 关闭列表
            else
            {
                // 状态未变化，不重复触发关闭动画
                if (_isOpen)
                {
                    animator.SetTrigger(AnimatorTriggerListClose);
                    _isOpen = false;
                }

                // 不需要展开列表的播放器（Random / ProgressBar / 没有播放器）一并淡出。
                // 从未淡入过的会被 FadeAnimActionList 自身的状态守卫挡掉，是空操作。
                if (!CanExpandList)
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

