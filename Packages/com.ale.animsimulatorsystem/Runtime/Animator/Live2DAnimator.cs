using System;
using System.Collections.Generic;
using UnityEngine;

#if ASS_LIVE2D
using Ale.Toolkit.Runtime;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.MotionFade;
using Live2D.Cubism.Rendering;
#endif

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// Live2D（Cubism）动画播放器。
    /// 状态机、轨道播放栈、计时、皮肤名册、淡入淡出等后端无关的机制全部在基类
    /// <see cref="AnimatorBase"/>，本类只负责把这些动作落到 Cubism 运行时上。
    ///
    /// <para><b>与 Spine 的三处根本差异，决定了本类的实现形态：</b></para>
    /// <list type="number">
    /// <item>Cubism <b>不能按名找动作</b>（motion3.json 导入成一个个散落的 <c>AnimationClip</c>），
    /// 故需要一张 <see cref="live2DAnimClips"/> 查找表把「动画名 → 剪辑」对上；</item>
    /// <item>Cubism 的<b>层（layer）数量很少</b>且在 <c>CubismMotionController</c> 上配置，
    /// 而本系统的轨道号是 <c>主轨道*10+子轨道</c>（值域 0..9990），二者必须显式映射，见 <see cref="live2DTrackLayers"/>；</item>
    /// <item>Cubism <b>没有读写播放进度的 API</b>，速度也必须 ≥ 0。故反向播放与进度擦洗
    /// （拖拽 / 旋转 / 按压三种交互）走单独的「采样通道」，见 <see cref="PlayAnimOnRenderer"/>。</item>
    /// </list>
    /// </summary>
    // 实现 ICubismUpdatable 是为了让采样通道能在 Cubism 的 LateUpdate 调度中拿到确定的时序，
    // 见 OnLateUpdate。接口类型来自 SDK，故连同基类列表一起按宏门控——宏关闭时仍保留一个
    // 空的类声明，预制体上的组件引用才不会变成 Missing Script。
#if ASS_LIVE2D
    public class Live2DAnimator : AnimatorBase, Live2D.Cubism.Framework.ICubismUpdatable
#else
    // 类名两个分支必须一致、且与文件名 Live2DAnimator.cs 相同，否则关掉宏时 Unity 会报
    // 「类名与文件名不符」——那正是本分支要保住的场合（空类体让预制体上的组件引用不变成 Missing Script）。
    public class Live2DAnimator : AnimatorBase
