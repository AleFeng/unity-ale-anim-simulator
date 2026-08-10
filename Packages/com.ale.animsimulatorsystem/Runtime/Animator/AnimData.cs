using System;
using UnityEngine;

#if ASS_SPINE
using Spine.Unity;
#endif

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画数据：一次动画播放的全部参数（播哪条、放在哪条轨道、循环 / 反转 / 速度 / 延时 / 间隔）。
    ///
    /// <para><b>后端中性</b>——同一份配置既描述 Spine 动画也描述 Live2D 动作，动画由
    /// <see cref="animName"/> 这个<b>字符串名</b>指定，两个后端使用相同的命名规则：
    /// Spine 侧按名在 <c>SkeletonData</c> 中查找，Live2D 侧按名在动作查找表中查找。</para>
    ///
    /// <para>本类由 <c>SpineAnimator.SpineAnimData</c> 提升而来。字段名与序列化布局<b>一字未改</b>，
    /// 故既有预制体上的配置原样存活——Unity 对非 <c>[SerializeReference]</c> 的托管
    /// <c>[Serializable]</c> 类数组按<b>字段名</b>而非类型名序列化，类型改名与提升为顶层均不影响数据。</para>
    /// </summary>
    [Serializable]
    public class AnimData
    {
        [Tooltip("动画名称：在动画软件中制作时的名称。Spine 与 Live2D 使用相同的命名规则。\n" +
                 "留空时回退读取下方的 动画引用资源（仅 Spine，兼容旧配置用）。")]
        public string animName;

#if ASS_SPINE
        [Tooltip("动画引用资源：Spine动画资产引用。\n" +
                 "【兼容字段】仅在 动画名称 留空时作为回退使用，新配置请直接填写 动画名称。")]
        public AnimationReferenceAsset animRefAsset;
#endif

        [Tooltip("动画轨道：用于不同类型的动画的区分。不同轨道的动画 可以同时播放。")]
        [SerializeField] private EAnimTrack animTrack;
        [Tooltip("动画子轨道：用于同一类型动画的区分。不同子轨道的动画 可以同时播放。"), Range(0, 9)]
        [SerializeField] private int animTrackSub;
        [Tooltip("是否循环播放")]
        public bool isLoop;
        [Tooltip("是否反转播放。反转时，动画将从 结束位置开始，并反方向播放。")]
        public bool isReverse;
        [Tooltip("播放速度倍率。默认为 1.0。"), Range(0f, 5f)]
        public float speed;

        [Tooltip("开始播放的延迟时间（秒）：动画将在指定的延迟后开始播放。")]
        public float startDelayTime;
        [Tooltip("循环间隔时间（秒）：仅当动画为 循环播放 时有效。动画播放完成后，在设定的范围内 随机一次 间隔时间。")]
        public Vector2 loopIntervalTimeRange;

        /// <summary>
        /// 动画轨道 默认：主轨道 * 10 + 子轨道，主轨道间隔10，子轨道0-9，保证不同 子轨道的动画 可以同时播放。
        /// 由构造函数直接指定轨道号时（<see cref="_animTrack"/> ≥ 0）优先返回它。
        /// </summary>
        public int AnimTrack
        {
            get
            {
                // 判据是 >= 0 而非 > 0：0 是合法轨道号，哨兵值是 -1。
                if (_animTrack >= 0)
                    return _animTrack;
                else
                    return (int)animTrack * 10 + animTrackSub;
            }
        }
        // 动画轨道。构造函数时，由外部传参进行设置。-1 表示未指定，改用上面的 主轨道/子轨道 计算。
        private int _animTrack = -1;

        /// <summary>
        /// 解析实际要播放的动画名：<see cref="animName"/> 优先；留空时回退到旧版的 Spine 动画引用资源
        /// （取其引用的动画名，取不到则退而取资产文件名）。两者都没有时返回 <c>null</c>。
        /// </summary>
        public string ResolveAnimName()
        {
            if (!string.IsNullOrEmpty(animName)) return animName;
#if ASS_SPINE
            if (animRefAsset)
                return animRefAsset.Animation != null ? animRefAsset.Animation.Name : animRefAsset.name;
#endif
            return null;
        }

        /// <summary>
        /// 构造函数：初始化动画数据
        /// </summary>
        public AnimData()
        {
            animName = null;
#if ASS_SPINE
            animRefAsset = null;
#endif
            animTrack = EAnimTrack.None;
            animTrackSub = 0;
            isLoop = false;
            isReverse = false;
            speed = 1.0f;
            startDelayTime = 0f;
            loopIntervalTimeRange = Vector2.zero;
        }

        /// <summary>
        /// 构造函数：按 动画名称 初始化动画数据（两个后端通用）。
        /// </summary>
        /// <param name="animName">动画名称：在动画软件中制作时的名称。</param>
        /// <param name="animTrack">动画轨道：用于不同类型的动画的区分。不同轨道的动画 可以同时播放。</param>
        /// <param name="isLoop">是否循环播放</param>
        /// <param name="isReverse">是否反转播放。反转时，动画将从 结束位置开始，并反方向播放。</param>
        /// <param name="speed">播放速度倍率。默认为 1.0。</param>
        /// <param name="startDelayTime">开始播放的延迟时间（秒）：动画将在指定的延迟后开始播放。</param>
        /// <param name="loopIntervalTimeRange">循环间隔时间（秒）：仅当动画为 循环播放 时有效。动画播放完成后，在设定的范围内 随机一次 间隔时间。</param>
        public AnimData
        (
            string animName,
            int animTrack = 0,
            bool isLoop = false,
            bool isReverse = false,
            float speed = 1f,
            float startDelayTime = 0f,
            Vector2 loopIntervalTimeRange = default
        )
        {
            this.animName = animName;
            this._animTrack = animTrack;
            this.isLoop = isLoop;
            this.isReverse = isReverse;
            this.speed = speed;
            this.startDelayTime = startDelayTime;
            this.loopIntervalTimeRange = loopIntervalTimeRange;
        }

#if ASS_SPINE
        /// <summary>
        /// 构造函数：按 Spine 动画引用资源 初始化动画数据。
        /// 【兼容重载】新代码请用接收 动画名称 的重载——它对两个后端都适用。
        /// </summary>
        /// <param name="animRefAsset">动画引用资源：Spine动画资产引用。</param>
        /// <param name="animTrack">动画轨道：用于不同类型的动画的区分。不同轨道的动画 可以同时播放。</param>
        /// <param name="isLoop">是否循环播放</param>
        /// <param name="isReverse">是否反转播放。反转时，动画将从 结束位置开始，并反方向播放。</param>
        /// <param name="speed">播放速度倍率。默认为 1.0。</param>
        /// <param name="startDelayTime">开始播放的延迟时间（秒）：动画将在指定的延迟后开始播放。</param>
        /// <param name="loopIntervalTimeRange">循环间隔时间（秒）：仅当动画为 循环播放 时有效。动画播放完成后，在设定的范围内 随机一次 间隔时间。</param>
        public AnimData
        (
            AnimationReferenceAsset animRefAsset,
            int animTrack = 0,
            bool isLoop = false,
            bool isReverse = false,
            float speed = 1f,
            float startDelayTime = 0f,
            Vector2 loopIntervalTimeRange = default
        )
        {
            // 设置动画数据
            this.animRefAsset = animRefAsset;
            this._animTrack = animTrack;
            this.isLoop = isLoop;
            this.isReverse = isReverse;
            this.speed = speed;
            this.startDelayTime = startDelayTime;
            this.loopIntervalTimeRange = loopIntervalTimeRange;
        }
#endif
    }
}
