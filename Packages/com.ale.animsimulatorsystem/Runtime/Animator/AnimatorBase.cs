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
    /// 这些动作落到具体后端上——见本类的抽象成员。</para>
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

        [Header("皮肤设置")]
        [Tooltip("基础皮肤 列表：始终显示的 基础皮肤名称。")]
        public string[] baseSkins;

        #endregion

        #region 后端契约

        /// <summary>解析默认渲染器（状态未指定专用渲染器时使用）。<c>Awake</c> 时调用一次。</summary>
        protected abstract Component ResolveDefaultRenderer();

        /// <summary>
        /// 枚举授权配置里的状态数据，转换为后端中性的记录。基类负责去重与建表。
        /// </summary>
        protected abstract IEnumerable<FAnimStateData> EnumerateStateDatas();

        /// <summary>在指定渲染器的指定轨道上播放一条动画。返回是否成功。</summary>
        protected abstract bool PlayAnimOnRenderer(Component renderer, AnimData animData, int trackIndex);

        /// <summary>
        /// 停止指定渲染器上某条轨道的动画。
        /// <paramref name="resumeAnimData"/> 非空时表示该轨道的播放栈里还压着上一条动画，应恢复播放它（循环）；
        /// 为空则直接停止该轨道。
        /// </summary>
        protected abstract void StopAnimOnRenderer(Component renderer, int trackIndex, AnimData resumeAnimData);

        /// <summary>清除指定渲染器上的全部动画并复位到初始姿势。</summary>
        protected abstract void ClearRendererAnim(Component renderer);

        /// <summary>取某条动画的时长（秒，不含速度倍率）。取不到返回 0。</summary>
        protected abstract float GetAnimDuration(Component renderer, string animName);

        /// <summary>读取渲染器的整体透明度（0~1）。</summary>
        protected abstract float GetRendererAlpha(Component renderer);

        /// <summary>写入渲染器的整体透明度（0~1）。</summary>
        protected abstract void SetRendererAlpha(Component renderer, float alpha);

        /// <summary>初始化后端的皮肤表（缓存皮肤名 → 后端皮肤对象）。首次用到皮肤时调用一次。</summary>
        protected abstract void InitSkinBackend();

        /// <summary>后端是否存在该名称的皮肤。</summary>
        protected abstract bool HasSkin(string skinName);

        /// <summary>把「基础皮肤 + 应用中皮肤」的并集应用到渲染器上。</summary>
        protected abstract void ApplySkins(IReadOnlyList<string> baseSkinNames, IReadOnlyList<string> applySkinNames);

        /// <summary>读取指定轨道上动画的播放进度（0~1）。轨道为空或无效时返回 0。</summary>
        protected abstract float GetAnimProgressOnRenderer(Component renderer, int trackIndex);

        /// <summary>写入指定轨道上动画的播放进度（0~1）。轨道为空或无效时返回 false。</summary>
        protected abstract bool SetAnimProgressOnRenderer(Component renderer, int trackIndex, float progress);

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
                    Debug.LogWarning($"{GetType().Name} >> InitAnimState: 重复的状态数据 State={data.stateName} GameObject={gameObject.name}", this);
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
            // 记录新状态
            _listStateCurrent.Add(state);

            // 获取状态数据
            if (_dicStateData.TryGetValue(state, out var stateData) == false) return;

            var renderer = ResolveStateRenderer(stateData);
            if (!renderer)
            {
                Debug.LogWarning($"{GetType().Name} >> AddAnimState: 渲染器为空，无法添加状态 {state} 的动画，GameObject={gameObject.name}", this);
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
            // 移除状态
            _listStateCurrent.Remove(state);

            // 获取状态数据
            if (_dicStateData.TryGetValue(state, out var stateData) == false) return;

            var renderer = ResolveStateRenderer(stateData);
            if (!renderer)
            {
                Debug.LogWarning($"{GetType().Name} >> RemoveAnimState: 渲染器为空，无法移除状态 {state} 的动画，GameObject={gameObject.name}", this);
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
            // 停止已有
            StopLoopIntervalSchedule(renderer, animData.AnimTrack);

            if (!renderer || animData == null) return;
            string animName = animData.ResolveAnimName();
            if (string.IsNullOrEmpty(animName)) return;

            float duration = GetAnimDuration(renderer, animName);
            if (duration <= 0f) return;

            // 设置为 非循环播放，由 递归调度 控制 循环和间隔
            animData.isLoop = false;

            // 预计算动画时长（考虑速度倍率）
            float animDuration = duration / Mathf.Max(0.001f, Mathf.Abs(animData.speed));
            int trackIndex = animData.AnimTrack;

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
                PlayAnimWhenTrackEmpty(renderer, animData);

                // 计算随机间隔，等待 动画时长 + 间隔时间 后，调度下一次循环
                float intervalDelay = UnityEngine.Random.Range(animData.loopIntervalTimeRange.x, animData.loopIntervalTimeRange.y);
                intervalDelay = Mathf.Max(0.001f, intervalDelay); // 最小等待时间保护
                // owner 传 this：本组件被销毁后调度自动中止，不会再对已失效的渲染器下发播放。
                dicTrackToHandle[trackIndex] =
                    ToolkitTween.DelayedCall(animDuration + intervalDelay, ScheduleNextLoop, owner: this);
            }

            // 处理初始延迟
            if (animData.startDelayTime > 0f)
                dicTrackToHandle[trackIndex] =
                    ToolkitTween.DelayedCall(animData.startDelayTime, ScheduleNextLoop, owner: this);
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
                Debug.LogWarning($"{GetType().Name} >> PlayAnim: 渲染器为空，播放动画失败，GameObject={gameObject.name}", this);
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
                Debug.LogWarning($"{GetType().Name} >> PlayAnimImmediate: 动画名为空，播放失败，GameObject={gameObject.name}", this);
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

            // 默认渲染器
            ClearRenderer(DefaultRenderer);

            // 其他渲染器
            foreach (var kv in _dicRendererUsageCount)
                ClearRenderer(kv.Key);
            _dicRendererUsageCount.Clear();

            // 清除 轨道记录
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
                    Debug.LogWarning($"{GetType().Name} >> SetBaseSkin: 基础皮肤名称 {skinName} 不存在，GameObject={gameObject.name}", this);
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
                Debug.LogWarning($"{GetType().Name} >> AddSkin: 皮肤名称 {skinName} 不存在，无法添加皮肤，GameObject={gameObject.name}", this);
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
                Debug.LogWarning($"{GetType().Name} >> RemoveSkin: 皮肤名称 {skinName} 不存在，无法移除皮肤，GameObject={gameObject.name}", this);
                return;
            }

            if (!_applySkinNames.Remove(skinName)) return;
            if (isRefresh) RefreshSkin();
        }

        /// <summary>刷新皮肤：把 基础皮肤 与 应用中皮肤 的并集应用到渲染器上。</summary>
        public void RefreshSkin() => ApplySkins(baseSkins, _applySkinNames);

        #endregion
    }
}
