using System;
using UnityEngine;

#if ATK_LOCALIZATION
using UnityEngine.Localization.Settings;
#endif

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 语言切换广播。
    ///
    /// <para>本包的展示文本改用 <c>TextValue</c> 之后，取文本是一次性的
    /// （<c>ResolveText()</c> 当场求值），不像 <c>LocalizeStringEvent</c> 那样自带「语言变了就重刷」。
    /// 这里把该能力补回来：<see cref="OnLocaleChanged"/> 在运行期语言切换时触发，各 UI 组件订阅后重取一次即可。</para>
    ///
    /// <para><b>全包唯一一处 <c>ATK_LOCALIZATION</c> 条件编译。</b>宏关闭时本事件永不触发
    /// （没有本地化就无所谓语言切换），订阅方无需分支。</para>
    /// </summary>
    public static class AnimLocale
    {
        /// <summary>运行期语言发生切换。宏关闭时永不触发。</summary>
        public static event Action OnLocaleChanged;

#if ATK_LOCALIZATION
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // 先减后加，避免「不重载域进入播放模式」时重复订阅
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
        }

        private static void HandleSelectedLocaleChanged(UnityEngine.Localization.Locale locale)
        {
            var handler = OnLocaleChanged;
            if (handler != null) handler.Invoke();
        }
#endif
    }
}
