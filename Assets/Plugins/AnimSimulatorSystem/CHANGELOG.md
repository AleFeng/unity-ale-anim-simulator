# 更新日志（Changelog）

本文件记录 Anim Simulator System（`com.ale.animsimulatorsystem`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [2.0.0] - 2026-08-10

**底层框架由 Fs Game Framework 整体迁移到 [Ale Toolkit](https://github.com/AleFeng/unity-ale-toolkit)（`com.ale.toolkit` ≥ 1.7.1）。** 迁移前插件编译期依赖 6 个 Fs 程序集，而 Fs 并不随本仓库分发——任何干净检出都无法编译。迁移后依赖仅剩 toolkit 与 Unity 官方包。**功能与交互语义保持不变**，但命名空间、包名与编译宏全部更换，属破坏性变更。

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
- **动作列表条目全挤在顶部、滚轮完全失效**。三处叠加所致，逐条修掉：
  - **焦点列表缺首尾留白**（根因，修在 toolkit 1.7.1）。Content 高度原本就是 `条目数 × 行高`：Demo 只有 3 个动作 → 180px，比 400px 的视口还矮，`ScrollRect` 判定无内容可滚，于是条目一律堆在视口顶部且滚轮无反应。补上留白后 Content 为 520px，三条各自都能滚到居中的焦点线上。
  - **`ScrollRect` 全域没有 raycast 目标**。原 `CircularScrollingList` 是自己轮询鼠标滚轮的（`controlMode = 2`），不依赖 UI 射线；换成原生 `ScrollRect` 后必须命中一个 `raycastTarget` 才会收到滚轮事件，而重搭出的 Viewport 只有 `RectMask2D`、没有任何 `Graphic`——只有光标恰好压在某个格子上时才滚得动。现于 Viewport 补一张全透明 `Image`（`cullTransparentMesh = false`，否则 alpha 为 0 的图形会被剔除，连带失去射线命中）。
  - **动作名标签被遮罩裁掉**。名称标签自格子中心向右伸到 +150px，而重搭出的 Viewport 只有 100px 宽，`RectMask2D` 把它们齐根切掉。视口加宽到 400×400（关于中心对称，格子与其子物体的相对位置不变）。
  - 顺带：滚轮灵敏度 20 → 60（= 行高，一档一条动作，还原原库的步进手感）；`movementType` 改 `Clamped`，两端不再回弹过冲。皮肤列表的 Viewport 同样缺 raycast 目标，一并补上。
- **Demo 场景的 UI 事件系统整体失效**（滚轮只是其中一个症状，UI 点击 / 拖拽同样全废）。`EventSystem` 上 `InputSystemUIInputModule` 的十个 `InputActionReference` 是内联序列化在场景里的，全部指向一份 GUID 为 `2bcd2660…`、**已不存在于工程**的 InputActions 资产（Fs 时代遗留，随 `PluginsIgnore` 一同消失），因此 `point` / `scrollWheel` 等无一能解析，EventSystem 产不出任何指针事件。现改为指向工程内的 `Assets/InputSystem_Actions.inputactions`——与场景中 `PlayerInput` 用的是同一份，故 `PlayerInput.uiInputModule` 在运行时的资产覆盖成为空操作，不会再把引用洗成 null。
- **测试用角色 / 背景增加 `AssetReference` 字段**，可直接拖预制体引用加载（引用优先于名称，`#if UNITY_EDITOR && ATK_ADDRESSABLE` 门控）；同时修正 Demo `AnimSimulatorConfig` 中指向 Fs 时代旧路径（`Assets/Plugins/Fs/...`）的角色 / 背景文件夹配置——此前运行 Demo 会抛 `InvalidKeyException`。

### 已知问题

- 动作列表的 `focusOffsetCurve` 目前填的是像素估算值（峰值 30px，由原 `boxPositionCurve` 的系数 0.6 折算）。原库该曲线的 y 是系数而非像素，无精确换算公式，需在编辑器内目视微调。
- 拖拽滚动后不做吸附对齐：松手停在两条之间时，焦点缩放曲线会让上下两条都呈半放大态。滚轮走的是整格步进不受影响。原库有对齐，`UiwFocusOrderList` 明确不接管输入、也不做释放吸附，如需要应在 toolkit 侧另加。

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
