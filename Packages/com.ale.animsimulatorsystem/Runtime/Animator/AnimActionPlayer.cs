#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using Ale.Condition;
using Ale.Toolkit.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动作播放器。
    /// 配置 可供播放的 动画动作组，响应玩家的交互操作，播放对应的动画动作。
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class AnimActionPlayer : MonoBehaviour
    {
#if UNITY_EDITOR
        /// <summary>
        /// 在编辑器中绘制 Gizmos可视化
        /// </summary>
        private void OnDrawGizmos()
        {
            // 绘制 动画动作组的Gizmos可视化
            OnDrawGizmosAnimActionGroup();
        }

        /// <summary>
        /// 绘制 动画动作组的Gizmos可视化
        /// </summary>
        private void OnDrawGizmosAnimActionGroup()
        {
            if (animActions == null) return;

            // 场景视图未必存在（Game 视图最大化、或尚未打开任何 Scene 视图时 lastActiveSceneView 为 null）。
            // 在循环外取一次即可：这一帧里它对所有动作都是同一个。
            var sceneView = SceneView.lastActiveSceneView;
            Camera sceneCamera = sceneView ? sceneView.camera : null;

            foreach (var animAction in animActions)
            {
                // 如果不显示Gizmos，则跳过
                if (animAction == null || animAction.showGizmos == false) continue;

                // 计算动作区域的方向
                Quaternion actionRotation = Quaternion.Euler
                (
                    animAction.actionDirectionX, 
                    animAction.actionDirectionY, 
                    animAction.actionDirectionZ
                );
                // 绘制动作区域的Gizmos
                Gizmos.color = Color.cyan;
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, actionRotation, Vector3.one);
                Gizmos.matrix = rotationMatrix;
                Gizmos.DrawWireSphere(Vector3.zero, animAction.actionRange * 0.5f);
                // 绘制一个圆形，始终面向摄像机 以增强球形边缘的可视化
                if (sceneCamera)
                {
                    Vector3 toCamera = (sceneCamera.transform.position - transform.position).normalized;
                    Quaternion toCameraRotation = Quaternion.LookRotation(toCamera);
                    Matrix4x4 toCameraMatrix = Matrix4x4.TRS(transform.position, toCameraRotation, Vector3.one);
                    Gizmos.matrix = toCameraMatrix;
                    Gizmos.DrawWireSphere(Vector3.zero, animAction.actionRange * 0.5f);
                }
                    
                // 绘制动作类型的箭头
                Vector3 actionDirWs = actionRotation * Vector3.up; // 箭头默认指向上方
                float directionLength = animAction.actionRange * 0.6f; // 箭头长度为动作范围的70%
                Gizmos.color = Color.yellow;
                // 在世界空间绘制箭头
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawLine(transform.position, transform.position + actionDirWs * directionLength); // 绘制 箭杆
                // 圆锥作为箭头头部
                Vector3 conePosition = transform.position + actionDirWs * directionLength;
                Quaternion coneRotation = Quaternion.FromToRotation(Vector3.up, actionDirWs);
                float coneHeight = Mathf.Max(0.01f, animAction.actionRange * 0.1f); // 圆锥高度为动作范围的10%
                float coneRadius = coneHeight * 0.5f; // 圆锥底部半径为高度的一半
                Vector3 coneScale = new Vector3(coneRadius * 2f, coneHeight, coneRadius * 2f); // mesh 基准半径为0.5
                Gizmos.DrawMesh(GetConeMesh(), conePosition, coneRotation, coneScale); // 绘制 圆锥
                
                // 动作类型
                float size = coneRadius * 0.2f;
                switch (animAction.actionOperationType)
                {
                    case EAnimActionOperationType.Click:
                        // 绘制 点击 动作的标识
                        Gizmos.color = Color.green;
                        Gizmos.DrawCube
                        (
                            transform.position + actionDirWs * (directionLength + coneHeight),
                            new Vector3(size, size, size)
                        );
                        break;
                    case EAnimActionOperationType.Drag:
                        // 绘制 拖拽 动作的标识
                        Gizmos.color = Color.blue;
                        Gizmos.DrawCube
                        (
                            transform.position + actionDirWs * (directionLength + coneHeight),
                            new Vector3(size, size, size)
                        );
                        break;
                    case EAnimActionOperationType.Rotate:
                        // 绘制 旋转 动作的标识
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawCube
                        (
                            transform.position + actionDirWs * (directionLength + coneHeight),
                            new Vector3(size, size, size)
                        );
                        
                        // 绘制 旋转方向的环形范围
                        // 最大角度范围
                        float radiusOuter = animAction.actionRange * 0.5f; // 环形半径为动作范围的一半
                        float radiusInner = radiusOuter * 0.7f; // 内环半径为外环的70%
                        // 绘制 环形区域
                        Vector3 centerPosWs = transform.position; // 中心锚点
                        // 摄像机向量。环形所在的平面是由它定的，没有场景视图就无从确定，整段跳过。
                        if (!sceneCamera) break;
                        Vector3 cameraDirWs = (sceneCamera.transform.position - centerPosWs).normalized;
                        // 计算 旋转平面的 法向量 轴心
                        Vector3 rotateAxisWs = Vector3.Cross(actionDirWs, Vector3.Cross(actionDirWs, cameraDirWs).normalized).normalized;

                        // 旋转方向。默认为 顺时针方向。isAntiClockwise为true 则为 逆时针方向
                        // 通过 AngleAxis 的角度乘以 dirSign 来控制旋转方向
                        float isAnticlockwise = animAction.isAntiClockwise ? 1f : -1f;

                        // 显示 角度扇形：绕 rotateAxisWs 以 actionDirWs 为 0 度方向绘制
                        Gizmos.color = new Color(1f, 0.1f, 1f, 1f);
                        // 三角形填充颜色
                        Handles.color = new Color(1f, 0.5f, 1f, 0.5f);
                        int steps = 36;
                        for (int i = 0; i < steps; i++)
                        {
                            // 夹角向量（使用 dirSign 控制旋转方向）
                            float angleDeg0 = isAnticlockwise * Mathf.Lerp(0, animAction.rotateModeAngleRangeMax, (float)i / steps);
                            Vector3 angleDir0 = Quaternion.AngleAxis(angleDeg0, rotateAxisWs) * actionDirWs;
                            float angleDeg1 = isAnticlockwise * Mathf.Lerp(0, animAction.rotateModeAngleRangeMax, (float)(i + 1) / steps);
                            Vector3 angleDir1 = Quaternion.AngleAxis(angleDeg1, rotateAxisWs) * actionDirWs;

                            // 内线
                            Vector3 inner0 = centerPosWs + angleDir0 * radiusInner;
                            Vector3 inner1 = centerPosWs + angleDir1 * radiusInner;
                            Gizmos.DrawLine(inner0, inner1);
                            // 外线
                            Vector3 outer0 = centerPosWs + angleDir0 * radiusOuter;
                            Vector3 outer1 = centerPosWs + angleDir1 * radiusOuter;
                            Gizmos.DrawLine(outer0, outer1);
                            // 夹角线
                            Gizmos.DrawLine(inner1, outer1);
                            
                            // 绘制两个三角形填充。复用同一个缓冲：DrawAAConvexPolygon 立即绘制、不持有传入的数组。
                            _gizmoTriBuffer[0] = outer0; _gizmoTriBuffer[1] = outer1; _gizmoTriBuffer[2] = inner1;
                            Handles.DrawAAConvexPolygon(_gizmoTriBuffer);
                            _gizmoTriBuffer[0] = outer0; _gizmoTriBuffer[1] = inner1; _gizmoTriBuffer[2] = inner0;
                            Handles.DrawAAConvexPolygon(_gizmoTriBuffer);
                        }
                        // 头部绘制 圆锥箭头
                        // 末端方向和位置
                        Vector3 fromDirPlane = Vector3.ProjectOnPlane(actionDirWs.normalized, rotateAxisWs);
                        Vector3 endDir = Quaternion.AngleAxis(isAnticlockwise * animAction.rotateModeAngleRangeMax, rotateAxisWs) * fromDirPlane;
                        Vector3 endPos = centerPosWs + endDir * radiusOuter;
                        // 圆锥朝向：将 Mesh 的 +Y 对齐到 切线方向
                        // 切线方向 考虑 旋转方向
                        Vector3 tangent = Quaternion.AngleAxis(90f * isAnticlockwise, rotateAxisWs) * endDir;
                        if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.Cross(rotateAxisWs, endDir).normalized;
                        Quaternion arrowRot = Quaternion.FromToRotation(Vector3.up, tangent.normalized);
                        float arrowH = Mathf.Max(0.01f, animAction.actionRange * 0.1f);
                        float arrowR = arrowH;
                        Vector3 arrowScale = new Vector3(arrowR, arrowH, arrowR);
                        // 绘制 圆锥箭头
                        Gizmos.DrawMesh(GetConeMesh(), endPos, arrowRot, arrowScale);
                        
                        break;
                    case EAnimActionOperationType.Press:
                        // 绘制 按压 动作的标识
                        Gizmos.color = Color.red;
                        Gizmos.DrawCube
                        (
                            transform.position + actionDirWs * (directionLength + coneHeight),
                            new Vector3(size, size, size)
                        );
                        break;
                }
            }
        }

        #region Gizmos Mesh生成
        // 缓存的圆锥 Mesh（仅编辑器使用）
        private static Mesh _coneMesh;

        // 扇形填充用的三角形顶点缓冲。原先在 36 次的循环里各 new 一个数组，
        // 每个「旋转」动作每帧就是 72 个短命数组——Gizmo 是每帧重绘的，累积起来相当可观。
        private static readonly Vector3[] _gizmoTriBuffer = new Vector3[3];

        /// <summary>
        /// 获取一个通用的单位圆锥 Mesh，轴向为 +Y（顶点在 y=1，基底在 y=0，基底半径为 0.5）
        /// 缓存以避免频繁创建。
        /// </summary>
        /// <param name="segments">基底圆的细分数</param>
        /// <returns></returns>
        private static Mesh GetConeMesh(int segments = 20)
        {
            if (_coneMesh != null) return _coneMesh;

            if (segments < 3) segments = 3;

            Mesh m = new Mesh();
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // 顶点（顶点位于 y=1）
            verts.Add(new Vector3(0f, 1f, 0f)); // index 0

            // 基底圆上的顶点（y=0，半径=0.5）
            for (int i = 0; i < segments; i++)
            {
                float ang = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * 0.5f, 0f, Mathf.Sin(ang) * 0.5f));
            }

            // 侧面三角形（顶点, i+1, i）
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris.Add(0);
                tris.Add(1 + next);
                tris.Add(1 + i);
            }

            // 基底中心点，用于填充底面
            int baseCenterIndex = verts.Count;
            verts.Add(new Vector3(0f, 0f, 0f));

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris.Add(baseCenterIndex);
                tris.Add(1 + i);
                tris.Add(1 + next);
            }

            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.hideFlags = HideFlags.HideAndDontSave;

            _coneMesh = m;
            return _coneMesh;
        }
        #endregion