#endif
    {
#if ASS_LIVE2D
        [Header("Live2D设置")]
        [Tooltip("Cubism 渲染控制器：本系统以它作为「渲染器」，淡入淡出即补间它的 Opacity。")]
        [SerializeField] private CubismRenderController live2DRenderController;
        [Tooltip("Cubism 动作控制器：常规播放走它，可享受 motion3.json 自带的淡入淡出。")]
        [SerializeField] private CubismMotionController live2DMotionController;
        [Tooltip("Live2D 状态数据列表")]
        [SerializeField] private FLive2DStateData[] live2DStateData;

        [Header("Live2D 动作查找表")]
        [Tooltip("动画名 → 动作剪辑。Cubism 没有按名查找动作的 API，故需在此显式登记。\n" +
                 "留空的动画名会用剪辑自身的资产名兜底。")]
        [SerializeField] private FLive2DAnimClip[] live2DAnimClips;

        [Header("Live2D 轨道映射")]
        [Tooltip("轨道 → Cubism 层。层号即覆盖优先级（层号大的后应用、盖住层号小的）。\n" +
                 "未显式映射的轨道按 轨道序数 自动分层并钳制在 LayerCount 内——保序、且与播放顺序无关。")]
        [SerializeField] private FLive2DTrackLayer[] live2DTrackLayers;
        // 这里原本还有一个「默认层索引」字段，用途是「自动分配把层用尽时的兜底层」。
        // 自动分层改为按轨道序数钳取之后，任何轨道都必得到一个有效层号，不再存在「用尽」这回事，
        // 该字段也就没有了任何作用，故删除——留着只会让人在 Inspector 上配了却不起作用。

        [Header("Live2D 皮肤")]
        [Tooltip("皮肤定义列表：皮肤名 → 要显示的部件 ID 集合。")]
        [SerializeField] private Live2DSkinData[] live2DSkins;

#if UNITY_EDITOR
        private void Reset()
        {
            if (!live2DRenderController)  live2DRenderController  = GetComponentInChildren<CubismRenderController>();
            if (!live2DMotionController)  live2DMotionController  = GetComponentInChildren<CubismMotionController>();
        }

        private void OnValidate()
        {
            ValidateCubismPrerequisites();
            ValidateAnimClipTable();
            ValidateTrackLayers();
        }

        /// <summary>
        /// Cubism 播放前提体检。
        ///
        /// <para><b>为什么这三项必须在运行前报</b>：它们缺任何一个，<c>CubismMotionController.OnEnable</c>
        /// 都会提前 return、把自己置为未激活，此后每次播放只在控制台留下一句笼统的
        /// <c>can't start motion.</c>——那句话既不说是哪个模型、也不说缺的是什么，
        /// 而真正的原因在三个不同的组件上，靠它根本回溯不到。</para>
        /// </summary>
        private void ValidateCubismPrerequisites()
        {
            // 常规播放全部经由 MotionController，缺了它动作一条都播不出来。
            if (!live2DMotionController)
            {
                AnimSimLog.Warn(this, $"「Live2D Motion Controller」未指定：常规动作播放全部经由它，" +
                                      $"留空则任何动作都播不出来。请在模型根物体上添加 CubismMotionController 并指到这里。" +
                                      $"GameObject={gameObject.name}");
            }
            else
            {
                // MotionController 在 OnEnable 里会去读同物体 FadeController 的动作淡入淡出列表，
                // 读不到就直接罢工。该列表由 Cubism 导入 motion3.json 时生成，但当 motion3.json
                // 平铺在模型目录下（而非官方约定的子文件夹）时，它会被生成到<b>上一级目录</b>且不会自动指派——
                // 于是资产明明存在、字段却是空的，这是最容易漏掉的一处。
                var fade = live2DMotionController.GetComponent<CubismFadeController>();
                if (!fade)
                    AnimSimLog.Warn(this, $"CubismMotionController 所在物体上没有 CubismFadeController：" +
                                          $"前者依赖后者提供动作淡入淡出数据，缺失则动作播放不会启动。" +
                                          $"GameObject={gameObject.name}");
                else if (!fade.CubismFadeMotionList)
                    AnimSimLog.Warn(this, $"CubismFadeController 的 CubismFadeMotionList 为空：" +
                                          $"CubismMotionController 启动时读不到它会直接停用，之后每次播放只报 " +
                                          $"「can't start motion.」。该列表资产通常已随 motion3.json 导入生成，" +
                                          $"但可能落在模型目录的上一级，找到后指派即可。" +
                                          $"GameObject={gameObject.name}");
            }

            // Animator 上挂了 AnimatorController 会让 MotionController 认为「你要走 Animator 那条路」而主动让位。
            // 而 Cubism 导入模型时<b>会自动生成一个空的 controller 资产</b>，手滑拖上去就中招。
            var animatorComponent = GetComponent<Animator>();
            if (animatorComponent && animatorComponent.runtimeAnimatorController)
                AnimSimLog.Warn(this, $"同物体的 Animator 挂了 Animator Controller " +
                                      $"'{animatorComponent.runtimeAnimatorController.name}'：" +
                                      $"CubismMotionController 检测到它就会停用自己，本系统的动作播放整条链路失效。" +
                                      $"走本系统时该栏应留空。GameObject={gameObject.name}");

            // CubismUpdateController 只登记<b>同物体</b>上的 ICubismUpdatable。挂到别处时本组件会退回自身的
            // LateUpdate，与 Cubism 各组件的先后顺序变成未定义——表现为拖拽/按压时姿势被 Cubism 盖回起始帧。
            if (!GetComponent<CubismModel>())
                AnimSimLog.Warn(this, $"本组件与 CubismModel 不在同一个物体上：" +
                                      $"CubismUpdateController 只调度同物体的 ICubismUpdatable，" +
                                      $"分开挂会让拖拽 / 旋转 / 按压的逐帧采样被 Cubism 覆盖回起始帧。" +
                                      $"请把本组件挂到模型根物体上。GameObject={gameObject.name}");
        }

        /// <summary>
        /// 动作查找表体检。
        /// <para>Cubism 没有按名找动作的 API，这张表是动作名的唯一来源；表里配歪了，
        /// 动作要到运行期真去播时才报，且只报「查不到某个名字」，回不到是哪一行配错。</para>
        /// </summary>
        private void ValidateAnimClipTable()
        {
            if (live2DAnimClips == null) return;

            var seen = new HashSet<string>();
            foreach (var entry in live2DAnimClips)
            {
                if (!entry.clip)
                {
                    AnimSimLog.Warn(this, $"动作查找表里有一行的「剪辑」为空（动画名 '{entry.animName}'）：" +
                                          $"该行无效。GameObject={gameObject.name}");
                    continue;
                }

                // 动画名留空时按约定用剪辑资产名兜底，重名判断也要基于兜底后的实际键。
                string key = string.IsNullOrEmpty(entry.animName) ? entry.clip.name : entry.animName;
                if (!seen.Add(key))
                    AnimSimLog.Warn(this, $"动作查找表里的动画名 '{key}' 重复：后一条会被忽略，" +
                                          $"按该名播放时拿到的始终是第一条。GameObject={gameObject.name}");
            }
        }

        /// <summary>轨道映射的层索引越界体检。</summary>
        private void ValidateTrackLayers()
        {
            // 层索引越界在运行前就报出来：越界的动作会被静默钳到别的层上，表现为「动画互相覆盖」，极难排查。
            if (!live2DMotionController || live2DTrackLayers == null) return;
            int layerCount = Mathf.Max(1, live2DMotionController.LayerCount);
            foreach (var m in live2DTrackLayers)
            {
                if (m.layerIndex >= 0 && m.layerIndex < layerCount) continue;
                AnimSimLog.Warn(this, $"轨道映射的层索引 {m.layerIndex} 越界：" +
                                      $"CubismMotionController.LayerCount = {layerCount}（有效范围 0..{layerCount - 1}）。" +
                                      $"GameObject={gameObject.name}");
            }
        }
#endif

        #region 后端契约实现-基础

        /// <inheritdoc/>
        protected override Component ResolveDefaultRenderer() => live2DRenderController;

        /// <inheritdoc/>
        protected override IEnumerable<FAnimStateData> EnumerateStateDatas()
        {
            if (live2DStateData == null) yield break;
            foreach (var data in live2DStateData)
                yield return new FAnimStateData
                {
                    stateName = data.stateName,
                    renderer  = data.live2DRenderController,
                    animDatas = data.live2DAnimDatas,
                };
        }

        // 把中性的渲染器令牌还原为 Cubism 的渲染控制器
        private static CubismRenderController AsRenderController(Component renderer)
            => renderer as CubismRenderController;

        #endregion

        #region 动作查找表

        // 动画名 → 剪辑。Awake 时由 live2DAnimClips 建表。
        private readonly Dictionary<string, AnimationClip> _clipByName = new Dictionary<string, AnimationClip>();
        private bool _clipTableBuilt;

        private void BuildClipTable()
        {
            if (_clipTableBuilt) return;
            _clipTableBuilt = true;

            if (live2DAnimClips == null) return;
            foreach (var item in live2DAnimClips)
            {
                if (!item.clip) continue;
                // 动画名留空时用剪辑自身的资产名兜底，省去手填
                string key = string.IsNullOrEmpty(item.animName) ? item.clip.name : item.animName;
                if (_clipByName.ContainsKey(key))
                {
                    AnimSimLog.Warn(this, $"动作查找表中有重复的动画名 '{key}'，后一条被忽略。" +
                                          $"GameObject={gameObject.name}");
                    continue;
                }
                _clipByName[key] = item.clip;
            }
        }

        private AnimationClip FindClip(string animName)
        {
            if (string.IsNullOrEmpty(animName)) return null;
            BuildClipTable();
            return _clipByName.TryGetValue(animName, out var clip) ? clip : null;
        }

        #endregion

        #region 轨道 → 层 映射

        private readonly Dictionary<int, int> _trackToLayer = new Dictionary<int, int>();
        // 这里原本还有一个「已占用的层」集合，供「第一个空闲层」式的自动分配避让用。
        // 自动分层改为按轨道序数确定性映射之后，分配结果只取决于轨道自身，不再需要知道别人占了哪些层，
        // 该集合也就只写不读了，故删除。
        private bool _layerTableBuilt;
        private bool _layerOverflowWarned;

        // 一次性把显式映射灌进表里
        private void BuildLayerTable()
        {
            if (_layerTableBuilt) return;
            _layerTableBuilt = true;

            if (live2DTrackLayers == null) return;
            foreach (var m in live2DTrackLayers)
            {
                if (_trackToLayer.ContainsKey(m.TrackIndex))
                {
                    AnimSimLog.Warn(this, $"轨道映射中有重复的轨道 {m.TrackIndex}，后一条被忽略。" +
                                          $"GameObject={gameObject.name}");
                    continue;
                }
                _trackToLayer[m.TrackIndex] = m.layerIndex;
            }
        }

        /// <summary>
        /// 把本系统的轨道号映射到 Cubism 的层索引。显式映射优先；缺失时按<b>轨道序数</b>确定性分层。
        ///
        /// <para><b>为什么按序数而不是「第一个空闲层」</b>：Cubism 的层号就是覆盖优先级（层混合器按层号
        /// 依次应用，层号大的后应用），这正是 <see cref="EAnimTrack"/>「枚举值大的轨道覆盖枚举值小的」
        /// 在本后端的落点。而「第一个空闲层」是按<b>首次播放的先后</b>发号的——先播 <c>Action</c> 再播
        /// <c>Body</c> 就会让 <c>Body</c> 拿到更大的层号、反过来盖住 <c>Action</c>，同一份配置还会因玩家
        /// 操作顺序不同而每次算出不同的层号。改用 <see cref="AnimTrackOrdinal"/> 的序数并钳进层数范围：
        /// 单调不降，故「高轨道的层号永远不低于低轨道」恒成立；且与播放顺序无关，可复现。</para>
        ///
        /// <para>层数不够时高序数的轨道会被钳到同一层、互相覆盖——这是 Cubism 层数有限的固有约束，
        /// 首次发生时告警一次。要精确控制请在「轨道映射」里显式指定，或调大 <c>LayerCount</c>。</para>
        /// </summary>
        private int MapTrackToLayer(int trackIndex)
        {
            BuildLayerTable();
            if (_trackToLayer.TryGetValue(trackIndex, out var layer)) return layer;

            int maxLayer = live2DMotionController ? Mathf.Max(0, live2DMotionController.LayerCount - 1) : 0;

            // 按轨道序数分层：序数保序，钳取后仍单调不降
            int ordinal = AnimTrackOrdinal.OrdinalOfTrack(trackIndex);
            layer = Mathf.Clamp(ordinal, 0, maxLayer);

            // 被钳住（序数超出层数）意味着若干高轨道会挤在最后一层互相覆盖，告警一次
            if (ordinal > maxLayer && !_layerOverflowWarned)
            {
                _layerOverflowWarned = true;
                AnimSimLog.Warn(this,
                    $"轨道 {trackIndex}（序数 {ordinal}）没有显式的层映射，序数超出层数范围" +
                    $"（LayerCount = {(live2DMotionController ? live2DMotionController.LayerCount : 0)}），" +
                    $"被钳到最高层 {layer}。同层的动作会互相覆盖——" +
                    "请在「轨道映射」里显式指定，或调大 CubismMotionController 的 Layer Count。" +
                    $" GameObject={gameObject.name}");
            }

            _trackToLayer[trackIndex] = layer;
            return layer;
        }

        #endregion

        #region 后端契约实现-播放

        // 某条轨道当前的播放形态
        private struct FTrackPlayState
        {
            public int           layerIndex;
            public AnimationClip clip;
            public bool          isScrub;   // true = 采样通道（由外部驱动进度），false = Cubism 原生通道
            public float         progress;  // 采样通道下的当前进度（0~1）
            // 这里原本还记着播放速度，但两条通道都用不上它：原生通道把速度直接交给 CubismMotionController，
            // 采样通道的进度完全由玩家操作驱动、与速度无关。只写不读，故删除。
            // 该轨道采样到哪个模型上。逐帧重采样时没有调用方传渲染器，只能由状态自己记着；
            // 一个角色由多个 Cubism 模型拼成时，各状态可以指定各自的渲染器，故不能图省事用默认渲染器。
            public CubismRenderController renderController;
        }

        private readonly Dictionary<int, FTrackPlayState> _trackPlayState = new Dictionary<int, FTrackPlayState>();

        /// <inheritdoc/>
        /// <remarks>
        /// 分两条通道：
        /// <list type="bullet">
        /// <item><b>原生通道</b>——循环 / 正向 / 速度&gt;0 的常规播放，交给 <c>CubismMotionController</c>，
        /// 可享受 motion3.json 自带的淡入淡出与层混合。</item>
        /// <item><b>采样通道</b>——反向播放、速度为 0、以及由玩家操作直接驱动进度的场合。
        /// Cubism 的速度必须 ≥ 0 且没有任何读写播放时间的 API，故这些场合改由本类
        /// 用 <c>AnimationClip.SampleAnimation</c> 逐帧把剪辑采样到模型上。
        /// 进入采样通道前会先停掉该层的原生播放，避免两者同帧争写同一批参数。</item>
        /// </list>
        /// </remarks>
        protected override bool PlayAnimOnRenderer(Component rendererParam, AnimData animData, int trackIndex)
        {
            var renderController = AsRenderController(rendererParam);
            if (!renderController) return false;

            string animName = animData.ResolveAnimName();
            var clip = FindClip(animName);
            if (!clip)
            {
                AnimSimLog.Warn(this, $"动作查找表中不存在动画 '{animName}'，" +
                                      $"播放失败。请在 Inspector 的「Live2D 动作查找表」中登记。GameObject={gameObject.name}");
                return false;
            }

            int layerIndex = MapTrackToLayer(trackIndex);
            float speed = Mathf.Abs(animData.speed);
            // 反向 / 暂停（速度 0）Cubism 都表达不了，只能走采样通道
            bool needScrub = animData.isReverse || speed <= 0f;

            var stateNew = new FTrackPlayState
            {
                layerIndex = layerIndex,
                clip       = clip,
                isScrub    = needScrub,
                // 反向从末尾起、正向从头起，与 Spine 侧的语义一致
                progress   = animData.isReverse ? 1f : 0f,
                renderController = renderController,
            };

            if (needScrub)
            {
                // 先停原生播放，避免它继续写同一批参数
                StopNativeOnLayer(layerIndex);
                _trackPlayState[trackIndex] = stateNew;
                SampleClip(renderController, stateNew);
                return true;
            }

            if (!live2DMotionController)
            {
                AnimSimLog.Warn(this, $"未设置 CubismMotionController，" +
                                      $"无法播放 '{animName}'。GameObject={gameObject.name}");
                return false;
            }

            // 设置 轨道混合权重（须在播放前设好，层权重是层混合器的输入）
            ApplyBlendWeight(layerIndex, animData.BlendWeight);

            // PriorityForce：本系统自己管轨道与覆盖关系，不希望被 Cubism 的优先级二次拦截
            live2DMotionController.PlayAnimation(clip, layerIndex,
                CubismMotionPriority.PriorityForce, animData.isLoop, speed);
            _trackPlayState[trackIndex] = stateNew;
            return true;
        }

        /// <summary>
        /// 把轨道混合权重落到 Cubism 的<b>层权重</b>上。
        ///
        /// <para><b>只对 0 号以上的层有效</b>：Cubism 的 <c>SetLayerWeight</c> 对 <c>layerIndex &lt;= 0</c>
        /// 直接静默返回——0 层是 <c>AnimationLayerMixerPlayable</c> 的基准层，权重恒为 1，这是 Unity 层混合器
        /// 的规则而非 Cubism 的选择。本系统的轨道序数从 1（<c>Body</c>）起，只有把动画配到
        /// <c>EAnimTrack.None</c> 才会落到 0 层，故正常配置下不受影响；真落到 0 层时权重设置无声失效，
        /// 这里显式告警一次而不是假装设置成功。</para>
        ///
        /// <para><b>采样通道不适用</b>：反向播放与进度擦洗由本类逐帧 <c>SampleAnimation</c> 直接把剪辑写到模型上，
        /// 根本不经过层混合器，层权重对它没有任何作用。</para>
        /// </summary>
        private void ApplyBlendWeight(int layerIndex, float blendWeight)
        {
            if (!live2DMotionController) return;

            if (layerIndex <= 0)
            {
                if (!_layerZeroWeightWarned && blendWeight < 1f)
                {
                    _layerZeroWeightWarned = true;
                    AnimSimLog.Warn(this,
                        $"轨道混合权重 {blendWeight:0.##} 落在 Cubism 的 0 号层上，无法生效——" +
                        "0 层是层混合器的基准层，权重恒为 1。请把该动画配到 EAnimTrack.None 以外的轨道，" +
                        "或在「轨道映射」里把它显式映射到 1 号及以上的层。" +
                        $" GameObject={gameObject.name}");
                }
                return;
            }

            // 经记账写入：GetLayerWeight 的读数以这本账为准，绕过它直接写会让账本说谎
            SetLayerWeightImmediate(layerIndex, blendWeight);
        }

        // 0 号层无法设权重的告警只发一次，避免逐次播放刷屏
        private bool _layerZeroWeightWarned;

        #region 层权重淡入淡出
        // 层索引 → 权重补间句柄。新指令到来时用于掐断在途的那条。
        private readonly Dictionary<int, ToolkitTweenHandle> _layerWeightTween = new Dictionary<int, ToolkitTweenHandle>();
        // 层索引 → 当前权重记账。Cubism 没有控制器级的权重读取接口（GetFadeStates 的间接路径在
        // 控制器未初始化时全是空元素），且 OnEnable 会把所有层的物理权重初始化成 1.0——
        // 首播空层时要靠这本账知道「其实还没人碰过这层」，先把物理值拉平再淡入。
        private readonly Dictionary<int, float> _layerWeight = new Dictionary<int, float>();

        // 误配模型（缺 FadeMotionList / 误挂 AnimatorController）上 SetLayerWeight 会 NRE 的一次性告警
        private bool _layerWeightWriteFailedWarned;

        /// <summary>取某层的记账权重。从未写过的层返回 0（见 <see cref="_layerWeight"/> 的说明）。</summary>
        private float GetLayerWeight(int layerIndex)
            => _layerWeight.TryGetValue(layerIndex, out var w) ? w : 0f;

        /// <summary>立即设置层权重并记账。0 层是基准层设不了权重（Cubism 对它静默返回），只记账。</summary>
        private void SetLayerWeightImmediate(int layerIndex, float weight)
        {
            weight = Mathf.Clamp01(weight);
            _layerWeight[layerIndex] = weight;
            if (layerIndex > 0 && live2DMotionController)
                live2DMotionController.SetLayerWeight(layerIndex, weight);
        }

        /// <summary>掐断某层在途的权重补间。不触发它挂的完成回调——淡出被掐断时绝不能顺手把层停掉。</summary>
        private void KillLayerWeightTween(int layerIndex)
        {
            if (!_layerWeightTween.TryGetValue(layerIndex, out var handle)) return;
            handle.Kill();
            _layerWeightTween.Remove(layerIndex);
        }

        /// <summary>
        /// 把某层权重补间到目标值，先掐断该层在途的补间。
        /// 目标与当前记账值几乎相等、或该层 / 时长不支持补间时，瞬置并同步回调。
        /// </summary>
        private void TweenLayerWeight(int layerIndex, float to, float duration, Action onComplete = null)
        {
            KillLayerWeightTween(layerIndex);

            to = Mathf.Clamp01(to);
            // 0 层设不了权重；时长非正没得补；已在目标值附近时补间只是空转
            if (layerIndex <= 0 || !live2DMotionController || duration <= 0f ||
                Mathf.Abs(GetLayerWeight(layerIndex) - to) < 0.001f)
            {
                SetLayerWeightImmediate(layerIndex, to);
                onComplete?.Invoke();
                return;
            }

            var handle = ToolkitTween.To(GetLayerWeight(layerIndex), to, duration,
                x => SetLayerWeightImmediate(layerIndex, x),
                EToolkitEase.Linear,
                onComplete: () =>
                {
                    _layerWeightTween.Remove(layerIndex);
                    SetLayerWeightImmediate(layerIndex, to);
                    onComplete?.Invoke();
                },
                owner: this);

            // owner 已死等场合 To 返回无效句柄且不跑回调，按瞬置兜底
            if (handle.IsActive) _layerWeightTween[layerIndex] = handle;
            else { SetLayerWeightImmediate(layerIndex, to); onComplete?.Invoke(); }
        }

        /// <summary>
        /// 批量把 1 号及以上的层权重置 0。
        /// <para><b>为什么需要</b>：<c>CubismMotionController.OnEnable</c> 把所有层的混合器权重初始化成 1.0，
        /// 空层满权重会以默认值压住基准层的输出——没有任何场景需要这个副作用。</para>
        /// </summary>
        /// <param name="onlyUntouched">true = 只清从未被本类记账过的层（初始化用——stateInitList 的播放
        /// 先于 <see cref="OnAnimatorStart"/> 执行，已设过权重的层不能碰）；false = 全清（清场用）。</param>
        private void ZeroLayerWeights(bool onlyUntouched)
        {
            if (!live2DMotionController) return;

            try
            {
                for (int i = 1; i < live2DMotionController.LayerCount; i++)
                {
                    if (onlyUntouched && _layerWeight.ContainsKey(i)) continue;
                    SetLayerWeightImmediate(i, 0f);
                }
            }
            catch (NullReferenceException)
            {
                // 缺 FadeMotionList 或 Animator 上误挂了 AnimatorController 时，CubismMotionController.OnEnable
                // 会提前返回、内部层数组从未创建，SetLayerWeight 直接 NRE。这类误配模型在播放动作时本来
                // 也会失败（播放路径的暴露维持原状），这里只保护新增的批量调用点，不让它变成启动崩溃。
                if (!_layerWeightWriteFailedWarned)
                {
                    _layerWeightWriteFailedWarned = true;
                    AnimSimLog.Warn(this, "CubismMotionController 未完成初始化（缺 CubismFadeMotionList，" +
                                          "或 Animator 上误挂了 AnimatorController），层权重无法设置。" +
                                          $"请检查模型配置。GameObject={gameObject.name}");
                }
            }
        }
        #endregion

        /// <inheritdoc/>
        protected override void StopAnimOnRenderer(Component rendererParam, int trackIndex, AnimData resumeAnimData)
        {
            if (!_trackPlayState.TryGetValue(trackIndex, out var state))
            {
                // 没有记录也尝试停一下映射到的层，避免残留
                StopNativeOnLayer(MapTrackToLayer(trackIndex));
                return;
            }

            StopNativeOnLayer(state.layerIndex);
            _trackPlayState.Remove(trackIndex);

            // 栈里还压着上一条：恢复播放它（循环）
            if (resumeAnimData != null)
            {
                var clip = FindClip(resumeAnimData.ResolveAnimName());
                if (clip && live2DMotionController)
                {
                    // 层权重是层上的持久设置，被恢复那条的权重未必与刚停掉那条相同，须重设
                    ApplyBlendWeight(state.layerIndex, resumeAnimData.BlendWeight);
                    live2DMotionController.PlayAnimation(clip, state.layerIndex,
                        CubismMotionPriority.PriorityForce, true, Mathf.Max(0.001f, Mathf.Abs(resumeAnimData.speed)));
                    _trackPlayState[trackIndex] = new FTrackPlayState
                    {
                        layerIndex = state.layerIndex, clip = clip,
                        isScrub = false, progress = 0f,
                    };
                }
            }
        }

        // 停止某一层的原生播放。CubismMotionController.StopAnimation 的第一个参数在 SDK 内部被忽略，
        // 实际行为就是「停掉该层的剪辑」，故此处固定传 0。
        private void StopNativeOnLayer(int layerIndex)
        {
            if (!live2DMotionController) return;
            if (layerIndex < 0 || layerIndex >= live2DMotionController.LayerCount) return;
            live2DMotionController.StopAnimation(0, layerIndex);
        }

        /// <inheritdoc/>
        protected override void ClearRendererAnim(Component rendererParam)
        {
            if (live2DMotionController) live2DMotionController.StopAllAnimation();
            _trackPlayState.Clear();

            // 清场连带掐断全部在途的层权重补间——它们挂的完成回调此刻已无意义，
            // 也不能让旧补间在下一次播放时还往层上写权重。层权重一并归零。
            foreach (var handle in _layerWeightTween.Values) handle.Kill();
            _layerWeightTween.Clear();
            ZeroLayerWeights(onlyUntouched: false);
        }

        /// <inheritdoc/>
        protected override float GetAnimDuration(Component rendererParam, string animName)
        {
            var clip = FindClip(animName);
            return clip ? clip.length : 0f;
        }

        #endregion

        #region 后端契约实现-进度

        /// <inheritdoc/>
        /// <remarks>
        /// 只有采样通道能给出可信进度：Cubism 原生通道不暴露播放时间，此时返回 0。
        /// </remarks>
        protected override float GetAnimProgressOnRenderer(Component rendererParam, int trackIndex)
        {
            if (!_trackPlayState.TryGetValue(trackIndex, out var state)) return 0f;
            return state.isScrub ? Mathf.Clamp01(state.progress) : 0f;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 该轨道若还在原生通道上，会就地切换到采样通道——写进度这个动作本身就意味着
        /// 调用方要接管播放（拖拽 / 旋转 / 按压），继续让 Cubism 驱动只会两者互相覆盖。
        /// </remarks>
        protected override bool SetAnimProgressOnRenderer(Component rendererParam, int trackIndex, float progress)
        {
            var renderController = AsRenderController(rendererParam);
            if (!renderController) return false;
            if (!_trackPlayState.TryGetValue(trackIndex, out var state) || !state.clip) return false;

            if (!state.isScrub)
            {
                // 切换到采样通道
                StopNativeOnLayer(state.layerIndex);
                state.isScrub = true;
            }

            state.progress = Mathf.Clamp01(progress);
            // 逐帧重采样要用它，这里一并刷新——调用方传进来的渲染器才是权威值
            state.renderController = renderController;
            _trackPlayState[trackIndex] = state;
            SampleClip(renderController, state);
            return true;
        }

        // 把剪辑按进度采样到模型上。
        // 用 SampleAnimation 而非另建 PlayableGraph：模型上的 Animator 已被 Cubism 的
        // PlayableGraph 占用，再挂一个 AnimationPlayableOutput 到同一个 Animator 上会互相覆盖。
        private void SampleClip(CubismRenderController renderController, FTrackPlayState state)
        {
            if (!state.clip || !renderController) return;
            var model = renderController.Model;
            var target = model ? model.gameObject : renderController.gameObject;
            state.clip.SampleAnimation(target, state.progress * state.clip.length);
        }

        #region 采样通道-逐帧重放

        //
        // 【为什么必须逐帧重采样】
        // AnimationClip.SampleAnimation 是<b>一次性写入</b>：它把剪辑在某个时刻的值写进模型参数，
        // 之后不再管。而 Cubism 每帧都会重写同一批参数——原生通道上的其它轨道由 PlayableGraph
        // 驱动，CubismFadeController 还会按动作淡入淡出再刷一遍。于是只在「进度发生变化」时采样
        // 的话，玩家一停手，下一帧参数就被它们盖回去，表现为动画瞬间弹回起始帧。
        //
        // Spine 侧不存在这个问题：那边写的是 TrackEntry.TrackTime，是持久状态，Spine 自己的
        // AnimationState.Apply 每帧都会照着它重新摆姿势。Cubism 没有等价物，只能自己每帧补写。
        //
        // 【为什么接 ICubismUpdatable 而不是直接写 LateUpdate】
        // 采样值必须在 CubismFadeController（执行序 100，负责把动作参数写进模型）<b>之后</b>、
        // CubismRenderController（10000，据参数建网格）<b>之前</b>落笔，否则要么被淡入淡出覆盖、
        // 要么这一帧根本没被画进去。普通 LateUpdate 与 Cubism 组件之间的先后是未定义的，
        // 接进 CubismUpdateController 的调度才能拿到确定的时序。
        //

        /// <summary>由 <c>CubismUpdateController</c> 读取，决定本组件在 LateUpdate 链上的位置。</summary>
        public int ExecutionOrder =>
            // 紧跟在动作淡入淡出之后：采样通道产出的就是「这一帧的动作姿势」，理应和动作走同一个位置，
            // 后面的姿势 / 表情 / 眨眼 / 物理才能一并基于被拖拽出来的姿势继续演算。
            Live2D.Cubism.Framework.CubismUpdateExecutionOrder.CubismFadeController + 1;

        /// <summary>编辑模式下不需要跑——采样通道只在运行期由玩家操作驱动。</summary>
        public bool NeedsUpdateOnEditing => false;

        /// <summary>同物体上是否有 <c>CubismUpdateController</c>。有则由它驱动，没有则回落到自身的 LateUpdate。</summary>
        public bool HasUpdateController { get; set; }

        // 挂 OnAnimatorStart 而不是 override Start：基类的 Start 已改为模板方法，
        // 会在本方法返回后才广播就绪信号——订阅者拿到信号时 HasUpdateController 必定已就位。
        protected override void OnAnimatorStart()
        {
            HasUpdateController = GetComponent<Live2D.Cubism.Framework.CubismUpdateController>() != null;

            // 清掉 CubismMotionController.OnEnable 留下的幻影满权重空层。
            // 只清没记过账的层：stateInitList 的初始状态在基类 Start 里已先播出去、权重已设好，不能碰。
            ZeroLayerWeights(onlyUntouched: true);
        }

        /// <summary>把所有处于采样通道的轨道按当前进度重新写一遍。</summary>
        public void OnLateUpdate()
        {
            if (!enabled || _trackPlayState.Count == 0) return;

            foreach (var kv in _trackPlayState)
            {
                var state = kv.Value;
                if (!state.isScrub) continue;
                SampleClip(state.renderController, state);
            }
        }

        // CubismUpdateController 只登记「与 CubismModel 同物体」的 ICubismUpdatable。
        // 本组件被挂到别处时收不到调度，退回自己的 LateUpdate——时序不如前者确定，但总好过不重放。
        private void LateUpdate()
        {
            if (!HasUpdateController) OnLateUpdate();
        }

        #endregion

        #endregion

        #region 后端契约实现-透明度

        /// <inheritdoc/>
        protected override float GetRendererAlpha(Component rendererParam)
        {
            var renderController = AsRenderController(rendererParam);
            return renderController ? renderController.Opacity : 0f;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 写的是模型整体不透明度 <c>CubismRenderController.Opacity</c>——Cubism 官方的模型级淡入淡出入口。
        /// <b>注意</b>：motion3.json 里若也给模型不透明度打了关键帧（导入后表现为剪辑里一条
        /// <c>CubismRenderController.Opacity</c> 曲线），它会与本系统的淡入淡出同帧争写。
        /// 用于本系统的 Live2D 动作请不要动画模型整体不透明度，整体淡入淡出交给 <see cref="AnimatorBase"/>。
        /// </remarks>
        protected override void SetRendererAlpha(Component rendererParam, float alpha)
        {
            var renderController = AsRenderController(rendererParam);
            if (renderController) renderController.Opacity = Mathf.Clamp01(alpha);
        }

        #endregion

        #region 后端契约实现-皮肤

        // 部件 ID → 部件
        private readonly Dictionary<string, CubismPart> _partById = new Dictionary<string, CubismPart>();
        // 可绘制对象 ID → 渲染器（贴图替换用）
        private readonly Dictionary<string, CubismRenderer> _rendererByDrawableId = new Dictionary<string, CubismRenderer>();
        // 皮肤名 → 皮肤定义
        private readonly Dictionary<string, Live2DSkinData> _skinByName = new Dictionary<string, Live2DSkinData>();
        // 被任一皮肤管辖的部件 ID 全集。不在此集合内的部件不受换装影响。
        private readonly HashSet<string> _managedPartIds = new HashSet<string>();
        // 可绘制对象 ID → 原始贴图（用于取消替换时还原）
        private readonly Dictionary<string, Texture2D> _originalTextures = new Dictionary<string, Texture2D>();

        /// <inheritdoc/>
        protected override void InitSkinBackend()
        {
            _partById.Clear();
            _rendererByDrawableId.Clear();
            _skinByName.Clear();
            _managedPartIds.Clear();
            _originalTextures.Clear();

            var model = live2DRenderController ? live2DRenderController.Model : null;
            if (model)
            {
                var parts = model.Parts;
                if (parts != null)
                    foreach (var part in parts)
                        if (part) _partById[part.Id] = part;
            }

            if (live2DRenderController && live2DRenderController.Renderers != null)
            {
                foreach (var r in live2DRenderController.Renderers)
                {
                    if (!r || !r.Drawable) continue;
                    _rendererByDrawableId[r.Drawable.Id] = r;
                    _originalTextures[r.Drawable.Id] = r.MainTexture;
                }
            }

            if (live2DSkins == null) return;
            foreach (var skin in live2DSkins)
            {
                if (skin == null || string.IsNullOrEmpty(skin.skinName)) continue;
                if (_skinByName.ContainsKey(skin.skinName))
                {
                    AnimSimLog.Warn(this, $"皮肤定义中有重复的皮肤名 '{skin.skinName}'，后一条被忽略。" +
                                          $"GameObject={gameObject.name}");
                    continue;
                }
                _skinByName[skin.skinName] = skin;
                if (skin.partIds == null) continue;
                foreach (var id in skin.partIds)
                    if (!string.IsNullOrEmpty(id)) _managedPartIds.Add(id);
            }
        }

        /// <inheritdoc/>
        protected override bool HasSkin(string skinName)
            => !string.IsNullOrEmpty(skinName) && _skinByName.ContainsKey(skinName);

        /// <inheritdoc/>
        /// <remarks>
        /// 只动「被任一皮肤管辖的部件」：未在任何皮肤里出现过的部件（身体、脸等模型固有部件）不受影响。
        /// </remarks>
        protected override void ApplySkins(IReadOnlyList<string> baseSkinNames, IReadOnlyList<string> applySkinNames)
        {
            if (_partById.Count == 0 && _managedPartIds.Count == 0) return;

            // 并集：本次要显示的部件
            var visible = new HashSet<string>();
            CollectSkinParts(baseSkinNames, visible);
            CollectSkinParts(applySkinNames, visible);

            // 管辖范围内的部件按是否入选设 1 / 0
            foreach (var id in _managedPartIds)
            {
                if (!_partById.TryGetValue(id, out var part) || !part) continue;
                part.Opacity = visible.Contains(id) ? 1f : 0f;
            }

            // 贴图替换：先全部还原，再按当前皮肤覆盖，避免上一套皮肤的贴图残留
            foreach (var kv in _originalTextures)
                if (_rendererByDrawableId.TryGetValue(kv.Key, out var r) && r)
                    r.MainTexture = kv.Value;

            ApplySkinTextures(baseSkinNames);
            ApplySkinTextures(applySkinNames);
        }

        private void CollectSkinParts(IReadOnlyList<string> skinNames, HashSet<string> into)
        {
            if (skinNames == null) return;
            for (int i = 0; i < skinNames.Count; i++)
            {
                if (!_skinByName.TryGetValue(skinNames[i] ?? string.Empty, out var skin)) continue;
                if (skin.partIds == null) continue;
                foreach (var id in skin.partIds)
                    if (!string.IsNullOrEmpty(id)) into.Add(id);
            }
        }

#if UNITY_EDITOR
        /// <inheritdoc/>
        /// <remarks>直接读 Inspector 上配置的皮肤定义，不依赖运行期状态。</remarks>
        public override IReadOnlyList<string> EditorCollectSkinNames()
        {
            if (live2DSkins == null) return null;
            var names = new List<string>(live2DSkins.Length);
            foreach (var skin in live2DSkins)
                if (skin != null && !string.IsNullOrEmpty(skin.skinName)) names.Add(skin.skinName);
            return names;
        }
#endif

        private void ApplySkinTextures(IReadOnlyList<string> skinNames)
        {
            if (skinNames == null) return;
            for (int i = 0; i < skinNames.Count; i++)
            {
                if (!_skinByName.TryGetValue(skinNames[i] ?? string.Empty, out var skin)) continue;
                if (skin.textures == null) continue;
                foreach (var tex in skin.textures)
                {
                    if (string.IsNullOrEmpty(tex.drawableId) || !tex.texture) continue;
                    if (_rendererByDrawableId.TryGetValue(tex.drawableId, out var r) && r)
                        r.MainTexture = tex.texture;
                }
            }
        }

        #endregion

        #region 定义-状态数据 与 配置项

        /// <summary>
        /// 游戏角色 状态数据（Live2D 授权用）。
        /// 状态名 → 该状态下要播放的一组动画，以及可选的专用渲染器。
        /// </summary>
        [Serializable]
        public struct FLive2DStateData
        {
            [Tooltip("状态名称")]
            public string stateName;
            [Tooltip("Cubism 渲染控制器（可选）：该状态使用另一个模型时填写，留空则用默认渲染器。")]
            public CubismRenderController live2DRenderController;
            [Tooltip("Live2D 动画数据 列表")]
            public AnimData[] live2DAnimDatas;
        }

        /// <summary>动作查找表的一项：动画名 → 动作剪辑。</summary>
        [Serializable]
        public struct FLive2DAnimClip
        {
            [Tooltip("动画名称：与 Spine 侧使用相同的命名规则。留空则用剪辑的资产名。")]
            public string animName;
            [Tooltip("动作剪辑：motion3.json 导入后生成的 AnimationClip。")]
            public AnimationClip clip;
        }

        /// <summary>轨道映射的一项：本系统的轨道 → Cubism 的层索引。</summary>
        [Serializable]
        public struct FLive2DTrackLayer
        {
            [Tooltip("动画轨道")]
            public EAnimTrack animTrack;
            [Tooltip("动画子轨道"), Range(0, 9)]
            public int animTrackSub;
            [Tooltip("Cubism 层索引：须小于 CubismMotionController 的 Layer Count。")]
            public int layerIndex;

            /// <summary>轨道号：主轨道 * 10 + 子轨道，与 <see cref="AnimData.AnimTrack"/> 的编码一致。</summary>
            public int TrackIndex => (int)animTrack * 10 + animTrackSub;
        }

        #endregion
        // ASS_LIVE2D 未启用时，本类只剩一个空的类声明——预制体上的组件引用因此不会变成 Missing Script，
        // 后端契约的 13 个成员直接继承 AnimatorBase 的默认空实现，不必在这里再抄一份空桩。
#endif
    }
}
