using Ale.Toolkit.Runtime;
using Ale.Toolkit.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// UI显示配置 面板：画面右下角一个眼睛按钮，点开是一列配置项，再点收起。
    ///
    /// <para>三条配置写死（UI / 操作点 / 操作提示），各自对应明确的代码路径。
    /// 由 <see cref="AnimSimulatorManager"/> 在 <c>Init()</c> 里按配置资产上的预制体实例化。</para>
    ///
    /// <para><b>面板自己在受控 Canvas 内部</b>：关掉「UI」时它会跟着一起消失，这是刻意的——
    /// 关 UI 的目的就是把画面让出来。恢复靠点击屏幕任意处（见
    /// <c>AnimSimulatorManager.OnLeftClick</c>），不需要面板留在画面上。</para>
    /// </summary>
    public class UIDisplayConfigPanel : MonoBehaviour
    {
        [Header("基础设置")]
        [Tooltip("展开 / 收起 按钮（眼睛图标）")]
        [SerializeField] private Button btnToggle;
        [Tooltip("配置列表 根节点：展开 / 收起时缩放")]
        [SerializeField] private RectTransform listRoot;
        [Tooltip("配置列表 CanvasGroup：收起时 alpha 归零，并停止接收射线")]
        [SerializeField] private CanvasGroup listCanvasGroup;

        [Header("配置条目")]
        [Tooltip("UI：直接控制根 Canvas 的显示与透明度")]
        [SerializeField] private UIDisplayConfigRow rowUi;
        [Tooltip("操作点：所有动作列表 UI 的点击提示圈")]
        [SerializeField] private UIDisplayConfigRow rowClickTip;
        [Tooltip("操作提示：所有动作列表 UI 的按下手指提示")]
        [SerializeField] private UIDisplayConfigRow rowOperationTip;

        // 展开 / 收起 时长（秒）
        private const float ToggleDuration = 0.2f;
        // 收起态的列表缩放。不缩到 0——留一点体积，展开时的弹出感更明显。
        private const float CollapsedScale = 0.85f;

        private bool _isExpanded;

        /// <summary>配置列表当前是否展开。</summary>
        public bool IsExpanded => _isExpanded;

        private void Awake()
        {
            if (btnToggle) btnToggle.onClick.AddListener(OnBtnToggleClick);

            if (rowUi) rowUi.onValueChanged = OnRowUiValueChanged;
            if (rowClickTip) rowClickTip.onValueChanged = OnRowClickTipValueChanged;
            if (rowOperationTip) rowOperationTip.onValueChanged = OnRowOperationTipValueChanged;

            // 初始收起。不走补间，免得开局闪一下。
            ApplyExpanded(false, animate: false);
        }

        private void OnEnable()
        {
            AnimSimulatorManager.OnUiDisplayConfigChanged += OnUiDisplayConfigChanged;
            PullFromManager();
        }

        private void OnDisable()
        {
            AnimSimulatorManager.OnUiDisplayConfigChanged -= OnUiDisplayConfigChanged;

            // 面板随 Canvas 一起被关掉时，输入独占标志必须撤掉——否则角色就永远点不动了。
            var manager = AnimSimulatorManager.Instance;
            if (manager) manager.IsUiCapturingInput = false;
        }

        /// <summary>收起配置列表。</summary>
        public void Collapse()
        {
            if (_isExpanded) ApplyExpanded(false, animate: true);
        }

        /// <summary>
        /// 屏幕上发生了一次点击：落在面板<b>之外</b>就收起配置列表。「点空白处关掉浮层」是这类面板的通用预期。
        ///
        /// <para>由 <see cref="AnimSimulatorManager"/> 在左键按下时调用——它直接吃 Input System 的事件、
        /// 不经 GraphicRaycaster，所以点在哪里都收得到，包括没有任何 UI 的空白处。</para>
        ///
        /// <para><b>眼睛按钮算「面板之内」</b>：否则点它会与按钮自己的 onClick 打架——
        /// 一边收起一边又切换，净效果取决于两者的执行先后，表现为「按钮时灵时不灵」。</para>
        /// </summary>
        /// <param name="screenPos">点击的屏幕坐标。</param>
        /// <returns>是否因此收起了列表。为 <c>true</c> 时调用方应把这次点击<b>消费掉</b>，不再派发给角色。</returns>
        public bool TryCollapseByOutsideClick(Vector2 screenPos)
        {
            if (!_isExpanded) return false;
            if (IsInsidePanel(screenPos)) return false;

            ApplyExpanded(false, animate: true);
            return true;
        }

        /// <summary>屏幕坐标是否落在眼睛按钮或配置列表的矩形内。</summary>
        private bool IsInsidePanel(Vector2 screenPos)
        {
            // Overlay 画布必须传 null 相机；其余渲染模式取画布自己的相机。
            var canvas = UIUtility.ResolveRootCanvas(this);
            Camera cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (btnToggle)
            {
                var rtButton = btnToggle.transform as RectTransform;
                if (rtButton && RectTransformUtility.RectangleContainsScreenPoint(rtButton, screenPos, cam))
                    return true;
            }

            return listRoot && RectTransformUtility.RectangleContainsScreenPoint(listRoot, screenPos, cam);
        }

        private void OnBtnToggleClick()
        {
            ApplyExpanded(!_isExpanded, animate: true);
            // 展开的那一刻回填一次：设置可能被别处改过（比如「点击恢复 UI」把 UI 开关翻了回来）。
            if (_isExpanded) PullFromManager();
        }

        /// <summary>
        /// 应用展开 / 收起状态。
        /// </summary>
        /// <param name="isExpanded">是否展开。</param>
        /// <param name="animate">是否走补间。初始化时为假，避免开局闪一下。</param>
        private void ApplyExpanded(bool isExpanded, bool animate)
        {
            _isExpanded = isExpanded;

            if (listCanvasGroup)
            {
                ToolkitTween.Kill(listCanvasGroup);
                float alpha = isExpanded ? 1f : 0f;
                if (animate) ToolkitTween.FadeCanvasGroup(listCanvasGroup, alpha, ToggleDuration);
                else listCanvasGroup.alpha = alpha;

                // 收起后不再吃射线，免得一块看不见的面板挡住底下的东西。
                listCanvasGroup.interactable = isExpanded;
                listCanvasGroup.blocksRaycasts = isExpanded;
            }

            if (listRoot)
            {
                ToolkitTween.Kill(listRoot);
                var scale = Vector3.one * (isExpanded ? 1f : CollapsedScale);
                if (animate) ToolkitTween.ScaleTransform(listRoot, scale, ToggleDuration);
                else listRoot.localScale = scale;
            }

            // 展开期间独占输入：本系统的悬停判定是物理射线、不经 GraphicRaycaster，
            // 点开关 / 拖滑条会穿透到后面的角色身上。详见 AnimSimulatorManager.IsUiCapturingInput。
            var manager = AnimSimulatorManager.Instance;
            if (manager) manager.IsUiCapturingInput = isExpanded;
        }

        /// <summary>从管理器回读三条配置，填进控件。</summary>
        private void PullFromManager()
        {
            var manager = AnimSimulatorManager.Instance;
            // 管理器尚未 Awake（Instance 不做惰性创建）时什么都不填，等它的第一次广播来补。
            if (!manager) return;

            if (rowUi) rowUi.SetValue(manager.UiDisplayOn, manager.UiDisplayAlpha);
            if (rowClickTip) rowClickTip.SetValue(manager.ClickTipDisplayOn, manager.ClickTipDisplayAlpha);
            if (rowOperationTip) rowOperationTip.SetValue(manager.OperationTipDisplayOn, manager.OperationTipDisplayAlpha);
        }

        /// <summary>
        /// 显示配置被改动（可能来自本面板，也可能来自「点击恢复 UI」那条路径）：回填控件。
        /// </summary>
        private void OnUiDisplayConfigChanged()
        {
            PullFromManager();

            // 玩家把 UI 关掉了：顺手收起列表。这样恢复之后画面上只剩右下角一只眼睛，干净。
            // 不走补间——Canvas 下一刻就整个 SetActive(false) 了，补间没机会跑完。
            var manager = AnimSimulatorManager.Instance;
            if (manager && !manager.UiDisplayOn && _isExpanded)
                ApplyExpanded(false, animate: false);
        }

        private void OnRowUiValueChanged(UIDisplayConfigRow row, bool isOn, float alpha)
        {
            var manager = AnimSimulatorManager.Instance;
            if (manager) manager.SetUiDisplay(isOn, alpha);
        }

        private void OnRowClickTipValueChanged(UIDisplayConfigRow row, bool isOn, float alpha)
        {
            var manager = AnimSimulatorManager.Instance;
            if (manager) manager.SetClickTipDisplay(isOn, alpha);
        }

        private void OnRowOperationTipValueChanged(UIDisplayConfigRow row, bool isOn, float alpha)
        {
            var manager = AnimSimulatorManager.Instance;
            if (manager) manager.SetOperationTipDisplay(isOn, alpha);
        }
    }
}
