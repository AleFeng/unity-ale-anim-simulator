using System;
using System.Collections.Generic;
using UnityEngine;

#if ASS_LIVE2D
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Motion;
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
    /// 故需要一张 <see cref="live2dAnimClips"/> 查找表把「动画名 → 剪辑」对上；</item>
    /// <item>Cubism 的<b>层（layer）数量很少</b>且在 <c>CubismMotionController</c> 上配置，
    /// 而本系统的轨道号是 <c>主轨道*10+子轨道</c>（值域 0..9990），二者必须显式映射，见 <see cref="live2dTrackLayers"/>；</item>
    /// <item>Cubism <b>没有读写播放进度的 API</b>，速度也必须 ≥ 0。故反向播放与进度擦洗
    /// （拖拽 / 旋转 / 按压三种交互）走单独的「采样通道」，见 <see cref="PlayAnimOnRenderer"/>。</item>
    /// </list>
    /// </summary>
    public class Live2dAnimator : AnimatorBase
    {
#if ASS_LIVE2D
        [Header("Live2D设置")]
        [Tooltip("Cubism 渲染控制器：本系统以它作为「渲染器」，淡入淡出即补间它的 Opacity。")]
        [SerializeField] private CubismRenderController live2dRenderController;
        [Tooltip("Cubism 动作控制器：常规播放走它，可享受 motion3.json 自带的淡入淡出。")]
        [SerializeField] private CubismMotionController live2dMotionController;
        [Tooltip("Live2D 状态数据列表")]
        [SerializeField] private FLive2dStateData[] live2dStateData;

        [Header("Live2D 动作查找表")]
        [Tooltip("动画名 → 动作剪辑。Cubism 没有按名查找动作的 API，故需在此显式登记。\n" +
                 "留空的动画名会用剪辑自身的资产名兜底。")]
        [SerializeField] private FLive2dAnimClip[] live2dAnimClips;

        [Header("Live2D 轨道映射")]
        [Tooltip("轨道 → Cubism 层。未显式映射的轨道按首次出现顺序自动分配，并钳制在 LayerCount 内。")]
        [SerializeField] private FLive2dTrackLayer[] live2dTrackLayers;
        [Tooltip("默认层索引：查不到映射且自动分配也用尽时的兜底层。")]
        [SerializeField] private int live2dLayerIndexDefault;

        [Header("Live2D 皮肤")]
        [Tooltip("皮肤定义列表：皮肤名 → 要显示的部件 ID 集合。")]
        [SerializeField] private Live2dSkinData[] live2dSkins;

#if UNITY_EDITOR
        private void Reset()
        {
            if (!live2dRenderController)  live2dRenderController  = GetComponentInChildren<CubismRenderController>();
            if (!live2dMotionController)  live2dMotionController  = GetComponentInChildren<CubismMotionController>();
        }

        private void OnValidate()
        {
            // 层索引越界在运行前就报出来：越界的动作会被静默钳到别的层上，表现为「动画互相覆盖」，极难排查。
            if (!live2dMotionController || live2dTrackLayers == null) return;
            int layerCount = Mathf.Max(1, live2dMotionController.LayerCount);
            foreach (var m in live2dTrackLayers)
            {
                if (m.layerIndex >= 0 && m.layerIndex < layerCount) continue;
                AnimSimLog.Warn(this, $"轨道映射的层索引 {m.layerIndex} 越界：" +
                                      $"CubismMotionController.LayerCount = {layerCount}（有效范围 0..{layerCount - 1}）。" +
                                      $"GameObject={gameObject.name}");
            }
            if (live2dLayerIndexDefault < 0 || live2dLayerIndexDefault >= layerCount)
                AnimSimLog.Warn(this, $"默认层索引 {live2dLayerIndexDefault} 越界（LayerCount = {layerCount}）。" +
                                      $"GameObject={gameObject.name}");
        }
#endif

        #region 后端契约实现-基础

        /// <inheritdoc/>
        protected override Component ResolveDefaultRenderer() => live2dRenderController;

        /// <inheritdoc/>
        protected override IEnumerable<FAnimStateData> EnumerateStateDatas()
        {
            if (live2dStateData == null) yield break;
            foreach (var data in live2dStateData)
                yield return new FAnimStateData
                {
                    stateName = data.stateName,
                    renderer  = data.live2dRenderController,
                    animDatas = data.live2dAnimDatas,
                };
        }

        // 把中性的渲染器令牌还原为 Cubism 的渲染控制器
        private static CubismRenderController AsRenderController(Component renderer)
            => renderer as CubismRenderController;

        #endregion

        #region 动作查找表

        // 动画名 → 剪辑。Awake 时由 live2dAnimClips 建表。
        private readonly Dictionary<string, AnimationClip> _clipByName = new Dictionary<string, AnimationClip>();
        private bool _clipTableBuilt;

        private void BuildClipTable()
        {
            if (_clipTableBuilt) return;
            _clipTableBuilt = true;

            if (live2dAnimClips == null) return;
            foreach (var item in live2dAnimClips)
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
        // 已被占用的层。自动分配必须避开它们——包括那些配置了但还没被访问到的显式映射，
        // 否则先来的自动分配会抢走某条轨道预定的层，两条动作被塞进同一层互相顶掉。
        private readonly HashSet<int> _layersInUse = new HashSet<int>();
        private bool _layerTableBuilt;
        private bool _layerOverflowWarned;

        // 一次性把显式映射灌进表里，并登记它们占用的层
        private void BuildLayerTable()
        {
            if (_layerTableBuilt) return;
            _layerTableBuilt = true;

            if (live2dTrackLayers == null) return;
            foreach (var m in live2dTrackLayers)
            {
                if (_trackToLayer.ContainsKey(m.TrackIndex))
                {
                    AnimSimLog.Warn(this, $"轨道映射中有重复的轨道 {m.TrackIndex}，后一条被忽略。" +
                                          $"GameObject={gameObject.name}");
                    continue;
                }
                _trackToLayer[m.TrackIndex] = m.layerIndex;
                _layersInUse.Add(m.layerIndex);
            }
        }

        /// <summary>
        /// 把本系统的轨道号映射到 Cubism 的层索引。
        /// 显式映射优先；缺失时分配<b>第一个尚未被占用的层</b>；无空闲层时退到
        /// <see cref="live2dLayerIndexDefault"/>（越界则钳进有效范围）并告警一次。
        /// </summary>
        private int MapTrackToLayer(int trackIndex)
        {
            BuildLayerTable();
            if (_trackToLayer.TryGetValue(trackIndex, out var layer)) return layer;

            int maxLayer = live2dMotionController ? Mathf.Max(0, live2dMotionController.LayerCount - 1) : 0;

            // 找第一个空闲层
            layer = -1;
            for (int i = 0; i <= maxLayer; i++)
                if (!_layersInUse.Contains(i)) { layer = i; break; }

            if (layer < 0)
            {
                // 无空闲层：退到配置的默认层（Inspector 上那个「默认层索引」的用途就在这里），
                // 越界时钳进有效范围，并告警一次——同层的动作会互相覆盖，必须让人知道。
                layer = Mathf.Clamp(live2dLayerIndexDefault, 0, maxLayer);
                if (!_layerOverflowWarned)
                {
                    _layerOverflowWarned = true;
                    AnimSimLog.Warn(this,
                        $"轨道 {trackIndex} 没有显式的层映射，且已无空闲层可分配" +
                        $"（LayerCount = {(live2dMotionController ? live2dMotionController.LayerCount : 0)}），" +
                        $"退到默认层 {layer}（配置值 {live2dLayerIndexDefault}）。" +
                        "同层的动作会互相覆盖——请在「轨道映射」里显式指定，或调大 CubismMotionController 的 Layer Count。" +
                        $" GameObject={gameObject.name}");
                }
            }

            _layersInUse.Add(layer);
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
            public float         speed;
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
        protected override bool PlayAnimOnRenderer(Component renderer, AnimData animData, int trackIndex)
        {
            var renderController = AsRenderController(renderer);
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
                speed      = speed,
                // 反向从末尾起、正向从头起，与 Spine 侧的语义一致
                progress   = animData.isReverse ? 1f : 0f,
            };

            if (needScrub)
            {
                // 先停原生播放，避免它继续写同一批参数
                StopNativeOnLayer(layerIndex);
                _trackPlayState[trackIndex] = stateNew;
                SampleClip(renderController, stateNew);
                return true;
            }

            if (!live2dMotionController)
            {
                AnimSimLog.Warn(this, $"未设置 CubismMotionController，" +
                                      $"无法播放 '{animName}'。GameObject={gameObject.name}");
                return false;
            }

            // PriorityForce：本系统自己管轨道与覆盖关系，不希望被 Cubism 的优先级二次拦截
            live2dMotionController.PlayAnimation(clip, layerIndex,
                CubismMotionPriority.PriorityForce, animData.isLoop, speed);
            _trackPlayState[trackIndex] = stateNew;
            return true;
        }

        /// <inheritdoc/>
        protected override void StopAnimOnRenderer(Component renderer, int trackIndex, AnimData resumeAnimData)
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
                if (clip && live2dMotionController)
                {
                    live2dMotionController.PlayAnimation(clip, state.layerIndex,
                        CubismMotionPriority.PriorityForce, true, Mathf.Max(0.001f, Mathf.Abs(resumeAnimData.speed)));
                    _trackPlayState[trackIndex] = new FTrackPlayState
                    {
                        layerIndex = state.layerIndex, clip = clip,
                        isScrub = false, speed = Mathf.Abs(resumeAnimData.speed), progress = 0f,
                    };
                }
            }
        }

        // 停止某一层的原生播放。CubismMotionController.StopAnimation 的第一个参数在 SDK 内部被忽略，
        // 实际行为就是「停掉该层的剪辑」，故此处固定传 0。
        private void StopNativeOnLayer(int layerIndex)
        {
            if (!live2dMotionController) return;
            if (layerIndex < 0 || layerIndex >= live2dMotionController.LayerCount) return;
            live2dMotionController.StopAnimation(0, layerIndex);
        }

        /// <inheritdoc/>
        protected override void ClearRendererAnim(Component renderer)
        {
            if (live2dMotionController) live2dMotionController.StopAllAnimation();
            _trackPlayState.Clear();
        }

        /// <inheritdoc/>
        protected override float GetAnimDuration(Component renderer, string animName)
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
        protected override float GetAnimProgressOnRenderer(Component renderer, int trackIndex)
        {
            if (!_trackPlayState.TryGetValue(trackIndex, out var state)) return 0f;
            return state.isScrub ? Mathf.Clamp01(state.progress) : 0f;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 该轨道若还在原生通道上，会就地切换到采样通道——写进度这个动作本身就意味着
        /// 调用方要接管播放（拖拽 / 旋转 / 按压），继续让 Cubism 驱动只会两者互相覆盖。
        /// </remarks>
        protected override bool SetAnimProgressOnRenderer(Component renderer, int trackIndex, float progress)
        {
            var renderController = AsRenderController(renderer);
            if (!renderController) return false;
            if (!_trackPlayState.TryGetValue(trackIndex, out var state) || !state.clip) return false;

            if (!state.isScrub)
            {
                // 切换到采样通道
                StopNativeOnLayer(state.layerIndex);
                state.isScrub = true;
            }

            state.progress = Mathf.Clamp01(progress);
            _trackPlayState[trackIndex] = state;
            SampleClip(renderController, state);
            return true;
        }

        // 把剪辑按进度采样到模型上。
        // 用 SampleAnimation 而非另建 PlayableGraph：模型上的 Animator 已被 Cubism 的
        // PlayableGraph 占用，再挂一个 AnimationPlayableOutput 到同一个 Animator 上会互相覆盖。
        private void SampleClip(CubismRenderController renderController, FTrackPlayState state)
        {
            if (!state.clip) return;
            var model = renderController.Model;
            var target = model ? model.gameObject : renderController.gameObject;
            state.clip.SampleAnimation(target, state.progress * state.clip.length);
        }

        #endregion

        #region 后端契约实现-透明度

        /// <inheritdoc/>
        protected override float GetRendererAlpha(Component renderer)
        {
            var renderController = AsRenderController(renderer);
            return renderController ? renderController.Opacity : 0f;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 写的是模型整体不透明度 <c>CubismRenderController.Opacity</c>——Cubism 官方的模型级淡入淡出入口。
        /// <b>注意</b>：motion3.json 里若也给模型不透明度打了关键帧（导入后表现为剪辑里一条
        /// <c>CubismRenderController.Opacity</c> 曲线），它会与本系统的淡入淡出同帧争写。
        /// 用于本系统的 Live2D 动作请不要动画模型整体不透明度，整体淡入淡出交给 <see cref="AnimatorBase"/>。
        /// </remarks>
        protected override void SetRendererAlpha(Component renderer, float alpha)
        {
            var renderController = AsRenderController(renderer);
            if (renderController) renderController.Opacity = Mathf.Clamp01(alpha);
        }

        #endregion

        #region 后端契约实现-皮肤

        // 部件 ID → 部件
        private readonly Dictionary<string, CubismPart> _partById = new Dictionary<string, CubismPart>();
        // 可绘制对象 ID → 渲染器（贴图替换用）
        private readonly Dictionary<string, CubismRenderer> _rendererByDrawableId = new Dictionary<string, CubismRenderer>();
        // 皮肤名 → 皮肤定义
        private readonly Dictionary<string, Live2dSkinData> _skinByName = new Dictionary<string, Live2dSkinData>();
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

            var model = live2dRenderController ? live2dRenderController.Model : null;
            if (model)
            {
                var parts = model.Parts;
                if (parts != null)
                    foreach (var part in parts)
                        if (part) _partById[part.Id] = part;
            }

            if (live2dRenderController && live2dRenderController.Renderers != null)
            {
                foreach (var r in live2dRenderController.Renderers)
                {
                    if (!r || !r.Drawable) continue;
                    _rendererByDrawableId[r.Drawable.Id] = r;
                    _originalTextures[r.Drawable.Id] = r.MainTexture;
                }
            }

            if (live2dSkins == null) return;
            foreach (var skin in live2dSkins)
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
            if (live2dSkins == null) return null;
            var names = new List<string>(live2dSkins.Length);
            foreach (var skin in live2dSkins)
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
        public struct FLive2dStateData
        {
            [Tooltip("状态名称")]
            public string stateName;
            [Tooltip("Cubism 渲染控制器（可选）：该状态使用另一个模型时填写，留空则用默认渲染器。")]
            public CubismRenderController live2dRenderController;
            [Tooltip("Live2D 动画数据 列表")]
            public AnimData[] live2dAnimDatas;
        }

        /// <summary>动作查找表的一项：动画名 → 动作剪辑。</summary>
        [Serializable]
        public struct FLive2dAnimClip
        {
            [Tooltip("动画名称：与 Spine 侧使用相同的命名规则。留空则用剪辑的资产名。")]
            public string animName;
            [Tooltip("动作剪辑：motion3.json 导入后生成的 AnimationClip。")]
            public AnimationClip clip;
        }

        /// <summary>轨道映射的一项：本系统的轨道 → Cubism 的层索引。</summary>
        [Serializable]
        public struct FLive2dTrackLayer
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