#endif
        [Header("基础设置")]
        [Tooltip("动画动作播放器 名称：唯一标识，用于配置和查找。")]
        [SerializeField] private string actionPlayerName;
#if UNITY_EDITOR
        [Tooltip("备注：仅用于编辑器查看。")]
        [SerializeField] private string comment;
#endif
        
        /// <summary>
        /// 动画动作播放器 名称
        /// </summary>
        public string ActionPlayerName => actionPlayerName;
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // 多态查找天然认得两种后端，无需按宏分支。
            // 动作播放器通常挂在角色的子物体上，播放器本体又不带动画组件，
            // 所以 FindFor 的「自身 → 子树 → 父级链」里真正命中的一般是最后一段。
            if (!animator) animator = AnimatorBase.FindFor(this);
        }
#endif

        private void OnEnable()
        {
            // 注册到 动画模拟器管理器
            var manager = AnimSimulatorManager.Instance;
            if (manager)
            {
                manager.RegisterAnimActionPlayer(this);
            }
            else
            {
                // 管理器不会被自动创建：必须先在场景中存在 AnimSimulatorManager，本组件才能注册进去。
                // 常规流程下角色是由管理器自己加载并实例化的，不会走到这里；
                // 手动把 AnimActionPlayer 摆进场景、却没有管理器时才会命中。
                // 此处不静默跳过——注册失败意味着该播放器整个不受管理器驱动，必须让它可被发现。
                AnimSimLog.Warn(this,
                    $"场景中没有 AnimSimulatorManager，'{name}' 未能注册，将不受管理器驱动。");
            }
        }

        private void OnDisable()
        {
            // 清除 延迟停止 动画动作 的协程
            ClearCorDelayStopAnimAction();

            // 注销出 动画模拟器管理器
            if (AnimSimulatorManager.Instance)
                AnimSimulatorManager.Instance.UnregisterAnimActionPlayer(this);
        }

        #region 操作事件
        // 是否允许操作
        private bool _isCanOperate = true;
        
        // 记录播放开始时间（Time.time）
        private float _animActionStartTime = -1f;
        // 延迟停止的协程句柄
        private Coroutine _delayStopAnimActionCor;
        // 是否正在播放 动画动作
        private bool _isAnimActionPlaying;
        
        /// <summary>
        /// 动作开始事件
        /// </summary>
        private Action<AnimActionPlayer> _onActionStartEvent;
        /// <summary>
        /// 动作完成事件
        /// </summary>
        private Action<AnimActionPlayer> _onActionCompleteEvent;

        /// <summary>
        /// 设置 是否允许操作
        /// </summary>
        /// <param name="isCanOperate"></param>
        /// <param name="isForceCanOperate">强制设置为 允许操作</param>
        public void SetIsCanOperate(bool isCanOperate, bool isForceCanOperate = false)
        {
            if (isCanOperate)
            {
                // 仅当 动画动作播放器 的类型为Operate时，才允许操作
                if (isForceCanOperate || animActionPlayerType == EAnimActionPlayerType.Operate)
                    _isCanOperate = true;
            }
            else
            {
                // 设置为 禁止操作
                _isCanOperate = false;
            }
        }
        
        /// <summary>
        /// 光标左键按下/开始拖拽
        /// </summary>
        /// <param name="cursorWorldPos">光标当前的世界坐标</param>
        /// <param name="onActionComplete">动作完成的回调</param>
        /// <param name="onActionStart">动作开始的回调</param>
        public bool OnLeftClickDown
        (
            Vector3 cursorWorldPos, 
            Action<AnimActionPlayer> onActionComplete = null,
            Action<AnimActionPlayer> onActionStart = null
        )
        {
            // 检查 是否允许操作
            if (!_isCanOperate) return false;
            
            // 播放 当前选中的 动画动作
            if (_animActionSelected != null)
            {
                if (PlayAnimAction(_animActionSelected, onActionComplete, onActionStart))
                    return true;
            }
            
            return false;
        }
    
        /// <summary>
        /// 光标左键抬起/结束拖拽
        /// </summary>
        /// <param name="cursorWorldPos">光标当前的世界坐标</param>
        public void OnLeftClickUp(Vector3 cursorWorldPos)
        {
            // 不检查 是否允许操作，确保能 停止动画动作
            // 停止 动画动作
            StopAnimAction();
        }
        
        /// <summary>
        /// 拖拽移动（世界空间）
        /// </summary>
        /// <param name="cursorWorldPos">光标当前的世界坐标</param>
        /// <param name="cursorDeltaDir">光标增量移动向量</param>
        public void OnDragMoveWS(Vector3 cursorWorldPos, Vector3 cursorDeltaDir)
        {
            // 不检查 是否允许操作，确保操作中的 动画动作 能够响应拖拽
            // 拖拽 动画动作
            DragAnimAction(cursorWorldPos);
        }

        // 这里原本还有 OnDragMoveSS（屏幕空间拖拽）与 OnRightClickDown / OnRightClickUp 三个方法，
        // 方法体是空的，却由管理器在每次指针移动 / 每次右键时照常调用。本类是 sealed 的，
        // 空实现也不可能作为子类的扩展点，故一并删除，管理器侧的调用同时移除。
        #endregion

        #region 动画 控制
        [Header("动画设置")]
        // 面向基类编程；FormerlySerializedAs 保住旧字段名 spineAnimator 上已配置的引用
        [FormerlySerializedAs("spineAnimator")]
        [Tooltip("动画播放器：Spine Animator 或 Live2D Animator")]
        [SerializeField] private AnimatorBase animator;
        [Tooltip("动画轨道 默认：用于不同类型的动画的区分。不同轨道的动画 可以同时播放。")]
        [SerializeField] private EAnimTrack animTrackDefault = EAnimTrack.Other;
        [Tooltip("动画子轨道 默认：用于同一类型动画的区分。不同子轨道的动画 可以同时播放。"), Range(0, 9)]
        [SerializeField] private int animTrackSubDefault;
        /// <summary>
        /// 动画轨道 默认：主轨道 * 10 + 子轨道，主轨道间隔10，子轨道0-9，保证不同 子轨道的动画 可以同时播放。
        /// </summary>
        public int AnimTrackDefault => (int)animTrackDefault * 10 + animTrackSubDefault;

        [Tooltip("最小间隔时间：连续播放 动画动作的 最小间隔时间。单位：秒。")]
        [SerializeField] private float playAnimActionIntervalTime = 0.5f;
        [Tooltip("停止动画动作 延迟时间：在动画动作 播放完成后，等待一段时间 再结束。单位：秒。")]
        [SerializeField] private float stopAnimActionDelayTime = 0.5f;
        
        #region 播放动画-普通模式
        // 播放 动画-普通模式（非循环）时，等待动画完成后调用 动作完成事件回调 的协程。
        private Coroutine _corPlayAnimNormalModeIsLoopOnComplete;
                
        /// <summary>
        /// 播放 动画-普通模式
        /// </summary>
        private void PlayAnimNormalMode()
        {
            if (!animator || _animDataCurrent == null) return;
            // 播放动画
            animator.PlayAnim
            (
                _animDataCurrent,
                animDataCompleted =>
                {
                    // 已经停止播放、或正在延迟停止中，忽略本次回调
                    if (_delayStopAnimActionCor == null && _animActionCurrent != null)
                    {
                        // 停止 当前选中的 动画动作
                        StopAnimActionCheckDelayTime();
                    }
                }
            );

            // 如果是 循环模式，启动 协程 在动画完成后调用 动作完成事件回调
            if (_animActionCurrent.isLoop)
            {
                // 停止 旧的协程
                KillCor(ref _corPlayAnimNormalModeIsLoopOnComplete);
                // 启动 新的协程
                _corPlayAnimNormalModeIsLoopOnComplete = StartCoroutine(CorPlayAnimNormalModeIsLoopOnComplete(stopAnimActionDelayTime));
            }
            
            // 修改 进度条的值。单次播放时，直接设置为 1。
            ModifyProgressBarsValue(1f);
        }
        
        /// <summary>
        /// 协程 播放动画-普通模式（循环）延迟后调用 动作完成事件回调。
        /// 循环动画 会持续播放，不会自动调用 动作完成事件回调。因此，等待延迟时间后，直接调用 动作完成事件回调。
        /// </summary>
        /// <returns></returns>
        private IEnumerator CorPlayAnimNormalModeIsLoopOnComplete(float delayTime = 0.1f)
        {
            // 等待 延迟时间
            yield return new WaitForSeconds(delayTime);
            
            // 调用 动作完成事件回调
            _onActionCompleteEvent?.Invoke(this);
            _onActionCompleteEvent = null; // 清除回调，避免重复调用
        }
        #endregion
        
        #region 播放动画-拖拽模式
        /// <summary>
        /// 设置 动画-拖拽模式
        /// </summary>
        private void SetAnimDragMode()
        {
            // 设置为 动画-进度控制模式。等待 光标拖拽 控制 动画播放进度
            SetAnimProgressMode();
        }

        /// <summary>
        /// 播放 动画-拖拽模式
        /// </summary>
        /// <param name="cursorWorldPos"></param>
        private void PlayAnimDragMode(Vector3 cursorWorldPos)
        {
            // 获取 光标向量 和 动作向量
            if (!GetCursorAndActionDirection(cursorWorldPos, out var cursorDirWs, out var actionDirWs))
            {
                // 无效向量，设置进度为0
                SetAnimProgress(0f);
                return;
            }
                         
            // 光标向量 在 动作向量 上的投影长度（像素）
            float projection = Vector2.Dot(cursorDirWs, actionDirWs);
            // 将投影长度规范化到 [0,1]（负值裁剪为 0，超过半径裁剪为 1）
            float progress = Mathf.Clamp01(projection / actionDirWs.magnitude);
            
            // 播放 动画-拖拽模式。设置动画进度值，带阻尼效果, 非循环模式
            SetAnimProgress(progress, false, _animActionCurrent.isReverse, _animActionCurrent.dampingTime);
        }
        #endregion
        
        #region 播放动画-旋转模式
        /// <summary>
        /// 设置 动画-旋转模式
        /// </summary>
        private void SetAnimRotateMode()
        {
            // 设置为 动画-进度控制模式。等待 光标旋转 控制 动画播放进度
            SetAnimProgressMode();
        }
        
        /// <summary>
        /// 播放 动画-旋转模式
        /// </summary>
        /// <param name="cursorWorldPos"></param>
        private void PlayAnimRotateMode(Vector3 cursorWorldPos)
        {
            // 获取 光标向量 和 动作向量（世界空间）
            if (!GetCursorAndActionDirection(cursorWorldPos, out _, out var actionDirWs))
            {
                // 无效向量，设置进度为0
                SetAnimProgress(0f);
                return;
            }
            
            // 使用摄像机 将 世界空间向量 转换到 屏幕空间 再计算角度
            Camera cam = Camera.main; // 优先使用主摄像机
            if (cam == null) cam = Camera.current;
            // 检查 摄像机有效性
            if (cam == null)
            {
                SetAnimProgress(0f);
                return;
            }
            
            // 计算 屏幕空间的 动作点（中心点 + 动作向量）
            actionDirWs.z = 0f; // 忽略 z分量
            Vector3 actionPosWs = transform.position + actionDirWs.normalized;
            Vector3 actionPosSs = cam.WorldToScreenPoint(actionPosWs); // 动作点 转换为 屏幕空间
            // 计算 屏幕空间的 中心点 和 光标点
            Vector3 centerPosSc = cam.WorldToScreenPoint(transform.position);
            Vector3 cursorPosSc = cam.WorldToScreenPoint(cursorWorldPos);
            // 计算 光标向量 与 动作向量（屏幕空间）
            Vector2 cursorDirSs = cursorPosSc - centerPosSc;
            Vector2 actionDisSs = actionPosSs - centerPosSc;
            // 检查有效性
            if (cursorDirSs.sqrMagnitude < Mathf.Epsilon || actionDisSs.sqrMagnitude < Mathf.Epsilon)
            {
                SetAnimProgress(0f);
                return;
            }

            // 计算 光标向量 与 动作向量 之间的 夹角。
            float signedAngleDeg = Vector2.SignedAngle(cursorDirSs, actionDisSs);
            // 规范化到 [0,360]。
            signedAngleDeg %= 360f;
            if (signedAngleDeg < 0f) signedAngleDeg += 360f;

            // 检查 是否为 逆时针选择。若不是，默认为 顺时针旋转。
            if (_animActionCurrent.isAntiClockwise)
                signedAngleDeg = (360f - signedAngleDeg) % 360f;
            // 限制在 允许的角度范围 内。
            var rotateModeAngleRangeMax = _animActionCurrent.rotateModeAngleRangeMax;
            signedAngleDeg = Mathf.Min(signedAngleDeg, rotateModeAngleRangeMax);
            // 将角度映射到进度 0~1。
            float progress = Mathf.Clamp01(signedAngleDeg / rotateModeAngleRangeMax);
            
            // 播放 Sp动画-旋转模式。设置动画进度值，带阻尼效果，循环模式。
            SetAnimProgress(progress, true, _animActionCurrent.isReverse, _animActionCurrent.dampingTime);
        }
        #endregion
        
        #region 播放动画-按压模式
        // 是否正在播放 按压模式动画
        private bool _isPressModeAnimPlaying;
        // 协程 按压模式：按下
        private Coroutine _pressModeAnimPressCor;
        // 协程 按压模式：松开
        private Coroutine _pressModeAnimReleaseCor;
        
        /// <summary>
        /// 设置 动画-按压模式
        /// </summary>
        private void SetAnimPressMode()
        {
            // 播放新动画之前，获取 当前动画进度。
            float progressStart = GetAnimProgress();
            
            // 设置为 动画-进度控制模式。等待 光标按压 控制 动画播放进度
            SetAnimProgressMode();
            
            // 检查 是否已经处于 按压模式 动画播放中
            if (_isPressModeAnimPlaying)
                // 重设 之前的动画进度。
                SetAnimProgress(progressStart);
            else
                // 标记为 正在播放 按压模式动画
                _isPressModeAnimPlaying = true;
        }
        
        /// <summary>
        /// 播放 动画-按压模式
        /// </summary>
        private void PlayAnimPressMode()
        {
            // 停止 松开协程
            KillCor(ref _pressModeAnimReleaseCor);
            // 启动 按下协程
            if (_pressModeAnimPressCor == null)
                _pressModeAnimPressCor = StartCoroutine(CorPressModeAnimPress());
        }
        
        /// <summary>
        /// 停止 动画-按压模式
        /// </summary>
        private void StopAnimPressMode()
        {
            // 停止 按下协程
            KillCor(ref _pressModeAnimPressCor);
            // 启动 松开协程
            if (_pressModeAnimReleaseCor == null)
                _pressModeAnimReleaseCor = StartCoroutine(CorPressModeAnimRelease());
        }
        
        /// <summary>
        /// 协程 动画-按压模式：按下
        /// </summary>
        /// <returns></returns>
        private IEnumerator CorPressModeAnimPress()
        {
            float progress = GetAnimProgress();
            // 如果反转，则进度也反转
            progress = _animActionCurrent.isReverse ? (1f - progress) : progress;
            
            // 从当前进度开始，逐渐增加到 1
            while (progress < 1f)
            {
                // 增加进度，速度由 pressModeAnimPressSpeed 控制（单位：进度/秒）
                progress += _animActionCurrent.pressModeAnimPressSpeed * Time.deltaTime;
                progress = Mathf.Clamp01(progress);
                // 直接应用进度（不阻尼，立即反映）
                SetAnimProgress(progress, false, _animActionCurrent.isReverse);

                yield return null;
            }

            _pressModeAnimPressCor = null;
        }
        
        /// <summary>
        /// 协程 Sp动画-按压模式：松开
        /// </summary>
        /// <returns></returns>
        private IEnumerator CorPressModeAnimRelease()
        {
            float progress = GetAnimProgress();
            // 如果反转，则进度也反转
            progress = _animActionCurrent.isReverse ? (1f - progress) : progress;
            
            // 从当前进度开始，逐渐下降到 0
            while (progress > 0f)
            {
                // 下降进度，速度由 pressModeAnimReleaseSpeed 控制（单位：进度/秒）
                progress -= _animActionCurrent.pressModeAnimReleaseSpeed * Time.deltaTime;
                progress = Mathf.Clamp01(progress);
                // 直接应用进度（不阻尼，立即反映）
                SetAnimProgress(progress, false, _animActionCurrent.isReverse);

                yield return null;
            }

            // 等待 延迟时间后 结束动作
            yield return new WaitForSeconds(_animActionCurrent.pressModeAnimActionStopDelay);

            // 先摘掉自己的句柄再收尾：StopAnimActionImmediate 现在会停掉本播放器全部在途协程，
            // 句柄还挂着的话那就是「协程停自己」——能跑通只是因为后面恰好没有 yield 了，太脆。
            _pressModeAnimReleaseCor = null;
            // 标记为 未在播放 按压模式动画
            _isPressModeAnimPlaying = false;
            // 立即停止 当前动画动作
            StopAnimActionImmediate();
        }
        #endregion

        #region 动画进度控制
        // 拖拽阻尼 补间句柄
        private ToolkitTweenHandle _animProgressDampingHandle;
        // 拖拽阻尼 起始进度值
        private float _animProgressDampingStart;
        // 拖拽阻尼 目标进度值
        private float _animProgressDampingTarget;
        // 拖拽阻尼 当前进度值
        private float _animProgressDampingCurrent;

        // 是否为 循环进度模式（头尾相连）
        private bool _animProgressIsLoop;
        // 循环模式下的进度增量（可能为负），表示从 start 到 target 的最短环绕增量（单位：0..1）
        private float _animProgressDampingDeltaLoop;
        
        /// <summary>
        /// 设置 动画-进度控制模式。作用对象是 <see cref="_animDataCurrent"/>。
        /// <para>原先带一个 <c>AnimAction</c> 入参，但方法体从不使用它，三个调用点又都传的
        /// <c>_animActionCurrent</c>——徒增「传进去的会被用到」的错觉，故去掉。</para>
        /// </summary>
        private void SetAnimProgressMode()
        {
            if (!animator || _animDataCurrent == null) return;
            // 不循环，播放速度为0。由玩家操作 直接设置动画进度。
            _animDataCurrent.isLoop = false;
            _animDataCurrent.speed = 0f;
            // 播放动画
            animator.PlayAnim(_animDataCurrent);
        }

        /// <summary>
        /// 设置 动画进度值
        /// </summary>
        /// <param name="progress">进度值</param>
        /// <param name="isProgressLoop">进度循环模式，头尾相接地进度值变化。例如，从0.9到0.1，会从 0.9 → 1.0 → 0.0 →0.1 就近变化</param>
        /// <param name="isReverse">是否反转进度映射（true 时 progress 映射为 1 - progress）。</param>
        /// <param name="dampingTime">阻尼的持续时间。时间越长，动画变化越缓慢。</param>
        private void SetAnimProgress(float progress, bool isProgressLoop = false, bool isReverse = false, float dampingTime = 0f)
        {
            // 记录 开始进度值
            _animProgressDampingStart = GetAnimProgress();
            // 记录 当前进度值
            _animProgressDampingCurrent = _animProgressDampingStart;
            // 记录 目标进度值
            _animProgressDampingTarget = Mathf.Clamp01(progress);
            // 是否 反转进度映射
            if (isReverse)
                _animProgressDampingTarget = 1f - _animProgressDampingTarget;
            // 记录 是否为循环模式
            _animProgressIsLoop = isProgressLoop;
            // 若为循环模式，计算从当前进度到目标进度的最短环绕增量（单位：0..1，可能为负）
            if (_animProgressIsLoop)
            {
                // 使用角度的 Delta 计算最短环绕距离
                float startAngleDeg = _animProgressDampingStart * 360f;
                float targetAngleDeg = _animProgressDampingTarget * 360f;
                float deltaAngleDeg = Mathf.DeltaAngle(startAngleDeg, targetAngleDeg); // -180..180
                _animProgressDampingDeltaLoop = deltaAngleDeg / 360f; // -0.5 .. 0.5
            }
            // 设置了阻尼时间，启动阻尼补间
            if (dampingTime > 0f)
            {
                StartAnimProgressDamping(dampingTime);
            }
            else
            {
                // 无阻尼时间，直接设置进度，但遵守最小帧阈值，避免小幅更新导致抖动
                _animProgressDampingCurrent = _animProgressDampingTarget; // 直接设置为目标进度值
                // 直接设置 动画进度值。轨道为空时 SetAnimProgress 返回 false 而非空引用。
                if (animator)
                    animator.SetAnimProgress(GetCurrentAnimActionTrackIndex(), _animProgressDampingTarget);
                // 修改 进度条的值
                ModifyProgressBarsValue(_animProgressDampingTarget - _animProgressDampingStart);
            }
        }

        /// <summary>
        /// 获取 动画的进度值。
        /// </summary>
        /// <returns>进度值 [0.0, 1.0]</returns>
        private float GetAnimProgress()
            => animator ? animator.GetAnimProgress(GetCurrentAnimActionTrackIndex()) : 0f;

        /// <summary>
        /// 启动 拖拽阻尼：动画进度值 平滑变化到目标值。
        ///
        /// <para>起止值与循环增量在调用前已由 <see cref="SetAnimProgress"/> 算好，这里按快照捕获——
        /// 拖拽期间每次指针移动都会重新发起，届时会打断在途的这一次并以当前实际进度为新起点，
        /// 与改造前「重置经过时间、重写起止值」的效果一致。</para>
        /// </summary>
        /// <param name="dampingTime">阻尼的持续时间。</param>
        private void StartAnimProgressDamping(float dampingTime)
        {
            // 打断在途的：起点已经重新取过（就是当前实际进度），继续用旧作业只会把插值算错
            _animProgressDampingHandle.Kill();
            _animProgressDampingHandle = default;

            if (!animator) return;

            // 记下发起时的轨道与播放令牌，逐帧比对，轨道被别的播放顶替时不再写入
            int trackIndex = GetCurrentAnimActionTrackIndex();
            int playToken = animator.GetAnimPlayToken(trackIndex);
            if (playToken == 0) return;

            // 快照本次的插值参数，避免回调里读到后续调用改写过的字段
            float progressStart = _animProgressDampingStart;
            float progressTarget = _animProgressDampingTarget;
            bool isProgressLoop = _animProgressIsLoop;
            float deltaLoop = _animProgressDampingDeltaLoop;

            // 走线性、由回调自行算进度：本处需要的不是缓动曲线，而是「循环模式下沿最短环绕路径插值」。
            // unscaled: false —— 原先协程用的是 Time.deltaTime（受 timeScale 影响），保持一致。
            // owner 传 this：组件销毁后补间自动作废。
            _animProgressDampingHandle = ToolkitTween.To(
                0f, 1f, dampingTime,
                t =>
                {
                    // 检查 当前轨道 未被替换。
                    // 用令牌比对而非持有后端播放句柄做引用比较：后端句柄普遍有对象池复用，
                    // 回收再分配后引用比较会假阳性，会继续去写一条早已不属于它的轨道。
                    if (!animator || animator.GetAnimPlayToken(trackIndex) != playToken) return;

                    float progressNew;
                    // 循环模式：沿最短环绕路径插值，然后规整到 [0,1]
                    if (isProgressLoop)
                    {
                        // 计算 新的进度值。只保留小数部分，将结果规整到 [0,1]。
                        progressNew = progressStart + deltaLoop * t;
                        progressNew -= Mathf.Floor(progressNew);
                    }
                    // 非循环模式：线性插值
                    else
                    {
                        // 计算 新的进度值。到 目标值 的线性插值。
                        progressNew = Mathf.Lerp(progressStart, progressTarget, t);
                    }

                    // 设置 动画进度值
                    animator.SetAnimProgress(trackIndex, progressNew);
                    // 修改 进度条的值
                    ModifyProgressBarsValue(progressNew - _animProgressDampingCurrent);
                    // 更新 当前进度值
                    _animProgressDampingCurrent = progressNew;
                },
                EToolkitEase.Linear, unscaled: false,
                onComplete: () => _animProgressDampingHandle = default,
                owner: this);
        }
        #endregion
        #endregion
        
        #region 动画动作 播放
        [Header("动作列表")]
        [Tooltip("动画动作播放器 类型：用于区分不同的 动画动作播放器类别。[Operate玩家操作 / ProgressBar进度条控制]。")]
        [SerializeField] private EAnimActionPlayerType animActionPlayerType;
        // 这里原本还有一个序列化的「动画动作 选择类型」字段，但全包无人读取——实际生效的是
        // ActionPlayConfig.animActionSelectType（由进度条配置给出，见 UIActionProgressBar），
        // 以及 PlayAnimActionByType 的入参。留着只会让人在 Inspector 上配了却不起作用，故删除。
        [Tooltip("动画动作列表：包含的动画动作。")]
        [SerializeField] private AnimAction[] animActions;
        
        /// <summary>
        /// 动画动作播放器 类型。
        /// [Operate玩家操作 / ProgressBar进度条控制]。
        /// </summary>
        public EAnimActionPlayerType AnimActionPlayerType => animActionPlayerType;
        
        // 当前正在播放的 动画动作
        private AnimAction _animActionCurrent;
        // 当前正在播放的 动画数据
        private AnimData _animDataCurrent;
        // 顺序播放时，当前的 动画动作索引
        private int _animActionIndexOrder;
        // 随机播放时，各动画动作 已播放次数（用于 randomTypePlayLimit 次数限制）
        private readonly Dictionary<AnimAction, int> _animActionRandomPlayCountMap = new Dictionary<AnimAction, int>();
        
        /// <summary>
        /// 获取 当前动画动作 的 轨道索引
        /// </summary>
        /// <returns></returns>
        private int GetCurrentAnimActionTrackIndex()
        {
            if (_animActionCurrent != null && _animActionCurrent.AnimTrack >= 0)
                // 使用 动画动作 指定的轨道
                return _animActionCurrent.AnimTrack;
            else
                // 使用默认轨道
                return AnimTrackDefault;
        }

        /// <summary>
        /// 播放 动画动作 通过类型
        /// </summary>
        /// <param name="selectType">选择类型。[Select选择/Order顺序/Random随机]</param>
        /// <param name="onActionComplete">动作完成的回调</param>
        /// <param name="onActionStart">动作开始的回调</param>
        public void PlayAnimActionByType
        (
            EAnimActionSelectType selectType, 
            Action<AnimActionPlayer> onActionComplete = null,
            Action<AnimActionPlayer> onActionStart = null
        )
        {
            // 判断 选择类型
            switch (selectType)
            {
                // 选择
                case EAnimActionSelectType.Select:
                {
                    var manager = AnimSimulatorManager.Instance;
                    if (!manager)
                    {
                        AnimSimLog.Warn(this, "场景中没有 AnimSimulatorManager，无法打开动画动作列表。");
                        break;
                    }
                    // 替换 当前悬停的 动画动作播放器。强制设置为 允许操作。
                    manager.ReplaceAnimActionPlayer(this, true);
                    // 打开 动画动作列表 界面，等待玩家选择动作
                    manager.OpenCloseAnimActionList(this, true, true);
                    // 记录回调
                    if (onActionComplete != null)
                        _onActionCompleteEvent = onActionComplete; // 记录动作完成的回调
                    if (onActionStart != null)
                        _onActionStartEvent = onActionStart; // 记录动作开始的回调
                    break;
                }
                // 顺序
                case EAnimActionSelectType.Order:
                {
                    // 仅在 满足条件的动作 中 按顺序选择
                    var candidatesOrder = GetAnimActionsMeetConditions();
                    if (candidatesOrder.Count == 0) break;
                    // 修正 越界的顺序索引
                    if (_animActionIndexOrder < 0 || _animActionIndexOrder >= candidatesOrder.Count)
                        _animActionIndexOrder = 0;
                    // 获取 当前顺序的动画动作
                    var animAction = candidatesOrder[_animActionIndexOrder];
                    // 播放 动画动作
                    if (PlayAnimAction(animAction, onActionComplete, onActionStart))
                        // 更新 顺序播放 的当前索引
                        _animActionIndexOrder = (_animActionIndexOrder + 1) % candidatesOrder.Count;
                    break;
                }
                // 随机
                case EAnimActionSelectType.Random:
                {
                    // 仅在 满足条件的动作 中 按随机权重选择（含次数限制）
                    var candidatesRandom = GetAnimActionsMeetConditions();
                    var animActionRandom = SelectRandomAnimActionByWeight(candidatesRandom);
                    // 播放 动画动作
                    if (animActionRandom != null && PlayAnimAction(animActionRandom, onActionComplete, onActionStart))
                    {
                        // 累计 该动作的 随机播放次数
                        _animActionRandomPlayCountMap.TryGetValue(animActionRandom, out int playedCount);
                        _animActionRandomPlayCountMap[animActionRandom] = playedCount + 1;
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 播放 动画动作
        /// </summary>
        /// <param name="animAction">播放的 动画动作</param>
        /// <param name="onActionComplete">动作完成的回调</param>
        /// <param name="onActionStart">动作开始的回调</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private bool PlayAnimAction
        (
            AnimAction animAction, 
            Action<AnimActionPlayer> onActionComplete = null,
            Action<AnimActionPlayer> onActionStart = null
        )
        {
            if (animAction == null) return false;
            
            // 限制短时间内连续播放
            if (Time.time - _animActionStartTime < playAnimActionIntervalTime) return false;
            
            // 立即停止 当前正在播放的 动画动作。
            if (_animActionCurrent != null)
                StopAnimActionImmediate();
            // 清除 延迟停止 动画动作 的协程
            ClearCorDelayStopAnimAction();
            
            // 标记为正在播放 动画动作
            _isAnimActionPlaying = true;
            // 记录 当前正在播放的 动画动作
            _animActionCurrent = animAction; 
            // 记录 当前正在播放的 动画数据
            _animDataCurrent = new AnimData
            (
                _animActionCurrent.ResolveAnimName(),
                GetCurrentAnimActionTrackIndex(),
                _animActionCurrent.isLoop,
                _animActionCurrent.isReverse,
                _animActionCurrent.clickModeAnimPlaySpeed,
                _animActionCurrent.startDelayTime
            );
            // 记录动作完成的回调
            if (onActionComplete != null)
                _onActionCompleteEvent = onActionComplete;
            // 记录动作开始的回调
            if (onActionStart != null)
                _onActionStartEvent = onActionStart;
            // 记录播放开始时间
            _animActionStartTime = Time.time;
            
            // 根据 动画动作的类型 处理不同的交互方式
            switch (_animActionCurrent.actionOperationType)
            {
                // 点击
                case EAnimActionOperationType.Click:
                    // 播放动画-普通模式
                    PlayAnimNormalMode();
                    break;
                // 拖拽
                case EAnimActionOperationType.Drag:
                    // 设置动画-拖拽模式。等待光标拖拽 控制 动画播放进度
                    SetAnimDragMode();
                    break;
                // 旋转
                case EAnimActionOperationType.Rotate:
                    // 设置动画-旋转模式。等待光标旋转 控制 动画播放进度
                    SetAnimRotateMode();
                    break;
                // 按压
                case EAnimActionOperationType.Press:
                    // 设置动画-按压模式。等待光标按压 控制 动画播放进度
                    SetAnimPressMode();
                    // 立刻播放 按压动画。动画进度会逐渐上升到100%。
                    PlayAnimPressMode();
                    break;
            }
            
            // 触发 动作开始的回调
            _onActionStartEvent?.Invoke(this);
            _onActionStartEvent = null; // 清除回调，避免重复调用
            
            return true;
        }
        
        /// <summary>
        /// 停止 动画动作
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private void StopAnimAction()
        {
            // 如果没有正在播放 动画动作，则直接返回
            if (!_isAnimActionPlaying)
                return;
            _isAnimActionPlaying = false;
            
            // 检查 当前正在播放的 动画动作 是否有效
            if (_animActionCurrent == null) return;
            // 根据 动画动作的类型 处理不同的停止方式
            switch (_animActionCurrent.actionOperationType)
            {
                // 点击
                case EAnimActionOperationType.Click:
                    // 单次播放完成后会自动停止，无需处理
                    // 若为循环播放模式，则需要 玩家播放其他动画动作 来停止当前动画。
                    break;
                // 拖拽
                case EAnimActionOperationType.Drag:
                    // 停止 当动画动作
                    StopAnimActionCheckDelayTime();
                    break;
                // 旋转
                case EAnimActionOperationType.Rotate:
                    // 停止 动画动作
                    StopAnimActionCheckDelayTime();
                    break;
                // 按压
                case EAnimActionOperationType.Press:
                    // 停止 Sp动画-按压模式
                    StopAnimPressMode();
                    break;
            }
        }
        
        #region 动画动作 停止：快速连点保护，限制最小时长。
        /// <summary>
        /// 停止 动画动作
        /// </summary>
        private void StopAnimActionCheckDelayTime()
        {
            // 如果尚未开始播放（未调用 OnLeftClickDown），或最小时间已到，立即停止
            if (_animActionStartTime < 0f)
            {
                StopAnimActionImmediate();
                return;
            }

            // 清除 延迟停止 动画动作 的协程
            ClearCorDelayStopAnimAction();
            
            // 最小间隔的 剩余时间
            float remainTime = Mathf.Max(0f, playAnimActionIntervalTime - (Time.time - _animActionStartTime));
            // 延迟时间
            float delayTime = Mathf.Max(0f, stopAnimActionDelayTime);
            // 启动 延迟停止 动画动作 的协程
            _delayStopAnimActionCor = StartCoroutine(CorDelayStopAnimAction(remainTime + delayTime));
        }

        /// <summary>
        /// 延迟停止 动画动作
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        private IEnumerator CorDelayStopAnimAction(float delay)
        {
            yield return new WaitForSeconds(delay);
            _delayStopAnimActionCor = null;
            StopAnimActionImmediate();
        }
        
        /// <summary>
        /// 立即停止 动画动作
        /// </summary>
        private void StopAnimActionImmediate()
        {
            // 必须最先做：下面会把 _animActionCurrent / _animDataCurrent 置空，而按压、松开、进度阻尼、
            // 循环完成这几类每帧都在解引用它们。原先这里只停了「延迟停止」一条，于是
            // 「按着不放时切换到另一个动作」（PlayAnimAction 会先调本方法）必然抛空引用。
            StopAllAnimActionRoutines();

            if (animator)
                // 停止动画
                animator.StopAnim(_animDataCurrent);
            else
                AnimSimLog.Warn(this, $"{gameObject.name} 的动画控制器 未设置。");

            // 触发 动作完成的回调
            _onActionCompleteEvent?.Invoke(this);
            _onActionCompleteEvent = null; // 清除回调，避免重复调用

            // 清除 当前正在播放的 动画动作
            _animActionCurrent = null;
            // 清除 当前正在播放的 动画数据
            _animDataCurrent = null;
            // 重置计时
            _animActionStartTime = -1f;
        }
        
        /// <summary>
        /// 清除 延迟停止 动画动作 的协程
        /// </summary>
        private void ClearCorDelayStopAnimAction() => KillCor(ref _delayStopAnimActionCor);

        /// <summary>
        /// 停止本播放器全部在途的协程与补间，并复位按压模式标记。
        ///
        /// <para>按压 / 松开 / 阻尼 / 循环完成 这几类都会逐帧解引用 <see cref="_animActionCurrent"/>
        /// 或 <see cref="_animDataCurrent"/>，所以必须在清空这两个字段之前先把它们停掉。</para>
        /// </summary>
        private void StopAllAnimActionRoutines()
        {
            KillCor(ref _delayStopAnimActionCor);
            KillCor(ref _corPlayAnimNormalModeIsLoopOnComplete);
            KillCor(ref _pressModeAnimPressCor);
            KillCor(ref _pressModeAnimReleaseCor);

            // 阻尼已改用 ToolkitTween，不再是协程
            _animProgressDampingHandle.Kill();
            _animProgressDampingHandle = default;

            // 按压被中途打断（例如按着不放时切到另一个动作）后，这个标记必须跟着复位，
            // 否则下次进入按压模式会误判为「已在按压中」而去恢复上一次的旧进度。
            _isPressModeAnimPlaying = false;
        }

        /// <summary>
        /// 停止一条协程并置空其句柄；句柄为空时什么也不做。
        ///
        /// <para>原先四处都写成 <c>try { StopCoroutine(...) } catch</c>——传入一个活着的
        /// <see cref="Coroutine"/> 句柄时 <c>StopCoroutine</c> 不会抛异常，那几个 catch 是无效防御，
        /// 反而掩盖了「句柄该不该置空」这件真正要紧的事。</para>
        /// </summary>
        private void KillCor(ref Coroutine cor)
        {
            if (cor == null) return;
            StopCoroutine(cor);
            cor = null;
        }
        #endregion
        
        /// <summary>
        /// 拖拽 动画动作
        /// </summary>
        /// <param name="cursorWorldPos">光标当前的世界坐标</param>
        private void DragAnimAction(Vector3 cursorWorldPos)
        {
            if (_animActionCurrent == null) return;
            
            switch (_animActionCurrent.actionOperationType)
            {
                // 点击
                case EAnimActionOperationType.Click:
                    break;
                // 拖拽
                case EAnimActionOperationType.Drag:
                    // 播放 Sp动画-拖拽模式
                    PlayAnimDragMode(cursorWorldPos);
                    break;
                // 旋转
                case EAnimActionOperationType.Rotate:
                    // 播放 Sp动画-旋转模式
                    PlayAnimRotateMode(cursorWorldPos);
                    break;
                // 按压
                case EAnimActionOperationType.Press:
                    break;
            }
        }
        
        #region 动画动作 获取与设置
        // 当前选中的 动画动作 索引
        private AnimAction _animActionSelected;
        
        /// <summary>
        /// 获取 满足条件的 动画动作 列表。
        ///
        /// <para>本方法是<b>纯查询</b>：只按当前状态求值，不登记任何回调。条件所依赖的输入发生变化时，
        /// 由 <see cref="AnimSimulatorManager.OnConditionInputsChanged"/> 统一广播，调用方收到后再查一次即可。
        /// 旧实现在这里顺带登记「条件满足后回调我」，两个列表 UI 同时使用时只有最后一个登记的收得到通知。</para>
        /// </summary>
        public List<AnimAction> GetAnimActionsMeetConditions()
        {
            List<AnimAction> animActionList = new List<AnimAction>();
            if (animActions == null) return animActionList;

            // 遍历所有 动画动作
            foreach (var animAction in animActions)
            {
                // 检查 是否满足所有条件
                if (animAction != null && animAction.CheckConditionsIsMet())
                    animActionList.Add(animAction);
            }

            return animActionList;
        }

        /// <summary>
        /// 从候选动作中 按随机权重 选择一个。
        /// 排除 已达随机播放次数上限（randomTypePlayLimit）的动作；按 randomTypeWeight 加权抽样。
        /// </summary>
        /// <param name="candidates">候选动画动作列表（一般为满足条件的动作）</param>
        /// <returns>选中的动画动作；无可用候选时返回 null</returns>
        private AnimAction SelectRandomAnimActionByWeight(List<AnimAction> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;

            // 过滤 已达次数上限的动作，并累计权重
            int totalWeight = 0;
            var picks = new List<AnimAction>();
            var weights = new List<int>();
            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                // 检查 随机播放次数限制（0 表示不限制）
                if (candidate.randomTypePlayLimit > 0)
                {
                    _animActionRandomPlayCountMap.TryGetValue(candidate, out int playedCount);
                    if (playedCount >= candidate.randomTypePlayLimit) continue;
                }
                // 权重最小为 0
                int weight = Mathf.Max(0, candidate.randomTypeWeight);
                picks.Add(candidate);
                weights.Add(weight);
                totalWeight += weight;
            }
            if (picks.Count == 0) return null;

            // 全部权重为 0，退化为 均匀随机
            if (totalWeight <= 0)
                return picks[UnityEngine.Random.Range(0, picks.Count)];

            // 加权随机：在 [0, totalWeight) 内取一点，落入对应权重区间
            int roll = UnityEngine.Random.Range(0, totalWeight);
            for (int i = 0; i < picks.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) return picks[i];
            }
            return picks[picks.Count - 1];
        }

        /// <summary>
        /// 设置 选中的动画动作
        /// </summary>
        /// <param name="animAction"></param>
        public void SetSelectedAnimAction(AnimAction animAction)
        {
            _animActionSelected = animAction;
        }
        
        /// <summary>
        /// 获取 光标向量 和 动作向量
        /// </summary>
        /// <param name="cursorWorldPos"></param>
        /// <param name="cursorDirWs"></param>
        /// <param name="actionDirWs"></param>
        /// <returns></returns>
        private bool GetCursorAndActionDirection(Vector3 cursorWorldPos, out Vector3 cursorDirWs, out Vector3 actionDirWs)
        {
            // 计算 光标向量
            // 计算从 中心点 到 光标 的向量，忽略Z轴。中心点 为AnimActionPlayer的世界位置
            cursorDirWs = cursorWorldPos - transform.position;
            
            // 计算 动作向量
            Quaternion actionDirRotationWs = Quaternion.Euler
            (
                _animActionCurrent.actionDirectionX, 
                _animActionCurrent.actionDirectionY, 
                _animActionCurrent.actionDirectionZ
            );
            // 动作向量 初始方向为 世界空间的Up方向。计算 旋转后的方向向量
            actionDirWs = (actionDirRotationWs * Vector3.up).normalized;
            // 计算 动作区域的半径 获取 动作向量 的 长度
            actionDirWs *= _animActionCurrent.actionRange * 0.5f;

            // 检查向量有效性
            if (cursorDirWs.sqrMagnitude < Mathf.Epsilon || actionDirWs.magnitude < Mathf.Epsilon)
                return false;
            else
                return true;
        }
        #endregion
        #endregion

        #region 进度条 关联
        /// <summary>
        /// 修改 进度条的值
        /// </summary>
        /// <param name="progressValueModify">进度值的修改差值。</param>
        private void ModifyProgressBarsValue(float progressValueModify)
        {
            // 动作已被停止时 _animActionCurrent 为 null（StopAnimActionImmediate 会清掉它），
            // 而本方法有从协程与回调里调用的路径，两者未必与停止同帧。
            if (_animActionCurrent == null) return;
            if (_animActionCurrent.progressBarConfigs == null || _animActionCurrent.progressBarConfigs.Length == 0)
                return;

            // 进度条由管理器统管，没有管理器则整段无从谈起。在循环外取一次并判空。
            var manager = AnimSimulatorManager.Instance;
            if (!manager) return;

            // 遍历所有 进度条 配置
            foreach (var progressBarConfig in _animActionCurrent.progressBarConfigs)
            {
                // 计算 本次变化值。变化值可以是负数。
                float valueModify = progressBarConfig.progressModifyValue * Mathf.Abs(progressValueModify);
                // 应用 进度变化值
                manager.ModifyProgressBars(progressBarConfig.progressName, valueModify);
            }
        }
        #endregion
    }
    
    #region 枚举定义
    /// <summary>
    /// 动画动作播放器 类型。
    /// </summary>
    [Serializable]
    public enum EAnimActionPlayerType
    {
        /// <summary>
        /// 操作模式。
        /// 由玩家 手动操作。
        /// </summary>
        Operate,
        
        /// <summary>
        /// 进度条模式。
        /// 由进度条 进行操作。
        /// </summary>
        ProgressBar,
    }
    
    /// <summary>
    /// 动画动作 播放类型。
    /// </summary>
    [Serializable]
    public enum EAnimActionPlayType
    {
        /// <summary>
        /// 自动播放。
        /// </summary>
        Auto,
        
        /// <summary>
        /// 手动播放。
        /// </summary>
        Manual,
    }
    
    /// <summary>
    /// 动画动作 选择类型。
    /// </summary>
    [Serializable]
    public enum EAnimActionSelectType
    {
        /// <summary>
        /// 选择播放。
        /// 由玩家 手动选择 播放 指定动作。
        /// </summary>
        Select,

        /// <summary>
        /// 顺序播放。
        /// 自动播放。按照 动作列表的顺序 依次播放。
        /// </summary>
        Order,

        /// <summary>
        /// 随机播放。
        /// 自动播放。从动作列表中 随机选择 一个动作进行播放。
        /// </summary>
        Random,
    }
    #endregion
    
    #region 类定义-动画动作
    /// <summary>
    /// 动作配置
    /// </summary>
    [Serializable]
    public class AnimAction
    {
        [Header("基础设置")] 
        [Tooltip("动作名称")] 
        public string actionName = "新动画动作";
#if UNITY_EDITOR
        [Tooltip("备注：仅用于编辑器查看。")] 
        public string comment;
#endif
        [Tooltip("UI中显示的动作名称：填纯文本；启用本地化后还可另选多语言条目，取不到时回退到纯文本。")]
        public TextValue uiDisplayActionName = new TextValue();
        [Tooltip("动作图标")] 
        public Sprite actionIcon;
        [Tooltip("动画轨道：用于不同类型的动画的区分。不同轨道的动画 可以同时播放。")]
        [SerializeField] private EAnimTrack animTrack;
        [Tooltip("动画子轨道：用于同一类型动画的区分。不同子轨道的动画 可以同时播放。"), Range(0, 9)]
        [SerializeField] private int animTrackSub;

        /// <summary>
        /// 动画轨道：主轨道 + 子轨道。
        /// </summary>
        public int AnimTrack
        {
            get
            {
                if (animTrack == EAnimTrack.None)
                    return -1;
                else
                    // 主轨道 * 10 + 子轨道，主轨道间隔10，子轨道0-9，保证不同 子轨道的动画 可以同时播放。
                    return (int)animTrack * 10 + animTrackSub; 
            }
        }
        
        [Header("动作设置")]
#if UNITY_EDITOR
        [Tooltip("显示Gizmos：在编辑器中显示相关Gizmos")]
        public bool showGizmos;
#endif
        [FormerlySerializedAs("actionType")] [Tooltip("动作类型：操作交互的类型")] 
        public EAnimActionOperationType actionOperationType = EAnimActionOperationType.Click;
        [Tooltip("动作交互范围：球形交互范围的直径。(单位：米)在交互范围内的操作 会影响动画动作的播放。")] 
        public float actionRange = 2f;
        [Tooltip("动作交互方向 X轴：动作交互区域的朝向，X轴的旋转角度。(单位：度)"), Range(0f, 360f)]
        public float actionDirectionX;
        [Tooltip("动作交互方向 Y轴：动作交互区域的朝向，Y轴的旋转角度。(单位：度)"), Range(0f, 360f)]
        public float actionDirectionY;
        [Tooltip("动作交互方向 Z轴：动作交互区域的朝向，Z轴的旋转角度。(单位：度)"), Range(0f, 360f)]
        public float actionDirectionZ;
        
        [Header("动画设置")]
        [Tooltip("动画名称：在动画软件中制作时的名称。Spine 与 Live2D 使用相同的命名规则。")]
        public string animName;

        /// <summary>
        /// 解析实际要播放的动画名。未填写 <see cref="animName"/> 时返回 <c>null</c>。
        /// </summary>
        public string ResolveAnimName() => string.IsNullOrEmpty(animName) ? null : animName;

        [Tooltip("动画阻尼时间：用于平滑过渡动画变化的时间（秒）")]
        public float dampingTime = 0.06f;
        [Tooltip("是否 循环播放")]
        public bool isLoop;
        [Tooltip("是否 反向播放动画。")]
        public bool isReverse;
        [Tooltip("动画动作开始的延迟时间（秒）：在玩家操作后，等待一段时间 再开始播放动画。")]
        public float startDelayTime;
        
        [Header("选择类型-随机")]
        [Tooltip("随机权重：当选择类型为 随机时，权重较高的动作被选择的概率更大。")]
        public int randomTypeWeight = 100;
        [Tooltip("限制播放次数：当选择类型为 随机时，限制动作的播放次数。0表示不限制。")]
        public int randomTypePlayLimit = 0;
        
        [Header("点击模式 设置")]
        [Tooltip("动画播放速度：速度倍率。"), Range(0.01f, 3f)]
        public float clickModeAnimPlaySpeed = 1f;
        
        [Header("旋转模式 设置")]
        [Tooltip("允许的角度范围-最大值"), Range(1f, 360f)]
        public float rotateModeAngleRangeMax = 360f;
        [Tooltip("是否 逆时针方向旋转。")]
        public bool isAntiClockwise;
        
        [Header("按压模式 设置")]
        [Tooltip("按压模式。动画按压速度（倍率）。"), Range(0.01f, 3f)]
        public float pressModeAnimPressSpeed = 1f;
        [Tooltip("按压模式。动画松开速度（倍率）。"), Range(0.01f, 3f)]
        public float pressModeAnimReleaseSpeed = 1.5f;
        [Tooltip("按压模式。动画动作停止延迟时间（秒），按压松开后 等待一段时间 再停止动作。")]
        public float pressModeAnimActionStopDelay = 0.5f;
        
        #region 进度条 设置
        [Header("进度条 设置")]
        [Tooltip("进度条 配置组：动作关联的 进度条 配置组，对进度条 进行操作。")]
        public AnimActionProgressBarConfig[] progressBarConfigs;

        /// <summary>
        /// 动画动作 进度条 配置
        /// </summary>
        [Serializable]
        public struct AnimActionProgressBarConfig
        {
            [Tooltip("进度条 名称：对指定的进度条 进行操作。")]
            public string progressName;

            [Tooltip("进度条 修改值：完成一次动画动作，对进度条 进行增加或减少的值。")]
            public float progressModifyValue;
        }
        #endregion
        
        #region 条件 设置
        [Header("条件 设置")]
        [Tooltip("条件：全部满足时，本动作才会出现在 动画动作列表 中。留空即为无条件。")]
        [SerializeField] private ConditionExpression conditions = new ConditionExpression();

        /// <summary>
        /// 检查 条件 是否满足。
        ///
        /// <para>判定交给 toolkit 的条件系统（<see cref="ConditionEngine"/>），数据源由
        /// <see cref="AnimSimulatorManager"/> 以 <see cref="IAnimSimConditionSource"/> 提供。
        /// 空表达式视为「无条件」，直接通过。</para>
        ///
        /// <para><b>与 2.2.0 之前的差异</b>：旧实现只有「大于等于」一种比较、只能 AND、没有分组与取反；
        /// 且在参数解析失败 / 取不到管理器 / 查不到进度条时一律<b>判为满足</b>，配错名字的条件会静默失效。
        /// 现在这些情况一律判否。</para>
        /// </summary>
        public bool CheckConditionsIsMet()
        {
            if (conditions == null || conditions.IsEmpty) return true;

            var context = AnimSimulatorManager.ConditionContext;
            if (context == null)
            {
                AnimSimLog.Warn(nameof(AnimAction), $"'{actionName}' 配了条件，但场景中没有 AnimSimulatorManager，条件无从判定，按不满足处理。");
                return false;
            }

            return conditions.Evaluate(context).Passed;
        }
        #endregion
    }

    /// <summary>
    /// 动画动作 操作类型。
    /// 操作方式 建议与动画的表现相匹配。
    /// 例如，来回拖拽模拟 锯木头 的动作。
    /// </summary>
    [Serializable]
    public enum EAnimActionOperationType
    {
        /// <summary>
        /// 点击。
        /// 点击后，直接播放动画。
        /// </summary>
        Click,
        /// <summary>
        /// 拖拽。
        /// 沿着指定方向来回拖拽，作为动画播放的进度参数。
        /// </summary>
        Drag,
        /// <summary>
        /// 旋转。
        /// 绕指定中心拖拽旋转，作为动画播放的进度参数。
        /// </summary>
        Rotate,
        /// <summary>
        /// 按压。
        /// 长按时，动画进度涨到100%。松开时，动画进度落到0%。
        /// </summary>
        Press,
    }
    #endregion
}
