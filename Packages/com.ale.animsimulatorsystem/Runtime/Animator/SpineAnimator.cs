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

        #region 后端契约实现-播放

        /// <inheritdoc/>
        protected override bool PlayAnimOnRenderer(Component renderer, AnimData animData, int trackIndex)
        {
            var skeletonAnimation = AsSkeleton(renderer);
            if (!skeletonAnimation || skeletonAnimation.AnimationState == null) return false;

            var skeletonData = GetSkeletonData(skeletonAnimation);
            if (skeletonData == null) return false;

            string animName = animData.ResolveAnimName();
            var animation = skeletonData.FindAnimation(animName);
            if (animation == null)
            {
                Debug.LogWarning($"SpineAnimator >> PlayAnimOnRenderer: 骨架中不存在动画 '{animName}'，播放失败，GameObject={gameObject.name}", this);
                return false;
            }

            // 设置动画
            var trackEntryPlay = skeletonAnimation.AnimationState.SetAnimation(trackIndex, animation, animData.isLoop);
            if (trackEntryPlay == null) return false;

            // 设置 混合时间
            float defaultMixDuration = skeletonAnimation.SkeletonDataAsset.defaultMix; // 默认混合时间
            float maxMixDuration = animation.Duration * 0.3f;                          // 最大混合时间
            if (defaultMixDuration > maxMixDuration)
                // 限制 混合时间 不超过 最大混合时间。避免 动画播放效果异常
                trackEntryPlay.MixDuration = maxMixDuration;

            // 设置 初始进度值。若反转，则从结束位置起、速度取负。
            if (animData.isReverse)
            {
                trackEntryPlay.TrackTime = animation.Duration;
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
        protected override void StopAnimOnRenderer(Component renderer, int trackIndex, AnimData resumeAnimData)
        {
            var skeletonAnimation = AsSkeleton(renderer);
            if (!skeletonAnimation || skeletonAnimation.AnimationState == null) return;

            if (resumeAnimData != null)
            {
                // 栈里还压着上一条：恢复播放它（循环）
                var skeletonData = GetSkeletonData(skeletonAnimation);
                var animation = skeletonData?.FindAnimation(resumeAnimData.ResolveAnimName());
                if (animation != null)
                {
                    skeletonAnimation.AnimationState.SetAnimation(trackIndex, animation, true);
                    return;
                }
            }

            // 否则，停止该轨道动画（0.2 秒淡出到空动画）
            skeletonAnimation.AnimationState.SetEmptyAnimation(trackIndex, 0.2f);
        }

        /// <inheritdoc/>
        protected override void ClearRendererAnim(Component renderer)
        {
            var skeletonAnimation = AsSkeleton(renderer);
            if (!skeletonAnimation || skeletonAnimation.state == null) return;

            // 清除所有轨道动画
            skeletonAnimation.state.ClearTracks();
            // 重置为初始姿势
            skeletonAnimation.Skeleton.SetToSetupPose();
            // 更新Skeleton以应用更改
            skeletonAnimation.LateUpdate();
        }

        /// <inheritdoc/>
        protected override float GetAnimDuration(Component renderer, string animName)
        {
            if (string.IsNullOrEmpty(animName)) return 0f;
            var skeletonData = GetSkeletonData(AsSkeleton(renderer));
            var animation = skeletonData?.FindAnimation(animName);
            return animation?.Duration ?? 0f;
        }

        /// <inheritdoc/>
        protected override float GetAnimProgressOnRenderer(Component renderer, int trackIndex)
        {
            var entry = GetTrackEntry(renderer, trackIndex);
            if (entry?.Animation == null || entry.Animation.Duration <= 0f) return 0f;
            return Mathf.Clamp01(entry.TrackTime / entry.Animation.Duration);
        }

        /// <inheritdoc/>
        protected override bool SetAnimProgressOnRenderer(Component renderer, int trackIndex, float progress)
        {
            var entry = GetTrackEntry(renderer, trackIndex);
            if (entry?.Animation == null || entry.Animation.Duration <= 0f) return false;
            entry.TrackTime = entry.Animation.Duration * Mathf.Clamp01(progress);
            return true;
        }

        // 取指定渲染器指定轨道上当前的 TrackEntry
        private static TrackEntry GetTrackEntry(Component renderer, int trackIndex)
        {
            var skeletonAnimation = AsSkeleton(renderer);
            if (!skeletonAnimation || skeletonAnimation.state == null) return null;
            return skeletonAnimation.state.GetCurrent(trackIndex);
        }

        #endregion

        #region 后端契约实现-透明度

        /// <inheritdoc/>
        protected override float GetRendererAlpha(Component renderer)
        {
            var skeleton = AsSkeleton(renderer)?.Skeleton;
            return skeleton?.A ?? 0f;
        }

        /// <inheritdoc/>
        protected override void SetRendererAlpha(Component renderer, float alpha)
        {
            var skeleton = AsSkeleton(renderer)?.Skeleton;
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
#else
        // ASS_SPINE 未启用：保留空实现，使预制体上的组件引用不至于变成 Missing Script。
        protected override Component ResolveDefaultRenderer() => null;
        protected override IEnumerable<FAnimStateData> EnumerateStateDatas() { yield break; }
        protected override bool PlayAnimOnRenderer(Component renderer, AnimData animData, int trackIndex) => false;
        protected override void StopAnimOnRenderer(Component renderer, int trackIndex, AnimData resumeAnimData) { }
        protected override void ClearRendererAnim(Component renderer) { }
        protected override float GetAnimDuration(Component renderer, string animName) => 0f;
        protected override float GetRendererAlpha(Component renderer) => 0f;
        protected override void SetRendererAlpha(Component renderer, float alpha) { }
        protected override void InitSkinBackend() { }
        protected override bool HasSkin(string skinName) => false;
        protected override void ApplySkins(IReadOnlyList<string> baseSkinNames, IReadOnlyList<string> applySkinNames) { }
        protected override float GetAnimProgressOnRenderer(Component renderer, int trackIndex) => 0f;
        protected override bool SetAnimProgressOnRenderer(Component renderer, int trackIndex, float progress) => false;
#endif
    }
}
