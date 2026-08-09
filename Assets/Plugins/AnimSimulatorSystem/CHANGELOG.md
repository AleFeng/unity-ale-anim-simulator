# 更新日志（Changelog）

本文件记录 Anim Simulator System（`com.fs.animsimulatorsystem`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

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
