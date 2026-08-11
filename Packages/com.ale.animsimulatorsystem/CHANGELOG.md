# 更新日志（Changelog）

本文件记录 Anim Simulator System（`com.ale.animsimulatorsystem`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [2.3.1] - 2026-08-11

**修复 Random(点击随机) 类型的点击提示动画表现。** 2.3.0 引入该类型时，只让它走到「淡入」这一级就停下了。

### 修复

- **Random 类型的点击提示不再停在半途。** 此前它悬停后停在 `A_ListClose`（提示圈 1.0 倍、不旋转），而 Operate 会继续走到 `A_ListOpen`（提示圈 1.55 倍 + 持续旋转）——两种同样「能点」的播放器给出的视觉反馈不一样，Random 的看起来像是没响应完。
  - 新增动画剪辑 **`A_TipOnly`** 与同名状态：对 `CircleClickTip` 的处理与 `A_ListOpen` **逐条相同**，只是把 `CircularScrollingList` 的 `CanvasGroup.Alpha` / `BlocksRaycasts` 两条曲线按住 0。保留曲线而非删除——删了就变成「不写」，列表会留在上一个状态写下的值上。
  - 新增 Animator 参数 **`TriggerTipOnly`**。`UIAnimActionList` 按播放器类型选触发器：Operate 走 `TriggerListOpen`，Random 走 `TriggerTipOnly`。
  - `A_ListOpen` 与 `A_TipOnly` 之间互设转换，光标从一种播放器直接滑到另一种时可就地切换；两者都接受 `TriggerListClose` 与 `TriggerFadeOut`。
  - `_isOpen` 的语义随之由「列表已铺开」放宽为「已进入展开态」，关闭分支因此对两种类型都能正确收回。

- **Live2D 的采样通道停手即弹回起始帧。** 拖拽 / 旋转 / 按压把进度停在中途后，只要玩家不再移动光标，下一帧姿势就被打回去——而 Spine 侧表现正常。
  - 成因：`AnimationClip.SampleAnimation` 是**一次性写入**，而原实现只在「进度发生变化」时采样一次。Cubism 每帧都会重写同一批参数（其它轨道的 `PlayableGraph` 播放、以及执行序 100 的 `CubismFadeController` 按动作淡入淡出再刷一遍），停手后没有任何东西把采样值补回去。Spine 不受影响是因为它写的是 `TrackEntry.TrackTime` 这种**持久状态**，`AnimationState.Apply` 每帧照着它重摆姿势；Cubism 没有等价物。
  - 修复：`Live2dAnimator` 实现 `ICubismUpdatable`，在 `OnLateUpdate` 里把所有处于采样通道的轨道按当前进度**逐帧重写**（类注释原本就写的是「逐帧采样」，此前只是没有真正做到）。
  - 执行序取 `CubismFadeController + 1`（= 101）：必须在动作淡入淡出**之后**落笔才不会被覆盖，又必须在 `CubismRenderController`（10000）**之前**才会被画进这一帧；且排在姿势 / 表情 / 眨眼 / 物理之前，让它们都基于被拖拽出来的姿势继续演算。普通 `LateUpdate` 与 Cubism 组件之间的先后是未定义的，故接进 `CubismUpdateController` 的调度而不是自己写 `LateUpdate`。
  - `FTrackPlayState` 随之记住该轨道的渲染器——逐帧重写时没有调用方传入，而一个角色由多个 Cubism 模型拼成时各状态可以有各自的渲染器，不能图省事用默认渲染器。
  - `CubismUpdateController` 只登记与 `CubismModel` **同物体**的 `ICubismUpdatable`；组件被挂到别处时回落到自身的 `LateUpdate`（时序不如前者确定，但总好过不重放）。

