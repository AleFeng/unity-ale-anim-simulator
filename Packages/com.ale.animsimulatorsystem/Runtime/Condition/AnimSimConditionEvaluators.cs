using System.Collections.Generic;
using Ale.Condition;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 条件判定所需的读侧数据源，由 <see cref="AnimSimulatorManager"/> 实现。
    ///
    /// <para>判定器只认这个接口，不认管理器本身——这样把「静态配置（条件参数）」与
    /// 「运行期状态（进度条读数）」分在两侧，判定器可脱离场景单测。</para>
    /// </summary>
    public interface IAnimSimConditionSource
    {
        /// <summary>取某条等级进度条的当前等级。名称查不到、或那条不是等级进度条时返回 <c>false</c>。</summary>
        bool TryGetLevel(string progressName, out int level);

        /// <summary>取某条进度条的当前进度值。名称查不到时返回 <c>false</c>。</summary>
        bool TryGetProgressValue(string progressName, out float value);
    }

    /// <summary>
    /// 判定器：等级进度条的<b>等级</b>与给定值比较。键 <c>AnimSim.LevelProgress</c>。
    ///
    /// <para>取代 2.2.0 之前写死在 <c>AnimAction</c> 里的那段判定——那段只有「大于等于」一种比较，
    /// 且解析不出参数、取不到管理器、查不到进度条时一律<b>判为满足</b>（失败即开），
    /// 于是配错名字的条件会静默失效、动作凭空解锁。这里一律失败即关。</para>
    /// </summary>
    [ConditionEvaluator("AnimSim.LevelProgress")]
    public sealed class AnimSimLevelProgressEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("progress", ConditionParamType.String, false, "等级进度条名称"),
            ConditionCompare.CreateOpParam(),
            new ConditionParamDef("level",    ConditionParamType.Int,    false, "等级"),
        };

        /// <inheritdoc/>
        public string Key => "AnimSim.LevelProgress";
        /// <inheritdoc/>
        public string DisplayName => "等级进度条-等级";
        /// <inheritdoc/>
        public string Category => "动画模拟器";
        /// <inheritdoc/>
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        /// <inheritdoc/>
        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var source = ctx?.GetService<IAnimSimConditionSource>();
            if (source == null) return false;

            string progressName = parameters.Find("progress")?.GetString();
            if (string.IsNullOrEmpty(progressName)) return false;

            if (!source.TryGetLevel(progressName, out int level))
            {
                AnimSimConditionWarn.UnknownProgress(nameof(AnimSimLevelProgressEvaluator), progressName, "等级进度条");
                return false;
            }

            long required = parameters.Find("level")?.GetInt() ?? 0L;
            int op = ConditionCompare.ReadOp(parameters);
            // level 是 int、required 是 long —— 绑到 Compare(long, long, int) 的精确重载。
            // 此前走的是浮点重载 + 1e-6 容差；对整数而言 |a-b| < 1e-6 与 a == b 等价，结果不变。
            return ConditionCompare.Compare(level, required, op);
        }
    }

    /// <summary>
    /// 判定器：进度条的<b>当前进度值</b>与给定值比较。键 <c>AnimSim.ActionProgress</c>。
    /// <para>等级条与动作条都适用——比的是进度值本身，不是等级。</para>
    /// </summary>
    [ConditionEvaluator("AnimSim.ActionProgress")]
    public sealed class AnimSimProgressValueEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("progress", ConditionParamType.String, false, "进度条名称"),
            ConditionCompare.CreateOpParam(),
            new ConditionParamDef("value",    ConditionParamType.Float,  false, "进度值"),
        };

        /// <inheritdoc/>
        public string Key => "AnimSim.ActionProgress";
        /// <inheritdoc/>
        public string DisplayName => "进度条-进度值";
        /// <inheritdoc/>
        public string Category => "动画模拟器";
        /// <inheritdoc/>
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        /// <inheritdoc/>
        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var source = ctx?.GetService<IAnimSimConditionSource>();
            if (source == null) return false;

            string progressName = parameters.Find("progress")?.GetString();
            if (string.IsNullOrEmpty(progressName)) return false;

            if (!source.TryGetProgressValue(progressName, out float value))
            {
                AnimSimConditionWarn.UnknownProgress(nameof(AnimSimProgressValueEvaluator), progressName, "进度条");
                return false;
            }

            double required = parameters.Find("value")?.GetFloat() ?? 0d;
            int op = ConditionCompare.ReadOp(parameters);
            // value 是 float —— 绑到浮点重载，容差取默认的 1e-6，与迁移前一致。
            return ConditionCompare.Compare(value, required, op);
        }
    }

    /// <summary>
    /// 两个判定器共用的「查无此进度条」告警，按名去重。
    ///
    /// <para><b>为什么这条值得单独报</b>：判定器一律「失败即关」——查不到进度条就判不满足。
    /// 这是刻意的设计（免得配错的条件静默失效、动作凭空解锁），但它同时意味着<b>名字打错与
    /// 条件真的不满足，表现完全一样</b>：动作就是不出现在列表里，没有任何其它线索。
    /// 报出来才能把「配错了」和「还没达标」区分开。</para>
    ///
    /// <para><b>必须去重</b>：条件在每次进度条读数变化时都会重新求值，不去重会刷屏。
    /// 去重表随程序域存活，域重载（改代码、进退播放模式）后重新计数。</para>
    /// </summary>
    internal static class AnimSimConditionWarn
    {
        private static readonly HashSet<string> Warned = new HashSet<string>();

        /// <summary>就某个判定器引用了不存在的进度条名告警一次。</summary>
        public static void UnknownProgress(string evaluatorName, string progressName, string kindLabel)
        {
            if (!Warned.Add(evaluatorName + "|" + progressName)) return;
            AnimSimLog.Warn(evaluatorName,
                $"条件引用的{kindLabel} '{progressName}' 不存在，本条件按「不满足」处理——" +
                $"该动作不会出现在动画动作列表里。请核对条件参数与 AnimSimulatorConfig 里的进度条名称是否一致。" +
                $"（同名只报一次）");
        }
    }
}
