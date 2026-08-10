using UnityEngine;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 标记一个 <c>string</c> 字段（或字符串数组的元素）是<b>皮肤名</b>，
    /// 使其在 Inspector 中显示为下拉选择而非手打文本。
    ///
    /// <para>下拉的候选来自角色实际挂着的动画控制器：<c>SpineAnimator</c> 取骨架数据里的皮肤名，
    /// <c>Live2dAnimator</c> 取自身配置的皮肤定义。取不到候选时（未指定控制器、骨架数据缺失等）
    /// 自动退化为普通文本框，不会挡住手工填写。</para>
    ///
    /// <para>它替代了原先直接挂在这些字段上的 Spine 专有特性 <c>[SpineSkin]</c>——
    /// 那个特性会给 Live2D 角色也弹出 Spine 的皮肤下拉，两个后端同时启用后就不成立了。</para>
    /// </summary>
    public class AnimSkinNameAttribute : PropertyAttribute
    {
    }
}
