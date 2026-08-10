using System;
using System.Collections.Generic;
using UnityEngine;
using Ale.Toolkit.Runtime;

#if ATK_INPUT_SYSTEM
using UnityEngine.InputSystem;
// 输入绑定器所在程序集同样受 ATK_INPUT_SYSTEM 约束，宏关闭时该命名空间不存在，故 using 也须一并门控。
using Ale.Toolkit.Runtime.InputSupport;
#endif
#if DOTWEEN
using DG.Tweening;
#endif
#if UNITY_EDITOR && ATK_ADDRESSABLE
// 仅供「测试用」字段直接挂载资产引用，非运行期依赖。
using UnityEngine.AddressableAssets;
#endif

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画模拟器管理器
    /// 主流程的执行与子组件的管理
    /// </summary>
    public class AnimSimulatorManager : ToolkitMonoSingleton<AnimSimulatorManager>
    {
        /// <summary>
        /// 初始化。由基类在 Awake 中设置好单例实例、并完成 DontDestroyOnLoad 之后调用。
        /// 重复实例不会走到这里——基类在 Awake 里已把后来者销毁。
        /// </summary>
        protected override void Init()
        {
            if (!animSimulatorConfig)
                Debug.LogWarning("[AnimSimulatorManager] AnimSimulatorConfig 未设置，动画模拟器系统 无法正常工作！");

            // 初始化 UI设置
            InitUI();
            // 初始化 进度条管理器
            InitProgressBar();
            // 初始化 角色管理器
            InitActor();
        }

        private void OnEnable()
        {
            // 订阅输入事件
            OnEnableInput();

#if UNITY_EDITOR
            // 测试用 直接加载指定角色
            if (HasTestActor)
            {
                // 开始 动画模拟器
                StartAnimSimulator();
                // 测试用 直接加载指定角色
                ReloadTestActor();
            }
#endif
        }

        private void OnDisable()
        {
            // 取消输入事件订阅
            OnDisableInput();
        }

        #region 基础设置
        [Header("基础设置")]
        [Tooltip("动画模拟器 系统配置")]
        [SerializeField] private AnimSimulatorConfig animSimulatorConfig;
        [Tooltip("玩家相机 组件（若为空则使用 主相机）")]
        [SerializeField] private Camera playerCamera;
        
        private bool _isAnimSimulatorStarted; // 动画模拟器 是否已开始
        
        /// <summary>
        /// 开始 动画模拟器
        /// </summary>
        public void StartAnimSimulator()
        {
            if (_isAnimSimulatorStarted) return; // 已经开始则不重复执行
            _isAnimSimulatorStarted = true;
            
            // 淡入 UI
            FadeInUI();
            // 淡入 角色。可能被暂时隐藏，之后继续使用。
            if (_animActorCurrent)
                _animActorCurrent.FadeIn();
            else if (_actorCurrentInstance)
                _actorCurrentInstance.SetActive(true);
            // 淡入 背景。可能被暂时隐藏，之后继续使用。
            if (_backgroundAnimActorCurrent)
                _backgroundAnimActorCurrent.FadeIn();
            else if (_backgroundCurrentInstance)
                _backgroundCurrentInstance.SetActive(true);
        }
        
        /// <summary>
        /// 结束 动画模拟器
        /// </summary>
        public void StopAnimSimulator(bool clearAllData = true)
        {
            if (!_isAnimSimulatorStarted) return; // 已经结束则不重复执行
            _isAnimSimulatorStarted = false;
            
            // 淡出 UI
            FadeOutUI(() =>
            {
                // 清除 所有数据
                if (clearAllData)
                {
                    UnloadActor(); // 卸载角色
                    UnloadBackground(); // 卸载背景
                }
            });
            // 淡出 角色
            if (_animActorCurrent)
                _animActorCurrent.FadeOut();
            else if (_actorCurrentInstance)
                _actorCurrentInstance.SetActive(false);
            // 淡出 背景
            if (_backgroundAnimActorCurrent)
                _backgroundAnimActorCurrent.FadeOut();
            else if (_backgroundCurrentInstance)
                _backgroundCurrentInstance.SetActive(false);
        }
        
        /// <summary>
        /// 使用参数 开始 动画模拟器。
        /// </summary>
        /// <param name="param">参数字符串，格式为 "ActorName" 或 "ActorName|SceneName"</param>
        public void StartAnimSimulatorWithParam(string param)
        {
            if (string.IsNullOrEmpty(param))
            {
                Debug.LogWarning("[AnimSimulatorManager] StartAnimSimulatorWithParam: param 为空，无法启动动画模拟器。");
                return;
            }
            
            // 解析参数，使用 | 分割
            var parts = param.Split('|');
            var actorName = parts.Length >= 1 ? parts[0].Trim() : null;
            var sceneName = parts.Length >= 2 ? parts[1].Trim() : null;
            
            // 加载 角色
            if (!string.IsNullOrEmpty(actorName))
            {
                LoadActor(actorName);
            }
            // 加载 场景（背景）
            if (!string.IsNullOrEmpty(sceneName))
            {
                LoadBackground(sceneName);
            }
            
            // 开始 动画模拟器
            StartAnimSimulator();
        }

        #region 射线检测
        /// <summary>
        /// 屏幕坐标转世界坐标
        /// </summary>
        /// <param name="screenPos"></param>
        /// <returns></returns>
        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            Camera cam = playerCamera ? playerCamera : Camera.main;
            if (cam)
            {
                Vector3 sp = new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z));
                return cam.ScreenToWorldPoint(sp);
            }
            
            return new Vector3(screenPos.x, screenPos.y, 0f);
        }
        
        /// <summary>
        /// 从相机发射射线进行检测
        /// </summary>
        /// <param name="rayScreenPos"></param>
        /// <param name="hitWorldPos"></param>
        /// <returns></returns>
        private Component RaycastFromCamera(Vector2 rayScreenPos, out Vector3 hitWorldPos)
        {
            hitWorldPos = Vector3.zero;
            
            // 获取相机
            Camera cam = playerCamera;
            if (!cam)
            {
                cam = Camera.main;
            }
            if (!cam) return null;

            // 使用相机的像素矩形将屏幕坐标映射到相机视口（支持 RenderTexture / 局部显示）
            Rect camRect = cam.pixelRect;
            Ray ray;
            if (camRect.width > 0f && camRect.height > 0f)
            {
                Vector2 vp = new Vector2((rayScreenPos.x - camRect.x) / camRect.width, (rayScreenPos.y - camRect.y) / camRect.height);
                // 若点击位置不在该相机视口内，则不命中
                if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return null;
                ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
            }
            else
            {
                // fallback：直接用屏幕坐标转射线
                ray = cam.ScreenPointToRay(rayScreenPos);
            }
            
            // 检测 2D 碰撞体（使用射线与 2D 碰撞体相交）
            RaycastHit2D hit2D = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
            if (hit2D.collider)
            {
                hitWorldPos = hit2D.point;
                return hit2D.collider;
            }
            
            // 检测 3D 碰撞体
            if (Physics.Raycast(ray, out RaycastHit hit3D, Mathf.Infinity))
            {
                hitWorldPos = hit3D.point;
                return hit3D.collider;
            }

            return null;
        }
        #endregion
        #endregion

        #region UI设置
        [Header("UI设置")]
        [Tooltip("UI Canvas 组件")]
        [SerializeField] private Canvas uiCanvas;
        [Tooltip("UI CanvasGroup 组件（用于控制 UI淡入淡出）")]
        [SerializeField] private CanvasGroup uiCanvasGroup;

        // 当前是否正在淡入UI。用于控制在拖拽过程中 不切换动画动作播放器时，保持UI状态不变。
        private bool _isUiFadeIn;
        
        /// <summary>
        /// 初始化 UI设置
        /// </summary>
        private void InitUI()
        {
            // 初始化 UI设置
            // UI画布 初始化非激活。等待其他系统 调用打开。
            if (uiCanvasGroup)
                uiCanvasGroup.alpha = 0f;
            if (uiCanvas)
                uiCanvas.gameObject.SetActive(false);
            _isUiFadeIn = false;
        }
        
        /// <summary>
        /// 淡入 UI
        /// </summary>
        private void FadeInUI()
        {
            if (_isUiFadeIn) return; // 已经是淡入状态则不重复执行
            _isUiFadeIn = true;
            
            // 淡入 UI
            if (uiCanvasGroup)
            {
                // 激活 UI
                if (uiCanvas)
                    uiCanvas.gameObject.SetActive(true);
#if DOTWEEN
                // DoTween 淡入动画
                uiCanvasGroup.DOFade(1f, 0.5f);
#else
                uiCanvasGroup.alpha = 1f;
#endif
            }
            else if (uiCanvas)
            {
                uiCanvas.gameObject.SetActive(true);
            }
        }
        
        /// <summary>
        /// 淡出 UI
        /// </summary>
        /// <param name="onComplete"></param>
        private void FadeOutUI(Action onComplete = null)
        {
            if (_isUiFadeIn == false) return; // 已经是淡出状态则不重复执行
            _isUiFadeIn = false;
            
            // 淡出 UI
            if (uiCanvasGroup)
            {
#if DOTWEEN
                // DoTween 淡出动画
                uiCanvasGroup.DOFade(0f, 0.5f).OnComplete(() =>
                {
                    // 非激活 UI
                    uiCanvas.gameObject.SetActive(false);
                    // 完成回调
                    onComplete?.Invoke();
                });
#else
                uiCanvasGroup.alpha = 0f;
                uiCanvas.gameObject.SetActive(false);
#endif
            }
            else if (uiCanvas)
            {
                uiCanvas.gameObject.SetActive(false);
            }
        }
        #endregion
        
        #region 操作输入
        [Header("操作输入")]
        [Tooltip("输入动作映射名称")]
        [SerializeField] private string inputActionMapName = "UI";
        [Tooltip("光标移动动作名称")]
        [SerializeField] private string inputActionPointName = "Point";
        [Tooltip("光标左键点击动作名称")]
        [SerializeField] private string inputActionLeftClickName = "Click";
        [Tooltip("光标右键点击动作名称")]
        [SerializeField] private string inputActionRightClickName = "RightClick";
        
        #region 操作事件注册
        /// <summary>
        /// 启用输入事件订阅
        /// </summary>
        private void OnEnableInput()
        {
#if ATK_INPUT_SYSTEM
            // 订阅 Input System 事件。
            // 绑定器允许此刻输入源（PlayerInput）尚未生成——它会把绑定挂起并逐帧重试，不会丢回调，
            // 因此这里无需自行判断输入是否就绪。
            // 光标移动
            ToolkitInputBinder.Bind
            (
                inputActionMapName,
                inputActionPointName,
                OnPointMove
            );
            // 光标左键点击
            ToolkitInputBinder.Bind
            (
                inputActionMapName,
                inputActionLeftClickName,
                OnLeftClick
            );
            // 光标右键点击
            ToolkitInputBinder.Bind
            (
                inputActionMapName,
                inputActionRightClickName,
                OnRightClick
            );
#endif
        }
        
        /// <summary>
        /// 禁用输入事件订阅
        /// </summary>
        private void OnDisableInput()
        {
#if ATK_INPUT_SYSTEM
            // 取消订阅 Input System 事件。绑定已生效则退订，仍在挂起则从待生效队列移除，两种情况都能正确撤销。
            // 光标移动
            ToolkitInputBinder.Unbind
            (
                inputActionMapName,
                inputActionPointName,
                OnPointMove
            );
            // 光标左键点击
            ToolkitInputBinder.Unbind
            (
                inputActionMapName,
                inputActionLeftClickName,
                OnLeftClick
            );
            // 光标右键点击
            ToolkitInputBinder.Unbind
            (
                inputActionMapName,
                inputActionRightClickName,
                OnRightClick
            );
#endif
        }
        #endregion

        #region 操作事件处理
        private bool _isDragging; // 是否正在拖拽中
        private bool _isLeftClickDown; // 是否 左键按下
        private bool _isRightClickDown; // 是否 右键按下
        private Vector2 _cursorScreenPos; // 光标屏幕坐标
        private Vector2 _cursorScreenPosLast; // 上一次光标的屏幕坐标（用于计算增量移动）
        private Vector3 _cursorWorldPos; // 光标世界坐标
        private Vector3 _cursorWorldPosLast; // 上一次光标的世界坐标（用于计算增量移动）
        
        // 当前光标悬停的 AnimActionPlayer组件
        private AnimActionPlayer _animActionPlayerHover;
        // 当前正在播放的 AnimActionPlayer组件 列表
        private List<AnimActionPlayer> _animActionPlayerPlayingList = new List<AnimActionPlayer>();
        
