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

    /// <summary>本包判定器共用的比较符。下拉里存的是索引，取值与 <see cref="Labels"/> 一一对应。</summary>
    internal static class AnimSimCompare
    {
        public const int Greater        = 0; // 大于
        public const int GreaterOrEqual = 1; // 大于等于
        public const int Equal          = 2; // 等于
        public const int LessOrEqual    = 3; // 小于等于
        public const int Less           = 4; // 小于

        public static readonly string[] Labels = { "大于", "大于等于", "等于", "小于等于", "小于" };

        public static bool Eval(double value, double amount, int op)
        {
            switch (op)
            {
                case Greater:        return value >  amount;
                case GreaterOrEqual: return value >= amount;
                case Equal:          return System.Math.Abs(value - amount) < 1e-6;
                case LessOrEqual:    return value <= amount;
                case Less:           return value <  amount;
                default:             return value >= amount;
            }
        }
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
            new ConditionParamDef("op",       ConditionParamType.Int,    false, "比较", null, AnimSimCompare.Labels),
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

            if (!source.TryGetLevel(progressName, out int level)) return false;

            long required = parameters.Find("level")?.GetInt() ?? 0L;
            int op = (int)(parameters.Find("op")?.GetInt() ?? AnimSimCompare.GreaterOrEqual);
            return AnimSimCompare.Eval(level, required, op);
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
            new ConditionParamDef("op",       ConditionParamType.Int,    false, "比较", null, AnimSimCompare.Labels),
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

            if (!source.TryGetProgressValue(progressName, out float value)) return false;

            double required = parameters.Find("value")?.GetFloat() ?? 0d;
            int op = (int)(parameters.Find("op")?.GetInt() ?? AnimSimCompare.GreaterOrEqual);
            return AnimSimCompare.Eval(value, required, op);
        }
    }
}
