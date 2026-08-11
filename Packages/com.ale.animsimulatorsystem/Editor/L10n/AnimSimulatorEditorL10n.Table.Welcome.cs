using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.AnimSimulatorSystem.Editor
{
    /// <summary>
    /// <see cref="AnimSimulatorWelcomeWindow"/> 的英 / 日译表。中文为源语言，故此处只登记英、日两栏；
    /// 未登记的条目在对应语言下自动回退中文。
    /// </summary>
    internal static partial class AnimSimulatorEditorL10NTables
    {
        static partial void RegisterWelcome()
        {
            // ── 窗口标题 / 页眉 ───────────────────────────────────────────────────
            Add("Anim Simulator 动画模拟器系统",
                "Anim Simulator System",
                "アニメーションシミュレーターシステム");
            Add("基于 Spine / Live2D 的 2D 动画模拟器",
                "A 2D animation simulator built on Spine / Live2D",
                "Spine / Live2D ベースの 2D アニメーションシミュレーター");

            // ── 全局设置跳转 ──────────────────────────────────────────────────────
            Add("打开 Ale Toolkit 设置（语言 / 插件宏）",
                "Open Ale Toolkit Settings (Language / Defines)",
                "Ale Toolkit 設定を開く（言語 / マクロ）");

            // ── 快捷操作 ──────────────────────────────────────────────────────────
            Add("快捷操作",     "Quick Actions",   "クイック操作");
            Add("创建配置资产", "Create Config",   "設定アセットを作成");
            Add("查看使用文档", "View Manual",     "使用ドキュメントを見る");
            Add("示例场景经 Package Manager > Anim Simulator System > Samples > Import 导入后，" +
                "位于 Assets/Samples/ 下。",
                "The sample scene lands under Assets/Samples/ after importing it via " +
                "Package Manager > Anim Simulator System > Samples > Import.",
                "サンプルシーンは Package Manager > Anim Simulator System > Samples > Import で" +
                "インポートすると Assets/Samples/ 配下に配置されます。");

            Add("创建 动画模拟器配置", "Create Anim Simulator Config", "アニメーションシミュレーター設定を作成");
            Add("选择 AnimSimulatorConfig 资产的保存位置",
                "Choose where to save the AnimSimulatorConfig asset",
                "AnimSimulatorConfig アセットの保存先を選択");
            Add("未能找到文档文件：\n{0}",
                "Could not find the documentation file:\n{0}",
                "ドキュメントファイルが見つかりませんでした：\n{0}");

            // ── 宏开关区 ──────────────────────────────────────────────────────────
            Add("按项目实际使用的动画运行时启用。两者可同时启用——一个工程里 Spine 与 Live2D 角色并存，" +
                "具体用哪个后端由角色预制体上挂的是 SpineAnimator 还是 Live2DAnimator 决定。",
                "Enable the ones matching the animation runtimes your project actually uses. Both can be enabled at once — " +
                "Spine and Live2D actors coexist in one project, and which backend an actor uses is decided by whether its " +
                "prefab carries a SpineAnimator or a Live2DAnimator.",
                "プロジェクトで実際に使用するアニメーションランタイムに合わせて有効にしてください。両方を同時に有効にできます——" +
                "1 つのプロジェクトで Spine と Live2D のキャラクターが共存でき、どちらのバックエンドを使うかは" +
                "キャラクタープレハブに SpineAnimator と Live2DAnimator のどちらが付いているかで決まります。");

            // Spine
            Add("启用后 SpineAnimator 参与编译，可播放 Spine 动画、按皮肤名组合换装。" +
                "需通过 Package Manager 安装 Spine Unity 运行时（git URL 分发，不在 UPM 注册表中）。",
                "When enabled, SpineAnimator is compiled in, allowing Spine animation playback and skin-name based outfit " +
                "composition. Requires the Spine Unity runtime installed via Package Manager (distributed by git URL, not on the UPM registry).",
                "有効にすると SpineAnimator がコンパイルされ、Spine アニメーションの再生とスキン名による着せ替えが可能になります。" +
                "Package Manager 経由で Spine Unity ランタイムのインストールが必要です（git URL 配布で、UPM レジストリにはありません）。");
            Add("尚未检测到 Spine Unity 运行时。\n启用宏后，SpineAnimator 将无法编译。\n\n确定要继续启用吗？",
                "The Spine Unity runtime was not detected.\nAfter enabling the define, SpineAnimator will fail to compile.\n\nEnable anyway?",
                "Spine Unity ランタイムが検出されませんでした。\nマクロを有効にすると、SpineAnimator はコンパイルできません。\n\nこのまま有効にしますか？");

            // Live2D
            Add("启用后 Live2DAnimator 参与编译，可播放 Cubism 动作、按部件组合换装。",
                "When enabled, Live2DAnimator is compiled in, allowing Cubism motion playback and part-based outfit composition.",
                "有効にすると Live2DAnimator がコンパイルされ、Cubism モーションの再生とパーツによる着せ替えが可能になります。");
            Add("  ⚠ {0} 未导入（非 UPM 包，需从官网下载 .unitypackage 导入）",
                "  ⚠ {0} not imported (not a UPM package — download the .unitypackage from the official site)",
                "  ⚠ {0} 未インポート（UPM パッケージではありません。公式サイトから .unitypackage を入手してください）");
            Add("尚未检测到 Live2D Cubism SDK。\n启用宏后，Live2DAnimator 将无法编译。\n\n确定要继续启用吗？",
                "The Live2D Cubism SDK was not detected.\nAfter enabling the define, Live2DAnimator will fail to compile.\n\nEnable anyway?",
                "Live2D Cubism SDK が検出されませんでした。\nマクロを有効にすると、Live2DAnimator はコンパイルできません。\n\nこのまま有効にしますか？");
            Add("Live2D Cubism SDK 不是 UPM 包：官方以 .unitypackage 分发（含专有 Cubism Core 原生库），" +
                "需从官网下载后拖入工程，导入到 Assets/Live2D/Cubism/。" +
                "Cubism 5 SDK 自带 Live2D.Cubism 程序集定义，导入后本插件即可自动引用。",
                "The Live2D Cubism SDK is not a UPM package: it ships as a .unitypackage (including the proprietary Cubism Core " +
                "native library) and must be downloaded from the official site and dragged into the project, landing in " +
                "Assets/Live2D/Cubism/. The Cubism 5 SDK ships a Live2D.Cubism assembly definition, which this plugin " +
                "references automatically once imported.",
                "Live2D Cubism SDK は UPM パッケージではありません：公式は .unitypackage で配布しており（専有の Cubism Core " +
                "ネイティブライブラリを含む）、公式サイトからダウンロードしてプロジェクトにドラッグし、" +
                "Assets/Live2D/Cubism/ に導入します。Cubism 5 SDK には Live2D.Cubism アセンブリ定義が同梱されており、" +
                "インポート後は本プラグインが自動的に参照します。");
            Add("打开 Live2D 官方下载页",
                "Open the Live2D download page",
                "Live2D 公式ダウンロードページを開く");
        }
    }
}
