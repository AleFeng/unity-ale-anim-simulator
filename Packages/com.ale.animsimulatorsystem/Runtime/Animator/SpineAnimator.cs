using System;
using System.Collections.Generic;
using UnityEngine;

#if ASS_SPINE
using Spine;
using Spine.Unity;
using Spine.Unity.AttachmentTools;
#endif

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// Spine 动画播放器。
    /// 状态机、轨道播放栈、计时、皮肤名册、淡入淡出等后端无关的机制全部在基类
    /// <see cref="AnimatorBase"/>，本类只负责把这些动作落到 Spine 运行时上。
    /// </summary>
    public class SpineAnimator : AnimatorBase
    {
#if ASS_SPINE
        [Header("Spine设置")]
        [Tooltip("Spine动画组件")]
        [SerializeField] private SkeletonAnimation spineSkeletonAnimation;
        [Tooltip("Spine状态数据列表")]
        [SerializeField] private FSpineStateData[] spineStateData;

#if UNITY_EDITOR
        private void Reset()
        {
            // 尝试自动获取 SkeletonAnimation组件
            if (!spineSkeletonAnimation)
                spineSkeletonAnimation = GetComponent<SkeletonAnimation>();
        }
#endif

        #region 后端契约实现-基础

        /// <inheritdoc/>
        protected override Component ResolveDefaultRenderer() => spineSkeletonAnimation;

        /// <inheritdoc/>
        protected override IEnumerable<FAnimStateData> EnumerateStateDatas()
        {
            if (spineStateData == null) yield break;
            foreach (var data in spineStateData)
                yield return new FAnimStateData
                {
                    stateName = data.stateName,
                    renderer  = data.spineSkeletonAnimation,
                    animDatas = data.spineAnimDatas,
                };
        }

        // 把中性的渲染器令牌还原为 Spine 的骨架动画组件
        private static SkeletonAnimation AsSkeleton(Component renderer) => renderer as SkeletonAnimation;

        // 取骨架数据（用于按名查找动画 / 皮肤）
        private static SkeletonData GetSkeletonData(SkeletonAnimation skeletonAnimation)
            => skeletonAnimation && skeletonAnimation.Skeleton != null ? skeletonAnimation.Skeleton.Data : null;

        #endregion

        #region 轨道号压缩

        /// <summary>
        /// 把系统轨道号（主轨道值 * 10 + 子轨道）换算为紧凑的 Spine 轨道号（主轨道序数 * 10 + 子轨道）。
        ///
        /// <para><b>为什么要压缩</b>：<c>EAnimTrack.Action = 900</c>、<c>Other = 999</c>，算出来的系统轨道号
        /// 高达 9000..9999。而 Spine 的 <c>AnimationState.SetAnimation</c> 会把内部的 <c>tracks</c> 列表
        /// Resize 到 <c>trackIndex + 1</c>（spine-csharp 的 <c>ExpandToIndex</c>），并在 Update / Apply 等
        /// <b>六处</b>按 <c>tracks.Count</c> 全量遍历——播一条 Other 轨道的动画就要每帧空转近六万次。</para>
        ///
        /// <para>序数由 <see cref="AnimTrackOrdinal"/> 给出，保序（Spine 的轨道号同时是混合优先级，
        /// 见那里的说明），且上界恒定（枚举内 21 个 + 枚举外 8 个槽位 = 序数至多 28），
        /// 故 Spine 轨道号上界恒定为 <c>28 * 10 + 9 = 289</c>。</para>
        ///
        /// <para>换算<b>只发生在本类与 Spine 之间</b>：基类、<c>AnimActionPlayer</c> 与序列化数据一律仍用系统轨道号。</para>
        /// </summary>
        private static int ToSpineTrack(int trackIndex)
        {
            if (trackIndex < 0) return trackIndex;

            int subTrack = trackIndex % 10;
            return AnimTrackOrdinal.OrdinalOfTrack(trackIndex) * 10 + subTrack;
        }

        #endregion

        #region 后端契约实现-播放

        /// <inheritdoc/>
        protected override bool PlayAnimOnRenderer(Component rendererParam, AnimData animData, int trackIndex)
        {
            var skeletonAnimation = AsSkeleton(rendererParam);
            if (!skeletonAnimation || skeletonAnimation.AnimationState == null) return false;

            // 交给 Spine 的轨道号一律先压缩
            trackIndex = ToSpineTrack(trackIndex);

            var skeletonData = GetSkeletonData(skeletonAnimation);
            if (skeletonData == null) return false;

            string animName = animData.ResolveAnimName();
            var anim = skeletonData.FindAnimation(animName);
            if (anim == null)
            {
                AnimSimLog.Warn(this, $"骨架中不存在动画 '{animName}'，播放失败，GameObject={gameObject.name}");
                return false;
            }

            // 设置动画
            var trackEntryPlay = skeletonAnimation.AnimationState.SetAnimation(trackIndex, anim, animData.isLoop);
            if (trackEntryPlay == null) return false;

            // 设置 轨道混合权重
            ApplyBlendWeight(trackEntryPlay, animData.BlendWeight);

            // 设置 混合时间
            float defaultMixDuration = skeletonAnimation.SkeletonDataAsset.defaultMix; // 默认混合时间
            float maxMixDuration = anim.Duration * 0.3f;                          // 最大混合时间
            if (defaultMixDuration > maxMixDuration)
                // 限制 混合时间 不超过 最大混合时间。避免 动画播放效果异常
                trackEntryPlay.MixDuration = maxMixDuration;

            // 设置 初始进度值。若反转，则从结束位置起、速度取负。
            if (animData.isReverse)
            {
                trackEntryPlay.TrackTime = anim.Duration;
                trackEntryPlay.TimeScale = -Mathf.Abs(animData.speed);
            }
            else
            {
                trackEntryPlay.TrackTime = 0f;
                trackEntryPlay.TimeScale = Mathf.Abs(animData.speed);
            }

            return true;
        }

        /// <inheritdoc/>
        protected override void StopAnimOnRenderer(Component rendererParam, int trackIndex, AnimData resumeAnimData)
        {
            var skeletonAnimation = AsSkeleton(rendererParam);
            if (!skeletonAnimation || skeletonAnimation.AnimationState == null) return;

            // 交给 Spine 的轨道号一律先压缩
            trackIndex = ToSpineTrack(trackIndex);

            if (resumeAnimData != null)
            {
                // 栈里还压着上一条：恢复播放它（循环）
                var skeletonData = GetSkeletonData(skeletonAnimation);
                var anim = skeletonData?.FindAnimation(resumeAnimData.ResolveAnimName());
                if (anim != null)
                {
                    var trackEntryResume = skeletonAnimation.AnimationState.SetAnimation(trackIndex, anim, true);
                    // 恢复出来的是一条新的 TrackEntry，权重不会自己继承，须按被恢复那条的配置重设
                    ApplyBlendWeight(trackEntryResume, resumeAnimData.BlendWeight);
                    return;
                }
            }

            // 否则，停止该轨道动画（0.2 秒淡出到空动画）
            skeletonAnimation.AnimationState.SetEmptyAnimation(trackIndex, 0.2f);
        }

        /// <summary>
        /// 把轨道混合权重落到 Spine 的 <c>TrackEntry.Alpha</c> 上。
        ///
        /// <para><c>Alpha</c> 默认 1、<c>MixBlend</c> 默认 <c>Replace</c>，语义正是本系统要的：
        /// 等于 1 时用本条动画完全覆写骨架当前姿势，小于 1 时与当前姿势（通常即更低轨道应用后的结果）按比例混合。
        /// 覆盖<b>方向</b>由轨道号给出——Spine 按轨道号升序 Apply，而 <see cref="AnimTrackOrdinal"/> 的序数保序。</para>
        ///
        /// <para><b>一处 Spine 语义需要知道</b>：<c>MixBlend.First</c>（「以本条为基准姿势、从初始姿势起混」）
        /// 只作用于 Spine 的 <b>0 号轨道</b>，而本系统的序数从 1（<c>Body</c>）起、0 号轨道实际空着。
        /// 因此权重取 1 时结果完全确定（覆写就是覆写）；取小于 1 时，<b>没有任何轨道打关键帧的骨骼</b>
        /// 会与上一帧的姿势相混而非从初始姿势起混，多帧下会渐近而非定值。真需要严格基准姿势的场合，
        /// 把基础循环动画配到 <c>EAnimTrack.None</c>（即 0 号轨道）即可。</para>
        /// </summary>
        private static void ApplyBlendWeight(TrackEntry trackEntry, float blendWeight)
        {
            if (trackEntry == null) return;
            trackEntry.Alpha = Mathf.Clamp01(blendWeight);
        }

        /// <inheritdoc/>
        protected override void ClearRendererAnim(Component rendererParam)
        {
            var skeletonAnimation = AsSkeleton(rendererParam);
            if (!skeletonAnimation || skeletonAnimation.state == null) return;

            // 清除所有轨道动画
            skeletonAnimation.state.ClearTracks();
            // 重置为初始姿势
            skeletonAnimation.Skeleton.SetToSetupPose();
            // 更新Skeleton以应用更改
            skeletonAnimation.LateUpdate();
        }

        /// <inheritdoc/>
        protected override float GetAnimDuration(Component rendererParam, string animName)
        {
            if (string.IsNullOrEmpty(animName)) return 0f;
            var skeletonData = GetSkeletonData(AsSkeleton(rendererParam));
            var anim = skeletonData?.FindAnimation(animName);
            return anim?.Duration ?? 0f;
        }

        /// <inheritdoc/>
        protected override float GetAnimProgressOnRenderer(Component rendererParam, int trackIndex)
        {
            var entry = GetTrackEntry(rendererParam, trackIndex);
            if (entry?.Animation == null || entry.Animation.Duration <= 0f) return 0f;
            return Mathf.Clamp01(entry.TrackTime / entry.Animation.Duration);
        }

        /// <inheritdoc/>
        protected override bool SetAnimProgressOnRenderer(Component rendererParam, int trackIndex, float progress)
        {
            var entry = GetTrackEntry(rendererParam, trackIndex);
            if (entry?.Animation == null || entry.Animation.Duration <= 0f) return false;
            entry.TrackTime = entry.Animation.Duration * Mathf.Clamp01(progress);
            return true;
        }

        // 取指定渲染器指定轨道上当前的 TrackEntry
        private static TrackEntry GetTrackEntry(Component renderer, int trackIndex)
        {
            var skeletonAnimation = AsSkeleton(renderer);
            if (!skeletonAnimation || skeletonAnimation.state == null) return null;
            // 交给 Spine 的轨道号一律先压缩
            return skeletonAnimation.state.GetCurrent(ToSpineTrack(trackIndex));
        }

        #endregion

        #region 后端契约实现-透明度

        /// <inheritdoc/>
        protected override float GetRendererAlpha(Component rendererParam)
        {
            var skeleton = AsSkeleton(rendererParam)?.Skeleton;
            return skeleton?.A ?? 0f;
        }

        /// <inheritdoc/>
        protected override void SetRendererAlpha(Component rendererParam, float alpha)
        {
            var skeleton = AsSkeleton(rendererParam)?.Skeleton;
            if (skeleton != null) skeleton.A = alpha;
        }

        #endregion

        #region 后端契约实现-皮肤

        // 缓存的 皮肤名称:皮肤数据
        private readonly Dictionary<string, Skin> _cachedSkins = new Dictionary<string, Skin>();

        /// <inheritdoc/>
        protected override void InitSkinBackend()
        {
            _cachedSkins.Clear();
            var skeletonData = GetSkeletonData(spineSkeletonAnimation);
            if (skeletonData == null) return;

            foreach (var skin in skeletonData.Skins)
                _cachedSkins[skin.Name] = skin;
        }

        /// <inheritdoc/>
        protected override bool HasSkin(string skinName)
            => !string.IsNullOrEmpty(skinName) && _cachedSkins.ContainsKey(skinName);

        /// <inheritdoc/>
        protected override void ApplySkins(IReadOnlyList<string> baseSkinNames, IReadOnlyList<string> applySkinNames)
        {
            if (!spineSkeletonAnimation) return;

            Skeleton skeleton = spineSkeletonAnimation.Skeleton;
            if (skeleton == null) return;

            var combinedSkin = new Skin("Combined Skin");

            // 添加基础皮肤
            if (baseSkinNames != null)
            {
                for (int i = 0; i < baseSkinNames.Count; i++)
                    if (_cachedSkins.TryGetValue(baseSkinNames[i] ?? string.Empty, out var skin))
                        combinedSkin.AddSkin(skin);
            }

            // 添加应用中的皮肤
            if (applySkinNames != null)
            {
                for (int i = 0; i < applySkinNames.Count; i++)
                    if (_cachedSkins.TryGetValue(applySkinNames[i] ?? string.Empty, out var skin))
                        combinedSkin.AddSkin(skin);
            }

            // 应用合并的皮肤
            skeleton.SetSkin(combinedSkin);
            skeleton.SetSlotsToSetupPose();
        }

#if UNITY_EDITOR
        /// <inheritdoc/>
        /// <remarks>直接读骨架数据资产，不依赖运行期已初始化的 <c>Skeleton</c>，故编辑模式下也能列出。</remarks>
        public override IReadOnlyList<string> EditorCollectSkinNames()
        {
            var dataAsset = spineSkeletonAnimation ? spineSkeletonAnimation.SkeletonDataAsset : null;
            if (!dataAsset) return null;

            // quiet: true —— 资产未就绪时静默返回 null，不在 Inspector 刷屏报错
            var skeletonData = dataAsset.GetSkeletonData(true);
            if (skeletonData == null || skeletonData.Skins == null) return null;

            var names = new List<string>(skeletonData.Skins.Count);
            foreach (var skin in skeletonData.Skins)
                if (skin != null) names.Add(skin.Name);
            return names;
        }
#endif

        #endregion

        #region Spine 专有-皮肤重打包

        // 打包的皮肤。用于优化皮肤，减少绘制调用和内存使用。
        private Material _repackedMaterial;
        // 打包的图集。
        private Texture2D _repackedAtlas;

        /// <summary>
        /// 打包皮肤。将 所有皮肤 合并重打包，减少 绘制调用 和 内存使用。
        /// 一般在 角色皮肤的 选择与切换 完成后调用。
        /// <para>Spine 专有：Live2D 无对应概念。</para>
        /// </summary>
        public void RepackedSkin()
        {
            if (!spineSkeletonAnimation) return;

            // 销毁 旧的打包材质与图集
            if (_repackedMaterial)
                Destroy(_repackedMaterial);
            if (_repackedAtlas)
                Destroy(_repackedAtlas);
            // 获取 当前皮肤
            Skin skinCurrent = spineSkeletonAnimation.Skeleton.Skin;
            // 打包成 新的皮肤
            Skin skinRepacked = skinCurrent.GetRepackedSkin
            (
                "Repacked Skin",
                spineSkeletonAnimation.SkeletonDataAsset.atlasAssets[0].PrimaryMaterial,
                out _repackedMaterial,
                out _repackedAtlas
            );
            // 清除 旧的皮肤数据
            skinCurrent.Clear();

            // 应用 新的打包皮肤
            spineSkeletonAnimation.Skeleton.Skin = skinRepacked;
            spineSkeletonAnimation.Skeleton.SetSlotsToSetupPose();
            spineSkeletonAnimation.AnimationState.Apply(spineSkeletonAnimation.Skeleton);

            // 清理未使用的资源。性能开销较大，建议仅在必要时调用。
            AtlasUtilities.ClearCache();
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region 定义-状态数据

        /// <summary>
        /// 游戏角色 状态数据（Spine 授权用）。
        /// 状态名 → 该状态下要播放的一组动画，以及可选的专用渲染器。
        ///
        /// <para>动画数据本身由后端中性的 <see cref="AnimData"/> 承载；本结构在
        /// <see cref="EnumerateStateDatas"/> 里转换为中性的 <see cref="FAnimStateData"/> 交给基类。</para>
        /// </summary>
        [Serializable]
        public struct FSpineStateData
        {
            [Tooltip("状态名称")]
            public string stateName;
            [Tooltip("Spine 动画组件")]
            public SkeletonAnimation spineSkeletonAnimation;
            [Tooltip("Spine 动画数据 列表")]
            public AnimData[] spineAnimDatas;
        }

        #endregion
        // ASS_SPINE 未启用时，本类只剩一个空的类声明——预制体上的组件引用因此不会变成 Missing Script，
        // 后端契约的 13 个成员直接继承 AnimatorBase 的默认空实现，不必在这里再抄一份空桩。
#endif
    }
}