#if ATK_INPUT_SYSTEM
        /// <summary>
        /// 光标移动处理
        /// 现在会从相机发射射线检测碰撞体，并尝试获取碰撞体上的 AnimActionPlayer 组件。
        /// 当检测到的 AnimActionPlayer 变化时，会调用 OnAnimActionPlayerChange(newPlayer)。
        /// </summary>
        /// <param name="ctx"></param>
        private void OnPointMove(InputAction.CallbackContext ctx)
        {
            // 光标的屏幕坐标 更新
            _cursorScreenPos = ctx.ReadValue<Vector2>();
            // 射线检测 碰撞体
            var hit = RaycastFromCamera(_cursorScreenPos, out Vector3 hitWorldPos);
            // 命中，使用射线的命中点世界位置 作为 光标世界位置
            _cursorWorldPos = hit ? hitWorldPos :
                // 未命中，使用屏幕坐标转换的世界位置 作为 光标世界位置
                ScreenToWorld(_cursorScreenPos);

            // 从命中的对象上获取 AnimActionPlayer组件
            AnimActionPlayer animActionPlayerFound = null;
            if (hit)
                // 获取 AnimActionPlayer组件
                animActionPlayerFound = hit.GetComponent<AnimActionPlayer>();
            // 替换 当前悬停的 AnimActionPlayer组件。为null时则替换为空。
            ReplaceAnimActionPlayer(animActionPlayerFound);
            
            // 处理拖拽移动
            if (_isLeftClickDown)
            {
                // 计算增量移动向量
                Vector2 cursorDeltaDirSs = _cursorScreenPos - _cursorScreenPosLast;
                Vector3 cursorDeltaDirWs = _cursorWorldPos - _cursorWorldPosLast;
                // 处理拖拽移动
                if (cursorDeltaDirSs.sqrMagnitude > Mathf.Epsilon || cursorDeltaDirWs.sqrMagnitude > Mathf.Epsilon)
                {
                    // 播放中的模块 处理拖拽移动
                    if (_isDragging && _animActionPlayerPlayingList.Count > 0)
                    {
                        foreach (var animActionPlayer in _animActionPlayerPlayingList)
                        {
                            // 通知模块拖拽移动。屏幕空间
                            animActionPlayer.OnDragMoveSS(_cursorScreenPos, cursorDeltaDirSs);
                            // 通知模块拖拽移动。世界空间
                            animActionPlayer.OnDragMoveWS(_cursorWorldPos, cursorDeltaDirWs);
                        }
                    }
                    _cursorScreenPosLast = _cursorScreenPos; // 更新上次 屏幕位置
                    _cursorWorldPosLast = _cursorWorldPos; // 更新上次 世界位置
                }
            }
        }
        
        /// <summary>
        /// 光标左键点击处理
        /// </summary>
        /// <param name="ctx"></param>
        private void OnLeftClick(InputAction.CallbackContext ctx)
        {
            // 判断左键按下还是抬起
            bool isDown = Mathf.Approximately(ctx.ReadValue<float>(), 1);
            if (isDown && _isLeftClickDown == false)
            {
                // 左键按下
                _isLeftClickDown = true;
                _cursorScreenPosLast = _cursorScreenPos; // 记录 起始屏幕位置
                _cursorWorldPosLast = _cursorWorldPos; // 记录 起始世界位置
                
                // 开始拖拽
                _isDragging = true;
                
                // 通知模块开始拖拽
                if (_animActionPlayerHover)
                {
                    // 通知模块开始拖拽
                    bool isPlaySuccess = _animActionPlayerHover.OnLeftClickDown
                    (
                        _cursorWorldPos, 
                        (animActionPlayer) =>
                        {
                            // 动作完成后的回调
                            // 清除 当前正在播放的模块
                            if (_animActionPlayerPlayingList.Contains(animActionPlayer))
                                _animActionPlayerPlayingList.Remove(animActionPlayer);
                            // 淡入 动画动作列表。
                            FadeAnimActionList(animActionPlayer, true);
                            // 仍然是当前悬停的模块时，打开列表
                            if (_animActionPlayerHover == animActionPlayer)
                                OpenCloseAnimActionList(_animActionPlayerHover, true);
                        }
                    );
                    
                    // 若动作播放成功
                    if (isPlaySuccess)
                    {
                        // 记录 当前正在播放的模块
                        if (_animActionPlayerPlayingList.Contains(_animActionPlayerHover) == false)
                            _animActionPlayerPlayingList.Add(_animActionPlayerHover);
                        // 淡出 动画动作列表
                        FadeAnimActionList(_animActionPlayerHover, false);
                    }
                }
            }
            else if (_isLeftClickDown)
            {
                // 左键抬起
                _isLeftClickDown = false;
                _cursorScreenPosLast = _cursorScreenPos; // 记录 结束屏幕位置
                _cursorWorldPosLast = _cursorWorldPos; // 记录 结束世界位置
                
                // 通知 正在播放的模块
                foreach (var animActionPlayer in _animActionPlayerPlayingList)
                    animActionPlayer.OnLeftClickUp(_cursorWorldPos);
                // 通知 悬停的模块（若不是 正在播放的模块）
                if (_animActionPlayerHover && _animActionPlayerPlayingList.Contains(_animActionPlayerHover) == false)
                    _animActionPlayerHover.OnLeftClickUp(_cursorWorldPos);
                
                // 结束拖拽
                _isDragging = false;
            }
        }
        
        /// <summary>
        /// 光标右键按下
        /// </summary>
        /// <param name="ctx"></param>
        private void OnRightClick(InputAction.CallbackContext ctx)
        {
            // 判断左键按下还是抬起
            bool isDown = Mathf.Approximately(ctx.ReadValue<float>(), 1);
            
            if (isDown && _isRightClickDown == false)
            {
                // 右键按下
                _isRightClickDown = true;
                
                // 通知模块右键按下
                if (_animActionPlayerHover)
                    _animActionPlayerHover.OnRightClickDown(_cursorWorldPos);
            }
            else if (_isRightClickDown)
            {
                // 右键抬起
                _isRightClickDown = false;
                
                // 通知模块 右键抬起
                if (_animActionPlayerHover)
                    _animActionPlayerHover.OnRightClickUp(_cursorWorldPos);
            }
        }
