using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画播放器基类：承载与动画后端<b>无关</b>的全部机制，由 <see cref="SpineAnimator"/> /
    /// <c>Live2dAnimator</c> 继承并各自实现后端差异。上层的 <see cref="AnimActor"/> 与
    /// <see cref="AnimActionPlayer"/> 一律面向本类编程，对具体后端无感。
    ///
    /// <para><b>基类负责</b>：状态机（状态名 → 一组动画）与渲染器引用计数、每轨道的播放栈
    /// （被覆盖时压栈、停止时弹栈并恢复上一条）、循环动画去重、循环随机间隔调度、起播延时、
    /// 单次播放完成计时、皮肤名册、淡入淡出、轨道编号规则。</para>
    ///
    /// <para><b>子类负责</b>：把「播放 / 停止 / 清除 / 查时长 / 读写透明度 / 读写进度 / 皮肤是否存在 / 应用皮肤」
    /// 这些动作落到具体后端上——见本类的「后端契约」区。</para>
    ///
    /// <para><b>渲染器一律以 <see cref="Component"/> 表示。</b>共享机制从不调用后端渲染器的任何 API，
    /// 只把它当三样东西用：① 字典的引用身份键 ② 可 <c>SetActive</c> 的 GameObject 宿主
    /// ③ 原样回传给后端虚方法的不透明令牌。授权侧仍由各子类声明强类型字段（如 Spine 的
    /// <c>SkeletonAnimation</c>），在 <see cref="EnumerateStateDatas"/> 里转换成中性记录——
    /// 这样 Inspector 的类型约束不退化，既有预制体的字段名与序列化布局也一字不改。</para>
    /// </summary>
    public abstract class AnimatorBase : MonoBehaviour
    {
        #region 序列化设置

        [Header("动画基础设置")]
        [FormerlySerializedAs("spineAnimSwitchSpineDuration")]
        [Tooltip("切换动画模型的淡入淡出时间（秒）")]
        [SerializeField] protected float animFadeDuration = 0.2f;
        [Tooltip("初始化时显示：在初始化时，就会显示动画的渲染。")]
        [SerializeField] protected bool isDisplayOnInit;

        [Header("动画状态设置")]
        [FormerlySerializedAs("spineStateInitList")]
        [Tooltip("初始状态：根据 动画制作时的 状态名称 设置，填写一个或多个 状态名称。")]
        [SerializeField] protected string[] stateInitList = { "idle" };

        /// <summary>
        /// 初始状态列表：<c>Start</c> 时切换到这一组状态。
        /// <para>本类是这项配置的<b>唯一持有者</b>——2.1.2 之前 <see cref="AnimActor"/> 上还有一份同名字段，
        /// 两个 <c>Start</c> 各应用一次、顺序未定义。</para>
        /// </summary>
        public string[] StateInitList
        {
            get => stateInitList;
            set => stateInitList = value ?? Array.Empty<string>();
        }

        [Header("皮肤设置")]
        [Tooltip("基础皮肤 列表：始终显示的 基础皮肤名称。")]
        [AnimSkinName] public string[] baseSkins;

        #endregion

        #region 后端契约

        // 这 13 个成员是 virtual 而非 abstract，默认实现一律「什么也不做 / 返回空值」。
        //
        // 这样声明是为了服务后端宏关闭的场合：ASS_SPINE / ASS_LIVE2D 未启用时，对应子类的整个类体
        // 都不参与编译，只剩一个空的类声明——若这些成员是 abstract，子类就必须在 #else 分支里再抄
        // 一遍 13 个空桩（两个后端各一份，逐字节相同），改动契约要同步 4 处。改为带默认实现的
        // virtual 之后，空类体直接继承默认实现，编辑点只剩本处一个。
        //
        // 代价：子类漏写 override 会静默无效而不是编译报错。本包只有 SpineAnimator 与 Live2dAnimator
        // 两个子类且同在本目录下，新增后端时请对照本区逐条实现。

        /// <summary>解析默认渲染器（状态未指定专用渲染器时使用）。<c>Awake</c> 时调用一次。</summary>
        protected virtual Component ResolveDefaultRenderer() => null;

        /// <summary>
        /// 枚举授权配置里的状态数据，转换为后端中性的记录。基类负责去重与建表。
        /// </summary>
        protected virtual IEnumerable<FAnimStateData> EnumerateStateDatas() => Array.Empty<FAnimStateData>();

        /// <summary>在指定渲染器的指定轨道上播放一条动画。返回是否成功。</summary>
        protected virtual bool PlayAnimOnRenderer(Component renderer, AnimData animData, int trackIndex) => false;

        /// <summary>
        /// 停止指定渲染器上某条轨道的动画。
        /// <paramref name="resumeAnimData"/> 非空时表示该轨道的播放栈里还压着上一条动画，应恢复播放它（循环）；
        /// 为空则直接停止该轨道。
        /// </summary>
        protected virtual void StopAnimOnRenderer(Component renderer, int trackIndex, AnimData resumeAnimData) { }

        /// <summary>清除指定渲染器上的全部动画并复位到初始姿势。</summary>
        protected virtual void ClearRendererAnim(Component renderer) { }

        /// <summary>取某条动画的时长（秒，不含速度倍率）。取不到返回 0。</summary>
        protected virtual float GetAnimDuration(Component renderer, string animName) => 0f;

        /// <summary>读取渲染器的整体透明度（0~1）。</summary>
        protected virtual float GetRendererAlpha(Component renderer) => 0f;

        /// <summary>写入渲染器的整体透明度（0~1）。</summary>
        protected virtual void SetRendererAlpha(Component renderer, float alpha) { }

        /// <summary>初始化后端的皮肤表（缓存皮肤名 → 后端皮肤对象）。首次用到皮肤时调用一次。</summary>
        protected virtual void InitSkinBackend() { }

        /// <summary>后端是否存在该名称的皮肤。</summary>
        protected virtual bool HasSkin(string skinName) => false;

        /// <summary>把「基础皮肤 + 应用中皮肤」的并集应用到渲染器上。</summary>
        protected virtual void ApplySkins(IReadOnlyList<string> baseSkinNames, IReadOnlyList<string> applySkinNames) { }

        /// <summary>读取指定轨道上动画的播放进度（0~1）。轨道为空或无效时返回 0。</summary>
        protected virtual float GetAnimProgressOnRenderer(Component renderer, int trackIndex) => 0f;

        /// <summary>写入指定轨道上动画的播放进度（0~1）。轨道为空或无效时返回 false。</summary>
        protected virtual bool SetAnimProgressOnRenderer(Component renderer, int trackIndex, float progress) => false;

        #endregion

        #region 查找

        /// <summary>
        /// 为某个组件查找它该用的动画播放器：<b>从自身开始逐级上溯，每一级都搜索该级的整棵子树</b>，
        /// 取第一个找到的。
        ///
        /// <para>这样依次覆盖：自身 → 自身子树 → 父级 → 父级子树（即<b>兄弟分支</b>）→ 更上层……
        /// 兄弟分支那一段是必需的：动作播放器与动画组件通常互为兄弟、同挂在角色根节点下，
        /// 只走「父级链」是找不到的。</para>
        ///
        /// <para>包内原先有三处各自为政的实现（<see cref="AnimActor"/>、<see cref="AnimActionPlayer"/>、
        /// 编辑器的皮肤名 Drawer），<b>搜索顺序互不相同</b>——同一个层级结构在不同入口会解析出不同的结果。
        /// 本方法取三者的并集。（两个后端的 <c>Reset</c> 找的是各自的渲染器组件，不是本类型，不在此列。）</para>
        ///
        /// <para>包含未激活对象：预制体编辑与初始隐藏的角色都属常态。</para>
        /// </summary>
        /// <param name="component">发起查找的组件。</param>
        /// <returns>找到的播放器；找不到返回 <c>null</c>。</returns>
        public static AnimatorBase FindFor(Component component)
        {
            if (!component) return null;

            for (var node = component.transform; node; node = node.parent)
            {
                // GetComponentInChildren 含节点自身
                var found = node.GetComponentInChildren<AnimatorBase>(true);
                if (found) return found;
            }

            return null;
        }

        #endregion

        #region 生命周期

        /// <summary>默认渲染器。<c>Awake</c> 时由 <see cref="ResolveDefaultRenderer"/> 解析。</summary>
        protected Component DefaultRenderer { get; private set; }

        protected virtual void Awake()
        {
            DefaultRenderer = ResolveDefaultRenderer();
            InitAnimState();
        }

        protected virtual void Start()
        {
            // 初始化 皮肤
            InitSkin();

            // 设置初始状态
            SwitchAnimStateArray(stateInitList);
        }

        #endregion

        #region 动画状态 管理与播放

        // 当前状态 列表
        private readonly List<string> _listStateCurrent = new List<string>();

        // 状态名称:状态数据 字典。通过 名称 快速查找 对应数据
        private readonly Dictionary<string, FAnimStateData> _dicStateData =
            new Dictionary<string, FAnimStateData>();

        // 轨道ID:正在循环播放的动画名（用于优化，避免重复设置同一循环动画）
        private readonly Dictionary<int, string> _dicTrackToAnimNameLooping =
            new Dictionary<int, string>();

        // 渲染器 计数器。状态使用指定渲染器时，根据计数 淡入/淡出 该渲染器
        private readonly Dictionary<Component, int> _dicRendererUsageCount =
            new Dictionary<Component, int>();

        /// <summary>初始化 动画状态：隐藏渲染器、建立状态表。</summary>
        private void InitAnimState()
        {
            // 初始化时 设置默认渲染器 隐藏或显示
            if (DefaultRenderer && isDisplayOnInit == false)
                HideRendererImmediate(DefaultRenderer);

            // 初始化状态数据字典
            _dicStateData.Clear();
            foreach (var data in EnumerateStateDatas())
            {
                // 检查是否已存在相同状态的数据
                if (_dicStateData.ContainsKey(data.stateName))
                {
                    // 重复的状态数据，添加失败
                    AnimSimLog.Warn(this, $"重复的状态数据 State={data.stateName} GameObject={gameObject.name}");
                    continue;
                }

                // 状态自带的专用渲染器 初始隐藏
                if (data.renderer)
                    HideRendererImmediate(data.renderer);

                _dicStateData.Add(data.stateName, data);
            }
        }

        // 立刻隐藏渲染器（不走补间）
        private void HideRendererImmediate(Component renderer)
        {
            if (!renderer) return;
            SetRendererAlpha(renderer, 0f);
            if (CanDeactivateRenderer(renderer)) renderer.gameObject.SetActive(false);
        }

        /// <summary>
        /// 该渲染器所在物体能否被禁用。
        ///
        /// <para><b>渲染器与本组件同体（或本组件在其子树内）时不能禁用它</b>——那会把本组件一起停掉，
        /// 状态机、补间回调、协程就再也跑不起来了。两个后端的常见挂法正好一个踩一个不踩：
        /// Live2D 的 <c>CubismRenderController</c> 通常就在模型根上，与 <c>Live2dAnimator</c> 同体；
        /// Spine 的 <c>SkeletonAnimation</c> 一般在 <c>SpineAnimator</c> 的子物体上。
        /// 这种情况下只把不透明度归零，物体保持激活。</para>
        /// </summary>
        private bool CanDeactivateRenderer(Component renderer)
            => renderer && !transform.IsChildOf(renderer.transform);

        // 取状态实际使用的渲染器：状态指定了就用它，否则用默认渲染器
        private Component ResolveStateRenderer(FAnimStateData stateData)
            => stateData.renderer ? stateData.renderer : DefaultRenderer;

        /// <summary>添加 动画状态。</summary>
        public void AddAnimState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;

            // 如果状态已存在，则不处理
            if (_listStateCurrent.Contains(state)) return;

            // 顺手清掉已销毁渲染器留下的条目
            PruneDestroyedRenderers();

            // 记录新状态
            _listStateCurrent.Add(state);

            // 获取状态数据
            if (_dicStateData.TryGetValue(state, out var stateData) == false) return;

            var renderer = ResolveStateRenderer(stateData);
            if (!renderer)
            {
                AnimSimLog.Warn(this, $"渲染器为空，无法添加状态 {state} 的动画，GameObject={gameObject.name}");
                return;
            }

            // 计数器增加
            int count = 1;
            if (_dicRendererUsageCount.ContainsKey(renderer) == false)
                _dicRendererUsageCount[renderer] = count;
            else
                count = ++_dicRendererUsageCount[renderer];

            // 第一次使用该渲染器，淡入显示
            if (count == 1)
                FadeAnimator(true, renderer);

            if (stateData.animDatas == null) return;
            // 播放 动画数据
            PlayAnimDatas(renderer, stateData.animDatas);
        }

        /// <summary>播放 状态数据中配置的一组动画。</summary>
        private void PlayAnimDatas(Component renderer, AnimData[] animDatas)
        {
            foreach (var animData in animDatas)
            {
                if (animData == null) continue;

                // 计算轨道索引: 主轨道 + 子轨道
                int trackIndex = animData.AnimTrack;
                string animName = animData.ResolveAnimName();

                // 循环动画去重：同一轨道上已在循环播放同名动画时跳过，避免把它重头拉起造成跳帧
                if (animData.isLoop)
                {
                    if (_dicTrackToAnimNameLooping.TryGetValue(trackIndex, out var existingName)
                        && existingName == animName)
                        continue;
                    _dicTrackToAnimNameLooping[trackIndex] = animName;
                }
                else if (_dicTrackToAnimNameLooping.ContainsKey(trackIndex))
                {
                    // 非循环动画，记录被移除
                    _dicTrackToAnimNameLooping.Remove(trackIndex);
                }

                // 需要「每次循环后随机等待一段间隔」时走调度器，否则直接播放
                bool hasLoopInterval =
                    animData.isLoop &&
                    animData.loopIntervalTimeRange != Vector2.zero &&
                    animData.loopIntervalTimeRange.y >= animData.loopIntervalTimeRange.x;
                if (hasLoopInterval)
                    StartLoopIntervalSchedule(renderer, animData);
                else
                    PlayAnim(renderer, animData);
            }
        }

        /// <summary>移除 动画状态。</summary>
        public void RemoveAnimState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;

            // 如果状态不存在，则不处理
            if (!_listStateCurrent.Contains(state)) return;

            // 顺手清掉已销毁渲染器留下的条目
            PruneDestroyedRenderers();

            // 移除状态
            _listStateCurrent.Remove(state);

            // 获取状态数据
            if (_dicStateData.TryGetValue(state, out var stateData) == false) return;

            var renderer = ResolveStateRenderer(stateData);
            if (!renderer)
            {
                AnimSimLog.Warn(this, $"渲染器为空，无法移除状态 {state} 的动画，GameObject={gameObject.name}");
                return;
            }

            // 计数器减少
            if (_dicRendererUsageCount.TryGetValue(renderer, out var count))
            {
                count--;
                if (count == 0)
                    // 不再使用该渲染器，淡出隐藏
                    FadeAnimator(false, renderer);
                // 更新计数器
                count = count < 0 ? 0 : count; // 避免负数
                _dicRendererUsageCount[renderer] = count;
            }

            if (stateData.animDatas == null) return;
            // 移除 状态数据中配置的 动画
            foreach (var animData in stateData.animDatas)
            {
                if (animData == null) continue;
                int trackIndex = animData.AnimTrack;

                // 如果是循环动画，移除去重记录
                if (animData.isLoop)
                    _dicTrackToAnimNameLooping.Remove(trackIndex);

                // 停止 循环随机间隔调度
                StopLoopIntervalSchedule(renderer, trackIndex);
                // 清除该轨道动画
                StopAnimOnRenderer(renderer, trackIndex, null);
                // 清空该轨道的播放栈与令牌
                _trackToAnimDataListPlayingMap.Remove(trackIndex);
                _trackToPlayToken.Remove(trackIndex);
            }
        }

        /// <summary>切换状态列表：整体切换到新的一组状态（差集移除、新增添加）。</summary>
        public void SwitchAnimStateArray(string[] states)
        {
            if (states == null) return;

            // 与现有的 状态列表 进行对比，移除不存在的状态，添加新的状态
            var statesToRemove = new List<string>();
            foreach (var existingState in _listStateCurrent)
            {
                if (Array.IndexOf(states, existingState) < 0)
                    statesToRemove.Add(existingState);
            }
            // 先移除 不存在的状态
            foreach (var stateToRemove in statesToRemove)
                RemoveAnimState(stateToRemove);
            // 再添加 新的状态
            foreach (var newState in states)
                AddAnimState(newState);
        }

        #endregion

        #region 循环动画 随机间隔

        // 渲染器:(轨道:调度句柄)。正在运行的 循环随机间隔调度
        private readonly Dictionary<Component, Dictionary<int, ToolkitTweenHandle>> _dicRendererTrackToLoopHandle =
            new Dictionary<Component, Dictionary<int, ToolkitTweenHandle>>();

        /// <summary>停止某渲染器上全部的 循环随机间隔调度。</summary>
        private void StopLoopIntervalScheduleAll(Component renderer)
        {
            if (!renderer) return;
            if (_dicRendererTrackToLoopHandle.Count == 0) return;

            if (_dicRendererTrackToLoopHandle.TryGetValue(renderer, out var dicTrackToHandle))
            {
                foreach (var kv in dicTrackToHandle)
                    kv.Value.Kill();
                dicTrackToHandle.Clear();
            }
        }

        /// <summary>停止 指定轨道的 循环随机间隔调度。</summary>
        private void StopLoopIntervalSchedule(Component renderer, int trackIndex)
        {
            if (!renderer) return;
            if (_dicRendererTrackToLoopHandle.TryGetValue(renderer, out var dicTrackToHandle))
            {
                if (dicTrackToHandle.TryGetValue(trackIndex, out var handle))
                {
                    handle.Kill();
                    dicTrackToHandle.Remove(trackIndex);
                }
            }
        }

        /// <summary>
        /// 启动指定轨道的 循环随机间隔调度：播放一次动画，等待「动画时长 + 随机间隔」后再播下一次，如此递归。
        /// </summary>
        private void StartLoopIntervalSchedule(Component renderer, AnimData animData)
        {
            if (!renderer || animData == null) return;

            // 停止已有（判空之后再做，否则 animData 为 null 时这一行就先抛了）
            StopLoopIntervalSchedule(renderer, animData.AnimTrack);

            string animName = animData.ResolveAnimName();
            if (string.IsNullOrEmpty(animName)) return;

            float duration = GetAnimDuration(renderer, animName);
            if (duration <= 0f) return;

            // 本调度要求「每次都按单次播放」，播完才等间隔。这个「单次」必须传到下游：后端据
            // isLoop 决定是否循环，PlayAnimImmediate 也据它决定要不要排「单次播放完成」的计时
            // ——而正是那次计时把动画从轨道播放栈里摘掉，下一轮才有空轨道可播。
            //
            // 所以取一份 isLoop=false 的副本，而不是把 animData.isLoop 就地改掉：animData 通常是
            // 本组件序列化数组里的元素，改了就永久生效，下次 AddAnimState 时 PlayAnimDatas 里的
            // hasLoopInterval 判不出来，这条「循环 + 随机间隔」就退化成只播一次。
            var animDataOnce = animData.CloneAsOnce();

            // 预计算动画时长（考虑速度倍率）
            float animDuration = duration / Mathf.Max(0.001f, Mathf.Abs(animDataOnce.speed));
            int trackIndex = animDataOnce.AnimTrack;

            // 获取或创建轨道字典
            if (!_dicRendererTrackToLoopHandle.TryGetValue(renderer, out var dicTrackToHandle))
            {
                dicTrackToHandle = new Dictionary<int, ToolkitTweenHandle>();
                _dicRendererTrackToLoopHandle.Add(renderer, dicTrackToHandle);
            }

            // 递归调度：播放一次动画，等待 动画时长+随机间隔 后，调度下一次
            void ScheduleNextLoop()
            {
                if (!renderer || !renderer.gameObject.activeInHierarchy) return;

                // 仅当该轨道空闲时才播放，避免与其它来源的动画抢轨道
                PlayAnimWhenTrackEmpty(renderer, animDataOnce);

                // 计算随机间隔，等待 动画时长 + 间隔时间 后，调度下一次循环
                float intervalDelay = UnityEngine.Random.Range(animDataOnce.loopIntervalTimeRange.x, animDataOnce.loopIntervalTimeRange.y);
                intervalDelay = Mathf.Max(0.001f, intervalDelay); // 最小等待时间保护
                // owner 传 this：本组件被销毁后调度自动中止，不会再对已失效的渲染器下发播放。
                dicTrackToHandle[trackIndex] =
                    ToolkitTween.DelayedCall(animDuration + intervalDelay, ScheduleNextLoop, owner: this);
            }

            // 处理初始延迟
            if (animDataOnce.startDelayTime > 0f)
                dicTrackToHandle[trackIndex] =
                    ToolkitTween.DelayedCall(animDataOnce.startDelayTime, ScheduleNextLoop, owner: this);
            else
                ScheduleNextLoop();
        }

        #endregion

        #region 动画 播放与停止

        /// <summary>
        /// 轨道ID:动画数据列表。记录 各个轨道上 正在播放的动画数据。
        /// 相同轨道上 被新的动画 覆盖时，将新动画 压栈并播放；
        /// 新动画停止时从栈中弹出，并恢复播放栈里的上一条，实现动画的 切换与恢复。
        /// </summary>
        private readonly Dictionary<int, List<AnimData>> _trackToAnimDataListPlayingMap =
            new Dictionary<int, List<AnimData>>();

        // 轨道ID:该轨道当前所用的渲染器。停止时据此找回是在哪个渲染器上播的。
        private readonly Dictionary<int, Component> _trackToRenderer = new Dictionary<int, Component>();

        // 轨道ID:播放令牌。每次成功播放自增，供外部判定「我发起的那次播放是否仍在进行」。
        private readonly Dictionary<int, int> _trackToPlayToken = new Dictionary<int, int>();
        private int _playTokenNext;

        /// <summary>
        /// 取某条轨道当前的播放令牌。0 表示该轨道上没有由本类发起的播放。
        ///
        /// <para>调用方在发起播放后记下令牌，之后比对令牌即可判断「轨道是否已被别的播放顶替」——
        /// 这比持有后端的播放句柄做引用比较可靠：后端句柄常有对象池复用，回收再分配后引用比较会假阳性。</para>
        /// </summary>
        public int GetAnimPlayToken(int trackIndex)
            => _trackToPlayToken.TryGetValue(trackIndex, out var token) ? token : 0;

        /// <summary>取某条轨道当前所用的渲染器，未记录时回退到默认渲染器。</summary>
        protected Component ResolveTrackRenderer(int trackIndex)
        {
            if (_trackToRenderer.TryGetValue(trackIndex, out var renderer) && renderer) return renderer;
            return DefaultRenderer;
        }

        /// <summary>读取指定轨道上动画的播放进度（0~1）。</summary>
        public float GetAnimProgress(int trackIndex)
            => GetAnimProgressOnRenderer(ResolveTrackRenderer(trackIndex), trackIndex);

        /// <summary>写入指定轨道上动画的播放进度（0~1）。轨道为空或无效时返回 false。</summary>
        public bool SetAnimProgress(int trackIndex, float progress)
            => SetAnimProgressOnRenderer(ResolveTrackRenderer(trackIndex), trackIndex, progress);

        /// <summary>
        /// 播放动画（默认渲染器）。
        /// </summary>
        /// <param name="animData">动画数据</param>
        /// <param name="onOncePlayComplete">单次播放 完成的回调。（循环播放时 不会被调用）</param>
        public void PlayAnim(AnimData animData, Action<AnimData> onOncePlayComplete = null)
            => PlayAnim(DefaultRenderer, animData, onOncePlayComplete);

        /// <summary>
        /// 播放动画（指定渲染器）。<paramref name="animData"/> 配置了起播延时的会先挂起。
        /// </summary>
        public void PlayAnim(Component renderer, AnimData animData, Action<AnimData> onOncePlayComplete = null)
        {
            if (animData == null) return;
            if (!renderer) renderer = DefaultRenderer;
            if (!renderer)
            {
                AnimSimLog.Warn(this, $"渲染器为空，播放动画失败，GameObject={gameObject.name}");
                return;
            }

            if (animData.startDelayTime > 0f)
            {
                if (_animStartDelayHandleMap.ContainsKey(animData)) return;
                // owner 传 this：本组件被销毁后延时自动作废，不会再对已失效的渲染器下发播放。
                var delayHandle = ToolkitTween.DelayedCall(animData.startDelayTime, () =>
                {
                    _animStartDelayHandleMap.Remove(animData);
                    PlayAnimImmediate(renderer, animData, onOncePlayComplete);
                }, owner: this);
                // 仅登记真正在途的延时。句柄无效时不登记，
                // 否则这条记录会永久卡住该动画数据——上面的 ContainsKey 会让后续播放一律提前返回。
                if (delayHandle.IsActive) _animStartDelayHandleMap.Add(animData, delayHandle);
                return;
            }

            PlayAnimImmediate(renderer, animData, onOncePlayComplete);
        }

        /// <summary>立刻播放动画（不走起播延时）。</summary>
        private void PlayAnimImmediate(Component renderer, AnimData animData, Action<AnimData> onOncePlayComplete = null)
        {
            if (!renderer || animData == null) return;

            string animName = animData.ResolveAnimName();
            if (string.IsNullOrEmpty(animName))
            {
                AnimSimLog.Warn(this, $"动画名为空，播放失败，GameObject={gameObject.name}");
                return;
            }

            // 获取 当前轨道上 正在播放的动画列表
            int trackIndex = animData.AnimTrack;
            if (!_trackToAnimDataListPlayingMap.TryGetValue(trackIndex, out var animDataListPlaying))
            {
                animDataListPlaying = new List<AnimData>();
                _trackToAnimDataListPlayingMap.Add(trackIndex, animDataListPlaying);
            }
            // 检查，是否 已经在播放 该动画数据。
            // 引用地址作为唯一标识：外部调用接口时一般会 new 一份新数据，引用地址是唯一的。
            if (animDataListPlaying.Contains(animData)) return;

            // 交给后端播放
            if (!PlayAnimOnRenderer(renderer, animData, trackIndex)) return;

            // 记录 新动画 与 轨道归属、并推进播放令牌
            animDataListPlaying.Add(animData);
            _trackToRenderer[trackIndex] = renderer;
            _trackToPlayToken[trackIndex] = ++_playTokenNext;

            // 非循环播放时，注册 动画完成的回调
            if (animData.isLoop == false)
                ScheduleOnceComplete(renderer, animData, onOncePlayComplete);
        }

        /// <summary>播放动画，仅当 指定轨道上 没有正在播放的动画。</summary>
        private void PlayAnimWhenTrackEmpty(Component renderer, AnimData animData)
        {
            if (!renderer || animData == null) return;

            int count = 0;
            if (_trackToAnimDataListPlayingMap.TryGetValue(animData.AnimTrack, out var listPlaying))
                count = listPlaying.Count;

            // 仅当轨道上 没有正在播放的动画时，才播放新动画
            if (count == 0)
                PlayAnimImmediate(renderer, animData);
        }

        /// <summary>停止动画。</summary>
        public void StopAnim(AnimData animData)
        {
            if (animData == null) return;

            // 如果正在 等待起播延时，直接取消延时即可，无需触碰轨道
            if (CancelAnimStartDelay(animData)) return;

            // 取消可能仍在计时的 单次播放完成回调
            CancelOnceComplete(animData);

            // 获取 当前轨道上 正在播放的动画列表
            int trackIndex = animData.AnimTrack;
            if (!_trackToAnimDataListPlayingMap.TryGetValue(trackIndex, out var animDataListPlaying)) return;

            // 从列表中 移除 指定动画。从最后面开始查找。
            int index = animDataListPlaying.LastIndexOf(animData);
            if (index < 0) return;

            animDataListPlaying.RemoveAt(index);

            var renderer = ResolveTrackRenderer(trackIndex);
            if (!renderer) return;

            if (index == animDataListPlaying.Count && index > 0)
            {
                // 被移除的是栈顶，且栈里还压着别的动画：恢复播放上一条
                StopAnimOnRenderer(renderer, trackIndex, animDataListPlaying[index - 1]);
                _trackToPlayToken[trackIndex] = ++_playTokenNext;
            }
            else
            {
                // 否则，直接停止该轨道动画
                StopAnimOnRenderer(renderer, trackIndex, null);
                _trackToPlayToken.Remove(trackIndex);
            }
        }

        #endregion

        #region 起播延时 与 单次播放完成

        // 动画数据:起播延时句柄。记录正在等待起播的动画，以便在需要时取消。
        private readonly Dictionary<AnimData, ToolkitTweenHandle> _animStartDelayHandleMap =
            new Dictionary<AnimData, ToolkitTweenHandle>();

        // 动画数据:单次播放完成计时句柄。持有它才能在动画被提前停止时取消这次计时。
        private readonly Dictionary<AnimData, ToolkitTweenHandle> _animOnceCompleteHandleMap =
            new Dictionary<AnimData, ToolkitTweenHandle>();

        /// <summary>清除所有 等待起播的动画。用于 切换状态时 清空上一状态的挂起项。</summary>
        private void ClearAllAnimStartDelay()
        {
            foreach (var kv in _animStartDelayHandleMap)
                kv.Value.Kill();
            _animStartDelayHandleMap.Clear();
        }

        /// <summary>取消某条动画的起播延时。</summary>
        /// <returns>是否确实取消了一条在途的延时。</returns>
        private bool CancelAnimStartDelay(AnimData animData)
        {
            if (animData == null) return false;

            if (_animStartDelayHandleMap.TryGetValue(animData, out var handle))
            {
                handle.Kill();
                _animStartDelayHandleMap.Remove(animData);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 排期「单次播放完成」：等待 时长/速度 后停止动画并触发回调。
        /// 句柄被登记下来，动画若被提前停止即可取消这次计时。
        /// </summary>
        private void ScheduleOnceComplete(Component renderer, AnimData animData, Action<AnimData> onOncePlayComplete)
        {
            // 计算动画时长（考虑速度倍率）
            float speed = Mathf.Abs(animData.speed);
            if (speed == 0f) return; // 速度为0, 动画暂停，无法完成

            float duration = GetAnimDuration(renderer, animData.ResolveAnimName());
            if (duration <= 0f) return;

            // 同一条数据不重复排期
            CancelOnceComplete(animData);

            // owner 传 this，组件销毁后不再回调
            var handle = ToolkitTween.DelayedCall(duration / speed, () =>
            {
                _animOnceCompleteHandleMap.Remove(animData);
                // 停止播放。从 正在播放的动画列表中移除。
                StopAnim(animData);
                // 触发回调
                onOncePlayComplete?.Invoke(animData);
            }, owner: this);

            if (handle.IsActive) _animOnceCompleteHandleMap[animData] = handle;
        }

        /// <summary>取消某条动画的「单次播放完成」计时。</summary>
        private void CancelOnceComplete(AnimData animData)
        {
            if (animData == null) return;
            if (_animOnceCompleteHandleMap.TryGetValue(animData, out var handle))
            {
                handle.Kill();
                _animOnceCompleteHandleMap.Remove(animData);
            }
        }

        /// <summary>清除所有 单次播放完成 计时。</summary>
        private void ClearAllOnceComplete()
        {
            foreach (var kv in _animOnceCompleteHandleMap)
                kv.Value.Kill();
            _animOnceCompleteHandleMap.Clear();
        }

        #endregion

        #region 淡入淡出

        // 渲染器:淡入淡出补间句柄
        private readonly Dictionary<Component, ToolkitTweenHandle> _dicRendererFadeHandle =
            new Dictionary<Component, ToolkitTweenHandle>();

        /// <summary>
        /// 淡入/淡出渲染器（补间其整体透明度）。
        /// </summary>
        /// <param name="isFadeIn">是否为淡入</param>
        /// <param name="renderer">目标渲染器，为空时使用默认渲染器</param>
        /// <param name="clearAnimOnFadeOut">淡出完成后是否清除动画数据。
        /// true（默认）= 正常销毁流程，清除动画轨道和数据；
        /// false = 临时隐藏，仅禁用对象，保留动画数据以便之后恢复。</param>
        public void FadeAnimator(bool isFadeIn, Component renderer = null, bool clearAnimOnFadeOut = true)
        {
            if (!renderer) renderer = DefaultRenderer;
            if (!renderer) return;

            // 立刻完成 当前的淡入/淡出：其完成回调会同步执行并把自己从表中移除，
            // 因此下面登记新句柄时表里必定不残留旧记录。必须在 SetActive(true) 之前——
            // 旧的淡出回调里带着 SetActive(false)，顺序反了会把刚激活的对象又关掉。
            if (_dicRendererFadeHandle.TryGetValue(renderer, out var handleFadeCur))
                handleFadeCur.Complete();

            // 如果是淡入，确保对象是激活状态
            if (isFadeIn)
                renderer.gameObject.SetActive(true);

            float fadeDur = Mathf.Max(0.001f, animFadeDuration);
            float targetAlpha = isFadeIn ? 1f : 0f;
            float startAlpha = GetRendererAlpha(renderer);

            // fadeDur 恒 > 0，故 To() 必定异步推进、完成回调不会在本方法返回前触发——
            // 下面那行「登记新句柄」才不会被回调里的 Remove 抢先。
            // owner 传 this：本组件被销毁后补间自动作废，不再向已失效的渲染器写透明度。
            var handleFadeNew = ToolkitTween.To(startAlpha, targetAlpha, fadeDur,
                x => SetRendererAlpha(renderer, x),
                EToolkitEase.Linear,
                onComplete: () =>
                {
                    // 目标已被销毁，直接移除记录并返回
                    if (!renderer)
                    {
                        _dicRendererFadeHandle.Remove(renderer);
                        return;
                    }
                    // 立刻设置为目标透明度
                    SetRendererAlpha(renderer, targetAlpha);
                    // 如果是淡出，禁用对象
                    if (isFadeIn == false)
                    {
                        if (clearAnimOnFadeOut)
                            ClearRenderer(renderer);
                        // 禁用对象（临时隐藏 和 正常销毁 都需要）。
                        // 与本组件同体的渲染器不能禁用，否则会把本组件一起停掉——见 CanDeactivateRenderer。
                        if (renderer && CanDeactivateRenderer(renderer)) renderer.gameObject.SetActive(false);
                    }

                    // 移除记录
                    _dicRendererFadeHandle.Remove(renderer);
                },
                owner: this);

            _dicRendererFadeHandle[renderer] = handleFadeNew;
        }

        /// <summary>
        /// 结清全部在途的淡入淡出。
        /// </summary>
        /// <param name="complete">
        /// <c>true</c> = 立刻推到终点（与 <see cref="FadeAnimator"/> 内部处理旧句柄的做法一致）：
        /// 透明度落到目标值，淡出的收尾还会顺带隐藏物体，不会把渲染器停在半透明的中途状态。
        /// <c>false</c> = 直接掐断，不跑完成回调；仅用于组件销毁——那时再去 <c>SetActive</c> 已无意义，
        /// 且在销毁流程里触发回调本身就有风险。
        /// </param>
        private void ClearAllRendererFade(bool complete)
        {
            if (_dicRendererFadeHandle.Count == 0) return;

            // 先快照：Complete 会同步跑完成回调，而回调里带着 _dicRendererFadeHandle.Remove，
            // 直接在字典上 foreach 会撞 InvalidOperationException。
            _fadeHandleScratch.Clear();
            foreach (var kv in _dicRendererFadeHandle)
                _fadeHandleScratch.Add(kv.Value);

            foreach (var handle in _fadeHandleScratch)
            {
                if (complete) handle.Complete();
                else handle.Kill();
            }

            _fadeHandleScratch.Clear();
            _dicRendererFadeHandle.Clear();
        }

        // 上面那趟结清用的暂存表，提为字段免得每次都新分配。
        private readonly List<ToolkitTweenHandle> _fadeHandleScratch = new List<ToolkitTweenHandle>();

        #endregion

        #region 清除与销毁

        /// <summary>
        /// 销毁动画：淡出隐藏，并返回需要等待的延迟时间（秒）。
        /// </summary>
        public bool DestroyAnim(out float delay)
        {
            delay = -1f;
            if (!DefaultRenderer) return false;

            // 淡出隐藏
            FadeAnimator(false, DefaultRenderer);
            // 返回延迟时间
            delay = animFadeDuration;

            return true;
        }

        /// <summary>清除某个渲染器上的全部动画（含循环间隔调度）。</summary>
        private void ClearRenderer(Component renderer)
        {
            if (renderer) ClearRendererAnim(renderer);
            // 清除所有正在运行的 循环随机间隔调度
            StopLoopIntervalScheduleAll(renderer);
        }

        /// <summary>清除所有动画与状态。</summary>
        public void ClearAllAnim()
        {
            // 移除所有状态
            _listStateCurrent.Clear();

            // 清除所有 挂起中的 起播延时 与 单次完成计时
            ClearAllAnimStartDelay();
            ClearAllOnceComplete();
            // 在途的淡入淡出也要结清：它会持续向渲染器写透明度，其完成回调还会再清一次动画。
            // 推到终点而非掐断，避免把渲染器停在半透明的中途状态。
            ClearAllRendererFade(complete: true);

            // 默认渲染器
            ClearRenderer(DefaultRenderer);

            // 其他渲染器
            foreach (var kv in _dicRendererUsageCount)
                ClearRenderer(kv.Key);
            _dicRendererUsageCount.Clear();
            // ClearRenderer 只清了各渲染器名下的调度，外层这张表本身还留着一堆空字典
            _dicRendererTrackToLoopHandle.Clear();

            // 清除 轨道记录
            _dicTrackToAnimNameLooping.Clear();
            _trackToAnimDataListPlayingMap.Clear();
            _trackToRenderer.Clear();
            _trackToPlayToken.Clear();
        }

        /// <summary>
        /// 剔除以「已销毁的渲染器」为键的字典条目。
        ///
        /// <para>这些表都以 <see cref="Component"/> 的引用身份作键，而 Unity 对象被销毁后其托管引用仍然存在——
        /// 条目不会自行消失，只会一直占着表并被后续的全表遍历（如 <see cref="ClearAllAnim"/>）扫到。
        /// 在状态增删这两个低频入口顺手扫一遍即可，表的量级只有个位数。</para>
        /// </summary>
        private void PruneDestroyedRenderers()
        {
            if (_dicRendererUsageCount.Count == 0
                && _dicRendererTrackToLoopHandle.Count == 0
                && _dicRendererFadeHandle.Count == 0
                && _trackToRenderer.Count == 0) return;

            _prunedRendererScratch.Clear();
            foreach (var kv in _dicRendererUsageCount)
                if (!kv.Key) _prunedRendererScratch.Add(kv.Key);
            foreach (var dead in _prunedRendererScratch)
                _dicRendererUsageCount.Remove(dead);

            _prunedRendererScratch.Clear();
            foreach (var kv in _dicRendererTrackToLoopHandle)
                if (!kv.Key) _prunedRendererScratch.Add(kv.Key);
            foreach (var dead in _prunedRendererScratch)
            {
                foreach (var kv in _dicRendererTrackToLoopHandle[dead]) kv.Value.Kill();
                _dicRendererTrackToLoopHandle.Remove(dead);
            }

            _prunedRendererScratch.Clear();
            foreach (var kv in _dicRendererFadeHandle)
                if (!kv.Key) _prunedRendererScratch.Add(kv.Key);
            foreach (var dead in _prunedRendererScratch)
            {
                _dicRendererFadeHandle[dead].Kill();
                _dicRendererFadeHandle.Remove(dead);
            }

            _prunedTrackScratch.Clear();
            foreach (var kv in _trackToRenderer)
                if (!kv.Value) _prunedTrackScratch.Add(kv.Key);
            foreach (var dead in _prunedTrackScratch)
                _trackToRenderer.Remove(dead);

            _prunedRendererScratch.Clear();
            _prunedTrackScratch.Clear();
        }

        // 上面那趟清扫用的暂存表。提为字段，免得每次状态增删都新分配两个 List。
        private readonly List<Component> _prunedRendererScratch = new List<Component>();
        private readonly List<int> _prunedTrackScratch = new List<int>();

        /// <summary>组件销毁：断掉全部在途的补间 / 延时 / 调度，并清空所有登记表。</summary>
        protected virtual void OnDestroy()
        {
            ClearAllAnimStartDelay();
            ClearAllOnceComplete();
            ClearAllRendererFade(complete: false);

            foreach (var kv in _dicRendererTrackToLoopHandle)
                foreach (var kvTrack in kv.Value)
                    kvTrack.Value.Kill();
            _dicRendererTrackToLoopHandle.Clear();

            _dicRendererUsageCount.Clear();
            _dicTrackToAnimNameLooping.Clear();
            _trackToAnimDataListPlayingMap.Clear();
            _trackToRenderer.Clear();
            _trackToPlayToken.Clear();
        }

        #endregion

        #region 皮肤管理

        // 是否已初始化皮肤
        private bool _isSkinInit;
        // 应用中的 皮肤名称列表
        private readonly List<string> _applySkinNames = new List<string>();

        /// <summary>初始化皮肤（幂等）。</summary>
        protected void InitSkin()
        {
            if (_isSkinInit) return;
            _isSkinInit = true;

            // 后端缓存皮肤表
            InitSkinBackend();

            // 设置 基础皮肤
            SetBaseSkin(baseSkins);
        }

        /// <summary>
        /// 设置 基础皮肤组。
        /// </summary>
        /// <param name="baseSkinNames">基础皮肤组：始终存在的 皮肤名称列表。</param>
        /// <param name="isRefresh">是否 刷新皮肤 显示。默认为 true。</param>
        public void SetBaseSkin(string[] baseSkinNames, bool isRefresh = true)
        {
            InitSkin();

            baseSkins = baseSkinNames ?? Array.Empty<string>();

            // 校验：不存在的皮肤名给出明确告警（否则只是静默不显示，极难排查）
            foreach (var skinName in baseSkins)
            {
                if (!HasSkin(skinName))
                    AnimSimLog.Warn(this, $"基础皮肤名称 {skinName} 不存在，GameObject={gameObject.name}");
            }

            if (isRefresh) RefreshSkin();
        }

        /// <summary>
        /// 添加皮肤。
        /// </summary>
        /// <param name="skinName">皮肤名称：有文件夹路径时，一般使用 '/' 作为分隔符。</param>
        /// <param name="isRefresh">立即刷新：是否立即刷新皮肤显示。默认为 true。</param>
        public void AddSkin(string skinName, bool isRefresh = true)
        {
            InitSkin();

            if (!HasSkin(skinName))
            {
                AnimSimLog.Warn(this, $"皮肤名称 {skinName} 不存在，无法添加皮肤，GameObject={gameObject.name}");
                return;
            }

            // 如果皮肤已存在，则不重复添加
            if (_applySkinNames.Contains(skinName)) return;

            _applySkinNames.Add(skinName);
            if (isRefresh) RefreshSkin();
        }

        /// <summary>
        /// 移除皮肤。
        /// </summary>
        /// <param name="skinName">皮肤名称：有文件夹路径时，一般使用 '/' 作为分隔符。</param>
        /// <param name="isRefresh">立即刷新：是否立即刷新皮肤显示。默认为 true。</param>
        public void RemoveSkin(string skinName, bool isRefresh = true)
        {
            InitSkin();

            if (!HasSkin(skinName))
            {
                AnimSimLog.Warn(this, $"皮肤名称 {skinName} 不存在，无法移除皮肤，GameObject={gameObject.name}");
                return;
            }

            if (!_applySkinNames.Remove(skinName)) return;
            if (isRefresh) RefreshSkin();
        }

        /// <summary>刷新皮肤：把 基础皮肤 与 应用中皮肤 的并集应用到渲染器上。</summary>
        public void RefreshSkin() => ApplySkins(baseSkins, _applySkinNames);

#if UNITY_EDITOR
        /// <summary>
        /// 【编辑器专用】列出该后端当前可用的皮肤名，供 <see cref="AnimSkinNameAttribute"/> 的下拉取用。
        /// 取不到时返回 <c>null</c>，下拉会自动退化为文本框。
        ///
        /// <para>放在运行时类型上而非编辑器侧去反射私有字段，是为了让编辑器程序集
        /// <b>不必引用 spine-unity / Live2D.Cubism</b>——后端各自知道自己的皮肤从哪来，报上来即可。</para>
        /// </summary>
        public virtual IReadOnlyList<string> EditorCollectSkinNames() => null;
#endif

        #endregion
    }
}
