using System;
using Ale.Toolkit.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// UI显示配置 的一行：从左到右是「功能名称」「显示开关」「透明度滑条」。
    ///
    /// <para>本类只管控件，不知道自己控的是哪个功能——由 <see cref="UIDisplayConfigPanel"/> 接上
    /// <see cref="onValueChanged"/> 后决定。三条配置各自的语义差别很大（一条关 Canvas、一条只关空闲态、
    /// 一条连播放都不播），把它们塞进同一个抽象反而要为每条写分支，故行只做「一个开关 + 一个滑条」这件事。</para>
    /// </summary>
    public class UIDisplayConfigRow : MonoBehaviour
    {
        [Header("基础设置")]
        [Tooltip("功能名称：上面一行直接填纯文本；启用本地化后还会多出一个「本地化」栏可选多语言条目，" +
                 "取不到时自动回退到纯文本。")]
        [SerializeField] private TextValue displayName = new TextValue();
        [Tooltip("功能名称 文本组件")]
        [SerializeField] private TMP_Text txtName;
        [Tooltip("显示开关")]
        [SerializeField] private Toggle toggleShow;
        [Tooltip("透明度滑条。取值上限在 Awake 里强制为 1，下限取下面的「透明度下限」。")]
        [SerializeField] private Slider sliderAlpha;
        [Tooltip("透明度下限：滑条能拖到的最小值，默认 0（可拖到全透明）。\n" +
                 "「UI」那一行必须留一个正的下限——它调到全透明后整个界面（含本面板）就再也看不见了，" +
                 "而「点击任意处恢复」只认显示开关、对透明度不生效。参见 AnimSimulatorManager.UiDisplayAlphaMin。")]
        [Range(0f, 1f)]
        [SerializeField] private float alphaMin;

        /// <summary>本行的值发生变化：(本行, 是否显示, 透明度)。由面板赋值。</summary>
        public Action<UIDisplayConfigRow, bool, float> onValueChanged;

        // 回填控件时抑制回调。写 Toggle.isOn / Slider.value 都会触发各自的 onValueChanged，
        // 不挡住的话「面板打开时回填一次」会被当成玩家改了设置，把刚读出来的值原样再写回去一轮。
        private bool _isApplyingValue;

        /// <summary>当前开关值。控件没接时回落为 true（保持功能开着，不因为漏接引用而把 UI 关掉）。</summary>
        public bool IsOn => !toggleShow || toggleShow.isOn;

        /// <summary>当前透明度。控件没接时回落为 1。</summary>
        public float Alpha => sliderAlpha ? sliderAlpha.value : 1f;

        /// <summary>本行的透明度下限。</summary>
        public float AlphaMin => alphaMin;

        private void Awake()
        {
            if (toggleShow) toggleShow.onValueChanged.AddListener(OnToggleValueChanged);
            if (sliderAlpha)
            {
                sliderAlpha.minValue = Mathf.Clamp01(alphaMin);
                sliderAlpha.maxValue = 1f;
                sliderAlpha.wholeNumbers = false;
                sliderAlpha.onValueChanged.AddListener(OnSliderValueChanged);
            }
        }

        private void OnEnable()
        {
            AnimLocale.OnLocaleChanged += RefreshDisplayName;
            RefreshDisplayName();
        }

        private void OnDisable()
        {
            AnimLocale.OnLocaleChanged -= RefreshDisplayName;
        }

        /// <summary>
        /// 回填控件的显示值。<b>不会</b>触发 <see cref="onValueChanged"/>。
        /// </summary>
        public void SetValue(bool isOn, float alpha)
        {
            _isApplyingValue = true;
            if (toggleShow) toggleShow.isOn = isOn;
            if (sliderAlpha) sliderAlpha.value = Mathf.Clamp(alpha, Mathf.Clamp01(alphaMin), 1f);
            _isApplyingValue = false;
        }

        private void OnToggleValueChanged(bool value) => RaiseValueChanged();

        private void OnSliderValueChanged(float value) => RaiseValueChanged();

        private void RaiseValueChanged()
        {
            if (_isApplyingValue) return;

            var handler = onValueChanged;
            if (handler != null) handler.Invoke(this, IsOn, Alpha);
        }

        /// <summary>
        /// 重取一次显示文本。<see cref="TextValue.ResolveText"/> 是一次性求值，
        /// 故运行期切语言要靠 <see cref="AnimLocale.OnLocaleChanged"/> 重刷。
        /// </summary>
        private void RefreshDisplayName()
        {
            if (!txtName) return;
            txtName.text = displayName != null ? displayName.ResolveText() : string.Empty;
        }
    }
}