#endif
        #endregion
        #endregion

        #region 动画动作播放器 管理
        [Header("动画动作播放器")]
        [Tooltip("动画动作列表 UI 根节点")]
        [SerializeField] private RectTransform uiAnimActionListRoot;
        
        // 字典 Key：动画动作播放器 名称 Value：动画动作播放器
        private readonly Dictionary<string, AnimActionPlayer> _animActionPlayerRegisterDic =
            new Dictionary<string, AnimActionPlayer>();
        // 字典 Key：动画动作播放器 Value：动画动作播放器列表UI 实例
        private readonly Dictionary<AnimActionPlayer, UIAnimActionList> _animActionPlayerToUIListDic =
            new Dictionary<AnimActionPlayer, UIAnimActionList>();
        // 动画动作播放器列表UI 实例 空闲列表
        private readonly List<UIAnimActionList> _animActionListInstanceListFree = new List<UIAnimActionList>();

        #region 注册与注销
        /// <summary>
        /// 注册 动画动作播放器
        /// </summary>
        /// <param name="animActionPlayer"></param>
        public bool RegisterAnimActionPlayer(AnimActionPlayer animActionPlayer)
        {
            // 注册 动画动作播放器
            if (!animActionPlayer) return false;
            // 重名则 直接覆盖。之后也会分配对应的 列表UI
            if (_animActionPlayerRegisterDic.ContainsKey(animActionPlayer.ActionPlayerName))
                Debug.LogWarning($"[AnimSimulatorManager] RegisterAnimActionPlayer: 动画动作播放器 名称 '{animActionPlayer.ActionPlayerName}' 重复注册，已覆盖旧的实例。");
            _animActionPlayerRegisterDic[animActionPlayer.ActionPlayerName] = animActionPlayer;

            // 从空闲列表 获取  动画动作播放器列表UI 实例
            UIAnimActionList uiAnimActionListInstance = null;
            if (_animActionListInstanceListFree.Count > 0)
            {
                uiAnimActionListInstance = _animActionListInstanceListFree[0];
                _animActionListInstanceListFree.RemoveAt(0);
            }
            else if (animSimulatorConfig)
            {
                // 创建一个新的 实例
                if (animSimulatorConfig.uiAnimActionListPrefab)
                    uiAnimActionListInstance = Instantiate(animSimulatorConfig.uiAnimActionListPrefab, uiAnimActionListRoot);
            }
            // 关联 动画动作播放器 与 列表UI实例
            if (uiAnimActionListInstance)
            {
                uiAnimActionListInstance.UICanvas = uiCanvas;
                // 设置 动画动作播放器
                uiAnimActionListInstance.SetAnimActionPlayer(animActionPlayer);
                // 添加到 使用中列表
                _animActionPlayerToUIListDic.Add(animActionPlayer, uiAnimActionListInstance);

                return true;
            }

            return false;
        }

        /// <summary>
        /// 注销 动画动作播放器
        /// </summary>
        /// <param name="animActionPlayer"></param>
        public bool UnregisterAnimActionPlayer(AnimActionPlayer animActionPlayer)
        {
            // 注销 动画动作播放器
            if (!animActionPlayer) return false;
            
            // 从 注册列表 移除
            _animActionPlayerRegisterDic.Remove(animActionPlayer.ActionPlayerName);
            
            // 从 列表UI使用中列表 移除
            if (_animActionPlayerToUIListDic.TryGetValue(animActionPlayer, out var animActionListInstance))
            {
                // 获取 对应的 动画动作播放器 列表实例
                if (animActionListInstance)
                {
                    // 隐藏 列表实例
                    animActionListInstance.SetAnimActionPlayer(null);
                    // 添加到 空闲列表
                    _animActionListInstanceListFree.Add(animActionListInstance);
                }
                // 从 使用中列表 移除
                _animActionPlayerToUIListDic.Remove(animActionPlayer);
            }
            
            return true;
        }
        #endregion

        #region 动画播放器 操作
        /// <summary>
        /// 替换 当前悬停的 动画动作播放器 组件
        /// </summary>
        /// <param name="animActionPlayerReplace"></param>
        /// <param name="isForceCanOperate">强制设置为 允许操作</param>
        public void ReplaceAnimActionPlayer(AnimActionPlayer animActionPlayerReplace, bool isForceCanOperate = false)
        {
            // 当 AnimActionPlayerHover组件变化时，进行替换
            if (_animActionPlayerHover != animActionPlayerReplace)
            {
                // 关闭 旧的动画动作播放器
                if (_animActionPlayerHover)
                {
                    OpenCloseAnimActionList(_animActionPlayerHover, false); // 旧的 淡出
                    _animActionPlayerHover.SetIsCanOperate(false); // 旧的 设置为不可操作
                }
                
                // 更新 当前悬停的 动画动作播放器
                _animActionPlayerHover = animActionPlayerReplace;
                
                // 打开 新的动画动作播放器。检查 拖拽中状态。
                if (_animActionPlayerHover && _isDragging == false)
                {
                    _animActionPlayerHover.SetIsCanOperate(true); // 新的 设置为可操作
                    OpenCloseAnimActionList(_animActionPlayerHover, true); // 新的 淡入
                }
            }
            
            // 强制设置为 允许操作时，再次进行设置
            if (isForceCanOperate && _animActionPlayerHover)
                _animActionPlayerHover.SetIsCanOperate(true, true);
        }
        
        /// <summary>
        /// 播放 动画动作
        /// </summary>
        /// <param name="animActionPlayerName">动画动作播放器 名称</param>
        /// <param name="animActionSelectType">动画动作 选择类型</param>
        /// <param name="onActionStart">动画动作 开始的回调</param>
        public void PlayAnimActionPlayerByType
        (
            string animActionPlayerName, 
            EAnimActionSelectType animActionSelectType,
            Action<AnimActionPlayer> onActionStart = null
        )
        {
            // 获取 指定的 动画动作播放器
            if (_animActionPlayerRegisterDic.TryGetValue(animActionPlayerName, out var animActionPlayer))
                // 播放 动画动作
                animActionPlayer.PlayAnimActionByType(animActionSelectType, null, onActionStart);
            else
                Debug.LogWarning($"[AnimSimulatorManager] PlayAnimAction: 未找到名称为 '{animActionPlayerName}' 的 动画动作播放器，请检查 注册情况。");
        }
        
        /// <summary>
        /// 将 动画动作播放器 移动到光标位置
        /// </summary>
        /// <param name="animActionPlayerName"></param>
        public void MoveAnimActionPlayerToCursor(string animActionPlayerName)
        {
            // 获取 指定的 动画动作播放器
            if (_animActionPlayerRegisterDic.TryGetValue(animActionPlayerName, out var animActionPlayer))
            {
                // 将AnimActionPlayer移动到鼠标的位置。
                animActionPlayer.transform.position = _cursorWorldPos;
                // 将对应的 动画动作列表UI 也移动到光标位置
                if (_animActionPlayerToUIListDic != null &&
                    _animActionPlayerToUIListDic.TryGetValue(animActionPlayer, out var animActionListInstance))
                    animActionListInstance.SetToCanvasSpacePosition(_cursorWorldPos);
            }
            else
                Debug.LogWarning($"[AnimSimulatorManager] MoveAnimActionPlayerToCursor: 未找到名称为 '{animActionPlayerName}' 的 动画动作播放器，请检查 注册情况。");
        }
        
        /// <summary>
        /// 淡入或淡出 动画动作列表
        /// </summary>
        /// <param name="animActionPlayer"></param>
        /// <param name="isFadeIn"></param>
        /// <param name="isForceFadeIn">强制淡入。不判断AnimActionPlayer的设定。</param>
        public void FadeAnimActionList(AnimActionPlayer animActionPlayer, bool isFadeIn, bool isForceFadeIn = false)
        {
            if (!animActionPlayer) return;

            // 获取 对应的 动画动作播放器 列表实例
            if (_animActionPlayerToUIListDic == null ||
                !_animActionPlayerToUIListDic.TryGetValue(animActionPlayer, out var animActionListInstance)) return;

            // 执行 淡入或淡出 动画
            if (animActionListInstance)
                animActionListInstance.FadeAnimActionList(isFadeIn, isForceFadeIn);
        }
        
        /// <summary>
        /// 打开或关闭 动画动作列表
        /// </summary>
        /// <param name="animActionPlayer"></param>
        /// <param name="isOpen"></param>
        /// <param name="isForceOpen">强制打开。不判断AnimActionPlayer的设定。</param>
        public void OpenCloseAnimActionList(AnimActionPlayer animActionPlayer, bool isOpen, bool isForceOpen = false)
        {
            if (!animActionPlayer) return;

            // 获取 对应的 动画动作播放器 列表实例
            if (_animActionPlayerToUIListDic.TryGetValue(animActionPlayer, out var animActionListInstance))
            {
                // 执行 打开或关闭 动画
                if (animActionListInstance)
                    animActionListInstance.OpenCloseAnimActionList(isOpen, isForceOpen);
            }
        }
        #endregion
        #endregion
        
        #region 动画角色皮肤组 管理
        [Header("动画角色皮肤组")]
        [Tooltip("UI皮肤组列表 根节点")]
        [SerializeField] private RectTransform uiAnimActorSkinGroupListRoot;
        
        // 角色皮肤组 列表UI 实例
        private UIAnimActorSkinGroupList _uiAnimActorSkinGroupListInstance;
        
        /// <summary>
        /// 初始化 角色皮肤
        /// </summary>
        private void InitActorSkin()
        {
            if (!animSimulatorConfig) return;
            
            // 初始化 进度条UI
            // 清理旧的 角色皮肤组 列表UI 实例
            if (_uiAnimActorSkinGroupListInstance)
            {
                Destroy(_uiAnimActorSkinGroupListInstance.gameObject);
                _uiAnimActorSkinGroupListInstance = null;
            }
            // 实例化新的 角色皮肤组 列表UI 实例
            if (!animSimulatorConfig.uiAnimActorSkinGroupListPrefab)
            {
                Debug.LogWarning("[AnimSimulatorManager] AwakeProgressBar: AnimSimulatorConfig 中 未配置 UIAnimActorSkinGroupListPrefab，角色皮肤功能无法使用！");
            }
            else
            {
                // 实例化 进度条UI预制体
                if (animSimulatorConfig.uiAnimActorSkinGroupListPrefab)
                    _uiAnimActorSkinGroupListInstance = Instantiate(animSimulatorConfig.uiAnimActorSkinGroupListPrefab, uiAnimActorSkinGroupListRoot);
            }
            if (!_uiAnimActorSkinGroupListInstance)
            {
                Debug.LogWarning("[AnimSimulatorManager] InitActorSkin: 角色皮肤组 列表UI实例化失败，角色皮肤功能无法使用！");
            }
        }
        
        /// <summary>
        /// 刷新 角色皮肤组 列表UI
        /// </summary>
        private void RefreshUIAnimActorSkinGroupList()
        {
            if (_animActorCurrent)
            {
                // 刷新 皮肤列表UI
                if (_uiAnimActorSkinGroupListInstance)
                    _uiAnimActorSkinGroupListInstance.SetAnimActor(_animActorCurrent);
            }
        }
        #endregion
        
        #region 进度条 管理
        [Header("进度条")]
        [Tooltip("UI进度条视口 根节点")]
        [SerializeField] private RectTransform uiProgressBarViewRoot;
        
        // UI进度条视口 实例
        private UIProgressBarView _uiProgressBarViewInstance;
        // 进度条实例 字典（Key：进度条名称）
        private Dictionary<string, UIBaseProgressBar> _progressBarInstanceDic = new Dictionary<string, UIBaseProgressBar>();

        #region 初始化
        /// <summary>
        /// 初始化 进度条管理器
        /// </summary>
        private void InitProgressBar()
        {
            // 初始化 进度条管理器
            if (!animSimulatorConfig) return;
            
            // 初始化 进度条UI
            // 清理旧的 进度条UI实例
            if (_uiProgressBarViewInstance)
            {
                Destroy(_uiProgressBarViewInstance.gameObject);
                _uiProgressBarViewInstance = null;
            }
            // 实例化新的 进度条UI实例
            if (!animSimulatorConfig.uiProgressBarViewPrefab)
            {
                Debug.LogWarning("[AnimSimulatorManager] AwakeProgressBar: AnimSimulatorConfig 中 未配置 ProgressBarUI，进度条功能无法使用！");
            }
            else
            {
                // 实例化 进度条UI预制体
                _uiProgressBarViewInstance = Instantiate(animSimulatorConfig.uiProgressBarViewPrefab, uiProgressBarViewRoot);
            }
            if (!_uiProgressBarViewInstance)
            {
                Debug.LogWarning("[AnimSimulatorManager] InitProgressBar: 进度条UI实例化失败，进度条功能无法使用！");
                return;
            }
            
            // 初始化 等级进度条
            for (int i = 0; i < animSimulatorConfig.levelProgressBarConfigs.Length; i++)
            {
                var levelProgressBarConfig = animSimulatorConfig.levelProgressBarConfigs[i];
                if (levelProgressBarConfig == null) continue;
                
                // 获取 等级进度条 预制体
                var levelProgressBar = levelProgressBarConfig.uiLevelProgressBar;
                if (!levelProgressBar)
                    levelProgressBar = animSimulatorConfig.uiLevelProgressBarDefault;
                if (!levelProgressBar)
                {
                    Debug.LogWarning("[AnimSimulatorManager] InitProgressBar: AnimSimulatorConfig 中 未配置 LevelProgressBar预制体，等级进度条功能无法使用！");
                    continue;
                }
                
                // 实例化 等级进度条
                var levelProgressBarInstance = InstantiateProgressBar<UILevelProgressBar>
                (
                    levelProgressBarConfig,
                    levelProgressBar,
                    levelProgressBarConfig.uiGroupName
                );
                // 设置信息
                levelProgressBarInstance.SetInfo(levelProgressBarConfig, 0, 0);
            }
            
            // 初始化 动作进度条
            for (int i = 0; i < animSimulatorConfig.actionProgressBarConfigs.Length; i++)
            {
                var actionProgressBarConfig = animSimulatorConfig.actionProgressBarConfigs[i];
                if (actionProgressBarConfig == null) continue;
                
                // 获取 动作进度条 预制体
                var actionProgressBar = actionProgressBarConfig.uiActionProgressBar;
                if (!actionProgressBar)
                    actionProgressBar = animSimulatorConfig.uiActionProgressBarDefault;
                if (!actionProgressBar)
                {
                    Debug.LogWarning("[AnimSimulatorManager] InitProgressBar: AnimSimulatorConfig 中 未配置 ActionProgressBar预制体，动作进度条功能无法使用！");
                    continue;
                }
                
                // 实例化 动作进度条
                var actionProgressBarInstance = InstantiateProgressBar<UIActionProgressBar>
                (
                    actionProgressBarConfig,
                    actionProgressBar,
                    actionProgressBarConfig.uiGroupName
                );
                // 设置信息
                actionProgressBarInstance.SetInfo(actionProgressBarConfig, 0);
            }
        }
        
        /// <summary>
        /// 实例化 进度条
        /// </summary>
        /// <param name="progressBarConfig">进度条 配置</param>
        /// <param name="progressBarPrefab">进度条 预制体</param>
        /// <param name="uiGroupName">UI分组名称</param>
        /// <typeparam name="T">进度条子类</typeparam>
        /// <returns></returns>
        private T InstantiateProgressBar<T>
        (
            ProgressBarConfig progressBarConfig, 
            UIBaseProgressBar progressBarPrefab, 
            string uiGroupName
        ) where T : UIBaseProgressBar
        {
            // 获取 UI分组 根节点
            Transform uiGroupRoot = _uiProgressBarViewInstance.GetUiGroupRoot(uiGroupName);
            // 实例化 等级进度条
            var levelProgressBarInstance = Instantiate
            (
                progressBarPrefab,
                uiGroupRoot
            );
            levelProgressBarInstance.gameObject.SetActive(true); // 确保激活
            levelProgressBarInstance.gameObject.name = progressBarConfig.progressName; // 设置名称
            
            // 检查是否重名 并添加到字典
            if (!_progressBarInstanceDic.TryAdd(progressBarConfig.progressName, levelProgressBarInstance))
                Debug.LogWarning($"[AnimSimulatorManager] InitProgressBar: 进度条名称 '{progressBarConfig.progressName}' 重复，请确保名称唯一。");
            
            // 返回 实例
            return levelProgressBarInstance as T;
        }
        #endregion

        #region 进度条操作
        /// <summary>
        /// 修改 进度条数值
        /// </summary>
        /// <param name="progressName"></param>
        /// <param name="valueModify">变化的进度值。不是进度的百分比。</param>
        public void ModifyProgressBars(string progressName, float valueModify)
        {
            // 获取 进度条实例
            if (_progressBarInstanceDic.TryGetValue(progressName, out var progressBarInstance))
            {
                // 修改 进度值
                progressBarInstance.ModifyProgressValue(valueModify);
            }
        }
        
        /// <summary>
        /// 获取 进度条实例
        /// </summary>
        /// <param name="progressName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetProgressBar<T>(string progressName) where T : UIBaseProgressBar
        {
            // 获取 进度条实例
            if (_progressBarInstanceDic.TryGetValue(progressName, out var progressBarInstance))
            {
                return progressBarInstance as T;
            }

            return null;
        }
        
        #region 等级进度条
        /// <summary>
        /// 获取 指定等级 所需的经验值
        /// </summary>
        /// <param name="levelProgressBarConfig">等级进度条配置</param>
        /// <param name="levelCurrent">当前等级。</param>
        /// <returns>升级到 下一级 所需的经验值。</returns>
        public int GetExpRequireForLevel(LevelProgressBarConfig levelProgressBarConfig, int levelCurrent)
        {
            // 检查 配置有效性
            if (!animSimulatorConfig)
            {
                Debug.LogWarning("[AnimSimulatorManager] GetExpForLevel: AnimSimulatorConfig 未设置，无法获取经验值！");
                return 0;
            }
            
            // 调用 配置的接口 获取经验值
            return animSimulatorConfig.GetExpForLevel(levelProgressBarConfig, levelCurrent);
        }
        #endregion
        #endregion
        #endregion

        #region 背景 管理