- **「背景-测试用」配置了却不加载。** `Test Background Name` / `Test Background Reference` 两个字段以及配套的 `HasTestBackground` / `ReloadTestBackground()` 一直都在，但 `OnEnable` 只判 `HasTestActor`、也只调 `ReloadTestActor()`——背景那套在启动时**零调用点**，只能靠组件右键菜单手动触发。现改为两者各自独立判定与加载：只配角色、只配背景、两个都配，三种情形都成立。
  - 判定门同时放宽为「两者任一」，此前只配背景时连 `StartAnimSimulator()` 都不会执行。

- **Random 类型在光标离开后不再整个淡出。** 它此前会直接走 `A_FadeOut`（提示圈消失），而 Operate 停在 `A_ListClose`（提示圈缩回 1.0 倍、子物体继续慢转）——「这里能点」这件事在光标离开后仍要提示，不该整个消失。
  - 起因是 2.3.0 在关闭分支里按 `CanExpandList` 决定要不要淡出，而该判据对 Random 也为假。现改为按 `CanFadeIn`：只有**根本不接受点击**的播放器才淡出。
  - 提示圈整体的淡入淡出本就归 `SetAnimActionPlayer` 管（绑定播放器时淡入、解绑时淡出），与悬停无关；关闭分支里那次淡出仅用于「播放器类型在运行期从 Operate/Random 切成 ProgressBar」这一种场合——已淡入的提示得收掉。

### 新增

- **播放速度为 0 时告警一次**（`AnimatorBase.WarnIfSpeedZero`）。速度 0 在两个后端都表现为「动画定格在起始帧」，而且**都不报错**——Spine 侧是 `TrackEntry.TimeScale = 0`，Live2D 侧因 Cubism 表达不了「暂停」而切到采样通道、停在进度 0。角色一动不动却毫无线索，排查代价很高。
  - 判据与两个后端一致（都取 `Mathf.Abs(speed)`），**反向播放不在告警范围内**——那是正常功能。
  - 加在 `AnimatorBase.PlayAnimImmediate` 这个两后端共同的收口点上，一处实现同时覆盖 Spine 与 Live2D。
  - 每个动画控制器实例只告警一次：同一角色上多条动作都配错时，刷满控制台反而会盖住别的信息。
  - 告警文案直接点出最常见的成因（动作上的 `Click Mode Anim Play Speed` 被置零），并说明拖拽 / 旋转 / 按压这类由玩家驱动进度的动作**也不需要**把速度设为 0——它们以正常速度起播，首次写入进度时会自动切到采样通道。

### 文档

- **补齐「URP 工程的三项必需设置」一节。** 三项缺一不可、症状互不相同、且**都不会报错**，此前文档只写了其中一项（Renderer List）。另两项是本轮在 Live2D 测试场景里实测踩出来的：
  - **HDR Precision 必须 64 Bits**（或关掉 HDR）。Cubism 把模型画进离屏 RT 再以预乘 alpha blit 回相机缓冲，而该 RT 的格式从相机颜色格式派生——URP 在「HDR 开 + 32 位」下用的 `B10G11R11_UFloat` **没有 alpha 通道**，采样出的 alpha 恒为 1，混合退化为直接覆盖，于是**除模型外满屏漆黑**（背景、Spine 角色等在模型之前绘制的东西全被抹掉）。
  - **需要排在模型之后的物体必须走不透明队列**（`renderQueue < 2500`）。Cubism 的绘制通道注入在 `BeforeRenderingTransparents`，透明队列在它之后绘制，会盖住模型。由此也得到一条分层规则：不透明队列 = 模型之后，透明队列 = 模型之前。
  - 附带说明 **Spine 不受第三项约束**——它是普通 `MeshRenderer`，与背景同在透明队列里按 Z 排序，仅靠空间坐标即可得到正确前后关系。这正是「同一套配置在两个后端表现不同」的原因。

### 变更

