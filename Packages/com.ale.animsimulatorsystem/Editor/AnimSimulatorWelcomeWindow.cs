using System;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.AnimSimulatorSystem.Editor
{
    /// <summary>
    /// 动画模拟器系统 欢迎窗口。承载本插件的<b>快捷入口</b>与<b>动画后端宏开关</b>
    /// （<c>ASS_SPINE</c> / <c>ASS_LIVE2D</c> / <c>ASS_UNITY_ANIM</c>）。
    /// 每次 Unity 会话启动时自动弹出一次（可通过页脚「启动时自动显示」关闭）。
    /// 通过菜单 <c>Tools &gt; Ale Toolkit &gt; Anim Simulator System &gt; Welcome</c> 手动打开。
    ///
    /// <para>界面语言与 <c>ATK_*</c> 可选依赖宏是<b>项目级全局设定</b>，已统一下沉到 toolkit，
    /// 由本窗口顶部的按钮跳转过去配置。</para>
    /// </summary>
    public class AnimSimulatorWelcomeWindow : EditorWindow
    {
        private const string Version = "2.4.0";

        // 打开时的初始高宽；窗口可手动调整（见 OpenWindow），缩放下限为 MinWindowSize。
        private static readonly Vector2 WindowSize    = new Vector2(540f, 660f);
        private static readonly Vector2 MinWindowSize = new Vector2(500f, 480f);

        // 内部 UI 状态
        private bool _spineEnabled,  _spineInstalled;
        private bool _live2DEnabled, _live2DInstalled;
        // Unity 后端没有「装没装」这一维（引擎内置），故只有启用状态，没有 _unityAnimInstalled
        private bool _unityAnimEnabled;
        private bool _autoShow;
        private bool _initialized;
        private bool _pendingRecompile;
        private Vector2 _scroll;

        // Logo 纹理缓存（从磁盘加载，FilterMode.Point 保持像素锐利）
        private Texture2D _logoTexture;
        private bool _logoLoadAttempted;

        #region 打开窗口

        [MenuItem("Tools/Ale Toolkit/Anim Simulator System/Welcome", priority = 1010)]
        public static void Open()
        {
            OpenWindow();
        }

        private static void OpenWindow()
        {
            var window = GetWindow<AnimSimulatorWelcomeWindow>(true, Tr("Anim Simulator 动画模拟器系统"), true);
            // 只设下限、不锁 max（min==max 会禁止缩放），使高宽可手动调整。
            window.minSize = MinWindowSize;
            window.CenterOnMainWin();
            window.Show();
            window.Focus();
        }

        private void CenterOnMainWin()
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            float x = main.x + (main.width  - WindowSize.x) * 0.5f;
            float y = main.y + (main.height - WindowSize.y) * 0.5f;
            position = new Rect(x, y, WindowSize.x, WindowSize.y);
        }

        #endregion

        #region 生命周期

        private void OnEnable()
        {
            _initialized = false;
            _logoTexture = null;
            _logoLoadAttempted = false;
        }

        private void OnDisable()
        {
            if (_logoTexture)
            {
                DestroyImmediate(_logoTexture);
                _logoTexture = null;
            }
        }

        private void LoadState()
        {
            if (_initialized) return;
            _initialized = true;

            _spineEnabled    = AnimSimulatorDefines.IsSpineEnabled();
            _spineInstalled  = AnimSimulatorDefines.IsSpinePackageInstalled();
            _live2DEnabled   = AnimSimulatorDefines.IsLive2DEnabled();
            _live2DInstalled = AnimSimulatorDefines.IsLive2DPackageInstalled();
            _unityAnimEnabled = AnimSimulatorDefines.IsUnityAnimEnabled();
            _autoShow        = EditorPrefs.GetBool(AnimSimulatorEditorPrefs.WelcomeAutoShow, true);
        }

        #endregion

        #region UI界面

        private void OnGUI()
        {
            LoadState();
            titleContent.text = Tr("Anim Simulator 动画模拟器系统");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            EditorGUILayout.Space(8);

            DrawGlobalSettingsLink();
            DrawSeparator();

            DrawQuickActions();
            DrawSeparator();

            DrawMacroSection();

            EditorGUILayout.EndScrollView();

            DrawSeparator();
            DrawFooter();
        }

        // ── GUIStyle 缓存 ─────────────────────────────────────────────────────────────
        // 这些样式原先都在 OnGUI 里 new 出来——OnGUI 每次重绘都会跑，鼠标在窗口上动一下就是几十次。
        // EditorStyles.* 要等编辑器 GUI 初始化后才可用，故不能写成静态字段初始化器，改为首次绘制时惰性构造。
        private static GUIStyle _styleHeader;
        private static GUIStyle _styleSub;
        private static GUIStyle _styleOk;
        private static GUIStyle _styleWarn;

        private static void EnsureStyles()
        {
            if (_styleHeader != null) return;

            _styleHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            _styleSub = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            _styleOk = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.3f, 0.8f, 0.3f) } };
            _styleWarn = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.9f, 0.7f, 0.2f) } };
        }

        private void DrawHeader()
        {
            EnsureStyles();
            var headerStyle = _styleHeader;
            var subStyle = _styleSub;

            EditorGUILayout.BeginVertical(GUILayout.Height(56));
            GUILayout.FlexibleSpace();

            EditorGUILayout.Space(20);
            var logo = GetLogoTexture();
            if (logo)
            {
                const int displaySize = 128;
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Rect logoRect = GUILayoutUtility.GetRect(
                    displaySize, displaySize,
                    GUILayout.Width(displaySize), GUILayout.Height(displaySize));
                GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit, true);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.LabelField($"Anim Simulator System  v{Version}", headerStyle);
            EditorGUILayout.LabelField(Tr("基于 Spine / Live2D / Unity 动画的 2D 动画模拟器"), subStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 跳转到 Ale Toolkit 欢迎 / 全局设置窗口。界面语言、枚举翻译、<c>ATK_*</c> 可选依赖宏
        /// （TMP / Localization / Addressables / Input System）均为<b>项目级全局设定</b>，在那里配置。
        /// </summary>
        private static void DrawGlobalSettingsLink()
        {
            if (GUILayout.Button(Tr("打开 Ale Toolkit 设置（语言 / 插件宏）"), GUILayout.Height(24)))
                ToolkitWelcomeWindow.Open();
        }

        private static void DrawQuickActions()
        {
            EditorGUILayout.LabelField(Tr("快捷操作"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(Tr("创建配置资产"), GUILayout.Height(28)))
                CreateConfigAsset();

            // 使用文档就是包根的 README——原先那份 Docs~/AnimSimulatorSystem/AnimSimulatorSystem.md
            // 已并入其中，旁边那个「查看 README」按钮因此与本按钮完全重复，一并去掉。
            if (GUILayout.Button(Tr("查看使用文档"), GUILayout.Height(28)))
                OpenDoc("Packages/com.ale.animsimulatorsystem/README.md");

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                Tr("示例场景经 Package Manager > Anim Simulator System > Samples > Import 导入后，" +
                   "位于 Assets/Samples/ 下。"),
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawMacroSection()
        {
            EditorGUILayout.LabelField(Tr("插件支持（编译宏）"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                Tr("按项目实际使用的动画运行时启用。三者可同时启用——一个工程里三种角色并存，" +
                   "具体用哪个后端由角色预制体上挂的是 SpineAnimator、Live2DAnimator 还是 UnityAnimator 决定。" +
                   "Unity 动画后端为引擎内置，无需安装任何外部运行时。"),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            DrawMacroToggle("Spine", AnimSimulatorDefines.Spine, AnimSimulatorDefines.PackageSpine,
                ref _spineEnabled, _spineInstalled,
                Tr("启用后 SpineAnimator 参与编译，可播放 Spine 动画、按皮肤名组合换装。" +
                   "需通过 Package Manager 安装 Spine Unity 运行时（git URL 分发，不在 UPM 注册表中）。"),
                Tr("  ⚠ {0} 未安装（需通过 Package Manager 安装）"),
                Tr("尚未检测到 Spine Unity 运行时。\n启用宏后，SpineAnimator 将无法编译。\n\n确定要继续启用吗？"),
                DrawSpineInstallHint);

            EditorGUILayout.Space(2);

            DrawMacroToggle("Live2D", AnimSimulatorDefines.Live2D, AnimSimulatorDefines.PackageLive2D,
                ref _live2DEnabled, _live2DInstalled,
                Tr("启用后 Live2DAnimator 参与编译，可播放 Cubism 动作、按部件组合换装。"),
                Tr("  ⚠ {0} 未导入（非 UPM 包，需从官网下载 .unitypackage 导入）"),
                Tr("尚未检测到 Live2D Cubism SDK。\n启用宏后，Live2DAnimator 将无法编译。\n\n确定要继续启用吗？"),
                DrawLive2DInstallHint);

            EditorGUILayout.Space(2);

            // runtimeName / missingLabel / warnDialog 对本后端都没有意义：引擎内置，不存在「没装」这一状态。
            // 传 hasRuntimeDependency: false 让状态行与确认弹窗整体让位——渲染一行
            // 「✓ UnityEngine.AnimationModule 已安装」只会误导读者去找一个并不存在的包。
            DrawMacroToggle("Unity Animation", AnimSimulatorDefines.UnityAnim, null,
                ref _unityAnimEnabled, true,
                Tr("启用后 UnityAnimator 参与编译，可用 Unity 自带的 AnimationClip 播放动画、按物体显隐换装。" +
                   "适用于 2D 精灵 / 逐帧动画、3D 模型与 UGUI。"),
                null, null,
                DrawUnityAnimHint, hasRuntimeDependency: false);
        }

        /// <summary>
        /// Spine 宏方块下的安装说明。它虽是 UPM 包，却<b>不在 UPM 注册表中</b>——官方以 git URL 分发，
        /// 只能手动添加，且 URL 末尾的分支号要与导出骨骼数据所用的 Spine 编辑器版本对应。
        /// 这两件事在 Package Manager 里都看不出来，故在此给出说明与官方安装页的入口。
        /// </summary>
        private static void DrawSpineInstallHint()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                Tr("Spine Unity 运行时不在 UPM 注册表中：官方以 git URL 分发，需在 Package Manager 用 " +
                   "Add package from git URL… 手动添加 spine-unity，以及它依赖的 spine-csharp。" +
                   "URL 末尾的分支号须与导出骨骼数据所用的 Spine 编辑器版本一致，" +
                   "否则导入时会报骨骼数据版本不匹配。"),
                EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(Tr("打开 Spine Unity 安装说明页"), GUILayout.Height(22)))
                Application.OpenURL(AnimSimulatorDefines.UrlSpineUnityInstall);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Live2D 宏方块下的安装说明。它<b>不是 UPM 包</b>——官方以 <c>.unitypackage</c> 分发
        /// （含专有的 Cubism Core 原生库），既无法写进 <c>package.json</c> 的 <c>dependencies</c>，
        /// 也无法由 Package Manager 安装，只能手动下载导入。
        /// </summary>
        private static void DrawLive2DInstallHint()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                Tr("Live2D Cubism SDK 不是 UPM 包：官方以 .unitypackage 分发（含专有 Cubism Core 原生库），" +
                   "需从官网下载后拖入工程，导入到 Assets/Live2D/Cubism/。" +
                   "Cubism 5 SDK 自带 Live2D.Cubism 程序集定义，导入后本插件即可自动引用。"),
                EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(Tr("打开 Live2D 官方下载页"), GUILayout.Height(22)))
                Application.OpenURL(AnimSimulatorDefines.UrlLive2DDownload);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Unity 宏方块下的说明。与另两个后端不同，这里<b>没有可跳转的安装页</b>——
        /// <c>Animator</c> / <c>AnimationClip</c> / Playables 都是引擎内置类型。说明要讲的是另外两件
        /// 在 Inspector 上看不出来、却最容易卡住人的事：不需要 AnimatorController，以及动画要登记进查找表。
        /// </summary>
        private static void DrawUnityAnimHint()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                Tr("引擎内置，无需安装任何外部运行时。角色只要有一个 Animator 组件作为输出目标即可，" +
                   "Controller 栏留空——状态机在本系统这一层，再叠一层 AnimatorController 会两边争写同一条动画流" +
                   "（默认会自动置空并告警）。要播放的 AnimationClip 需在 UnityAnimator 的「Unity 动画查找表」" +
                   "中登记，动画名留空则用剪辑的资产名。"),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 动画后端宏开关的绘制。Toggle 操作 PlayerSettings 宏（经 <see cref="DefineUtils.ApplyDefine"/>），
        /// 运行时未安装时勾选会弹确认对话框。
        ///
        /// <para>与 toolkit 版的差异：<paramref name="missingLabel"/> 可定制——Live2D 不是 UPM 包，
        /// 不能沿用「需通过 Package Manager 安装」的措辞。</para>
        /// </summary>
        /// <param name="titleName">显示名（如 "Spine"）。</param>
        /// <param name="define">宏名称。</param>
        /// <param name="runtimeName">依赖的动画运行时名称，填入状态行的 {0}。</param>
        /// <param name="enabled">当前启用状态，随勾选就地更新。</param>
        /// <param name="runtimeInstalled">运行时是否已安装。</param>
        /// <param name="description">功能说明。</param>
        /// <param name="missingLabel">未安装时的状态行模板（含 {0}）。</param>
        /// <param name="warnDialog">未安装却勾选时的确认对话框正文。</param>
        /// <param name="drawAdditionalFields">宏方块下的额外内容（如安装说明）。</param>
        /// <param name="hasRuntimeDependency">
        /// 该后端是否依赖外部运行时。<c>false</c>（Unity 内置动画）时<b>整块跳过安装状态行与启用前的确认弹窗</b>——
        /// 这两样都建立在「可能没装」之上，而引擎模块不存在这一状态。
        /// 此时 <paramref name="runtimeName"/> / <paramref name="missingLabel"/> /
        /// <paramref name="warnDialog"/> 均不被读取，可传 <c>null</c>。
        /// </param>
        private void DrawMacroToggle(string titleName, string define, string runtimeName,
            ref bool enabled, bool runtimeInstalled, string description,
            string missingLabel, string warnDialog, Action drawAdditionalFields = null,
            bool hasRuntimeDependency = true)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUILayout.ToggleLeft(
                $"{titleName}  ({define})", enabled, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                if (hasRuntimeDependency && newEnabled && !runtimeInstalled)
                {
                    if (!EditorUtility.DisplayDialog(Tr("警告"), warnDialog, Tr("确定"), Tr("取消")))
                        newEnabled = false;
                }
                if (newEnabled != enabled)
                {
                    enabled = newEnabled;
                    DefineUtils.ApplyDefine(enabled, define);
                    _pendingRecompile = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            EnsureStyles();
            // 无外部运行时依赖的后端不画安装状态行，说明块里直接讲「引擎内置」即可
            if (hasRuntimeDependency)
            {
                if (runtimeInstalled)
                    EditorGUILayout.LabelField(Fmt("  ✓ {0} 已安装", runtimeName), _styleOk);
                else
                    EditorGUILayout.LabelField(string.Format(missingLabel, runtimeName), _styleWarn);
            }

            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

            if (_pendingRecompile)
            {
                EditorGUILayout.LabelField(Tr("  ⏳ 宏定义已更改，等待 Unity 重新编译…"), _styleWarn);
                if (!EditorApplication.isCompiling) _pendingRecompile = false;
            }

            // 宏方块下的额外内容（无论宏是否启用都绘制：未启用时正是最需要看安装说明的时候）
            if (drawAdditionalFields != null)
            {
                EditorGUILayout.Space(4);
                drawAdditionalFields.Invoke();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUI.BeginChangeCheck();
            bool newAutoShow = EditorGUILayout.ToggleLeft(Tr("启动时自动显示"), _autoShow, GUILayout.Width(140));
            if (EditorGUI.EndChangeCheck())
            {
                _autoShow = newAutoShow;
                EditorPrefs.SetBool(AnimSimulatorEditorPrefs.WelcomeAutoShow, _autoShow);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// FilterMode.Point 确保放大时像素边缘保持锐利清晰。结果缓存，避免每帧重复 I/O。
        /// Logo 文件不存在时静默跳过绘制（<c>Docs~</c> 不被 AssetDatabase 索引，走文件系统读取）。
        /// </summary>
        private Texture2D GetLogoTexture()
        {
            if (_logoTexture) return _logoTexture;
            if (_logoLoadAttempted) return null;

            _logoLoadAttempted = true;

            string logoPath = System.IO.Path.GetFullPath(
                "Packages/com.ale.animsimulatorsystem/Docs~/Images/AnimSimulatorSystem_Logo.png");

            if (!System.IO.File.Exists(logoPath)) return null;

            byte[] bytes = System.IO.File.ReadAllBytes(logoPath);
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,   // 像素锐利，无插值模糊
                wrapMode   = TextureWrapMode.Clamp
            };
            if (tex.LoadImage(bytes))
            {
                _logoTexture = tex;
            }
            else
            {
                DestroyImmediate(tex);
            }

            return _logoTexture;
        }

        #endregion

        #region 快捷操作

        /// <summary>创建一份 <see cref="AnimSimulatorConfig"/> 资产并选中它。</summary>
        private static void CreateConfigAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                Tr("创建 动画模拟器配置"),
                "AnimSimulatorConfig", "asset",
                Tr("选择 AnimSimulatorConfig 资产的保存位置"));
            if (string.IsNullOrEmpty(path)) return;

            var config = ScriptableObject.CreateInstance<AnimSimulatorConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>
        /// 用系统默认程序打开包内的 Markdown 文档。<c>.md</c> 与 <c>Docs~</c> 均不被 AssetDatabase 索引，
        /// 故取绝对路径后交给系统打开。
        /// </summary>
        private static void OpenDoc(string packageRelativePath)
        {
            string absolutePath = System.IO.Path.GetFullPath(packageRelativePath);

            if (System.IO.File.Exists(absolutePath))
            {
                Application.OpenURL("file:///" + absolutePath.Replace('\\', '/'));
            }
            else
            {
                EditorUtility.DisplayDialog(Tr("文档未找到"),
                    Fmt("未能找到文档文件：\n{0}", packageRelativePath), Tr("确定"));
            }
        }

        #endregion
    }
}
