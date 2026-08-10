# 更新日志（Changelog）

本文件记录 Anim Simulator System（`com.ale.animsimulatorsystem`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [2.1.0] - 2026-08-10

**接入 Live2D，并使 Spine 与 Live2D 可在同一工程内同时生效。** 此前插件只有 `SpineAnimator` 一个动画后端，且 `AnimActor` / `AnimActionPlayer` 用 `#if ASS_SPINE / #else` 的互斥分支硬绑它——`#else` 分支里那个 `Animator live2DAnimator` 字段全仓库零引用，只是个占位。现在后端无关的机制全部收敛到新的抽象基类 `AnimatorBase`，两个后端各自实现差异部分，上层对具体后端完全无感。

同时**移除了 DOTween 这个第三方硬依赖**——插件现在只依赖 Ale Toolkit 与 Unity 官方包。

### 新增

- **`AnimatorBase` 抽象基类**：承载状态机与渲染器引用计数、每轨道的播放栈（被覆盖时压栈、停止时弹栈并恢复上一条）、循环动画去重、循环随机间隔调度、起播延时、单次播放完成计时、皮肤名册、淡入淡出、轨道编号规则。后端差异收敛为 13 个抽象成员，公开 API 16 个。
  - **渲染器一律以 `Component` 表示**。共享机制从不调用后端渲染器的任何 API，只把它当三样东西用：字典的引用身份键、可 `SetActive` 的 GameObject 宿主、原样回传给后端虚方法的不透明令牌。授权侧仍由各子类声明强类型字段（Spine 的 `SkeletonAnimation` / Live2D 的 `CubismRenderController`），在 `EnumerateStateDatas()` 里转成中性记录——Inspector 的类型约束不退化，既有预制体的字段名与序列化布局也一字不改。
  - **新增 `GetAnimPlayToken(int trackIndex)`**：每次成功播放自增的单调令牌，供调用方判断「我发起的那次播放是否已被顶替」。它取代了原先持有后端播放句柄做引用比较的做法——见「修复」。
- **`Live2dAnimator` 与 `Live2dSkinData`**（受 `ASS_LIVE2D` 约束）。Cubism 与 Spine 有三处根本差异，决定了它的实现形态：
  - **不能按名找动作**（motion3.json 导入成一个个散落的 `AnimationClip`），故需一张「动画名 → 剪辑」查找表；
  - **层（layer）数量很少**且在 `CubismMotionController` 上配置，而本系统的轨道号是 `主轨道*10+子轨道`（值域 0..9990），二者必须显式映射；缺映射时自动分配第一个空闲层，无空闲则钳制并告警。
  - **没有读写播放进度的 API，速度也必须 ≥ 0**。故常规播放（循环 / 正向 / 速度>0）走 `CubismMotionController`，保留 motion3.json 自带的淡入淡出；而反向播放、速度为 0、以及拖拽 / 旋转 / 按压三种进度擦洗，改由 `AnimationClip.SampleAnimation` 逐帧采样驱动，进入前先停掉该层的原生播放以免同帧争写。**采样通道刻意不另建 `PlayableGraph`**：模型上的 `Animator` 已被 Cubism 自己的图占用，再挂一个 `AnimationPlayableOutput` 会互相覆盖。
  - **Cubism 没有「皮肤」概念**，故 `Live2dSkinData` 把一件皮肤定义为「皮肤名 → 部件 ID 集合（+ 可选的贴图替换）」。换装只动「被任一皮肤管辖的部件」，未在任何皮肤里出现过的部件（身体、脸等模型固有部件）不受影响。