- **动作列表的倒序显示改由滚动列表承担，`UIAnimActionList.reverseContentOrder` 删除。** 该字段此前的做法是把 `_animActionContentList` 整个 `Reverse()` 掉，于是「第几条」在数据层与配置层含义相反，读代码时得时刻记着这层反转。现改用 `com.ale.toolkit` 1.7.7 在顺序虚拟列表上新增的同名开关——它只是把条目排到另一个槽位上，**数据索引不变**，`FocusedIndex` 拿到的就是配置里的自然序号。
  - 依赖的 `com.ale.toolkit` 最低版本随之由 1.7.5 抬到 **1.7.7**。低于该版本插件仍能编译运行，但动作列表会按配置的正序显示。
  - Demo 的 `UIAnimActionList.prefab` 已把 `UIAnimActionScrollList` 的 `Reverse Content Order` 勾上，观感与此前一致。**自制动作列表 UI 预制体的工程需要照做**，否则升级后动作会从正序显示。
  - 同一批 toolkit 改动还带来 `Reverse Scroll Direction`（反向滚轮，只影响滚轮不影响拖拽），本插件默认不开启。

- **`UIAnimActionList` 触发 Animator 参数前先探测参数是否存在。** Unity 的 `SetTrigger` / `ResetTrigger` 遇到不存在的参数会往控制台刷错误，而 `TriggerTipOnly` 是本版新增的——沿用旧动作列表 UI 预制体的工程没有这个参数。现在探测一次并缓存，缺失时静默退回 2.3.0 的表现（提示圈停在已淡入未展开的形态），并**告警一次**提示更新预制体。Animator 尚未初始化时不缓存探测结果，避免把「参数一个都不存在」错误地记下来。

> **自制动作列表 UI 预制体的工程需要补这个状态**，否则 Random 类型的提示圈表现仍是 2.3.0 的样子。Demo 侧改的是 `AC_UIAnimActionList.controller`（新增参数、状态与转换）并新增 `A_TipOnly.anim`，`UIAnimActionList.prefab` 本身没有改动。

## [2.3.0] - 2026-08-10

**三项功能新增：动作播放器的 Random(点击随机) 类型、可配的轨道混合权重、动作列表的行距倍率与焦点居中。** 前两项是纯加量，既有配置的行为一字不变；Live2D 的自动分层规则有一处行为变更（见「变更」）。

### 新增

- **`EAnimActionPlayerType` 新增 `Random`（点击随机）**：光标悬停时**只淡入点击提示、不铺开动作列表**，点击时从**满足解锁条件**的动作中当场随机抽一条播放。适合「这里可以点，但点出什么不由玩家挑」的轻交互。
  - 抽样复用既有的 `randomTypeWeight`（随机权重）与 `randomTypePlayLimit`（限制播放次数），与进度条驱动的随机播放是同一套逻辑（已抽成共用的 `PlayRandomAnimAction`）。
  - 新枚举值**追加在末尾**（`Operate=0` / `ProgressBar=1` / `Random=2`），既有预制体上配好的类型不受影响。
  - 播放器上新增 `IsPlayerOperable`（接不接受玩家点击）与 `IsAnimActionSelectable`（要不要玩家挑一条）两个语义属性。`UIAnimActionList` 原先用一个「是不是 Operate」同时管着「淡入」与「展开」两级状态，现按这两个属性拆开——`OpenCloseAnimActionList` 的状态守卫也随之按级下放，否则 `_isOpen` 恒为 false 的 Random 类型会连淡入淡出一起被挡掉。
- **轨道混合权重 `Anim Track Blend Weight`**（配在 `AnimActionPlayer` 上，`0~1`，默认 `1.0`）：本播放器的动作**压在更低轨道之上的强度**。1 = 完全覆盖（与本版之前完全一致），小于 1 时与低轨道的姿势按比例混合。
  - 覆盖的**方向**仍由轨道号决定（`EAnimTrack` 枚举值大的轨道盖枚举值小的），权重只决定盖得有多实。
  - Spine 落到 `TrackEntry.Alpha`，Live2D 落到 `CubismMotionController.SetLayerWeight`。轨道播放栈「弹栈恢复上一条」时按**被恢复那条**的权重重设——恢复出来的是一条新的 `TrackEntry` / 层设置，权重不会自己继承。
  - 权重经 `AnimData` 传递，但该字段**刻意不序列化**（与同类的轨道号哨兵一样）：入口在播放器上，逐条状态动画不需要这个旋钮，既有预制体的 `AnimData` 数组布局因此一字未改。
  - 两处后端语义已写进使用文档：Spine 的 `MixBlend.First` 只作用于 0 号轨道（本系统的轨道从 `Body` 起，故 0 号空着）；Cubism 的 0 号层是层混合器的基准层、权重恒为 1，落到那里会告警一次。
