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

### 可选：由 asmdef `versionDefines` 自动探测

| 包 | 宏 | 缺失时的影响 |
|---|---|---|
| `com.esotericsoftware.spine.spine-unity` | `HAS_SPINE` | Spine 动画播放与换装功能不可用 |
| `com.unity.localization` | `HAS_LOCALIZATION` | 动作名 / 皮肤名 / 进度条名 退化为纯文本字段 |
| `com.unity.inputsystem` | `HAS_INPUT_SYSTEM` | 光标操作输入（点击 / 拖拽 / 旋转 / 按压）不可用 |
| `com.unity.addressables` | `HAS_ADDRESSABLES` | 该宏当前未被插件代码使用（asmdef 中保留了 versionDefine） |

### 其他

插件还依赖 **Fs Game Framework**（`AssetManager` / `ControllerManager` / `MonoBehaviourSingleton` / `UIUtility` 及其内置的 `CircularScrollingList`）与 **TextMeshPro**。

## 使用文档

- [AnimSimulatorSystem 使用文档](Docs~/AnimSimulatorSystem/AnimSimulatorSystem.md)