- **`ASS_LIVE2D` 编译宏**，与 `ASS_SPINE` **可同时启用**——一个工程里两种角色并存，用哪个后端由角色预制体上挂的是 `SpineAnimator` 还是 `Live2dAnimator` 决定。
- **包内 Editor 程序集与欢迎窗口**（`Tools > Ale Toolkit > Anim Simulator System > Welcome`）：两个后端宏的开关、运行时安装状态检测、快捷操作与文档入口。宏的增删复用 toolkit 的 `DefineUtils`，界面三语复用 `ToolkitEditorL10n`。
- **动画改为按字符串名配置**：`AnimData.animName` / `AnimAction.animName`，两个后端使用相同的命名规则——同一份动作配置对 Spine 与 Live2D 都成立。Spine 侧按名在 `SkeletonData` 中查找，Live2D 侧按名查动作表。
- **`[AnimSkinName]` 特性与皮肤名下拉**，替代原先直接挂在皮肤名字段上的 Spine 专有特性 `[SpineSkin]`（两个后端同时启用后，它会给 Live2D 角色也弹出 Spine 的下拉）。候选由各后端经 `AnimatorBase.EditorCollectSkinNames()` 提供，因此**编辑器程序集不必引用 spine-unity / Live2D.Cubism**。取不到候选时退化为文本框；已填但不在候选内的值会被保留并标注「缺失」，不会一打开 Inspector 就被洗掉。

### 破坏性变更

- **`SpineAnimator` 的公开 API 全部移入基类并去 Spine 化**。该类现在只剩 `RepackedSkin()` 一个自有公开方法（Spine 专有优化，Live2D 无对应概念）。

  | 旧 | 新 |
  |---|---|
  | `PlaySpineAnim(SpineAnimData, Action<TrackEntry>)` | `PlayAnim(AnimData, Action<AnimData>)` |
  | `StopSpineAnim` / `DestroySpineAnim` / `ClearAllSpineAnim` | `StopAnim` / `DestroyAnim` / `ClearAllAnim` |
  | `AddSpineAnimState` / `RemoveSpineAnimState` / `SwitchStateArray` | `AddAnimState` / `RemoveAnimState` / `SwitchAnimStateArray` |
  | `FadeSpineAnimator(bool, SkeletonAnimation, bool)` | `FadeAnimator(bool, Component, bool)` |
  | `GetTrackEntry(int)` | 删除 → `GetAnimProgress` / `SetAnimProgress` / `GetAnimPlayToken` |
  | 嵌套类型 `SpineAnimator.SpineAnimData` | 顶层类型 `AnimData` |

- **序列化字段更名与移位**（均带 `[FormerlySerializedAs]`，既有预制体数据自动迁移）：
  - `SpineAnimator.spineAnimSwitchSpineDuration` → `AnimatorBase.animFadeDuration`
  - `SpineAnimator.spineStateInitList` → `AnimatorBase.stateInitList`
  - `SpineAnimator.baseSkins` → `AnimatorBase.baseSkins`（同名，移到基类）
  - `AnimActor.spineAnimator` / `AnimActionPlayer.spineAnimator` → `animator`，类型由 `SpineAnimator` 改为 `AnimatorBase`
- **删除 `AnimActor.live2DAnimator`**：`#else` 分支里的占位字段，全仓库零引用。
- **`AnimAction.animReferenceAsset` 与 `AnimData.animRefAsset` 降级为兼容字段**，仅在 `animName` 留空时作为回退。计划在 2.2.0 移除，新配置请直接填动画名。
- **不再依赖 DOTween**：`DOTWEEN` 宏与相关 `#if` 分支全部删除，asmdef 中解析不到的 DOTween 引用一并移除。补间改用 toolkit 的 `ToolkitTween`（需 `com.ale.toolkit` **≥ 1.7.3**，该版本新增了通用浮点补间 `To()`）。
- asmdef 中 Spine 的两个引用由 GUID 改为**名称引用**（`spine-csharp` / `spine-unity`），并新增名称引用 `Live2D.Cubism`。可选依赖用名称而非 GUID：未安装时 Inspector 能显示缺的是谁，且 SDK 重新导入后 asmdef 的 GUID 若变化，名称引用能自动接上。

### 修复

