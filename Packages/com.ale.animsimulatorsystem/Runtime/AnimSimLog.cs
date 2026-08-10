using System.Runtime.CompilerServices;
using UnityEngine;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 包内统一的日志出口，格式固定为 <c>[类名] 方法名: 内容</c>。
    ///
    /// <para><b>为什么要收在一处</b>：改造前包内三种前缀并存——基类用 <c>{GetType().Name} &gt;&gt; </c>、
    /// 两个后端用<b>硬编码的类名</b>（类改名后不会跟着变，也丢掉了多态类型）、其余四十余处用
    /// <c>[类名] 方法名:</c>。其中还有五处前缀写的是别的类名、两处写的方法名早已不存在。</para>
    ///
    /// <para><b>方法名由 <see cref="CallerMemberNameAttribute"/> 自动取</b>，调用方不必手写——
    /// 这从结构上消除了「方法改名后日志里仍是旧名」这类错误。类名取自 <c>context</c> 的<b>运行期</b>
    /// 类型，因此基类里打的日志会显示实际的子类名。</para>
    ///
    /// <para><c>context</c> 一律传 <c>this</c>：控制台里点这条日志就能选中出问题的那个对象。</para>
    /// </summary>
    internal static class AnimSimLog
    {
        /// <summary>警告。<paramref name="context"/> 传 <c>this</c>。</summary>
        public static void Warn(Object context, string message, [CallerMemberName] string member = null)
            => Debug.LogWarning(Compose(context ? context.GetType().Name : nameof(AnimSimulatorSystem), member, message), context);

        /// <summary>警告（无 Unity 对象上下文时用，需自行给出类名）。</summary>
        public static void Warn(string typeName, string message, [CallerMemberName] string member = null)
            => Debug.LogWarning(Compose(typeName, member, message));

        /// <summary>普通信息。<paramref name="context"/> 传 <c>this</c>。</summary>
        public static void Info(Object context, string message, [CallerMemberName] string member = null)
            => Debug.Log(Compose(context ? context.GetType().Name : nameof(AnimSimulatorSystem), member, message), context);

        private static string Compose(string typeName, string member, string message)
            => string.IsNullOrEmpty(member)
                ? $"[{typeName}] {message}"
                : $"[{typeName}] {member}: {message}";
    }
}
