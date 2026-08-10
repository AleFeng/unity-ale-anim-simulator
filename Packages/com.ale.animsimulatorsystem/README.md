# AnimSimulatorSystem 动画模拟器系统

此插件，是基于Unity6000.3.8f1开发，在Unity中使用Spine或Live2D制作的2D动画资源，进行游玩的 动画模拟器系统。

可使用光标来进行 点击、拖拽、旋转、按压 4种主流的操作方式，来游玩 2D动画资源的 动画动作。并影响 等级进度条、动作进度条的进度。\
等级进度条 的等级，可作为 动画动作 的解锁条件。在达到 指定的等级后，会自动解锁 指定的 动画动作。\
动作进度条 的进度，可作为 动画动作 的播放条件。在达到 指定的进度值后，会消耗进度值 并自动播放 指定的 动画动作。

支持自定义的 换装系统，在Spine或Live2D中，制作好的 皮肤，可以直接在Unity中进行配置并使用。\
可将皮肤进行 分组配置，并显示在 各自的列表中。例如，衣服、裤子、头发、眼睛等。\

## 动画后端

插件支持 **Spine** 与 **Live2D** 两个动画后端，**可在同一工程内同时启用**——一个工程里两种角色并存，用哪个后端由角色预制体上挂的是 `SpineAnimator` 还是 `Live2dAnimator` 决定。

后端无关的机制（状态机、轨道播放栈、计时、皮肤名册、淡入淡出）都在抽象基类 `AnimatorBase` 里，上层的 `AnimActor` / `AnimActionPlayer` 一律面向它编程，对具体后端无感。**动画与皮肤都用字符串名指定，两个后端使用相同的命名规则**，因此同一份动作 / 皮肤组配置对两边都成立。

