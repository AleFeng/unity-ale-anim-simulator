# AnimSimulatorSystem 动画模拟器系统

此插件，是基于Unity6000.3.8f1开发，在Unity中使用Spine或Live2D制作的2D动画资源，进行游玩的 动画模拟器系统。

可使用光标来进行 点击、拖拽、旋转、按压 4种主流的操作方式，来游玩 2D动画资源的 动画动作。并影响 等级进度条、动作进度条的进度。\
等级进度条 的等级，可作为 动画动作 的解锁条件。在达到 指定的等级后，会自动解锁 指定的 动画动作。\
动作进度条 的进度，可作为 动画动作 的播放条件。在达到 指定的进度值后，会消耗进度值 并自动播放 指定的 动画动作。

支持自定义的 换装系统，在Spine或Live2D中，制作好的 皮肤，可以直接在Unity中进行配置并使用。\
可将皮肤进行 分组配置，并显示在 各自的列表中。例如，衣服、裤子、头发、眼睛等。\

## 依赖

### 必需：DOTween

本插件 **必需** DOTween（Pro 或免费版均可），且以下两项缺一不可：

1. **程序集**：插件 asmdef 硬引用 `Demigiant.DOTween` 程序集。工程内没有 DOTween 时，该引用无法解析，Unity 会报缺失程序集引用的错误。
2. **编译宏 `DOTWEEN`**：需存在于 `Player Settings > Scripting Define Symbols`。DOTween 的 `Tools > Demigiant > DOTween Utility Panel > Setup DOTween...` 会写入该宏，也可手动添加。

> DOTween 以 Asset Store 资源形式分发（不在 UPM 注册表中），因此既无法通过 `package.json` 的 `dependencies` 声明，也无法通过 asmdef 的 `versionDefines` 自动探测——必须手动确保上述两项。

**未定义 `DOTWEEN` 宏时，插件仍能编译，但 Spine 动画相关功能会静默失效**（`SpineAnimator` 中的 DOTween 代码整段被编译掉，且没有回退实现）：

| 失效项 | 后果 |
|---|---|
| `FadeSpineAnimator` 淡入淡出 | 方法成为空操作，**Spine 角色 / 背景永远不会显示** |
| `OncePlaySpineAnimComplete` 单次播放完成 | 非循环动画**不会自动结束**，完成回调不触发 |
| `PlaySpineAnimLoopIntervalTime` 循环随机间隔 | 配置了 `loopIntervalTimeRange` 的循环动画**不会播放** |
| 动画延时播放 `startDelayTime` | 延时被忽略，立即播放 |

（`AnimSimulatorManager` 的 UI 淡入淡出有 `#else` 回退，仅退化为瞬间显示 / 隐藏，不影响可用性。）

### 必需：Ale Toolkit

本插件构建于 **[Ale Toolkit](https://github.com/AleFeng/unity-ale-toolkit)**（`com.ale.toolkit`，**≥ 1.7.0**）之上，用到的底层能力：

| toolkit 能力 | 插件中的用途 |
|---|---|
| `ToolkitMonoSingleton<T>` | `AnimSimulatorManager` 的单例基类 |
| `ToolkitAssets`（按地址加载） | 角色 / 背景资产的加载、实例化与释放 |
| `ToolkitInputBinder` | 光标移动 / 左键 / 右键的输入绑定 |
| `UIUtility.WorldPosToUILocalPos` | 动作列表跟随角色的世界坐标定位 |
| `UiwFocusOrderList<,>` / `UiwVirtualOrderList<,>` | 动画动作列表与皮肤列表的虚拟滚动 |
| `LocalizedTextEvent` / `LocalizedFontEvent` | Demo 预制体的文本与字体本地化 |

toolkit 走 git URL / 本地路径分发（不在 UPM 注册表），故未写进 `package.json` 的 `dependencies`，需自行安装。

> **`ToolkitMonoSingleton` 的行为提示**：它的 `Instance` **不会自动创建实例**。场景中必须先存在 `AnimSimulatorManager` 组件，`AnimActionPlayer` 等才能注册进去；否则会给出明确警告而非静默失效。

### 编译宏

以下宏均为**项目级手动开关**（写在 `Player Settings > Scripting Define Symbols`），插件 asmdef 的 `versionDefines` 已清空——自动探测与手动开关并存会导致「装了包就强制置位、开关关不掉」，故统一由手动开关决定。

| 宏 | 由谁管理 | 需要的包 | 未启用时的影响 |
|---|---|---|---|
| `ATK_LOCALIZATION` | Ale Toolkit 欢迎窗口<br/>（`Tools > Ale Toolkit > Welcome`） | `com.unity.localization` | 动作名 / 皮肤名 / 进度条名 退化为纯 `string` 字段 |
| `ATK_TMP` | 同上 | 内置于 `com.unity.ugui` | toolkit 的本地化字体组件不参与编译 |
| `ATK_INPUT_SYSTEM` | 同上 | `com.unity.inputsystem` | 光标操作输入（点击 / 拖拽 / 旋转 / 按压）不可用 |
| `ATK_ADDRESSABLE` | 同上 | `com.unity.addressables` | 角色 / 背景无法按地址异步加载（退化为 `Resources` 兜底并告警） |
| `ASS_SPINE` | 本插件自有前缀（`ASS_` = AnimSimulatorSystem） | `com.esotericsoftware.spine.spine-unity` | Spine 动画播放与换装功能不可用 |

> ⚠️ **`ATK_LOCALIZATION` 会改变序列化字段的类型**（`LocalizedString` ↔ `string`）。在已有配置资产的工程里切换该宏会丢失对应字段的数据，请在项目初期就定好。

### 其他

插件还依赖 **TextMeshPro**（`UILevelProgressBar` 的等级数字使用 `TextMeshProUGUI`）。

## 使用文档

- [AnimSimulatorSystem 使用文档](Docs~/AnimSimulatorSystem/AnimSimulatorSystem.md)