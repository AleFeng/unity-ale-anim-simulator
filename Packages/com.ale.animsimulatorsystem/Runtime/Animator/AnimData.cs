using System;
using UnityEngine;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画状态数据（后端中性的运行期形态）：状态名 → 该状态下要播放的一组动画，以及可选的专用渲染器。
    ///
    /// <para>各后端的授权类型（如 Spine 的 <c>FSpineStateData</c>，其渲染器字段是强类型的
    /// <c>SkeletonAnimation</c>）在 <c>AnimatorBase.EnumerateStateDatas()</c> 里转换成本记录，
    /// 使基类的状态机实现与后端完全无关。<see cref="renderer"/> 为空表示使用默认渲染器。</para>
    /// </summary>
    public struct FAnimStateData
    {
        /// <summary>状态名称。</summary>
        public string stateName;
        /// <summary>该状态专用的渲染器；为空则使用默认渲染器。</summary>
        public Component renderer;
        /// <summary>该状态下要播放的动画列表。</summary>
        public AnimData[] animDatas;
    }

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
        [Tooltip("动画名称：在动画软件中制作时的名称。Spine 与 Live2D 使用相同的命名规则。")]
        public string animName;

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
        /// 解析实际要播放的动画名。未填写 <see cref="animName"/> 时返回 <c>null</c>。
        /// </summary>
        public string ResolveAnimName() => string.IsNullOrEmpty(animName) ? null : animName;

        /// <summary>
        /// 构造函数：初始化动画数据
        /// </summary>
        public AnimData()
        {
            animName = null;
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
        /// <remarks>
        /// 这里的 <paramref name="animTrack"/> 是<b>完整的轨道号</b>（主轨道 * 10 + 子轨道），
        /// 而非 <see cref="EAnimTrack"/> 的枚举值。
        /// </remarks>
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
    }
}
