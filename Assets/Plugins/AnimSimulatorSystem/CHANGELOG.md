# 更新日志（Changelog）

本文件记录 Anim Simulator System（`com.ale.animsimulatorsystem`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [2.0.0] - 2026-08-10

**底层框架由 Fs Game Framework 整体迁移到 [Ale Toolkit](https://github.com/AleFeng/unity-ale-toolkit)（`com.ale.toolkit` ≥ 1.7.0）。** 迁移前插件编译期依赖 6 个 Fs 程序集，而 Fs 并不随本仓库分发——任何干净检出都无法编译。迁移后依赖仅剩 toolkit 与 Unity 官方包。**功能与交互语义保持不变**，但命名空间、包名与编译宏全部更换，属破坏性变更。

### 破坏性变更

- **命名空间**：`Fs.GameFramework.Gameplay.AnimSimulatorSystem` → `Ale.AnimSimulatorSystem`（18 个脚本）。脚本文件 GUID 全部保留，预制体 / 场景上的组件引用不受影响。
- **程序集**：`Fs.GameFramework.Gameplay.AnimSimulatorSystem` → `Ale.AnimSimulatorSystem`（asmdef GUID 保留）。
- **包名**：`com.fs.animsimulatorsystem` → `com.ale.animsimulatorsystem`。
- **编译宏全面更名**，且**不再由 asmdef `versionDefines` 自动探测**，改为项目级手动开关：
  | 旧 | 新 | 归属 |
  |---|---|---|
  | `HAS_LOCALIZATION` | `ATK_LOCALIZATION` | Ale Toolkit 欢迎窗口 |
  | `HAS_INPUT_SYSTEM` | `ATK_INPUT_SYSTEM` | 同上 |
  | `HAS_SPINE` | `ASS_SPINE` | 本插件（`ASS_` 前缀） |
  | `HAS_ADDRESSABLES` | 删除 | 代码中从未使用 |

  自动 versionDefine 与手动开关不能并存——前者会在装了包时强制置位，令欢迎窗口的开关无法关闭它，故 `versionDefines` 整体清空。
- **`AnimSimulatorManager.Instance` 不再自动创建实例**。Fs 的 `MonoBehaviourSingleton` 在实例缺失时会凭空造一个 GameObject，`ToolkitMonoSingleton` 不会。场景中必须先放置该组件；插件内 8 处调用点已补判空，`AnimActionPlayer.OnEnable` 注册失败时会给出明确警告而非静默跳过。
- **移除第三方 `CircularScrollingList`**（`AirFishLab.ScrollingList`）。两个列表改用 toolkit 的虚拟滚动列表，`UIAnimActionListBank` / `UIAnimActorSkinListBank` 两个组件类被删除，数据组装职责并入各自的宿主脚本。

### 变更

- **动画动作列表**改用 `UiwFocusOrderList<,>`：「焦点条目 = 当前选中动作」的语义保留（滚到哪儿就选中哪儿），焦点缩放与横向偏移由两条 `AnimationCurve` 驱动。滚动本身交还给 `ScrollRect`，因此**除滚轮外也支持拖拽**（原配置为仅滚轮）。
- **皮肤列表**改用 `UiwVirtualOrderList<,>`（选中由格子内按钮驱动，不需要焦点语义）。
- 列表项由 `ListBox` 子类改为普通 `MonoBehaviour`，`UpdateDisplayContent(IListContent)` 拆为 `Bind(content)` / `Clear()`。
- 角色 / 背景资产的加载卸载改用 `ToolkitAssets` 的按地址接口；实例化时直接指定父节点，省去回调内的二次重挂。
- 输入绑定改用 `ToolkitInputBinder`：绑定时输入源（`PlayerInput`）尚未生成也不会丢回调——先挂起、逐帧重试到其出现为止。

### 修复

- **皮肤格子在虚拟化复用下的状态错位**：`AnimActor.OnSkinAddOrRemove` 的订阅现与绑定严格配对（`Bind` 订阅 / `Clear` 退订 / `OnDestroy` 兜底），此前被回收的格子会继续响应事件、按早已不属于自己的皮肤刷新显示；选中提示的写入改为绑定时强制，避免复用后 `_isSelected` 残留导致增量判断误跳过。
- **Demo 资产切断 Fs 引用**：本地化组件 `LocalizeTmpTextEvent` / `LocalizeTmpFontEvent` → toolkit 的 `LocalizedTextEvent` / `LocalizedFontEvent`（字符串表与字体资产表引用完整保留）；场景中的 Fs `PlayerController` → Unity 原生 `PlayerInput`（`defaultActionMap = "UI"`）。

### 已知问题

- Demo 的 `AnimSimulatorConfig` 中，角色 / 背景的 Addressable 地址仍指向 Fs 时代的旧路径（`Assets/Plugins/Fs/...`），运行 Demo 会抛 `InvalidKeyException`。需按实际资产路径重新配置。
- 动作列表的 `focusOffsetCurve` 目前填的是像素估算值（峰值 30px，由原 `boxPositionCurve` 的系数 0.6 折算）。原库该曲线的 y 是系数而非像素，无精确换算公式，需在编辑器内目视微调。

## [1.0.0] - 2026-07-14
### 新增
- **UPM 包支持**：新增 `package.json`（`com.fs.animsimulatorsystem`），插件现可通过 Unity Package Manager 以 UPM 形式安装。
- **示例（Samples）**：Demo 迁移至 `Samples~/Demo`，作为可选导入的示例（配置资产 `AnimSimulatorConfig`、管理器预制体、多语言表与 UI 示例场景），安装后可在 Package Manager 的 Samples 页导入，不再随包强制编译。
- 首个正式版本，功能沿用现有 Anim Simulator System：
  - **动画模拟器核心**：基于 Spine / Live2D 的 2D 动画资源，用光标进行点击 / 拖拽 / 旋转 / 按压 4 种操作驱动动画动作。
  - **等级进度条**：作为动画动作的解锁条件，达到指定等级后自动解锁对应动作。
  - **动作进度条**：作为动画动作的播放条件，达到指定进度值后消耗进度并自动播放对应动作。
  - **自定义换装系统**：支持将 Spine / Live2D 皮肤分组配置（如衣服、裤子、头发、眼睛等），并显示在各自列表中。

### 说明
- **必需依赖 DOTween**：插件 asmdef 硬引用 `Demigiant.DOTween` 程序集，且插件内全部 DOTween 代码由全局 `DOTWEEN` 宏门控。DOTween 以 Asset Store 资源分发（非 UPM 注册表包），无法经 `dependencies` 声明或 `versionDefines` 探测，须手动导入并定义 `DOTWEEN` 宏——否则 Spine 的淡入淡出 / 单次播放完成 / 循环随机间隔 / 延时播放 等功能会静默失效。详见 README 的『依赖』章节。
- 可选集成（Spine / Localization / Input System / Addressables）均由 asmdef 的 `versionDefines` 门控（`HAS_SPINE` / `HAS_LOCALIZATION` / `HAS_INPUT_SYSTEM` / `HAS_ADDRESSABLES`），故 `package.json` 的 `dependencies` 留空，不硬声明这些依赖；Spine 亦不在 UPM 注册表中，需自行安装。其中 `HAS_ADDRESSABLES` 目前未被插件代码使用。
