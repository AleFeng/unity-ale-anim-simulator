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
            Add("基于 Spine / Live2D / Unity 动画的 2D 动画模拟器",
                "A 2D animation simulator built on Spine / Live2D / Unity animation",
                "Spine / Live2D / Unity アニメーションベースの 2D アニメーションシミュレーター");

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

            // ── 演示样例（Addressables 登记）──────────────────────────────────────
            Add("演示样例", "Demo Sample", "デモサンプル");
            Add("启用 ATK_ADDRESSABLE 后，演示样例的文件夹需要登记进 Addressables，并把地址改成 " +
                "AnimSimulator——AnimSimulatorConfig 上的背景 / 角色两个地址前缀就是按这个短地址写的" +
                "（AnimSimulator/Assets/Backgrounds/ 与 AnimSimulator/Assets/Actors/）。下面的按钮代劳这一步。",
                "With ATK_ADDRESSABLE enabled, the demo sample folder must be registered in Addressables with its " +
                "address set to AnimSimulator — the background / actor address prefixes on AnimSimulatorConfig are " +
                "written against that short address (AnimSimulator/Assets/Backgrounds/ and AnimSimulator/Assets/Actors/). " +
                "The button below does that for you.",
                "ATK_ADDRESSABLE を有効にした場合、デモサンプルのフォルダーを Addressables に登録し、" +
                "アドレスを AnimSimulator に設定する必要があります——AnimSimulatorConfig の背景 / キャラクターの" +
                "アドレス接頭辞は、この短いアドレスを前提に書かれています" +
                "（AnimSimulator/Assets/Backgrounds/ と AnimSimulator/Assets/Actors/）。下のボタンがこれを代行します。");
            Add("登记样例到 Addressables",
                "Register sample in Addressables",
                "サンプルを Addressables に登録");
            Add("登记失败：{0}", "Registration failed: {0}", "登録に失敗しました：{0}");
            Add("  ⚠ 未找到演示样例。请先在 Package Manager 里导入 Anim Simulator System Demo。",
                "  ⚠ Demo sample not found. Import Anim Simulator System Demo from the Package Manager first.",
                "  ⚠ デモサンプルが見つかりません。まず Package Manager から Anim Simulator System Demo をインポートしてください。");
            Add("  ⚠ 本工程尚未初始化 Addressables 设置，点击下方按钮会一并创建。",
                "  ⚠ This project has no Addressables settings yet; the button below will create them.",
                "  ⚠ このプロジェクトにはまだ Addressables 設定がありません。下のボタンで併せて作成されます。");
            Add("  ⚠ 暂时读不到 Addressables 设置（可能正在编译）。稍后重试。",
                "  ⚠ Addressables settings are momentarily unreadable (a compile may be in progress). Try again shortly.",
                "  ⚠ Addressables 設定を一時的に読み取れません（コンパイル中の可能性）。しばらくしてから再試行してください。");
            Add("  ⚠ 发现 {0} 个样例目录，它们会争用同一个地址。建议只保留一个。",
                "  ⚠ Found {0} sample folders; they would contend for the same address. Keep only one.",
                "  ⚠ サンプルフォルダーが {0} 個見つかりました。同じアドレスを奪い合うため、1 つだけ残してください。");
            Add("  ⚠ 样例已就位但尚未登记：{0}",
                "  ⚠ The sample is present but not registered yet: {0}",
                "  ⚠ サンプルはありますが、まだ登録されていません：{0}");
            Add("  ⚠ 已登记，但地址是「{0}」而不是「{1}」，资源会加载不到。",
                "  ⚠ Registered, but the address is \"{0}\" rather than \"{1}\", so assets will fail to load.",
                "  ⚠ 登録済みですが、アドレスが「{1}」ではなく「{0}」のため、アセットを読み込めません。");
            Add("  ✓ 已登记：地址「{0}」，分组「{1}」",
                "  ✓ Registered: address \"{0}\", group \"{1}\"",
                "  ✓ 登録済み：アドレス「{0}」、グループ「{1}」");
            Add("未找到演示样例。请先在 Package Manager 里导入 Anim Simulator System Demo，再回来点这个按钮。" +
                "（判定条件：目标目录下同时存在 Assets/Actors 与 Assets/Backgrounds 两个子目录。）",
                "Demo sample not found. Import Anim Simulator System Demo from the Package Manager first, then come back " +
                "and press this button. (A folder counts as the sample only if it contains both Assets/Actors and Assets/Backgrounds.)",
                "デモサンプルが見つかりません。まず Package Manager から Anim Simulator System Demo をインポートし、" +
                "その後このボタンを押してください。（判定条件：対象フォルダーに Assets/Actors と Assets/Backgrounds の両方が存在すること。）");
            Add("发现 {0} 个样例目录：\n{1}\n它们会争用同一个地址，请先删掉多余的，只保留一个。",
                "Found {0} sample folders:\n{1}\nThey would contend for the same address; delete the extras and keep one.",
                "サンプルフォルダーが {0} 個見つかりました：\n{1}\n同じアドレスを奪い合うため、余分なものを削除して 1 つだけ残してください。");
            Add("取不到文件夹的 GUID：{0}",
                "Could not get the folder's GUID: {0}",
                "フォルダーの GUID を取得できませんでした：{0}");
            Add("无法获取或创建 Addressables 设置。请稍后重试，或先手工创建一次 Addressables 配置。",
                "Could not get or create the Addressables settings. Try again later, or create an Addressables configuration manually once.",
                "Addressables 設定を取得または作成できませんでした。しばらくしてから再試行するか、先に手動で一度 Addressables 設定を作成してください。");
            Add("已经登记好了，无需改动。（分组「{0}」，地址「{1}」）",
                "Already registered; nothing to change. (group \"{0}\", address \"{1}\")",
                "すでに登録済みで、変更は不要です。（グループ「{0}」、アドレス「{1}」）");
            Add("Addressables 没有默认分组。请先在 Addressables Groups 窗口里指定一个默认分组。",
                "Addressables has no default group. Set one in the Addressables Groups window first.",
                "Addressables に既定のグループがありません。まず Addressables Groups ウィンドウで既定のグループを指定してください。");
            Add("将把文件夹\n{0}\n登记到分组「{1}」，并把地址设为「{2}」。",
                "The folder\n{0}\nwill be registered in group \"{1}\" with its address set to \"{2}\".",
                "フォルダー\n{0}\nをグループ「{1}」に登録し、アドレスを「{2}」に設定します。");
            Add("该文件夹已登记在分组「{0}」，但地址是「{1}」。\n将把地址改为「{2}」（不移动分组）。",
                "The folder is already registered in group \"{0}\" but its address is \"{1}\".\nThe address will be changed to \"{2}\" (the group is left alone).",
                "このフォルダーはグループ「{0}」に登録済みですが、アドレスが「{1}」です。\nアドレスを「{2}」に変更します（グループは移動しません）。");
            Add("\n\n⚠ 另有 {0} 个资源此前被单独登记过，它们会保留各自的旧地址、不受文件夹条目影响：\n{1}",
                "\n\n⚠ Another {0} assets were registered individually beforehand; they keep their own old addresses and are unaffected by the folder entry:\n{1}",
                "\n\n⚠ ほかに {0} 個のアセットが個別に登録済みです。これらは各自の古いアドレスを保持し、フォルダーエントリの影響を受けません：\n{1}");
            Add("已取消，未做任何改动。", "Cancelled; nothing was changed.", "キャンセルしました。変更はありません。");
            Add("创建 Addressables 条目失败。请检查 Addressables 配置是否正常。",
                "Failed to create the Addressables entry. Check that your Addressables configuration is healthy.",
                "Addressables エントリの作成に失敗しました。Addressables 設定が正常か確認してください。");
            Add("已登记：分组「{0}」，地址「{1}」。",
                "Registered: group \"{0}\", address \"{1}\".",
                "登録しました：グループ「{0}」、アドレス「{1}」。");
            Add("（{0} 个此前单独登记过的资源保留了原地址）",
                "({0} individually registered assets kept their original addresses)",
                "（個別に登録されていた {0} 個のアセットは元のアドレスを保持しました）");

            // ── 宏开关区 ──────────────────────────────────────────────────────────
            Add("按项目实际使用的动画运行时启用。三者可同时启用——一个工程里三种角色并存，" +
                "具体用哪个后端由角色预制体上挂的是 SpineAnimator、Live2DAnimator 还是 UnityAnimator 决定。" +
                "Unity 动画后端为引擎内置，无需安装任何外部运行时。",
                "Enable the ones matching the animation runtimes your project actually uses. All three can be enabled at once — " +
                "the three kinds of actor coexist in one project, and which backend an actor uses is decided by whether its " +
                "prefab carries a SpineAnimator, a Live2DAnimator or a UnityAnimator. " +
                "The Unity animation backend is built into the engine and needs no external runtime.",
                "プロジェクトで実際に使用するアニメーションランタイムに合わせて有効にしてください。3 つを同時に有効にできます——" +
                "1 つのプロジェクトで 3 種類のキャラクターが共存でき、どのバックエンドを使うかは" +
                "キャラクタープレハブに SpineAnimator・Live2DAnimator・UnityAnimator のどれが付いているかで決まります。" +
                "Unity アニメーションバックエンドはエンジン内蔵で、外部ランタイムのインストールは不要です。");

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
            Add("Spine Unity 运行时不在 UPM 注册表中：官方以 git URL 分发，需在 Package Manager 用 " +
                "Add package from git URL… 手动添加 spine-unity，以及它依赖的 spine-csharp。" +
                "URL 末尾的分支号须与导出骨骼数据所用的 Spine 编辑器版本一致，" +
                "否则导入时会报骨骼数据版本不匹配。",
                "The Spine Unity runtime is not on the UPM registry: it is distributed by git URL and must be added " +
                "manually via Package Manager > Add package from git URL…, together with the spine-csharp package it " +
                "depends on. The branch at the end of the URL must match the Spine editor version used to export the " +
                "skeleton data, otherwise the import reports a skeleton data version mismatch.",
                "Spine Unity ランタイムは UPM レジストリにありません：公式は git URL で配布しており、" +
                "Package Manager の Add package from git URL… から spine-unity と、それが依存する spine-csharp を" +
                "手動で追加する必要があります。URL 末尾のブランチ番号は、スケルトンデータの書き出しに使用した " +
                "Spine エディターのバージョンと一致させてください。一致しないとインポート時に" +
                "スケルトンデータのバージョン不一致エラーになります。");
            Add("打开 Spine Unity 安装说明页",
                "Open the spine-unity installation guide",
                "spine-unity インストール手順を開く");

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

            // Unity 内置动画（无安装状态行、无确认弹窗，故只有两条）
            Add("启用后 UnityAnimator 参与编译，可用 Unity 自带的 AnimationClip 播放动画、按物体显隐换装。" +
                "适用于 2D 精灵 / 逐帧动画、3D 模型与 UGUI。",
                "When enabled, UnityAnimator is compiled in, playing Unity's own AnimationClips and composing outfits by " +
                "toggling GameObjects. Works for 2D sprite / frame-by-frame animation, 3D models and UGUI.",
                "有効にすると UnityAnimator がコンパイルされ、Unity 標準の AnimationClip の再生と、" +
                "オブジェクトの表示切り替えによる着せ替えが可能になります。" +
                "2D スプライト / コマ送りアニメーション、3D モデル、UGUI に対応します。");
            Add("引擎内置，无需安装任何外部运行时。角色只要有一个 Animator 组件作为输出目标即可，" +
                "Controller 栏留空——状态机在本系统这一层，再叠一层 AnimatorController 会两边争写同一条动画流" +
                "（默认会自动置空并告警）。要播放的 AnimationClip 需在 UnityAnimator 的「Unity 动画查找表」" +
                "中登记，动画名留空则用剪辑的资产名。",
                "Built into the engine — no external runtime to install. The actor only needs an Animator component as the " +
                "output target, with its Controller field left empty: the state machine lives in this system, and stacking an " +
                "AnimatorController on top makes the two fight over the same animation stream (by default it is cleared " +
                "automatically with a warning). AnimationClips to be played must be registered in UnityAnimator's " +
                "\"Unity Anim Clips\" lookup table; leaving the animation name blank falls back to the clip's asset name.",
                "エンジン内蔵のため、外部ランタイムのインストールは不要です。キャラクターには出力先として Animator " +
                "コンポーネントが 1 つあれば十分で、Controller 欄は空のままにしてください——ステートマシンは本システム側にあり、" +
                "AnimatorController を重ねると同じアニメーションストリームを奪い合います（既定では自動的に空にし、警告を出します）。" +
                "再生する AnimationClip は UnityAnimator の「Unity 動画検索テーブル」に登録が必要です。" +
                "アニメーション名を空にすると、クリップのアセット名が使われます。");
        }
    }
}
