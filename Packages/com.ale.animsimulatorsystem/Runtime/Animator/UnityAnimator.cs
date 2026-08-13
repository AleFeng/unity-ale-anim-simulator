using System;
using System.Collections.Generic;
using UnityEngine;

#if ASS_UNITY_ANIM
using UnityEngine.Animations;
using UnityEngine.Playables;
#endif

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// Unity 内置动画播放器（Playables）。
    /// 状态机、轨道播放栈、计时、皮肤名册、淡入淡出等后端无关的机制全部在基类
    /// <see cref="AnimatorBase"/>，本类只负责把这些动作落到 Unity 的动画系统上。
    ///
    /// <para><b>不需要 AnimatorController。</b>本类在运行时自建一张
    /// <c>PlayableGraph</c>（<c>AnimationLayerMixerPlayable</c> + 每轨道一个 <c>AnimationClipPlayable</c>），
    /// 经 <c>AnimationPlayableOutput</c> 输出到 <see cref="unityAnimator"/>。角色只需要一个
    /// <c>Animator</c> 组件作为输出目标，控制器栏留空即可——状态机在本系统这一层，
    /// 再叠一层 AnimatorController 的状态机只会两边打架，见 <see cref="NormalizeAnimator"/>。</para>
    ///
    /// <para><b>与另两个后端的差异：</b></para>
    /// <list type="number">
    /// <item>Unity <b>不能按名找剪辑</b>（<c>RuntimeAnimatorController.animationClips</c> 只在有控制器时才有，
    /// 且与状态名不是一回事），故与 Live2D 一样需要一张 <see cref="unityAnimClips"/> 查找表；</item>
    /// <item><b>时间由本类自己驱动</b>：每个剪辑 playable 一律 <c>SetSpeed(0)</c>，本类持每轨道的时间游标，
    /// 在 <c>Update()</c> 里逐帧 <c>SetTime</c>。这是因为 <c>AnimationClipPlayable</c> 没有公开的
    /// 循环开关（<c>SetLoopTime</c> 是 internal，只有 Timeline 用得到），若靠剪辑资产的 Loop Time 导入设置，
    /// 权威就跑到美术的导入配置上去了，而本系统的权威只能是 <see cref="AnimData.isLoop"/>。
    /// 自驱动之后，循环 / 反向 / 速度 / 进度擦洗四件事走的是同一条路径，
    /// 也就<b>不需要 Live2D 那套「原生通道 + 采样通道」的双通道</b>；</item>
    /// <item>Unity 没有「皮肤」概念，换装是显隐物体组 + 可选材质 / 贴图覆盖，见 <see cref="UnityAnimSkinData"/>。</item>
    /// </list>
    /// </summary>
    public class UnityAnimator : AnimatorBase
    {
#if ASS_UNITY_ANIM
        // 透明度色属性取不到时的兜底属性名（内置管线的材质用它）
        private const string AlphaColorPropertyFallback = "_Color";
        // 皮肤贴图覆盖的默认属性名（URP）
        private const string SkinTexturePropertyDefault = "_BaseMap";
        // 低于该值即认为「已隐藏」，用于不透明材质下切 Renderer.enabled 兜底
        private const float  AlphaVisibleThreshold = 0.001f;
        // 非循环播放时游标的上界留一点余量：正好等于时长会让 playable 翻 IsDone
        private const double CursorEndEpsilon = 1e-4;

        [Header("Unity 动画设置")]
        [Tooltip("Animator 组件：本系统以它作为「渲染器」，也是 Playables 图的输出目标。\n" +
                 "不需要给它指定 Controller——状态机在本系统这一层。")]
        [SerializeField] private Animator unityAnimator;
        [Tooltip("Unity 状态数据列表")]
        [SerializeField] private FUnityStateData[] unityStateData;

        [Header("Unity 动画查找表")]
        [Tooltip("动画名 → 动画剪辑。Unity 没有按名查找剪辑的 API，故需在此显式登记。\n" +
                 "留空的动画名会用剪辑自身的资产名兜底。")]
        [SerializeField] private FUnityAnimClip[] unityAnimClips;

        [Header("Unity 透明度")]
        [Tooltip("透明度落点。Auto 依次探测 CanvasGroup → SpriteRenderer → Renderer 材质色。")]
        [SerializeField] private EUnityAlphaMode unityAlphaMode = EUnityAlphaMode.Auto;
        [Tooltip("材质色属性名：Renderer 模式下把透明度写到该属性的 alpha 通道上。\n" +
                 "URP 用 _BaseColor，内置管线用 _Color；材质没有该属性时自动退到 _Color。")]
        [SerializeField] private string unityAlphaColorProperty = "_BaseColor";

        [Header("Unity 皮肤")]
        [Tooltip("皮肤定义列表：皮肤名 → 要显示的物体集合。")]
        [SerializeField] private UnityAnimSkinData[] unitySkins;

        [Header("Unity 兼容性")]
        [Tooltip("起播时清空 Animator 的 Controller：控制器会自建一张图，与本类的输出同帧争写同一条动画流。\n" +
                 "关闭则只告警、不改动，此时表现如何取决于两张图的先后，没有定义。")]
        [SerializeField] private bool unityClearControllerOnPlay = true;
        [Tooltip("起播时修正 Animator 的三项设置（根运动 / 剔除模式 / 更新模式）。\n" +
                 "它们与「时间由本类驱动」不兼容，具体原因见各自的告警文案。")]
        [SerializeField] private bool unityForceAnimatorSettings = true;

#if UNITY_EDITOR
        private void Reset()
        {
            if (!unityAnimator) unityAnimator = GetComponent<Animator>();
            if (!unityAnimator) unityAnimator = GetComponentInChildren<Animator>();
        }

        private void OnValidate()
        {
            if (unitySkins == null) return;

            // 同一个渲染器槽位既被材质替换、又被贴图替换（两者可能来自<b>不同的皮肤</b>）：
            // 贴图替换要求该槽位常驻一份私有材质实例，而材质替换会把整个槽位换成另一个材质资产，
            // 两者互相驱逐——换装结果取决于应用顺序，不可预期。运行前就报出来。
            foreach (var skinWithMaterial in unitySkins)
            {
                if (skinWithMaterial?.materials == null) continue;
                foreach (var overrideMaterial in skinWithMaterial.materials)
                {
                    if (!overrideMaterial.renderer || !overrideMaterial.material) continue;
                    int slot = Mathf.Max(0, overrideMaterial.materialIndex);

                    foreach (var skinWithTexture in unitySkins)
                    {
                        if (skinWithTexture?.materials == null) continue;
                        foreach (var overrideTexture in skinWithTexture.materials)
                        {
                            if (!overrideTexture.texture) continue;
                            if (overrideTexture.renderer != overrideMaterial.renderer) continue;
                            if (Mathf.Max(0, overrideTexture.materialIndex) != slot) continue;

                            AnimSimLog.Warn(this,
                                $"渲染器 '{overrideMaterial.renderer.name}' 的材质槽位 {slot} 同时被材质替换" +
                                $"（皮肤 '{skinWithMaterial.skinName}'）与贴图替换（皮肤 '{skinWithTexture.skinName}'）指向。" +
                                "这两种覆盖会互相驱逐、换装结果取决于应用顺序。" +
                                "请统一成一种：为每种花色各做一个材质、只用材质替换，或者只用贴图替换。" +
                                $"（本告警只报第一处）GameObject={gameObject.name}");
                            return;
                        }
                    }
                }
            }
        }
#endif

        #region 后端契约实现-基础

        /// <inheritdoc/>
        protected override Component ResolveDefaultRenderer() => unityAnimator;

        /// <inheritdoc/>
        protected override IEnumerable<FAnimStateData> EnumerateStateDatas()
        {
            if (unityStateData == null) yield break;
            foreach (var data in unityStateData)
                yield return new FAnimStateData
                {
                    stateName = data.stateName,
                    renderer  = data.unityAnimator,
                    animDatas = data.unityAnimDatas,
                };
        }

        // 把中性的渲染器令牌还原为 Animator
        private static Animator AsAnimator(Component renderer) => renderer as Animator;

        #endregion

        #region 动画查找表

        // 动画名 → 剪辑。首次用到时由 unityAnimClips 建表。
        private readonly Dictionary<string, AnimationClip> _clipByName = new Dictionary<string, AnimationClip>();
        private bool _clipTableBuilt;

        private void BuildClipTable()
        {
            if (_clipTableBuilt) return;
            _clipTableBuilt = true;

            if (unityAnimClips == null) return;
            foreach (var item in unityAnimClips)
            {
                if (!item.clip) continue;
                // 动画名留空时用剪辑自身的资产名兜底，省去手填
                string key = string.IsNullOrEmpty(item.animName) ? item.clip.name : item.animName;
                if (_clipByName.ContainsKey(key))
                {
                    AnimSimLog.Warn(this, $"动画查找表中有重复的动画名 '{key}'，后一条被忽略。" +
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

        #region 轨道号压缩

        /// <summary>
        /// 把系统轨道号（主轨道值 * 10 + 子轨道）换算为层混合器的<b>输入下标</b>
        /// （主轨道序数 * 10 + 子轨道）——与 <c>SpineAnimator.ToSpineTrack</c> 是同一条公式。
        ///
        /// <para><b>为什么要压缩</b>：<c>EAnimTrack.Action = 900</c>、<c>Other = 999</c>，系统轨道号高达
        /// 9000..9999，而层混合器的输入数就是每帧要遍历的槽位数。序数由 <see cref="AnimTrackOrdinal"/> 给出，
        /// 保序（<b>输入下标大的后应用、盖住下标小的</b>，这正是 <see cref="EAnimTrack"/>「枚举值大的覆盖枚举值小的」
        /// 所依赖的）且上界恒定（枚举内 21 个 + 枚举外 8 个槽位 = 序数至多 28），
        /// 故输入下标上界恒定为 <c>28 * 10 + 9 = 289</c>。</para>
        ///
        /// <para><b>不做 +1 偏移</b>：0 号输入并没有「基准层、权重恒为 1」这回事——实测
        /// <c>AnimationLayerMixerPlayable</c> 的 0 号输入照常认权重（0.5 就是初始姿势与剪辑姿势的中点）。
        /// 只配到 <c>EAnimTrack.None</c> 的动画会落在 0 号输入上，行为与其它输入一致。</para>
        ///
        /// <para>换算<b>只发生在本类与 Playables 之间</b>：基类、<c>AnimActionPlayer</c> 与序列化数据一律仍用系统轨道号。</para>
        /// </summary>
        private static int ToMixerInput(int trackIndex)
        {
            if (trackIndex < 0) return trackIndex;

            int subTrack = trackIndex % 10;
            return AnimTrackOrdinal.OrdinalOfTrack(trackIndex) * 10 + subTrack;
        }

        #endregion

        #region Playables 图

        // 一个 Animator 令牌一张图。角色由多个模型拼成时，各状态可以指定各自的 Animator。
        //
        // 【必须是实例字段】关闭 Domain Reload 时静态字段会跨播放会话存活，
        // 那样表里会攥着上一次会话遗留的、已失效的原生图句柄。
        private readonly Dictionary<Animator, FUnityGraph> _graphs = new Dictionary<Animator, FUnityGraph>();

        private struct FUnityGraph
        {
            public PlayableGraph               graph;
            public AnimationLayerMixerPlayable mixer;
        }

        /// <summary>
        /// 取该 Animator 的图，没有则建一张。<b>惰性创建</b>——状态数据里声明的专用 Animator
        /// 可能一次都不会进入，在 <c>Awake</c> 里全建出来是白花原生分配。
        /// </summary>
        private bool TryGetGraph(Animator animator, out FUnityGraph entry)
        {
            if (!animator)
            {
                entry = default;
                return false;
            }

            if (_graphs.TryGetValue(animator, out entry) && entry.graph.IsValid()) return true;

            // 先修正 Animator 自身的设置，再建图：清空 Controller 会释放 Animator 自建的那张图，
            // 顺序反了就成了「我们的输出建好之后它才退场」。
            NormalizeAnimator(animator);

            var graph = PlayableGraph.Create($"{gameObject.name}.UnityAnimator");
            // 输入数按需增长，见 EnsureInputCount。singleLayerOptimization 保持默认关闭——
            // 它会在「只有一层有非零权重」时短路混合，而淡入淡出过程中权重恰恰是连续变化的。
            var mixer  = AnimationLayerMixerPlayable.Create(graph, 1);
            var output = AnimationPlayableOutput.Create(graph, "AnimSimulator", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();

            entry = new FUnityGraph { graph = graph, mixer = mixer };
            _graphs[animator] = entry;
            return true;
        }

        /// <summary>
        /// 保证层混合器至少有 <paramref name="index"/> + 1 个输入。
        /// <para><b>只增不减</b>：缩容会把被截掉的那些输入连带断开。增长不会破坏已有连接
        /// （<c>PlayableExtensions.AddInput</c> 本身就是「扩容 1 → Connect」实现的）。</para>
        /// </summary>
        private static void EnsureInputCount(FUnityGraph entry, int index)
        {
            int need = index + 1;
            if (entry.mixer.GetInputCount() >= need) return;
            entry.mixer.SetInputCount(need);
        }

        // 这些告警各只发一次，避免逐次播放刷屏
        private bool _controllerWarned;
        private bool _rootMotionWarned;
        private bool _cullingWarned;
        private bool _updateModeWarned;

        /// <summary>
        /// 建图前修正 Animator 上与「时间由本类驱动」不兼容的设置。
        /// </summary>
        private void NormalizeAnimator(Animator animator)
        {
            if (animator.runtimeAnimatorController)
            {
                if (!_controllerWarned)
                {
                    _controllerWarned = true;
                    AnimSimLog.Warn(this,
                        $"Animator '{animator.name}' 上挂着 Controller '{animator.runtimeAnimatorController.name}'。" +
                        "控制器会自建一张动画图，与本组件的输出同帧争写同一条动画流，先后没有定义、表现随机。" +
                        (unityClearControllerOnPlay
                            ? "已自动置空该栏（本组件不需要 Controller，状态机在本系统这一层）。"
                            : "「起播时清空 Controller」已关闭，故保持原样——请自行确认这是你要的。") +
                        $" GameObject={gameObject.name}");
                }
                if (unityClearControllerOnPlay) animator.runtimeAnimatorController = null;
            }

            if (!unityForceAnimatorSettings) return;

            if (animator.applyRootMotion)
            {
                if (!_rootMotionWarned)
                {
                    _rootMotionWarned = true;
                    AnimSimLog.Warn(this,
                        $"Animator '{animator.name}' 开着 Apply Root Motion，已自动关闭。" +
                        "根运动按「上一次时间 → 本次时间」的增量累积，而循环播放每绕回一圈就会产生一次" +
                        "「整条剪辑长度的负增量」，物体会瞬移。" +
                        $" GameObject={gameObject.name}");
                }
                animator.applyRootMotion = false;
            }

            if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
            {
                if (!_cullingWarned)
                {
                    _cullingWarned = true;
                    AnimSimLog.Warn(this,
                        $"Animator '{animator.name}' 的 Culling Mode 是 {animator.cullingMode}，已改为 Always Animate。" +
                        "离屏不求值时本类的时间游标照推，回到画面里姿势会突然跳一下。" +
                        $" GameObject={gameObject.name}");
                }
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            if (animator.updateMode != AnimatorUpdateMode.Normal)
            {
                if (!_updateModeWarned)
                {
                    _updateModeWarned = true;
                    AnimSimLog.Warn(this,
                        $"Animator '{animator.name}' 的 Update Mode 是 {animator.updateMode}，已改为 Normal。" +
                        "其它模式会把图的求值挪出常规帧循环，与本类在 Update() 里推进的游标脱钩。" +
                        $" GameObject={gameObject.name}");
                }
                animator.updateMode = AnimatorUpdateMode.Normal;
            }
        }

        private void DestroyAllGraphs()
        {
            foreach (var kv in _graphs)
                if (kv.Value.graph.IsValid()) kv.Value.graph.Destroy();
            _graphs.Clear();
        }

        #endregion

        #region 后端契约实现-播放

        // 某条轨道当前的播放状态
        private struct FUnityTrackPlay
        {
            public Animator              animator;      // 该轨道播在哪个 Animator 上
            public int                   mixerInput;    // ToMixerInput(trackIndex)
            public AnimationClipPlayable clipPlayable;
            public AnimationClip         clip;
            public float                 length;        // clip.length 缓存，<= 0 视为无效
            public double                cursor;        // 手动时间游标（秒）
            public float                 speed;         // Mathf.Abs(animData.speed)
            public int                   dir;           // +1 正向 / -1 反向
            public bool                  isLoop;
            public float                 weight;        // animData.BlendWeight
            public int                   progressWriteFrame; // 见 SetAnimProgressOnRenderer
        }

        private readonly Dictionary<int, FUnityTrackPlay> _trackPlay = new Dictionary<int, FUnityTrackPlay>();
        // 遍历 _trackPlay 时的键暂存表。字典的值是结构体，改完要写回，不能在枚举器上边遍历边写。
        // 提为字段，免得每帧新分配一个 List。
        private readonly List<int> _trackKeyScratch = new List<int>();

        /// <inheritdoc/>
        protected override bool PlayAnimOnRenderer(Component rendererParam, AnimData animData, int trackIndex)
        {
            var animator = AsAnimator(rendererParam);
            if (!animator) return false;

            string animName = animData.ResolveAnimName();
            var clip = FindClip(animName);
            if (!clip)
            {
                AnimSimLog.Warn(this, $"动画查找表中不存在动画 '{animName}'，播放失败。" +
                                      $"请在 Inspector 的「Unity 动画查找表」中登记。GameObject={gameObject.name}");
                return false;
            }

            return PlayClipOnTrack(animator, trackIndex, clip,
                animData.isLoop, animData.isReverse, Mathf.Abs(animData.speed), animData.BlendWeight);
        }

        /// <summary>
        /// 在指定轨道上起播一条剪辑。<see cref="PlayAnimOnRenderer"/> 与
        /// <see cref="StopAnimOnRenderer"/> 的「恢复上一条」共用本方法。
        /// </summary>
        private bool PlayClipOnTrack(Animator animator, int trackIndex, AnimationClip clip,
                                     bool isLoop, bool isReverse, float speed, float weight)
        {
            if (!TryGetGraph(animator, out var entry)) return false;

            int mixerInput = ToMixerInput(trackIndex);
            if (mixerInput < 0) return false;
            EnsureInputCount(entry, mixerInput);

            _trackPlay.TryGetValue(trackIndex, out var play);

            // 同一槽位重播同一条剪辑时复用 playable，不销毁重建——重建要走一次原生分配 + 连线，
            // 而「循环 + 随机间隔」这类播法会反复起播同一条。
            bool reuse = play.clipPlayable.IsValid()
                         && play.clip == clip
                         && play.animator == animator
                         && play.mixerInput == mixerInput;
            if (!reuse)
            {
                ReleaseSlot(ref play);

                var clipPlayable = AnimationClipPlayable.Create(entry.graph, clip);
                // 默认为 true，在人形骨架上会重解算双脚、改变采样出来的姿势
                clipPlayable.SetApplyFootIK(false);
                // 时间一律由本类驱动，见类注释
                clipPlayable.SetSpeed(0d);
                entry.graph.Connect(clipPlayable, 0, entry.mixer, mixerInput);

                play.clipPlayable = clipPlayable;
                play.clip         = clip;
            }

            play.animator           = animator;
            play.mixerInput         = mixerInput;
            play.length             = clip.length;
            play.isLoop             = isLoop;
            play.dir                = isReverse ? -1 : 1;
            play.speed              = speed;
            play.weight             = Mathf.Clamp01(weight);
            // 反向从末尾起、正向从头起，与 Spine / Live2D 两侧的语义一致
            play.cursor             = isReverse ? ClampCursor(clip.length, clip.length) : 0d;
            play.progressWriteFrame = -1;

            entry.mixer.SetInputWeight(mixerInput, play.weight);
            ApplyCursor(play.clipPlayable, play.cursor);

            _trackPlay[trackIndex] = play;
            return true;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <paramref name="resumeAnimData"/> 非空表示该轨道的播放栈里还压着上一条，应恢复播放它（循环）。
        /// </remarks>
        protected override void StopAnimOnRenderer(Component rendererParam, int trackIndex, AnimData resumeAnimData)
        {
            if (!_trackPlay.TryGetValue(trackIndex, out var play))
            {
                // 本后端的槽位与轨道一一对应，没有记录就没有占用，不必再做清理
                return;
            }

            var animator = play.animator ? play.animator : AsAnimator(rendererParam);
            ReleaseSlot(ref play);
            _trackPlay.Remove(trackIndex);

            if (resumeAnimData == null) return;

            var clip = FindClip(resumeAnimData.ResolveAnimName());
            if (!clip || !animator) return;

            // 恢复的是一条新的播放：混合权重是<b>槽位</b>上的持久设置，不会自己继承，
            // 须按被恢复那条自己的配置重设（Spine 侧对 TrackEntry 有完全相同的处理）。
            PlayClipOnTrack(animator, trackIndex, clip,
                true, resumeAnimData.isReverse,
                Mathf.Max(0.001f, Mathf.Abs(resumeAnimData.speed)), resumeAnimData.BlendWeight);
        }

        /// <summary>释放一个槽位：权重归零 → 断开连接 → 销毁 playable。</summary>
        /// <remarks>
        /// <c>DestroyPlayable</c> 不能省：图的寿命与角色一样长，每次起播漏一个原生节点就是持续泄漏。
        /// </remarks>
        private void ReleaseSlot(ref FUnityTrackPlay play)
        {
            if (play.animator && _graphs.TryGetValue(play.animator, out var entry) && entry.graph.IsValid())
            {
                if (play.mixerInput >= 0 && play.mixerInput < entry.mixer.GetInputCount())
                {
                    entry.mixer.SetInputWeight(play.mixerInput, 0f);
                    entry.mixer.DisconnectInput(play.mixerInput);
                }
                if (play.clipPlayable.IsValid()) entry.graph.DestroyPlayable(play.clipPlayable);
            }

            play.clipPlayable = default;
            play.clip         = null;
            play.length       = 0f;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 权重全零的层混合器输出的正是<b>绑定期的初始姿势</b>，这就是本契约要的「复位」。
        /// <para><b>不要改用 <c>Animator.Rebind()</c></b>：它会让已绑定的图输出失效，整张图得推倒重建。
        /// 它看着像 Spine 侧 <c>SetToSetupPose()</c> 的对应物，其实不是。</para>
        /// </remarks>
        protected override void ClearRendererAnim(Component rendererParam)
        {
            var animator = AsAnimator(rendererParam);
            if (!animator) return;

            _trackKeyScratch.Clear();
            foreach (var kv in _trackPlay)
                if (kv.Value.animator == animator) _trackKeyScratch.Add(kv.Key);

            foreach (int trackIndex in _trackKeyScratch)
            {
                var play = _trackPlay[trackIndex];
                ReleaseSlot(ref play);
                _trackPlay.Remove(trackIndex);
            }
            _trackKeyScratch.Clear();

            // 求值一次，让复位在当帧就可见，而不是等到下一次 PreLateUpdate
            if (_graphs.TryGetValue(animator, out var entry) && entry.graph.IsValid())
                entry.graph.Evaluate(0f);
        }

        /// <inheritdoc/>
        protected override float GetAnimDuration(Component rendererParam, string animName)
        {
            var clip = FindClip(animName);
            return clip ? clip.length : 0f;
        }

        #endregion

        #region 时间游标

        /// <summary>
        /// 逐帧推进各轨道的时间游标。
        ///
        /// <para><b>为什么是 <c>Update</c> 而不是 <c>LateUpdate</c></b>：帧内顺序是
        /// <c>Update</c> → <c>PreLateUpdate.DirectorUpdateAnimation*</c>（图在这里求值）→ <c>LateUpdate</c>。
        /// 放 <c>LateUpdate</c> 写进去的时间要到下一帧才被采样，会稳定慢一帧。</para>
        /// </summary>
        private void Update()
        {
            if (_trackPlay.Count == 0) return;

            _trackKeyScratch.Clear();
            foreach (var kv in _trackPlay) _trackKeyScratch.Add(kv.Key);

            float deltaTime = Time.deltaTime;
            int frame = Time.frameCount;

            foreach (int trackIndex in _trackKeyScratch)
            {
                if (!_trackPlay.TryGetValue(trackIndex, out var play)) continue;
                if (!AdvanceCursor(ref play, deltaTime, frame)) continue;
                _trackPlay[trackIndex] = play;
            }
            _trackKeyScratch.Clear();
        }

        /// <returns>游标是否发生了变化（需要写回字典）。</returns>
        private static bool AdvanceCursor(ref FUnityTrackPlay play, float deltaTime, int frame)
        {
            if (!play.clipPlayable.IsValid()) return false;
            // 物体被禁用时图根本不求值，硬推游标会让淡入时的姿势凭空跳一段
            if (!play.animator || !play.animator.isActiveAndEnabled) return false;
            // 本帧已被外部写过进度，让位给它，见 SetAnimProgressOnRenderer
            if (play.progressWriteFrame == frame) return false;
            // 速度 0 = 定格在当前帧（基类的 WarnIfSpeedZero 已就此告警过）
            if (play.speed <= 0f || play.length <= 0f) return false;

            double cursor = play.cursor + deltaTime * play.speed * play.dir;
            play.cursor = play.isLoop ? WrapCursor(cursor, play.length) : ClampCursor(cursor, play.length);
            ApplyCursor(play.clipPlayable, play.cursor);
            return true;
        }

        // 循环回绕。反向播放时游标会走成负数，取模后要补回一个周期。
        private static double WrapCursor(double time, float length)
        {
            if (length <= 0f) return 0d;
            double wrapped = time % length;
            return wrapped < 0d ? wrapped + length : wrapped;
        }

        // 非循环钳位。上界留 CursorEndEpsilon 的余量：正好等于时长会让 playable 翻 IsDone。
        private static double ClampCursor(double time, float length)
        {
            double max = length - CursorEndEpsilon;
            if (max < 0d) max = 0d;
            if (time < 0d) return 0d;
            return time > max ? max : time;
        }

        /// <summary>把游标写进 playable。</summary>
        /// <remarks>
        /// <b>连设两次是有意的</b>：第二次让 <c>GetPreviousTime() == GetTime()</c>，把
        /// <c>[previousTime, time]</c> 这个区间压成零。根运动的累积与动画事件的触发都以该区间为依据，
        /// 而循环回绕会造出一个横跨整条剪辑的<b>负区间</b>，正是这套机制最怕的输入。
        /// </remarks>
        private static void ApplyCursor(AnimationClipPlayable clipPlayable, double cursor)
        {
            if (!clipPlayable.IsValid()) return;
            clipPlayable.SetTime(cursor);
            clipPlayable.SetTime(cursor);
        }

        #endregion

        #region 后端契约实现-进度

        /// <inheritdoc/>
        protected override float GetAnimProgressOnRenderer(Component rendererParam, int trackIndex)
        {
            if (!_trackPlay.TryGetValue(trackIndex, out var play) || play.length <= 0f) return 0f;
            return Mathf.Clamp01((float)(play.cursor / play.length));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 直接写游标即可，<b>不需要 Live2D 那样切换通道</b>——本类的播放本来就是「自己驱动时间」，
        /// 外部写进度与内部推进走的是同一个游标。
        /// <para><b>记下写入帧号</b>是必要的：调用方（<c>AnimActionPlayer</c> 的拖拽 / 旋转 / 按压）
        /// 是在补间回调里写进度的，而本类在 <c>Update()</c> 里推进，两者的先后没有定义。
        /// 不记的话，同一帧内的写入会被推进覆盖掉，表现为拖拽时画面抖动。</para>
        /// </remarks>
        protected override bool SetAnimProgressOnRenderer(Component rendererParam, int trackIndex, float progress)
        {
            if (!_trackPlay.TryGetValue(trackIndex, out var play)) return false;
            if (play.length <= 0f || !play.clipPlayable.IsValid()) return false;

            play.cursor             = ClampCursor(play.length * Mathf.Clamp01(progress), play.length);
            play.progressWriteFrame = Time.frameCount;
            _trackPlay[trackIndex]  = play;

            ApplyCursor(play.clipPlayable, play.cursor);
            return true;
        }

        #endregion

        #region 后端契约实现-透明度

        // 某个 Animator 令牌的透明度落点
        private struct FUnityAlphaTarget
        {
            public EUnityAlphaMode  mode;
            public CanvasGroup      canvasGroup;
            public SpriteRenderer[] sprites;
            public Renderer[]       renderers;
            public int              colorPropId;
            public int              colorPropIdFallback;
            public float            alpha;     // 缓存值：Renderer 模式无法可靠回读，见 GetRendererAlpha
        }

        private readonly Dictionary<Animator, FUnityAlphaTarget> _alphaTargets =
            new Dictionary<Animator, FUnityAlphaTarget>();
        private MaterialPropertyBlock _alphaBlock;
        private bool _alphaTargetMissingWarned;

        /// <summary>
        /// 解析该 Animator 的透明度落点并缓存。
        /// <para>必须能在 <c>Awake</c> 期间工作——<c>base.Awake()</c> 里的 <c>HideRendererImmediate</c>
        /// 就会调 <see cref="SetRendererAlpha"/>。</para>
        /// </summary>
        private FUnityAlphaTarget ResolveAlphaTarget(Animator animator)
        {
            if (_alphaTargets.TryGetValue(animator, out var target)) return target;

            target = new FUnityAlphaTarget
            {
                mode                = EUnityAlphaMode.None,
                alpha               = 1f,
                colorPropId         = Shader.PropertyToID(string.IsNullOrEmpty(unityAlphaColorProperty)
                                                          ? AlphaColorPropertyFallback : unityAlphaColorProperty),
                colorPropIdFallback = Shader.PropertyToID(AlphaColorPropertyFallback),
            };

            bool auto = unityAlphaMode == EUnityAlphaMode.Auto;

            if (auto || unityAlphaMode == EUnityAlphaMode.CanvasGroup)
            {
                // 含父级：UGUI 的惯例是 CanvasGroup 挂在角色根上，而 Animator 未必在同一个物体
                var canvasGroup = animator.GetComponentInParent<CanvasGroup>(true);
                if (canvasGroup)
                {
                    target.mode        = EUnityAlphaMode.CanvasGroup;
                    target.canvasGroup = canvasGroup;
                    target.alpha       = canvasGroup.alpha;
                }
            }

            if (target.mode == EUnityAlphaMode.None && (auto || unityAlphaMode == EUnityAlphaMode.SpriteRenderer))
            {
                var sprites = animator.GetComponentsInChildren<SpriteRenderer>(true);
                if (sprites != null && sprites.Length > 0)
                {
                    target.mode    = EUnityAlphaMode.SpriteRenderer;
                    target.sprites = sprites;
                    target.alpha   = sprites[0] ? sprites[0].color.a : 1f;
                }
            }

            // SpriteRenderer 也是 Renderer，故这一条必须排在上面之后
            if (target.mode == EUnityAlphaMode.None && (auto || unityAlphaMode == EUnityAlphaMode.Renderer))
            {
                var renderers = animator.GetComponentsInChildren<Renderer>(true);
                if (renderers != null && renderers.Length > 0)
                {
                    target.mode      = EUnityAlphaMode.Renderer;
                    target.renderers = renderers;
                }
            }

            if (target.mode == EUnityAlphaMode.None
                && unityAlphaMode != EUnityAlphaMode.None
                && !_alphaTargetMissingWarned)
            {
                _alphaTargetMissingWarned = true;
                AnimSimLog.Warn(this,
                    $"Animator '{animator.name}' 下找不到任何可用的透明度落点" +
                    "（CanvasGroup / SpriteRenderer / Renderer 都没有），淡入淡出将不产生视觉效果。" +
                    $" GameObject={gameObject.name}");
            }

            _alphaTargets[animator] = target;
            return target;
        }

        /// <inheritdoc/>
        protected override float GetRendererAlpha(Component rendererParam)
        {
            var animator = AsAnimator(rendererParam);
            if (!animator) return 0f;

            var target = ResolveAlphaTarget(animator);
            switch (target.mode)
            {
                case EUnityAlphaMode.CanvasGroup:
                    return target.canvasGroup ? target.canvasGroup.alpha : target.alpha;

                case EUnityAlphaMode.SpriteRenderer:
                    if (target.sprites != null)
                        foreach (var sprite in target.sprites)
                            if (sprite) return sprite.color.a;
                    return target.alpha;

                default:
                    // Renderer 模式读缓存：MaterialPropertyBlock 对没设过的属性回读的是默认值，
                    // 拿它当淡入淡出的起始值会得到垃圾数。
                    return target.alpha;
            }
        }

        /// <inheritdoc/>
        protected override void SetRendererAlpha(Component rendererParam, float alpha)
        {
            var animator = AsAnimator(rendererParam);
            if (!animator) return;

            var target = ResolveAlphaTarget(animator);
            alpha = Mathf.Clamp01(alpha);
            target.alpha = alpha;
            _alphaTargets[animator] = target;

            switch (target.mode)
            {
                case EUnityAlphaMode.CanvasGroup:
                    if (target.canvasGroup) target.canvasGroup.alpha = alpha;
                    break;

                case EUnityAlphaMode.SpriteRenderer:
                    if (target.sprites == null) break;
                    foreach (var sprite in target.sprites)
                    {
                        if (!sprite) continue;
                        var color = sprite.color;
                        color.a = alpha;
                        sprite.color = color;
                    }
                    break;

                case EUnityAlphaMode.Renderer:
                    if (target.renderers == null) break;
                    if (_alphaBlock == null) _alphaBlock = new MaterialPropertyBlock();
                    bool visible = alpha > AlphaVisibleThreshold;
                    foreach (var renderer in target.renderers)
                    {
                        if (!renderer) continue;
                        WriteRendererAlpha(renderer, target, alpha);
                        // 【必要的兜底】不透明材质根本不采 alpha（URP Lit 的 Surface Type = Opaque 时
                        // 着色器不读这一路），写了也看不出效果；而与本组件同体、或本组件在其子树内的渲染器
                        // 又不会被基类 SetActive(false)（见 AnimatorBase.CanDeactivateRenderer），
                        // 两条路都不通的话角色会「以为自己隐藏了却仍然全见」。
                        renderer.enabled = visible;
                    }
                    break;
            }
        }

        // 把 alpha 写进渲染器的属性块。材质色每次现读——皮肤可能中途替换过材质，缓存会过期。
        private void WriteRendererAlpha(Renderer renderer, FUnityAlphaTarget target, float alpha)
        {
            var material = renderer.sharedMaterial;
            int propId = target.colorPropId;
            if (material && !material.HasProperty(propId)) propId = target.colorPropIdFallback;

            Color color = material && material.HasProperty(propId) ? material.GetColor(propId) : Color.white;
            color.a = alpha;

            // 先取回现有属性块再改：块里可能还有别人（或换装的贴图覆盖）设过的键，整块覆盖会把它们抹掉
            renderer.GetPropertyBlock(_alphaBlock);
            _alphaBlock.SetColor(propId, color);
            renderer.SetPropertyBlock(_alphaBlock);
        }

        #endregion

        #region 后端契约实现-皮肤

        // 皮肤名 → 皮肤定义
        private readonly Dictionary<string, UnityAnimSkinData> _skinByName =
            new Dictionary<string, UnityAnimSkinData>();
        // 被任一皮肤管辖的物体全集。不在此集合内的物体不受换装影响。
        private readonly HashSet<GameObject> _managedObjects = new HashSet<GameObject>();
        // 本次要显示的物体（ApplySkins 的暂存表，提为字段免得每次换装都新分配）
        private readonly HashSet<GameObject> _skinVisibleScratch = new HashSet<GameObject>();

        // 材质替换涉及的槽位 → 初始材质
        private readonly List<FUnitySkinMaterialSlot> _skinMaterialSlots = new List<FUnitySkinMaterialSlot>();
        // 贴图替换涉及的槽位 → 私有材质实例
        private readonly List<FUnitySkinInstanceSlot> _skinInstanceSlots = new List<FUnitySkinInstanceSlot>();
        // 贴图替换涉及的属性 → 初始贴图
        private readonly List<FUnitySkinTextureSlot> _skinTextureSlots = new List<FUnitySkinTextureSlot>();

        private struct FUnitySkinMaterialSlot
        {
            public Renderer renderer;
            public int      materialIndex;
            public Material original;
        }

        private struct FUnitySkinInstanceSlot
        {
            public Renderer renderer;
            public int      materialIndex;
            public Material instance;   // 本组件创建，OnDestroy 时释放
        }

        private struct FUnitySkinTextureSlot
        {
            public Material instance;
            public int      propId;
            public Texture  original;   // 可以为空——正因如此贴图覆盖不能走属性块，见下
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <b>捕获分三遍，顺序不能换</b>：贴图覆盖会往槽位里装一份私有材质实例（见
        /// <see cref="ResolveSkinMaterialInstance"/>），而材质替换要记下「无皮肤时该槽位应有的样子」。
        /// 若两者混着一遍扫完，先扫到的材质替换记下的就是<b>装实例之前</b>的那个材质，
        /// 之后每次还原都会把实例踢出槽位，贴图便写在了一个不再被使用的材质上。
        /// </remarks>
        protected override void InitSkinBackend()
        {
            ReleaseSkinMaterialInstances();
            _skinByName.Clear();
            _managedObjects.Clear();
            _skinMaterialSlots.Clear();
            _skinTextureSlots.Clear();

            if (unitySkins == null) return;

            // 第一遍：建名册、收受管物体
            foreach (var skin in unitySkins)
            {
                if (skin == null || string.IsNullOrEmpty(skin.skinName)) continue;
                if (_skinByName.ContainsKey(skin.skinName))
                {
                    AnimSimLog.Warn(this, $"皮肤定义中有重复的皮肤名 '{skin.skinName}'，后一条被忽略。" +
                                          $"GameObject={gameObject.name}");
                    continue;
                }
                _skinByName[skin.skinName] = skin;

                if (skin.objects == null) continue;
                foreach (var go in skin.objects)
                    if (go) _managedObjects.Add(go);
            }

            // 第二遍：把贴图覆盖要用的私有材质实例全部装好，并记下各属性的初始贴图
            foreach (var skin in _skinByName.Values)
            {
                if (skin.materials == null) continue;
                foreach (var materialOverride in skin.materials)
                    CaptureSkinTextureOverride(materialOverride);
            }

            // 第三遍：再记材质替换的初始值——此时槽位上已经是私有实例（若该槽位有贴图覆盖）
            foreach (var skin in _skinByName.Values)
            {
                if (skin.materials == null) continue;
                foreach (var materialOverride in skin.materials)
                    CaptureSkinMaterialOverride(materialOverride);
            }
        }

        // 记下材质替换涉及槽位的初始材质，供换装时还原
        private void CaptureSkinMaterialOverride(FUnitySkinMaterialOverride materialOverride)
        {
            var renderer = materialOverride.renderer;
            if (!renderer || !materialOverride.material) return;

            int materialIndex = Mathf.Max(0, materialOverride.materialIndex);
            var sharedMaterials = renderer.sharedMaterials;
            if (materialIndex >= sharedMaterials.Length) return;

            for (int i = 0; i < _skinMaterialSlots.Count; i++)
                if (_skinMaterialSlots[i].renderer == renderer && _skinMaterialSlots[i].materialIndex == materialIndex)
                    return;

            _skinMaterialSlots.Add(new FUnitySkinMaterialSlot
            {
                renderer = renderer, materialIndex = materialIndex, original = sharedMaterials[materialIndex],
            });
        }

        // 装好贴图覆盖用的私有材质实例，并记下该属性的初始贴图
        private void CaptureSkinTextureOverride(FUnitySkinMaterialOverride materialOverride)
        {
            if (!materialOverride.renderer || !materialOverride.texture) return;

            int materialIndex = Mathf.Max(0, materialOverride.materialIndex);
            var instance = ResolveSkinMaterialInstance(materialOverride.renderer, materialIndex);
            if (!instance) return;

            int propId = ResolveSkinTextureProperty(materialOverride);
            for (int i = 0; i < _skinTextureSlots.Count; i++)
                if (_skinTextureSlots[i].instance == instance && _skinTextureSlots[i].propId == propId) return;

            _skinTextureSlots.Add(new FUnitySkinTextureSlot
            {
                instance = instance,
                propId   = propId,
                original = instance.HasProperty(propId) ? instance.GetTexture(propId) : null,
            });
        }

        /// <summary>
        /// 取该槽位的私有材质实例，没有则克隆一份并装回渲染器。
        ///
        /// <para><b>为什么贴图替换必须走私有实例，而不是属性块</b>：换装要能<b>还原</b>，而还原意味着
        /// 「把这个贴图键去掉」。<c>MaterialPropertyBlock</c> 没有删除单个键的接口，
        /// <c>SetTexture(propId, null)</c> 直接抛 <c>ArgumentNullException</c>，
        /// <c>Clear()</c> 又会连透明度一起抹掉。改写材质<b>资产</b>更不行——那会弄脏磁盘上的 .mat，
        /// 且同一材质的所有使用者都会跟着变。克隆一份自己管，是唯一既能还原又不波及资产的做法。</para>
        /// </summary>
        private Material ResolveSkinMaterialInstance(Renderer renderer, int materialIndex)
        {
            for (int i = 0; i < _skinInstanceSlots.Count; i++)
                if (_skinInstanceSlots[i].renderer == renderer && _skinInstanceSlots[i].materialIndex == materialIndex)
                    return _skinInstanceSlots[i].instance;

            var sharedMaterials = renderer.sharedMaterials;
            if (materialIndex >= sharedMaterials.Length) return null;

            var source = sharedMaterials[materialIndex];
            if (!source) return null;

            var instance = new Material(source) { name = source.name + " (AnimSim Skin)" };
            sharedMaterials[materialIndex] = instance;
            renderer.sharedMaterials = sharedMaterials;

            _skinInstanceSlots.Add(new FUnitySkinInstanceSlot
            {
                renderer = renderer, materialIndex = materialIndex, instance = instance,
            });
            return instance;
        }

        private int ResolveSkinTextureProperty(FUnitySkinMaterialOverride materialOverride)
            => Shader.PropertyToID(string.IsNullOrEmpty(materialOverride.texturePropertyName)
                                   ? SkinTexturePropertyDefault : materialOverride.texturePropertyName);

        private void ReleaseSkinMaterialInstances()
        {
            for (int i = 0; i < _skinInstanceSlots.Count; i++)
            {
                var slot = _skinInstanceSlots[i];
                if (!slot.instance) continue;
                if (Application.isPlaying) Destroy(slot.instance);
                else DestroyImmediate(slot.instance);
            }
            _skinInstanceSlots.Clear();
        }

        /// <inheritdoc/>
        protected override bool HasSkin(string skinName)
            => !string.IsNullOrEmpty(skinName) && _skinByName.ContainsKey(skinName);

        /// <inheritdoc/>
        /// <remarks>
        /// 只动「被任一皮肤管辖的物体」：未在任何皮肤里出现过的物体（身体、脸等角色固有部分）不受影响。
        /// </remarks>
        protected override void ApplySkins(IReadOnlyList<string> baseSkinNames, IReadOnlyList<string> applySkinNames)
        {
            if (_managedObjects.Count == 0 && _skinMaterialSlots.Count == 0 && _skinTextureSlots.Count == 0) return;

            // 并集：本次要显示的物体
            _skinVisibleScratch.Clear();
            CollectSkinObjects(baseSkinNames, _skinVisibleScratch);
            CollectSkinObjects(applySkinNames, _skinVisibleScratch);

            foreach (var go in _managedObjects)
            {
                if (!go) continue;
                bool visible = _skinVisibleScratch.Contains(go);
                if (go.activeSelf != visible) go.SetActive(visible);
            }
            _skinVisibleScratch.Clear();

            // 材质 / 贴图覆盖：先全部还原，再按当前皮肤覆盖，避免上一套皮肤的覆盖残留
            RestoreSkinMaterials();
            ApplySkinMaterials(baseSkinNames);
            ApplySkinMaterials(applySkinNames);
        }

        private void CollectSkinObjects(IReadOnlyList<string> skinNames, HashSet<GameObject> into)
        {
            if (skinNames == null) return;
            for (int i = 0; i < skinNames.Count; i++)
            {
                if (!_skinByName.TryGetValue(skinNames[i] ?? string.Empty, out var skin)) continue;
                if (skin.objects == null) continue;
                foreach (var go in skin.objects)
                    if (go) into.Add(go);
            }
        }

        private void RestoreSkinMaterials()
        {
            for (int i = 0; i < _skinMaterialSlots.Count; i++)
            {
                var slot = _skinMaterialSlots[i];
                if (!slot.renderer) continue;
                SetRendererMaterial(slot.renderer, slot.materialIndex, slot.original);
            }

            for (int i = 0; i < _skinTextureSlots.Count; i++)
            {
                var slot = _skinTextureSlots[i];
                // Material.SetTexture 接受 null，这正是不用属性块的原因
                if (slot.instance) slot.instance.SetTexture(slot.propId, slot.original);
            }
        }

        private void ApplySkinMaterials(IReadOnlyList<string> skinNames)
        {
            if (skinNames == null) return;
            for (int i = 0; i < skinNames.Count; i++)
            {
                if (!_skinByName.TryGetValue(skinNames[i] ?? string.Empty, out var skin)) continue;
                if (skin.materials == null) continue;

                foreach (var materialOverride in skin.materials)
                {
                    var renderer = materialOverride.renderer;
                    if (!renderer) continue;
                    int materialIndex = Mathf.Max(0, materialOverride.materialIndex);

                    if (materialOverride.material)
                        SetRendererMaterial(renderer, materialIndex, materialOverride.material);

                    if (!materialOverride.texture) continue;
                    var instance = ResolveSkinMaterialInstance(renderer, materialIndex);
                    if (instance) instance.SetTexture(ResolveSkinTextureProperty(materialOverride), materialOverride.texture);
                }
            }
        }

        private static void SetRendererMaterial(Renderer renderer, int materialIndex, Material material)
        {
            var sharedMaterials = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= sharedMaterials.Length) return;
            if (sharedMaterials[materialIndex] == material) return;
            sharedMaterials[materialIndex] = material;
            renderer.sharedMaterials = sharedMaterials;
        }

#if UNITY_EDITOR
        /// <inheritdoc/>
        /// <remarks>直接读 Inspector 上配置的皮肤定义，不依赖运行期状态。</remarks>
        public override IReadOnlyList<string> EditorCollectSkinNames()
        {
            if (unitySkins == null) return null;
            var names = new List<string>(unitySkins.Length);
            foreach (var skin in unitySkins)
                if (skin != null && !string.IsNullOrEmpty(skin.skinName)) names.Add(skin.skinName);
            return names;
        }
#endif

        #endregion

        #region 生命周期

        /// <inheritdoc/>
        /// <remarks>
        /// 图不在 <c>OnDisable</c> 里销毁：基类的淡入淡出会常规地禁用 / 启用渲染器物体，
        /// 那样每淡出一次就会把全部轨道状态推倒重来。
        /// </remarks>
        protected override void OnDestroy()
        {
            DestroyAllGraphs();
            ReleaseSkinMaterialInstances();

            _trackPlay.Clear();
            _trackKeyScratch.Clear();
            _alphaTargets.Clear();

            // 基类要掐掉全部在途补间 / 延时 / 调度，并清空七张登记表
            base.OnDestroy();
        }

        #endregion

        #region 定义-状态数据 与 配置项

        /// <summary>
        /// 游戏角色 状态数据（Unity 授权用）。
        /// 状态名 → 该状态下要播放的一组动画，以及可选的专用渲染器。
        /// </summary>
        [Serializable]
        public struct FUnityStateData
        {
            [Tooltip("状态名称")]
            public string stateName;
            [Tooltip("Animator（可选）：该状态使用另一个模型时填写，留空则用默认渲染器。")]
            public Animator unityAnimator;
            [Tooltip("Unity 动画数据 列表")]
            public AnimData[] unityAnimDatas;
        }

        /// <summary>动画查找表的一项：动画名 → 动画剪辑。</summary>
        [Serializable]
        public struct FUnityAnimClip
        {
            [Tooltip("动画名称：与 Spine / Live2D 侧使用相同的命名规则。留空则用剪辑的资产名。")]
            public string animName;
            [Tooltip("动画剪辑（AnimationClip）")]
            public AnimationClip clip;
        }

        #endregion
        // ASS_UNITY_ANIM 未启用时，本类只剩一个空的类声明——预制体上的组件引用因此不会变成 Missing Script，
        // 后端契约的 13 个成员直接继承 AnimatorBase 的默认空实现，不必在这里再抄一份空桩。
#endif
    }

    /// <summary>
    /// <c>UnityAnimator</c> 的透明度落点。淡入淡出补间的就是这里选中的那一路。
    /// <para>放在类外、且<b>不受 <c>ASS_UNITY_ANIM</c> 门控</b>：宏关闭时类体整体消失，
    /// 若枚举也跟着消失，预制体上已序列化的该字段会连带报「类型丢失」。</para>
    /// </summary>
    [Serializable]
    public enum EUnityAlphaMode
    {
        /// <summary>自动探测：CanvasGroup → SpriteRenderer → Renderer 材质色。</summary>
        Auto = 0,
        /// <summary>UGUI：写 <c>CanvasGroup.alpha</c>。</summary>
        CanvasGroup,
        /// <summary>2D 精灵：写各 <c>SpriteRenderer.color</c> 的 alpha 通道。</summary>
        SpriteRenderer,
        /// <summary>
        /// 3D：经 <c>MaterialPropertyBlock</c> 写材质色的 alpha 通道。
        /// <b>材质必须是透明混合的</b>，不透明材质根本不采 alpha。
        /// </summary>
        Renderer,
        /// <summary>不做任何透明度处理。</summary>
        None,
    }
}
