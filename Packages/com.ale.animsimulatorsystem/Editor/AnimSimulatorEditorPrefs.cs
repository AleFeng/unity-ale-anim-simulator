namespace Ale.AnimSimulatorSystem.Editor
{
    /// <summary>
    /// 动画模拟器系统 编辑器偏好设置的键名常量。所有 <c>EditorPrefs</c> / <c>SessionState</c> 的读写
    /// 统一经由此类，避免键名散落各处。
    ///
    /// <para>编辑器界面语言与 <c>ATK_*</c> 可选依赖宏是<b>项目级全局设定</b>，由 toolkit 统一管理
    /// （<c>Tools &gt; Ale Toolkit &gt; Welcome</c>），本类不持有。</para>
    /// </summary>
    public static class AnimSimulatorEditorPrefs
    {
        /// <summary>欢迎窗口是否在启动时自动显示（<c>EditorPrefs</c>，每人自定，默认 true）。</summary>
        public const string WelcomeAutoShow = "ASS_WelcomeAutoShow";

        /// <summary>欢迎窗口本会话是否已显示过（<c>SessionState</c> 键，重启 Unity 后重置）。</summary>
        public const string WelcomeShownThisSession = "ASS_WelcomeShownThisSession";
    }
}
