using System.Collections.Generic;
using Ale.Toolkit.Editor;

namespace Ale.AnimSimulatorSystem.Editor
{
    /// <summary>
    /// 动画模拟器系统 自有编译宏（<c>ASS_</c> = AnimSimulatorSystem）的中枢：宏名常量、对应的动画运行时、
    /// 宏启用状态 / 运行时安装状态检测，以及旧宏名（<c>HAS_SPINE</c>）→ 新宏名（<c>ASS_SPINE</c>）的迁移映射。
    ///
    /// <para>这两个宏决定<b>本插件支持哪些动画后端</b>，与 toolkit 的 <c>ATK_*</c>（项目级可选依赖）平级但各管各的：
    /// <c>ATK_*</c> 由 toolkit 欢迎窗口管理，<c>ASS_*</c> 由本包的
    /// <see cref="AnimSimulatorWelcomeWindow"/> 管理。宏的增删统一经 toolkit 的 <see cref="DefineUtils"/>。</para>
    ///
    /// <para><b>两个宏可以同时启用</b>：一个工程里 Spine 与 Live2D 角色并存，具体用哪个后端由角色预制体上
    /// 挂的是 <c>SpineAnimator</c> 还是 <c>Live2DAnimator</c> 决定。</para>
    /// </summary>
    public static class AnimSimulatorDefines
    {
        // ── 宏名常量 ──────────────────────────────────────────────────────────────
        /// <summary>Spine 支持宏。启用后 <c>SpineAnimator</c> 参与编译，可播放 Spine 动画与换装。</summary>
        public const string Spine = "ASS_SPINE";
        /// <summary>Live2D 支持宏。启用后 <c>Live2DAnimator</c> 参与编译，可播放 Cubism 动作与换装。</summary>
        public const string Live2D = "ASS_LIVE2D";

        // ── 依赖的动画运行时 ───────────────────────────────────────────────────────
        /// <summary>Spine Unity 运行时的 UPM 包名（git URL 分发，不在 UPM 注册表）。</summary>
        public const string PackageSpine = "com.esotericsoftware.spine.spine-unity";
        /// <summary>
        /// Live2D Cubism SDK for Unity。<b>它不是 UPM 包</b>——官方以 <c>.unitypackage</c> 分发
        /// （含专有的 Cubism Core 原生库），导入后落在 <c>Assets/Live2D/Cubism/</c>。
        /// </summary>
        public const string PackageLive2D = "Live2D Cubism SDK for Unity";

        /// <summary>spine-unity 官方安装说明页（git URL 的取法、与 Spine 编辑器版本的对应关系）。</summary>
        public const string UrlSpineUnityInstall = "https://esotericsoftware.com/spine-unity-installation";

        /// <summary>Live2D Cubism SDK for Unity 官方下载页。</summary>
        public const string UrlLive2DDownload = "https://www.live2d.com/en/sdk/download/unity/";

        // ── 探测用命名空间 ──────────────────────────────────────────────────────────
        // Spine 与 Cubism 的程序集都独立于本插件的宏编译，故命名空间存在即代表运行时已安装。
        private const string NsSpine  = "Spine.Unity";
        private const string NsLive2D = "Live2D.Cubism";

        /// <summary>
        /// 旧宏名 → 新宏名 迁移映射。2.0.0 从 Fs 框架迁到 Ale Toolkit 时把 <c>HAS_SPINE</c> 改名为
        /// <c>ASS_SPINE</c>，但老工程的 Player Settings 里仍是旧名——旧名不再被任何代码引用，
        /// 于是 Spine 代码会静默地整段编译不进去。由 <see cref="AnimSimulatorDefineChecker"/> 在加载时一次性改写。
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> LegacyRename =
            new Dictionary<string, string>
            {
                { "HAS_SPINE", Spine },
            };

        // ── 宏启用状态 ──────────────────────────────────────────────────────────────
        /// <summary>当前是否启用了 <see cref="Spine"/> 宏。</summary>
        public static bool IsSpineEnabled() => ToolkitDefines.IsDefineEnabled(Spine);

        /// <summary>当前是否启用了 <see cref="Live2D"/> 宏。</summary>
        public static bool IsLive2DEnabled() => ToolkitDefines.IsDefineEnabled(Live2D);

        // ── 运行时安装状态 ─────────────────────────────────────────────────────────
        /// <summary>Spine Unity 运行时（<c>Spine.Unity</c> 命名空间）是否可用。</summary>
        public static bool IsSpinePackageInstalled() => DefineUtils.HasNamespace(NsSpine);

        /// <summary>Live2D Cubism SDK（<c>Live2D.Cubism</c> 命名空间）是否可用。</summary>
        public static bool IsLive2DPackageInstalled() => DefineUtils.HasNamespace(NsLive2D);
    }
}