| 后端 | 需要的运行时 | 安装方式 | 对应组件 |
|---|---|---|---|
| Spine | `com.esotericsoftware.spine.spine-unity`（+ `spine-csharp`） | git URL，经 Package Manager 安装 | `SpineAnimator` |
| Live2D | Cubism SDK for Unity（≥ Cubism 5 SDK R1 beta2，需自带 asmdef） | **不是 UPM 包**，从[官网](https://www.live2d.com/en/sdk/download/unity/)下载 `.unitypackage` 导入到 `Assets/Live2D/Cubism/` | `Live2dAnimator` |

> **Live2D 为什么不能走 UPM**：官方以 `.unitypackage` 分发，其中包含专有的 Cubism Core 原生库；开源的 `Live2D/CubismUnityComponents` 仓库既没有 `package.json`、也不含 Core。因此它无法写进 `package.json` 的 `dependencies`，只能手动导入。好在 Cubism 5 SDK 自带 `Live2D.Cubism` 程序集定义，导入后本插件即可自动引用。

### Live2D 的三处使用约束

Cubism 与 Spine 的模型差异较大，有三点需要在配置时注意：

1. **动作要登记进查找表**。Cubism 没有「按名查找动作」的 API（motion3.json 导入成一个个散落的 `AnimationClip`），需在 `Live2dAnimator` 的「Live2D 动作查找表」里把动画名与剪辑对应起来。
2. **轨道要映射到层**。本系统的轨道号是 `主轨道*10+子轨道`（值域 0..9990），而 Cubism 的层数很少（在 `CubismMotionController` 上配置）。请在「Live2D 轨道映射」里显式指定；未指定的轨道会自动分配第一个空闲层，无空闲层时钳制并告警——同层的动作会互相覆盖。
3. **动作不要动画「模型整体不透明度」**。若 motion3.json 给模型不透明度打了关键帧（导入后表现为剪辑里一条 `CubismRenderController.Opacity` 曲线），会与本系统的淡入淡出同帧争写。整体淡入淡出请交给 `AnimatorBase`。

另外，**进度控制（拖拽 / 旋转 / 按压）与反向播放在 Live2D 侧走单独的采样通道**——Cubism 没有读写播放进度的 API、速度也必须 ≥ 0，故这些场合改由逐帧采样剪辑驱动，期间不经过 Cubism 自己的动作淡入淡出。

## 依赖

### 必需：Ale Toolkit

本插件构建于 **[Ale Toolkit](https://github.com/AleFeng/unity-ale-toolkit)**（`com.ale.toolkit`，**≥ 1.7.3**）之上，用到的底层能力：

| toolkit 能力 | 插件中的用途 |
|---|---|
| `ToolkitMonoSingleton<T>` | `AnimSimulatorManager` 的单例基类 |
| `ToolkitAssets`（按地址加载） | 角色 / 背景资产的加载、实例化与释放 |
| `ToolkitInputBinder` | 光标移动 / 左键 / 右键的输入绑定 |
| `ToolkitTween` | 淡入淡出、起播延时、单次播放完成、循环随机间隔的全部计时 |
| `UIUtility.WorldPosToUILocalPos` | 动作列表跟随角色的世界坐标定位 |
| `UiwFocusOrderList<,>` / `UiwVirtualOrderList<,>` | 动画动作列表与皮肤列表的虚拟滚动 |
| `TextValue` | 动作名 / 皮肤名 / 进度条名的展示文本（纯文本 + 可选的多语言条目） |
| `Ale.Condition`（条件系统） | 动画动作的解锁条件：两级与或非组合、内联编辑界面、可扩展判定器 |
| `LocalizedFontEvent` | Demo 预制体的字体随语言切换 |
| `DefineUtils` / `ToolkitEditorL10n` | 本插件欢迎窗口的宏开关与界面三语 |

toolkit 走 git URL / 本地路径分发（不在 UPM 注册表），故未写进 `package.json` 的 `dependencies`，需自行安装。

> **最低版本是 1.7.3**：该版本给 `ToolkitTween` 新增了通用浮点补间 `To()`，本插件用它补间 Spine 的 `Skeleton.A` 与 Live2D 的 `CubismRenderController.Opacity`——这两个目标都不是 `UnityEngine.Object`，落不到 toolkit 原有的任何固定通道上。

> **2.1.0 起不再依赖 DOTween**。此前它是硬依赖且缺失时会让淡入淡出、单次播放完成、循环随机间隔、起播延时四项静默失效（角色因此永远不显示）；现已全部改用 `ToolkitTween`，插件的第三方运行时依赖归零。

> **`ToolkitMonoSingleton` 的行为提示**：它的 `Instance` **不会自动创建实例**。场景中必须先存在 `AnimSimulatorManager` 组件，`AnimActionPlayer` 等才能注册进去；否则会给出明确警告而非静默失效。

### 编译宏

以下宏均为**项目级手动开关**（写在 `Player Settings > Scripting Define Symbols`），插件 asmdef 的 `versionDefines` 已清空——自动探测与手动开关并存会导致「装了包就强制置位、开关关不掉」，故统一由手动开关决定。

| 宏 | 由谁管理 | 需要的运行时 | 未启用时的影响 |
|---|---|---|---|
| `ATK_LOCALIZATION` | Ale Toolkit 欢迎窗口<br/>（`Tools > Ale Toolkit > Welcome`） | `com.unity.localization` | `TextValue` 只剩纯文本一项，多语言条目不参与编译 |
| `ATK_TMP` | 同上 | 内置于 `com.unity.ugui` | toolkit 的本地化字体组件不参与编译 |
| `ATK_INPUT_SYSTEM` | 同上 | `com.unity.inputsystem` | 光标操作输入（点击 / 拖拽 / 旋转 / 按压）不可用 |
| `ATK_ADDRESSABLE` | 同上 | `com.unity.addressables` | 角色 / 背景无法按地址异步加载（退化为 `Resources` 兜底并告警） |
| `ASS_SPINE` | 本插件欢迎窗口<br/>（`Tools > Ale Toolkit > Anim Simulator System > Welcome`） | `com.esotericsoftware.spine.spine-unity` | `SpineAnimator` 不参与编译，Spine 动画播放与换装不可用 |
| `ASS_LIVE2D` | 同上 | Cubism SDK for Unity | `Live2dAnimator` 不参与编译，Live2D 动作播放与换装不可用 |

`ASS_` 是本插件自有前缀（= AnimSimulatorSystem）。**两个后端宏可以同时启用**，互不排斥。两个都不启用时插件仍能编译，但角色动画无法播放——欢迎窗口会在加载时给出提示。

> **2.2.0 起，关掉 `ATK_LOCALIZATION` 不再丢失展示名。** 此前动作名 / 皮肤名 / 进度条名是「同名字段按宏在 `LocalizedString` 与 `string` 之间换类型」，切宏即丢数据；现在统一为 `TextValue`——纯文本那一项<b>始终存在</b>，多语言条目是附加的。关掉宏只是不再走本地化查表，纯文本照常显示。
>
> 但另一个方向仍需注意：**关着宏保存过的资产，其多语言条目会被丢弃**（该字段此时不参与序列化）。这一点对所有按宏门控的字段都成立，两个后端宏（`ASS_SPINE` / `ASS_LIVE2D`）同理——关着宏保存角色预制体会丢掉对应后端的配置。

> 从 2.0.0 升级上来的工程，`ASS_SPINE` 的前身 `HAS_SPINE` 会在编辑器加载时被自动改写（幂等），无需手动处理。

### 其他

插件还依赖 **TextMeshPro**（`UILevelProgressBar` 的等级数字使用 `TextMeshProUGUI`）。

## 使用文档

- [AnimSimulatorSystem 使用文档](Docs~/AnimSimulatorSystem/AnimSimulatorSystem.md)