using System;
using System.Collections.Generic;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 轨道<b>序数</b>换算：把 <see cref="EAnimTrack"/> 的主轨道值映射成从 0 起、连续且<b>保序</b>的秩。
    ///
    /// <para><b>为什么需要它</b>：本系统的轨道号是「主轨道值 × 10 + 子轨道」，而 <c>Action = 900</c>、
    /// <c>Other = 999</c>，算出来的轨道号高达 9000..9990。两个后端都无法直接消化这么稀疏的编号——
    /// Spine 会把内部 <c>tracks</c> 列表 Resize 到 <c>trackIndex + 1</c> 并逐帧全量遍历，
    /// Cubism 的层数则只有个位数。两边都需要先压到一个紧凑的区间里。</para>
    ///
    /// <para><b>为什么必须保序</b>：两个后端的「谁盖谁」都由编号大小决定——Spine 按轨道号升序 Apply、
    /// 高轨道覆盖低轨道；Cubism 的层混合器同样是层号大的后应用。这正是
    /// <see cref="EAnimTrack"/>「枚举值大的轨道覆盖枚举值小的」这条语义所依赖的。
    /// 因此绝不能按「首次使用顺序」发号——那样先播 <c>Action</c> 后播 <c>Body</c>
    /// 会让 <c>Body</c> 反过来盖住 <c>Action</c>，同一份配置还会因播放顺序不同而算出不同的编号。</para>
    ///
    /// <para>取<b>枚举的声明序数</b>即可天然保序：<c>Enum.GetValues</c> 按常量数值升序返回，遍历下标就是秩。
    /// 且 <c>Body</c>..<c>Parts</c>（值 1..18）的序数恰等于其值，故除 <c>Action</c>(900→19) 与
    /// <c>Other</c>(999→20) 外，<b>既有配置的换算都是恒等式</b>。</para>
    ///
    /// <para>换算<b>只发生在各后端与其运行时之间</b>：基类、<c>AnimActionPlayer</c> 与序列化数据一律仍用系统轨道号。</para>
    /// </summary>
    internal static class AnimTrackOrdinal
    {
        // 主轨道值 → 紧凑序数。静态只读：类型初始化时按 EAnimTrack 的声明序数一次建好，此后只读不写。
        private static readonly Dictionary<int, int> MainTrackToOrdinal = BuildMainTrackOrdinals();

        /// <summary>枚举内主轨道的个数，同时也是「枚举之外的主轨道」的序数起点。</summary>
        public static int CountInEnum { get; } = MainTrackToOrdinal.Count;

        /// <summary>留给「枚举之外的主轨道号」的序数槽位数。</summary>
        public const int SlotsOutOfEnum = 8;

        private static Dictionary<int, int> BuildMainTrackOrdinals()
        {
            var map = new Dictionary<int, int>();
            // Enum.GetValues 按枚举常量的数值升序返回，故遍历下标即保序的秩
            foreach (EAnimTrack value in (EAnimTrack[])Enum.GetValues(typeof(EAnimTrack)))
            {
                int main = (int)value;
                if (!map.ContainsKey(main)) map.Add(main, map.Count);
            }
            return map;
        }

        /// <summary>
        /// 取主轨道值对应的序数。
        ///
        /// <para><b>枚举之外的主轨道号</b>（<see cref="AnimData"/> 的构造函数允许直接传任意轨道号）折叠到枚举序数
        /// 之后的 <see cref="SlotsOutOfEnum"/> 个固定槽位里。刻意<b>不</b>按首次使用顺序发号——那样同一份配置会因
        /// 播放顺序不同而算出不同的序数，还需要一张持续增长的可变静态表（所有实例共享，关闭 Domain Reload 时
        /// 更会跨播放会话累积）。保序性对枚举外的轨道本就没有约定，取模折叠不破坏任何已承诺的语义。
        /// 序数上界因此恒定为 <see cref="CountInEnum"/> + <see cref="SlotsOutOfEnum"/> - 1。</para>
        /// </summary>
        /// <param name="mainTrack">主轨道值（= 系统轨道号 / 10）。</param>
        public static int OrdinalOfMainTrack(int mainTrack)
        {
            int ordinal;
            if (MainTrackToOrdinal.TryGetValue(mainTrack, out ordinal)) return ordinal;
            return CountInEnum + mainTrack % SlotsOutOfEnum;
        }

        /// <summary>取系统轨道号（主轨道值 × 10 + 子轨道）所属主轨道的序数。负数原样返回（无效轨道）。</summary>
        public static int OrdinalOfTrack(int trackIndex)
            => trackIndex < 0 ? trackIndex : OrdinalOfMainTrack(trackIndex / 10);
    }
}