#if UNITY_EDITOR
        [Header("背景-测试用")]
        [Tooltip("背景名称-测试用。按 AnimSimulatorConfig 的背景文件夹拼出地址。")]
        [SerializeField] private string testBackgroundName;
#if ATK_ADDRESSABLE
        [Tooltip("背景资产引用-测试用。直接挂载背景预制体即可，无需依赖配置里的文件夹与命名约定。\n" +
                 "与上方的名称二选一，本引用优先。")]
        [SerializeField] private AssetReference testBackgroundReference;
#endif

        /// <summary>是否配置了测试用背景（名称或资产引用任一）。</summary>
        private bool HasTestBackground
        {
            get
            {
#if ATK_ADDRESSABLE
                if (testBackgroundReference != null && testBackgroundReference.RuntimeKeyIsValid()) return true;
#endif
                return !string.IsNullOrEmpty(testBackgroundName);
            }
        }

        /// <summary>
        /// 重新加载 测试用背景
        /// </summary>
        [ContextMenu("背景-测试用-重新加载")]
        private void ReloadTestBackground()
        {
#if ATK_ADDRESSABLE
            // 资产引用优先：RuntimeKey 即该资产的 GUID，Addressables 可直接作为地址使用，
            // 因此不必再走「配置文件夹 + 名称」拼地址那条路。
            if (testBackgroundReference != null && testBackgroundReference.RuntimeKeyIsValid())
            {
                LoadBackgroundByAddress(testBackgroundReference.RuntimeKey.ToString());
                return;
            }
#endif
            LoadBackground(testBackgroundName);
        }