- **动作列表的条目间距可调、焦点条目严格居中**——由 `com.ale.toolkit` 1.7.5 提供，本插件侧无需改动，在 `UIAnimActionScrollList` 上多出一个 `Row Pitch Scale`（行距倍率，默认 1.0）。此前想调疏密只能改格子预制体的高度，而那会连带改变格子里所有元素的可用空间；焦点条目也会因顶端轴心比焦点线低 `(缩放 − 1) × 行距 / 2`（缩放峰值 1.5、行距 60 时正好 15px）。

### 变更

- **依赖的 `com.ale.toolkit` 最低版本由 1.7.3 抬到 1.7.5**（行距倍率与焦点居中）。低于该版本插件仍能编译运行，只是动作列表的间距不可调、焦点对不准中线。
- **Live2D：未显式映射的轨道改为按轨道序数自动分层**（`Clamp(序数, 0, LayerCount-1)`）。
  - 此前是「按首次播放的先后抢第一个空闲层」——而 Cubism 的层号就是覆盖优先级，于是先播 `Action` 再播 `Body` 会让 `Body` 拿到更大的层号、**反过来盖住 `Action`**，同一份配置还会因玩家操作顺序不同而每次算出不同的层。新规则单调不降且与播放顺序无关，「枚举值大的轨道覆盖枚举值小的」在 Live2D 侧也成立了。
  - ⚠️ **已有 Live2D 角色且依赖自动分层的工程，层号会变**。显式映射优先，语义不变，需要精确控制请在「轨道映射」里指定。
  - 随之**删除 `Live2dAnimator.live2dLayerIndexDefault`（默认层索引）**：它的用途是「自动分配把层用尽时的兜底层」，钳取之后任何轨道都必得到有效层号，不再存在「用尽」这回事。
- **轨道序数表由 `SpineAnimator` 提取为 `AnimTrackOrdinal`**，供两个后端共用。换算结果与提取前逐值一致（`Body`..`Parts` 恒等、`Action`→19、`Other`→20、Spine 轨道号上界仍为 289）。

## [2.2.0] - 2026-08-10

**两处自研的小系统换成 toolkit 的通用实现：展示文本改用 `TextValue`，动作解锁条件改用 `Ale.Condition`。** 两项都改动序列化布局，属破坏性变更。

顺带查出并修好了一个一直没人发现的问题：**展示名配置从来没接到 UI 上**（详见「修复」第一条）。

### 破坏性变更

- **展示名字段统一改用 `TextValue`**（toolkit 提供）。涉及 `AnimAction.uiDisplayActionName`、`AnimActorSkin.uiDisplaySkinName`、`ProgressBarConfig.uiDisplayName`，以及列表项数据 `UIAnimActionListBoxContent.UIDisplayActionName`。
  - 旧实现是「同名字段按 `ATK_LOCALIZATION` 在 `LocalizedString` 与 `string` 之间换类型」，切宏即丢数据。`TextValue` 把纯文本与多语言条目并置：**纯文本那一项始终存在**，多语言条目是附加的、取不到时自动回退。
  - ⚠️ **升级后需重新指定多语言条目。** 新字段沿用了原字段名，而 Unity 反序列化时真实字段名优先于 `[FormerlySerializedAs]`——旧 YAML 会被喂给新类型、因形状不符而丢弃，兼容字段接不住。这一点无法绕过，故本版不提供自动迁移。纯文本一项本就是新增的，不存在丢失。
  - 三个 UI 组件（`UIBaseProgressBar` / `UIAnimActionListBox` / `UIAnimActorSkinBox`）不再持有 `LocalizeStringEvent` 与 `Text` 两个按宏二选一的字段，改为一个 `TMP_Text`，直接写 `TextValue.ResolveText()`。**预制体需要重新指定该文本组件。**
  - 新增 `AnimLocale`：运行期语言切换的广播。`LocalizeStringEvent` 自带的「语言变了就重刷」由它补回。
  - 副作用（正面）：`ATK_LOCALIZATION` 条件编译由 **7 个运行时文件、20 处** 收敛到 **`AnimLocale.cs` 一处**。

