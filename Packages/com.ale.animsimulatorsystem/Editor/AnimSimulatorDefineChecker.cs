using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.AnimSimulatorSystem.Editor
{
    /// <summary>
    /// 动画模拟器系统 编辑器加载检查器（<c>[InitializeOnLoad]</c>）。每次启动 / 域重载后延迟执行三件事：
    /// ① <b>旧宏迁移</b>：把老工程里仍启用的 <c>HAS_SPINE</c> 一次性改写为 <c>ASS_SPINE</c>；
    /// ② <b>运行时 / 宏一致性提示</b>：开了宏却没装对应动画运行时时在 Console 警告（仅提示，不强制修改）；
    /// ③ <b>首次自动弹窗</b>：本会话尚未显示过欢迎窗口且未禁用自动显示时，弹出
    ///    <see cref="AnimSimulatorWelcomeWindow"/>。
    ///
    /// <para><c>ATK_*</c> 宏属项目级全局设定，其迁移与一致性检查由 toolkit 的 <c>ToolkitDefineChecker</c>
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

            // 迁移确实改写了宏：PlayerSettings 变更会触发重新编译与域重载，本轮到此为止，
            // 一致性检查与欢迎窗口留到重载后的下一轮——此时读到的宏状态才是最终的。
            if (MigrateLegacyDefines()) return;

            CheckRuntimeConsistency();
            CheckWelcomeWindow();
        }

        /// <summary>
        /// 把旧宏名迁移为新宏名：对每个仍启用的旧宏，补上对应新宏并移除旧宏。
        /// </summary>
        /// <returns>是否确实改写了宏（改写会触发重新编译）。</returns>
        private static bool MigrateLegacyDefines()
        {
            bool changed = false;
            foreach (var kv in AnimSimulatorDefines.LegacyRename)
            {
                string legacy = kv.Key;
                string modern = kv.Value;
                if (!ToolkitDefines.IsDefineEnabled(legacy)) continue;      // 旧宏未启用：无需迁移

                if (!ToolkitDefines.IsDefineEnabled(modern))
                    DefineUtils.ApplyDefine(true, modern);                  // 补上新宏
                DefineUtils.ApplyDefine(false, legacy);                     // 移除旧宏
                changed = true;

                Debug.Log($"[Anim Simulator System] 已迁移旧宏 '{legacy}' → '{modern}'。");
            }
            return changed;
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