#endif
        
        // 背景名称 当前记录
        private string _backgroundCurrentName;
        // 背景实例 当前引用
        private GameObject _backgroundCurrentInstance;
        // 背景实例的动画组件 当前引用
        private AnimActor _backgroundAnimActorCurrent;
        
        /// <summary>
        /// 加载 背景
        /// </summary>
        /// <param name="backgroundName">背景的名称</param>
        private void LoadBackground(string backgroundName)
        {
            // 参数 有效性检查
            if (string.IsNullOrEmpty(backgroundName)) return;
            // 配置 有效性检查
            if (!animSimulatorConfig)
            {
                Debug.LogWarning("[AnimSimulatorManager] LoadBackground: AnimSimulatorConfig 未设置，无法加载背景资产！");
                return;
            }
            
            // 组合 背景资产 地址 并加载
            LoadBackgroundByAddress(
                $"{animSimulatorConfig.backgroundAddressableFolder}{backgroundName}.prefab");
        }

        /// <summary>
        /// 按 Addressable 地址 加载 背景。
        /// <para>地址可以是「文件夹 + 名称」拼出的路径，也可以是资产 GUID——Addressables 两者都认。</para>
        /// </summary>
        /// <param name="address">背景资产地址</param>
        private void LoadBackgroundByAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return;

            // 卸载 旧的背景
            UnloadBackground();

            // 记录 当前地址（卸载时按同一地址释放）
            _backgroundCurrentName = address;
            // 使用 资产门面 加载并实例化 背景资产。直接挂到本管理器下，实例化时即完成父子关系，
            // 免得先建到场景根部再重挂一次（其组件的 Awake 也就能在已挂好父节点的状态下执行）。
            ToolkitAssets.InstantiateByAddress<GameObject>(
                _backgroundCurrentName, OnLoadBackgroundComplete, transform);
        }
        
        /// <summary>
        /// 背景 加载完成回调
        /// </summary>
        /// <param name="backgroundInstance"></param>
        private void OnLoadBackgroundComplete(GameObject backgroundInstance)
        {
            // 记录 背景实例
            if (backgroundInstance)
            {
                _backgroundCurrentInstance = backgroundInstance;
                // 父节点已在实例化时设好，此处无需再挂。
                // 从实例上 获取 AnimActor组件
                _backgroundAnimActorCurrent = _backgroundCurrentInstance.GetComponent<AnimActor>();
                if (_backgroundAnimActorCurrent)
                {
                    // TODO:加载背景数据。
                    _backgroundAnimActorCurrent.LoadData(new AnimActorSaveData());
                }
            }
            // 加载失败，立刻卸载
            else
                UnloadBackground();
        }
        
        /// <summary>
        /// 卸载 背景
        /// </summary>
        private void UnloadBackground()
        {
            // 销毁 背景实例
            if (_backgroundCurrentInstance)
            {
                Destroy(_backgroundCurrentInstance);
                _backgroundCurrentInstance = null;
                _backgroundAnimActorCurrent = null;
            }
            // 卸载 背景资产。释放的是源资源句柄，与上面销毁实例是两件事。
            if (!string.IsNullOrEmpty(_backgroundCurrentName))
            {
                ToolkitAssets.ReleaseAddress(_backgroundCurrentName);
                _backgroundCurrentName = null;
            }
        }
        #endregion
        
        #region 角色 管理
