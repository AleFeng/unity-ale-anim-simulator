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

        /// <summary>
        /// 获取 UI Canvas 组件
        /// </summary>
        public Canvas UICanvas { get; set; }

        /// <summary>
        /// 把 <see cref="AnimActionPlayer"/> 的世界坐标投影到屏幕所用的相机。
        ///
        /// <para>必须与 <see cref="AnimSimulatorManager"/> 做射线检测的那台是同一个，
        /// 否则提示圈会飘到射线打不到的地方——看着能点，点下去没反应。由管理器统一下发。</para>
        /// </summary>
        public Camera WorldCamera { get; set; }

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
                SetToCanvasSpacePosition(_animActionPlayerCurrent.transform.position);
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
                    // 数据一律按配置的自然顺序装填。「倒序显示」是呈现层的事，交给滚动列表自己的
                    // reverseContentOrder（在 UIAnimActionScrollList 上勾选）——它只是把条目排到
                    // 另一个槽位上，数据索引不变。此前是在这里把数据翻过来，于是「第几条」在数据层
                    // 与配置层含义相反，读代码时得时刻记着这层反转。
                    for (int i = 0; i < allAnimActions.Count; i++)
                        _animActionContentList.Add(new UIAnimActionListBoxContent(allAnimActions[i]));
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
        // 触发 「只有点击提示进入展开态、列表不铺开」。Random 类型专用，见 OpenCloseAnimActionList。
        private static readonly int AnimatorTriggerTipOnly = Animator.StringToHash("TriggerTipOnly");
        
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
        //   Random       淡入 + 展开   —— 但只有点击提示进入展开态，列表不铺开：点了就随机播，
        //                                没什么可挑的，铺开列表反而误导。提示圈的表现与 Operate 一致，
        //                                否则同样「能点」的两种播放器给出的视觉反馈会不一样。
        //   ProgressBar  两者皆无      —— 根本不接受点击
        //
        // 判定一律先判播放器非空：本类的这几个方法都是 public，外部在没有播放器时调用即空引用。
        //

        /// <summary>当前播放器是否需要显示「可点击」的提示（即是否接受玩家点击）。没有播放器时视为否。</summary>
        private bool CanFadeIn => _animActionPlayerCurrent && _animActionPlayerCurrent.IsPlayerOperable;

        /// <summary>当前播放器是否需要铺开 动画动作列表 让玩家挑一条。没有播放器时视为否。</summary>
        private bool CanExpandList => _animActionPlayerCurrent && _animActionPlayerCurrent.IsAnimActionSelectable;

        /// <summary>
        /// 触发一个 Animator 触发器，并把其余的复位。
        /// <para>仅用于淡入淡出这一对：打开 / 关闭那一对必须<b>保留</b>已置位的淡入触发器
        /// （打开流程会先淡入再打开），故不走这里。</para>
        /// </summary>
        private void SetTriggerExclusive(int trigger)
        {
            if (!TrySetTrigger(trigger)) return;

            foreach (var other in TriggersPresent)
                if (other != trigger) animator.ResetTrigger(other);
        }

        /// <summary>
        /// 触发一个 Animator 触发器；控制器上没有该参数时返回 <c>false</c> 而不去触发。
        /// <para>Unity 的 <c>SetTrigger</c> / <c>ResetTrigger</c> 遇到不存在的参数会往控制台刷错误，
        /// 而 <c>TriggerTipOnly</c> 是后加到动作列表 UI 上的——沿用旧 UI 预制体的工程没有这个参数。</para>
        /// </summary>
        private bool TrySetTrigger(int trigger)
        {
            if (!animator) return false;

            if (System.Array.IndexOf(TriggersPresent, trigger) < 0)
            {
                if (trigger == AnimatorTriggerTipOnly && !_tipOnlyMissingWarned)
                {
                    _tipOnlyMissingWarned = true;
                    AnimSimLog.Warn(this,
                        "动作列表 UI 的 Animator 上没有 TriggerTipOnly 参数，Random(点击随机) 类型的点击提示" +
                        "将停在「已淡入但未展开」的形态，而不是与 Operate 一致的展开态。" +
                        "请更新动作列表 UI 预制体（新增 A_TipOnly 状态与同名触发器）。" +
                        $"GameObject={gameObject.name}");
                }
                return false;
            }

            animator.SetTrigger(trigger);
            return true;
        }

        // 五个互斥触发器。实际存在哪些由 TriggersPresent 探测——见那里的说明。
        private static readonly int[] AnimatorTriggersAll =
        {
            AnimatorTriggerFadeIn, AnimatorTriggerFadeOut,
            AnimatorTriggerListOpen, AnimatorTriggerListClose, AnimatorTriggerTipOnly,
        };

        // 控制器上确实存在的那些触发器。null = 尚未探测成功。
        private int[] _triggersPresent;
        private bool _tipOnlyMissingWarned;

        /// <summary>
        /// <see cref="AnimatorTriggersAll"/> 中在当前控制器上<b>确实存在</b>的那些。
        /// <para>Animator 尚未初始化时不缓存结果，留到下次再探——此时 <c>parameters</c> 返回空数组，
        /// 缓存下来会让后续所有触发器都被误判为不存在。</para>
        /// </summary>
        private int[] TriggersPresent
        {
            get
            {
                if (_triggersPresent != null) return _triggersPresent;
                if (!animator || !animator.isInitialized) return System.Array.Empty<int>();

                var parameters = animator.parameters;
                var present = new List<int>(AnimatorTriggersAll.Length);
                foreach (var hash in AnimatorTriggersAll)
                    foreach (var p in parameters)
                        if (p.type == AnimatorControllerParameterType.Trigger && p.nameHash == hash)
                        {
                            present.Add(hash);
                            break;
                        }

                _triggersPresent = present.ToArray();
                return _triggersPresent;
            }
        }
        
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

                // 【第二级：进入展开态】接受玩家点击的播放器都要走这一级——点击提示的表现
                // （放大 + 旋转）对 Operate 与 Random 必须一致，差别只在列表铺不铺开，
                // 故只是触发器不同：
                //   Operate / 强制  → TriggerListOpen  提示圈展开态 + 列表铺开
                //   Random          → TriggerTipOnly   提示圈展开态，列表保持隐藏且不吃射线
                // ProgressBar 与「没有播放器」在上一级就被 CanFadeIn 挡住，到不了这里。
                if (!CanFadeIn && !isForceOpen) return;
                // 状态未变化，不重复触发
                if (_isOpen) return;

                // 触发失败（旧 UI 预制体没有 TriggerTipOnly）时不置位状态，退回「已淡入未展开」的旧表现
                if (!TrySetTrigger((CanExpandList || isForceOpen) ? AnimatorTriggerListOpen : AnimatorTriggerTipOnly))
                    return;
                _isOpen = true;
            }
            // 关闭列表
            else
            {
                // 状态未变化，不重复触发关闭动画。Random 的展开态同样由这里收回——
                // 它进的是 A_TipOnly，与 A_ListOpen 一样接受 TriggerListClose。
                if (_isOpen)
                {
                    TrySetTrigger(AnimatorTriggerListClose);
                    _isOpen = false;
                }

                // 光标离开后停在 idle 形态（A_ListClose：提示圈缩回 1.0 倍、子物体继续慢转），
                // Operate 与 Random 一致——「这里能点」这件事在光标离开后仍要提示，不该整个消失。
                //
                // 只有**根本不接受点击**的播放器才在这里淡出。判据是 CanFadeIn 而不是 CanExpandList：
                // 后者对 Random 也为假，用它会把 Random 的提示圈一起淡掉。ProgressBar 与「没有播放器」
                // 本就没淡入过，此处是空操作（被 FadeAnimActionList 自身的状态守卫挡掉）；真正会走到的
                // 是播放器类型在运行期从 Operate/Random 切成 ProgressBar 的场合——已淡入的提示得收掉。
                //
                // 提示圈整体的淡入淡出归 SetAnimActionPlayer 管（绑定播放器时淡入、解绑时淡出）。
                if (!CanFadeIn)
                    FadeAnimActionList(false);
            }
        }
        
        /// <summary>
        /// 设置 UI空间的位置。
        ///
        /// <para>换算基准是 <c>rectTrans</c> 的<b>父级</b>（由 <see cref="UIUtility.PositionAtWorldPos"/> 内部取），
        /// 不是 Canvas——赋的是 <c>localPosition</c>，原点得跟父级走。</para>
        /// </summary>
        /// <param name="worldPos"></param>
        public void SetToCanvasSpacePosition(Vector3 worldPos)
        {
            UIUtility.PositionAtWorldPos(rectTrans, worldPos, UICanvas, WorldCamera);
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