- **`HAS_SPINE` → `ASS_SPINE` 未落地**。2.0.0 改了宏名却没改工程的 Player Settings，于是**所有 Spine 代码实际上一直被编译掉**，`SpineAnimator` 编译产物是个没有任何序列化字段的空 `MonoBehaviour`——而预制体上那些配置成了孤儿 YAML，一旦有人弄脏并保存就会被静默丢弃。现由 `AnimSimulatorDefineChecker` 在编辑器加载时一次性改写（幂等）。
- **「启用 `ASS_SPINE` 但未装 DOTween」的组合根本无法编译**：循环间隔的记录表声明在 `#if DOTWEEN` 内，却在宏外被访问（3 处 `CS0103`）。因 `ASS_SPINE` 从未真正打开过而一直没暴露。随 DOTween 移除彻底消失。
- **单次播放完成的计时器无法取消**：原先那个延时调用无人持有，动画被提前停止后计时照走，到点再触发一次停止与回调。现登记句柄，`StopAnim` 会取消它。
- **状态自带的渲染器覆盖从不影响真正的播放**：`AddSpineAnimState` 把该渲染器传了下去，但下游的播放实现写死用默认字段——覆盖只影响淡入淡出与引用计数。新的 `PlayAnimOnRenderer(renderer, …)` 接收渲染器。
- **循环动画去重表永远是空的**：悬垂 `else` 导致「表中无记录时不登记」，于是去重从未生效，同一轨道上重复添加同名循环动画会把它重头拉起、产生可见跳帧。
- **进度阻尼协程的轨道身份误判**：原先持有 Spine 的 `TrackEntry` 做引用比较，而它是**对象池复用**的——旧句柄被回收再分配后比较会假阳性，协程会继续去写一条早已易主的轨道。改用 `(轨道, 播放令牌)` 比对。
- **无阻尼分支的空引用**：轨道为空时取到 `null` 却紧接着解引用，且漏了播放器判空。现经 `SetAnimProgress` 返回 `false` 而非抛异常。
- **渲染器与动画组件同体时被误禁用**：淡出会 `SetActive(false)` 渲染器所在物体，若它正是承载动画组件的那个物体，就把组件自己一起停掉，状态机再也跑不起来。Live2D 的 `CubismRenderController` 通常就在模型根上、与 `Live2dAnimator` 同体，一装上就会命中。现同体时只把不透明度归零，物体保持激活。
- **UI 淡入淡出互相打架**：淡出中途改淡入时，旧淡出的完成回调仍会在稍后触发并把刚激活的 UI 关掉。现在发起前先打断在途补间。
- **起播延时句柄无效时永久卡住动画**：无效句柄仍被登记，导致后续播放一律被开头的「已在等待」判断挡回。现仅登记真正在途的延时。
- 所有补间与延时均绑定 `owner`，组件销毁后自动作废，不再对已失效的骨架 / 模型下发写入。

## [2.0.0] - 2026-08-10

**底层框架由 Fs Game Framework 整体迁移到 [Ale Toolkit](https://github.com/AleFeng/unity-ale-toolkit)（`com.ale.toolkit` ≥ 1.7.2）。** 迁移前插件编译期依赖 6 个 Fs 程序集，而 Fs 并不随本仓库分发——任何干净检出都无法编译。迁移后依赖仅剩 toolkit 与 Unity 官方包。**功能与交互语义保持不变**，但命名空间、包名与编译宏全部更换，属破坏性变更。

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
- **滚轮切换焦点条目时的跳变**。原生 `ScrollRect` 直接改写 `content.anchoredPosition`，一档滚轮正好跨一整条，于是整条列表瞬间跳一格、焦点缩放曲线也跟着突变。改由 toolkit 1.7.2 的 `UiwFocusOrderList` 接管滚轮并按缓出曲线逐帧插值，时长由 `Scroll Tween Duration` 配置（默认 0.1 秒，置 0 恢复跳变）。一档的位移距离仍取自 `ScrollRect` 的 `Scroll Sensitivity`。
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