#if UNITY_EDITOR
        [Header("角色-测试用")]
        [Tooltip("角色名称-测试用。按 AnimSimulatorConfig 的角色文件夹拼出地址。")]
        [SerializeField] private string testActorName;
#if ATK_ADDRESSABLE
        [Tooltip("角色资产引用-测试用。直接挂载角色预制体即可，无需依赖配置里的文件夹与命名约定。\n" +
                 "与上方的名称二选一，本引用优先。")]
        [SerializeField] private AssetReference testActorReference;
#endif

        /// <summary>是否配置了测试用角色（名称或资产引用任一）。</summary>
        private bool HasTestActor
        {
            get
            {
#if ATK_ADDRESSABLE
                if (testActorReference != null && testActorReference.RuntimeKeyIsValid()) return true;
#endif
                return !string.IsNullOrEmpty(testActorName);
            }
        }

        /// <summary>
        /// 重新加载 测试用角色
        /// </summary>
        [ContextMenu("角色-测试用-重新加载")]
        private void ReloadTestActor()
        {
#if ATK_ADDRESSABLE
            // 资产引用优先：RuntimeKey 即该资产的 GUID，Addressables 可直接作为地址使用。
            if (testActorReference != null && testActorReference.RuntimeKeyIsValid())
            {
                LoadActorByAddress(testActorReference.RuntimeKey.ToString());
                return;
            }
#endif
            LoadActor(testActorName);
        }