- **动作解锁条件改用 `Ale.Condition`**。`AnimAction.conditions` 由 `AnimActionCondition[]` 改为 `ConditionExpression`；`AnimActionCondition` 结构与 `EAnimActionConditionType` 枚举删除。
  - 免费获得**两级 AND/OR、两级取反、条件分组**，以及内联的条件编辑界面——声明字段即可渲染，本包没有为此写任何编辑器代码。
  - 新增两个判定器：`AnimSim.LevelProgress`（等级进度条的等级）与 `AnimSim.ActionProgress`（进度条的当前进度值）。比较符由旧实现写死的「大于等于」扩展为五种下拉可选。
  - ⚠️ **升级后需重新配置条件。** 同上，类型变更无法自动迁移。
  - **删除「道具持有」条件类型**：它是个 TODO 空壳，且**静默返回「条件满足」**。需要的话自行实现一个 `[ConditionEvaluator("...")]` 类并注册即可，这正是换用条件系统的价值所在。
  - `AnimAction.CheckAllConditionsIsMet(Action<AnimAction>)` → `CheckConditionsIsMet()`；`AnimActionPlayer.GetAnimActionsMeetConditions(Action<AnimAction>)` 去掉入参，成为纯查询。
  - asmdef 新增引用：运行时 `Ale.Condition.Core` / `Ale.Condition.Runtime`，编辑器 `Ale.Condition.Core` / `Ale.Condition.Editor`。

### 修复

- **展示名配置一直没接到 UI 上，同一预制体的多个实例全部显示同一个名字。** 三个 UI 组件的本地化文本字段在示例预制体里**全部未赋值**，`uiDisplayName` 等配置流向 `null`；屏幕上的文字实际来自预制体上 toolkit 的 `LocalizedTextEvent`，而它配的是**每个预制体一个写死的条目**——可这些预制体会被反复实例化。于是三条等级进度条全显示「手臂敏感度」、两条动作进度条全显示「快感」、所有皮肤格与动作格也各自只显示同一个名字。改用 `TextValue` 后这条链路接通，各实例显示各自的名称。示例预制体上那几个 `LocalizedTextEvent` 随之移除（否则两个写入方争写同一个文本组件）。
- **条件判定「失败即开」。** 旧实现在参数解析失败、取不到管理器、进度条名称查不到时一律返回「条件满足」——配错名字的条件会静默失效、动作凭空解锁。新判定器一律判否，并在缺少管理器时给出明确警告。
- **增量解锁只有最后一个订阅者收得到通知。** 旧实现在条件不满足时登记回调，而每次检查都会先把回调置空再只登记最新的一个；那张「未满足条件」映射表还以**活着的 UI 组件的 `GetHashCode()`** 为键，不是稳定标识。现由 `AnimSimulatorManager.OnConditionInputsChanged` 统一广播，各使用方收到后重新求值。

### 新增

- `AnimSimulatorManager` 实现 `IAnimSimConditionSource`（按名取等级 / 取进度值），并提供条件判定用的 `IConditionContext`。
- `AnimSimulatorManager.OnConditionInputsChanged`：条件输入（进度条读数、等级）变化的广播。

## [2.1.2] - 2026-08-10

