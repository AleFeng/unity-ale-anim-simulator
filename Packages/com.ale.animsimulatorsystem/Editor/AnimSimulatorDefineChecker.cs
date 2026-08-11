using UnityEditor;
using UnityEngine;

namespace Ale.AnimSimulatorSystem.Editor
{
    /// <summary>
    /// 动画模拟器系统 编辑器加载检查器（<c>[InitializeOnLoad]</c>）。每次启动 / 域重载后延迟执行两件事：
    /// ① <b>运行时 / 宏一致性提示</b>：开了宏却没装对应动画运行时时在 Console 警告；
    /// ② <b>首次自动弹窗</b>：本会话尚未显示过欢迎窗口且未禁用自动显示时，弹出
    ///    <see cref="AnimSimulatorWelcomeWindow"/>。
    ///
    /// <para><b>只提示，绝不改写 PlayerSettings。</b><c>ASS_*</c> 的增删一律由用户经
    /// <see cref="AnimSimulatorWelcomeWindow"/> 显式操作——自动改写会与别的插件对同名宏的管理逻辑
    /// 互相覆盖（例如 Fs 框架会按 Spine 命名空间存在与否写 <c>HAS_SPINE</c>），每次写入触发一次重编译，
    /// 编辑器会陷入「Compiling Scripts」死循环。<c>HAS_SPINE</c> 归其定义方管，本包不干涉。</para>
    ///
    /// <para><c>ATK_*</c> 宏属项目级全局设定，其一致性检查由 toolkit 的 <c>ToolkitDefineChecker</c>
    /// 负责，本类只管本插件自有的 <c>ASS_*</c>。</para>
    /// </summary>
    [InitializeOnLoad]
    public static class AnimSimulatorDefineChecker
    {
        static AnimSimulatorDefineChecker()
        {
            // 延迟到编辑器完全就绪后执行，避免在域初始化期间操作 PlayerSettings / UI。
            EditorApplication.delayCall += OnDelayedInit;
        }

        private static void OnDelayedInit()
        {
            EditorApplication.delayCall -= OnDelayedInit;

            CheckRuntimeConsistency();
            CheckWelcomeWindow();
        }

        /// <summary>动画运行时 / 宏一致性检查（仅提示，不自动修改）。</summary>
        private static void CheckRuntimeConsistency()
        {
            if (AnimSimulatorDefines.IsSpineEnabled() && !AnimSimulatorDefines.IsSpinePackageInstalled())
                Debug.LogWarning(
                    $"[Anim Simulator System] Spine 宏 '{AnimSimulatorDefines.Spine}' 已启用，但未检测到 " +
                    $"{AnimSimulatorDefines.PackageSpine}。\n" +
                    "SpineAnimator 将无法编译。请通过 Package Manager 安装 Spine Unity 运行时，" +
                    "或在欢迎窗口中关闭该宏。\n（Tools > Ale Toolkit > Anim Simulator System > Welcome）");

            if (AnimSimulatorDefines.IsLive2DEnabled() && !AnimSimulatorDefines.IsLive2DPackageInstalled())
                Debug.LogWarning(
                    $"[Anim Simulator System] Live2D 宏 '{AnimSimulatorDefines.Live2D}' 已启用，但未检测到 " +
                    $"{AnimSimulatorDefines.PackageLive2D}。\n" +
                    "Live2DAnimator 将无法编译。它不是 UPM 包，需从官网下载 .unitypackage 导入，" +
                    "或在欢迎窗口中关闭该宏。\n（Tools > Ale Toolkit > Anim Simulator System > Welcome）");

            // 两个后端都没开：插件的动画播放能力整体不可用，值得提示一次。
            if (!AnimSimulatorDefines.IsSpineEnabled() && !AnimSimulatorDefines.IsLive2DEnabled())
                Debug.LogWarning(
                    "[Anim Simulator System] 尚未启用任何动画后端宏" +
                    $"（'{AnimSimulatorDefines.Spine}' / '{AnimSimulatorDefines.Live2D}'）。\n" +
                    "角色动画将无法播放。请在欢迎窗口中按实际使用的动画运行时启用对应的宏。\n" +
                    "（Tools > Ale Toolkit > Anim Simulator System > Welcome）");
        }

        /// <summary>判断是否需要自动弹出欢迎窗口并弹出。</summary>
        private static void CheckWelcomeWindow()
        {
            // 本会话已经显示过则跳过（SessionState 在重启 Unity 后重置）。
            if (SessionState.GetBool(AnimSimulatorEditorPrefs.WelcomeShownThisSession, false))
                return;

            SessionState.SetBool(AnimSimulatorEditorPrefs.WelcomeShownThisSession, true);

            // 用户禁用了自动显示则跳过。
            if (!EditorPrefs.GetBool(AnimSimulatorEditorPrefs.WelcomeAutoShow, true))
                return;

            AnimSimulatorWelcomeWindow.Open();
        }
    }
}