#endif
        
        // 角色名称 当前记录
        private string _actorCurrentName;
        // 角色实例 当前引用
        private GameObject _actorCurrentInstance;
        // 角色动画演员 当前引用
        private AnimActor _animActorCurrent;
        
        /// <summary>
        /// 初始化 角色
        /// </summary>
        private void InitActor()
        {
            // 初始化 角色皮肤
            InitActorSkin();
        }
        
        /// <summary>
        /// 加载 角色
        /// </summary>
        /// <param name="actorName">角色的名称</param>
        private void LoadActor(string actorName)
        {
            // 参数 有效性检查
            if (string.IsNullOrEmpty(actorName)) return;
            // 配置 有效性检查
            if (!animSimulatorConfig)
            {
                Debug.LogWarning("[AnimSimulatorManager] LoadActor: AnimSimulatorConfig 未设置，无法加载角色资产！");
                return;
            }
            
            // 组合 角色资产 地址 并加载
            LoadActorByAddress(
                $"{animSimulatorConfig.actorAddressableFolder}{actorName}.prefab");
        }

        /// <summary>
        /// 按 Addressable 地址 加载 角色。
        /// <para>地址可以是「文件夹 + 名称」拼出的路径，也可以是资产 GUID——Addressables 两者都认。</para>
        /// </summary>
        /// <param name="address">角色资产地址</param>
        private void LoadActorByAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return;

            // 卸载 旧的角色
            UnloadActor();

            // 记录 当前地址（卸载时按同一地址释放）
            _actorCurrentName = address;
            // 加载 新的角色 并实例化。同背景，直接挂到本管理器下。
            ToolkitAssets.InstantiateByAddress<GameObject>(
                _actorCurrentName, OnLoadActorComplete, transform);
        }
        
        /// <summary>
        /// 角色 加载完成回调
        /// </summary>
        /// <param name="actorInstance"></param>
        private void OnLoadActorComplete(GameObject actorInstance)
        {
            // 记录 角色实例
            if (actorInstance)
            {
                _actorCurrentInstance = actorInstance;
                // 父节点已在实例化时设好，此处无需再挂。
                // 从实例上 获取 AnimActor组件
                _animActorCurrent = _actorCurrentInstance.GetComponent<AnimActor>();
                if (_animActorCurrent)
                {
                    // TODO:加载角色数据。
                    _animActorCurrent.LoadData(new AnimActorSaveData());
                    // 订阅 初始化完成事件。在Start中完成初始化后的回调。
                    _animActorCurrent.OnInitComplete += (animActor) =>
                    {
                        if (_animActorCurrent == animActor)
                        {
                            // 刷新 角色皮肤组 列表UI
                            RefreshUIAnimActorSkinGroupList();
                        }
                    };
                }
            }
            // 加载失败，立刻卸载
            else
                UnloadActor();
        }
        
        /// <summary>
        /// 卸载 角色
        /// </summary>
        private void UnloadActor()
        {
            // 销毁 角色实例
            if (_actorCurrentInstance)
            {
                Destroy(_actorCurrentInstance);
                _actorCurrentInstance = null;
                _animActorCurrent = null;
                // 刷新 角色皮肤组 列表UI
                RefreshUIAnimActorSkinGroupList();
            }
            // 卸载 角色资产。释放的是源资源句柄，与上面销毁实例是两件事。
            if (!string.IsNullOrEmpty(_actorCurrentName))
            {
                ToolkitAssets.ReleaseAddress(_actorCurrentName);
                _actorCurrentName = null;
            }
        }
        #endregion
    }
}