**一轮全包体检。** 扫描下来，这个包的问题集中在「同一件事写了两遍」——Spine / Live2D 两个后端、角色 / 背景两条资产链、等级条 / 动作条两种进度条、本地化的开 / 关两个分支，四组孪生结构各自复制了一份逻辑，并派生出若干只在其中一份里修过的缺陷。本版把这些结构收敛掉，并修掉沿途查出的运行期缺陷。

**本版不含新功能，也没有可见的行为变化**（下述缺陷修复本身除外）。

### 修复

- **「循环 + 随机间隔」在第二次进入同一状态后退化为只播一次。** 调度器把 `animData.isLoop` 就地改成了 `false`，而 `animData` 是动画组件上序列化数组的元素——改完永久生效，下次 `AddAnimState` 时 `PlayAnimDatas` 里的 `hasLoopInterval` 便判不出来了。现改用 `AnimData.CloneAsOnce()` 取一份一次性副本，原件一字不动。
- **按压过程中切换到另一个动作必然抛空引用。** `StopAnimActionImmediate` 会清空 `_animActionCurrent` / `_animDataCurrent`，但原先只停了「延迟停止」一条协程；按压、松开、进度阻尼、循环完成这四类每帧都在解引用它们，而 `PlayAnimAction` 正是先调该方法再起新动作。现在最先停掉全部在途协程与补间。顺带：按压被打断后 `_isPressModeAnimPlaying` 未复位，会让下次进入按压模式误判为「已在按压中」而恢复上一次的旧进度。
- **`AnimActor` 与 `AnimatorBase` 各存一份「初始状态 / 基础皮肤」，两个 `Start` 各应用一次、执行顺序未定义。** 两处填得不一样时角色最终显示哪一套是不确定的。现统一由 `AnimatorBase` 持有，详见下方「破坏性变更」。
- **Gizmo 两处 `SceneView.lastActiveSceneView` 无判空解引用**（一处是「先抛异常再判空」，另一处完全没判）。Game 视图最大化时该属性为 `null`，画 Gizmo 即抛。
- **`AnimSimulatorManager` 遍历「正在播放」列表时被回调改写集合。** 分发进去的输入回调可能同步走完动作，其完成回调会把该播放器从表里移除，`foreach` 撞 `InvalidOperationException`。拖拽移动与左键抬起两处均改为遍历快照。
- **皮肤格 `SelectSkin` 传入的是旧选中值**，使显示更新分支成为空操作——一直靠 `AnimActor.OnSkinAddOrRemove` 走另一条路兜住，角色引用为空时标记则永不更新。
- **进度条实例化缺两处防护**：配置数组直接取 `.Length`（新建的 `AnimSimulatorConfig` 里这些数组默认为 `null`），以及 `InstantiateProgressBar<T>` 的 `as T` 可能为 `null` 而调用方立刻解引用。后者随泛型参数一并去掉，隐患从根上消失；预制体类型配错时现在有明确告警而非静默空引用。
- **`ClearAllAnim` 未结清在途的淡入淡出**（其余集合都清了），也未清掉循环间隔调度表外层残留的空字典。
- **Live2D 的「默认层索引」形同虚设**：`OnValidate` 里校验它，`MapTrackToLayer` 却回退到 `maxLayer`，Inspector 上那个字段承诺的行为从未实现。
- **动作列表三处对当前播放器的裸解引用**：`FadeAnimActionList` / `OpenCloseAnimActionList` 是 `public` 的，外部在没有播放器时调用即空引用。
- **右键状态在「按住不放又收到一次按下事件」时会被误清成未按下。**
- 清理两处失效逻辑：`UILevelProgressBar.SetInfo` 里不可达的重复判空（日志文案还与入口那条一字不差）、`UIProgressBarView.Init` 里 `TryAdd` 成功分支中多余的重复赋值。

### 变更

均为内部结构调整，序列化布局与运行行为不变：

- **后端契约的 13 个成员由 `abstract` 改为带默认空实现的 `virtual`**，两个后端各 16 行逐字节相同的 `#else` 空桩归零。宏关闭时子类只剩空类体，直接继承默认实现；改契约的编辑点由 4 处降到 1 处。
- **`SpineAnimator` 的轨道压缩表改为静态只读**（原先是可变静态状态，所有实例共享，关闭 Domain Reload 时更会跨播放会话累积）。枚举之外的主轨道号改用确定性折叠，轨道号上界恒为 289。既有配置的换算结果不变。
- **角色与背景的六对孪生加载方法与六个孪生字段抽成 `AnimAssetSlot`**；两段进度条实例化循环靠 `ProgressBarConfig` 新增的 `ResolvePrefab` / `ApplyTo` 两个虚方法收成一个。管理器由 1285 行降至约 1150 行。
- **补间机制归一到 `ToolkitTween`**：进度条滑块与动作进度阻尼两处手写协程改用它，四处无效的 `try/catch StopCoroutine` 收成一个 `KillCor`。缓动曲线与 `timeScale` 取用方式保持原样（OutCubic 手写在回调内、`unscaled` 传 `false`），观感不变。
- **新增 `AnimSimLog` 作为包内统一日志出口**，格式固定为 `[类名] 方法名: 内容`。方法名由 `CallerMemberName` 自动取（消除「方法改名后日志里仍是旧名」这类错误，包内原有 3 处）、类名取自运行期类型、`context` 一律传 `this`（点日志即可选中出问题的对象，原先只有约三分之一传了）。运行时 49 处调用全部迁移，顺带修正 5 处张冠李戴的前缀。
- **两个滚动列表适配器的绑定 / 清空收进 `IUiAnimListCell` 接口与统一助手**（二者继承的是 toolkit 的两个不同基类，C# 单继承下塞不进共同的中间基类）；动作列表的淡入淡出两分支合一并抽出互斥触发器；皮肤组页签的选中态由复制的 `if/else` 压成无分支。
- **三处「查找 AnimatorBase」归一到 `AnimatorBase.FindFor`**，按「自身逐级上溯、每级搜索整棵子树」执行——这是原来三种互不相同的搜索顺序的并集，其中「兄弟分支」一段是必需的（动作播放器与动画组件通常互为兄弟）。
- 欢迎窗口的 `GUIStyle` 移出 `OnGUI`（原先每次重绘都新建）；运行时 asmdef 的 7 个 GUID 引用改为名称引用，与编辑器 asmdef 一致——缺包时 Inspector 能显示缺的是谁，SDK 重导致 GUID 变化时也能自动接上。

### 破坏性变更

本版号是 patch，以下改动都不影响运行行为，但严格说属于 API / 序列化布局变更，特此列出。

- **`AnimActor` 的 `stateInitList` 与 `baseSkins` 移交给动画组件**（`SpineAnimator` / `Live2dAnimator` 的 Inspector）。两个字段降级为隐藏的迁移入口（`[FormerlySerializedAs]` 接住旧数据），`OnValidate` 与 `Awake` 会自动把非空的值搬过去并给出说明去向的日志，**打开一次预制体 / 场景并保存即可固化**。计划在 2.2.0 删除这两个 legacy 字段。
- **删除 3 个方法体为空的公开方法**：`AnimActionPlayer.OnDragMoveSS` / `OnRightClickDown` / `OnRightClickUp`。它们由管理器每次指针移动 / 右键照常调用，而 `AnimActionPlayer` 是 `sealed` 的，空实现不可能作为扩展点。管理器侧的调用同步移除；右键的按下状态改由新增的 `AnimSimulatorManager.IsRightClickDown` 只读属性对外提供。
- **删除 2 个从不被读取的序列化字段**：`AnimActionPlayer` 的「动画动作 选择类型」（实际生效的是 `ActionPlayConfig.animActionSelectType`，留着只会让人在 Inspector 上配了却不起作用）与 `UIAnimActionListBox.btnPlayAction`。
- 删除 `UIAnimActionListBoxContent` / `UIAnimActorSkinBoxContent` 的无参构造（零调用），并去掉 `SetAnimProgressMode` 那个从不使用的入参。
- 新增 `AnimatorBase.StateInitList` 属性与 `AnimatorBase.FindFor` 静态方法（均为新增，不影响既有代码）。

## [2.1.1] - 2026-08-10

**收尾 2.1.0 的动画名迁移，并消除 Spine 侧一处埋着的每帧空转。**

### 破坏性变更

- **移除兼容字段 `AnimData.animRefAsset` 与 `AnimAction.animReferenceAsset`**，动画一律由字符串 `animName` 指定。
  - ⚠️ **这次移除原本公告在 2.2.0，实际提前到了本版本**（2.1.1 是 patch 号，按语义化版本本不该承载破坏性变更，特此说明）。升级前请确认自己工程里的角色预制体与动画动作已经填好 `Anim Name`——2.1.0 起 Inspector 里就有这个字段，留空时才回退读旧的引用资源，现在回退分支已不存在，留空即播不出动画。
  - 随之 `AnimData` 的「按 Spine 动画引用资源」构造重载一并移除；`ResolveAnimName()` 未填名时返回 `null`。
  - 预制体里残留的 `animRefAsset:` / `animReferenceAsset:` YAML 键会被 Unity 忽略，并在下次保存该预制体时自动清除，无需手工处理。
- 副作用（正面）：`AnimData.cs` 与 `AnimActionPlayer.cs` 的 `#if ASS_SPINE` 条件编译**归零**。后端宏现在只出现在各自的后端文件里——`SpineAnimator.cs` 与 `Live2dAnimator.cs`。

### 变更

- **`SpineAnimator` 内部对轨道号做保序压缩**，序列化配置与对外 API 均不变。
  - 起因：`EAnimTrack.Action = 900`、`Other = 999`，算出的轨道号（`主轨道 × 10 + 子轨道`）高达 9000..9999；而 Spine 的 `SetAnimation` 会把 `AnimationState.tracks` 扩容到 `trackIndex + 1`，并在 Update / Apply 等六处按 `tracks.Count` 全量遍历——播一条 `Other` 轨道的动画就要每帧空转近六万次。
  - 做法：改用 `EAnimTrack` 的**声明序数**作紧凑主轨道号。这保住了「轨道号即混合优先级、高轨道覆盖低轨道」这一 `EAnimTrack.Action` 所依赖的语义（若按首次使用顺序发号则会把优先级压反）。
  - **`Body`..`Parts`（值 1..18）的序数恰等于其值，因此除 `Action`(900→19) 与 `Other`(999→20) 外，既有配置的换算都是恒等式**，行为零变化。轨道号上界由 9999 降至 209。

### 文档

- 使用文档的「测试场景」一节改写为 **「正式项目的资产布局」**：删除遗留的 `VNStoryTest.unity`（该场景属于另一个系统，本包内无从验证），并把其中的路径清单明确为**建议布局**而非既存目录；「打开场景直接运行」的内容本就由上面的「示例场景」一节完整覆盖。
- 美术资产规范文档清理同批遗留：标题、简介与目录项由 “VNStoryManager 剧情演出系统” 更正为本系统，其中指向外部仓库 VNStoryManager 文档的链接改为指向本包自己的使用文档。
- 状态数据与动画动作两处的 “Anim Ref Asset / Anim Reference Asset” 说明随字段移除一并删除；「Anim Track」条目补充上述保序压缩的说明。
- 修正使用文档中 4 处失效 / 张冠李戴的链接与措辞：两处进度条章节的锚点缺了「 配置」后缀而点不过去；Spine 官方教程那条的 B 站搜索关键词写的是 “Dialogue System”；Live2D 官方网站那条把其文档描述成了 “Dialogue System 的各项功能”。

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
- **`AnimAction.animReferenceAsset` 与 `AnimData.animRefAsset` 降级为兼容字段**，仅在 `animName` 留空时作为回退。计划在 2.2.0 移除，新配置请直接填动画名。（*后续更正：实际已在 2.1.1 移除，见上。*）
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
