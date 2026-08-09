#if UNITY_EDITOR
using UnityEditor;
#endif

#if HAS_LOCALIZATION
using UnityEngine.Localization;
#endif

#if HAS_SPINE
using Spine;
using Spine.Unity;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fs.GameFramework.Gameplay.AnimSimulatorSystem
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
        public void OnDrawGizmos()
        {
            // 绘制 动画动作组的Gizmos可视化
            OnDrawGizmosAnimActionGroup();
        }
        
        /// <summary>
        /// 绘制 动画动作组的Gizmos可视化
        /// </summary>
        private void OnDrawGizmosAnimActionGroup()
        {
            foreach (var animAction in animActions)
            {
                // 如果不显示Gizmos，则跳过
                if (animAction.showGizmos == false) continue;

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
                Camera sceneCamera = SceneView.lastActiveSceneView.camera;
                if (sceneCamera != null)
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
                        // 摄像机向量
                        Camera sceneCam = SceneView.lastActiveSceneView.camera;
                        Vector3 cameraDirWs = (sceneCam.transform.position - centerPosWs).normalized;
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
                            
                            // 绘制两个三角形填充
                            Handles.DrawAAConvexPolygon(new[] { outer0, outer1, inner1 });
                            Handles.DrawAAConvexPolygon(new[] { outer0, inner1, inner0 });
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
#if HAS_SPINE
            if (spineAnimator == false)
            {
                // 尝试从父物体获取 SpineAnimator 组件
                spineAnimator = GetComponentInParent<SpineAnimator>();
                // 尝试从其他子物体获取 SpineAnimator 组件
                if (!spineAnimator && transform.parent)
                    spineAnimator = transform.parent.GetComponentInChildren<SpineAnimator>();
            }
#endif
        }
#endif

        private void OnEnable()
        {
            // 注册到 动画模拟器管理器
            if (AnimSimulatorManager.Instance)
                AnimSimulatorManager.Instance.RegisterAnimActionPlayer(this);
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
        /// 拖拽移动（屏幕空间）
        /// </summary>
        /// <param name="cursorScreenPos"></param>
        /// <param name="cursorDeltaDir"></param>
        public void OnDragMoveSS(Vector3 cursorScreenPos, Vector3 cursorDeltaDir)
        {
            // 不检查 是否允许操作，确保操作中的 动画动作 能够响应拖拽
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
    
        /// <summary>
        /// 光标右键按下
        /// </summary>
        /// <param name="cursorWorldPos">光标当前的世界坐标</param>
        public void OnRightClickDown(Vector3 cursorWorldPos)
        {
            
        }
    
        /// <summary>
        /// 光标右键抬起
        /// </summary>
        /// <param name="cursorWorldPos">光标当前的世界坐标</param>
        public void OnRightClickUp(Vector3 cursorWorldPos)
        {
            
        }
        #endregion

        #region 动画 控制
        [Header("动画设置")]
#if HAS_SPINE
        [Tooltip("Spine动画播放器")]
        [SerializeField] private SpineAnimator spineAnimator;
#endif
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
#if HAS_SPINE
            if (spineAnimator == null || _animActionCurrent.animReferenceAsset == null) return;
            // 播放动画
            spineAnimator.PlaySpineAnim
            (
                _spineAnimDataCurrent,
                (trackEntry) =>
                {
                    if (trackEntry.Animation == null) return;

                    // 已经停止播放、或正在延迟停止中，忽略本次回调
                    if (_delayStopAnimActionCor == null &&
                        _animActionCurrent != null &&
                        _animActionCurrent.animReferenceAsset)
                    {
                        // 停止 当前选中的 动画动作
                        StopAnimActionCheckDelayTime();
                    }
                }
            );
#endif
            // 如果是 循环模式，启动 协程 在动画完成后调用 动作完成事件回调
            if (_animActionCurrent.isLoop)
            {
                // 停止 旧的协程
                if (_corPlayAnimNormalModeIsLoopOnComplete != null)
                {
                    try { StopCoroutine(_corPlayAnimNormalModeIsLoopOnComplete); }
                    catch (Exception ex) { Debug.LogWarning($"[AnimActionPlayer] Stop coroutine failed: {ex.Message}"); }
                }
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
            SetAnimProgressMode(_animActionCurrent);
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
            SetAnimProgressMode(_animActionCurrent);
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
            SetAnimProgressMode(_animActionCurrent);
            
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
            if (_pressModeAnimReleaseCor != null)
            {
                try { StopCoroutine(_pressModeAnimReleaseCor); }
                catch (Exception ex) { Debug.LogWarning($"[AnimActionPlayer] Stop press-mode release coroutine failed: {ex.Message}"); }
                _pressModeAnimReleaseCor = null;
            }
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
            if (_pressModeAnimPressCor != null)
            {
                try { StopCoroutine(_pressModeAnimPressCor); }
                catch (Exception ex) { Debug.LogWarning($"[AnimActionPlayer] Stop press-mode press coroutine failed: {ex.Message}"); }
                _pressModeAnimPressCor = null;
            }
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
            // 立即停止 当前动画动作
            StopAnimActionImmediate();
            // 标记为 未在播放 按压模式动画
            _isPressModeAnimPlaying = false;

            _pressModeAnimReleaseCor = null;
        }
        #endregion

        #region Sp动画 动画进度控制
        // 拖拽阻尼 协程
        private Coroutine _animProgressDampingCor;
        // 拖拽阻尼 起始进度值
        private float _animProgressDampingStart;
        // 拖拽阻尼 目标进度值
        private float _animProgressDampingTarget;
        // 拖拽阻尼 当前进度值
        private float _animProgressDampingCurrent;
        
        // 拖拽阻尼 持续时间
        private float _animProgressDampingDurationTime;
        // 拖拽阻尼 已经过的时间
        private float _animProgressDampingElapsedTime;
        // 是否为 循环进度模式（头尾相连）
        private bool _animProgressIsLoop;
        // 循环模式下的进度增量（可能为负），表示从 start 到 target 的最短环绕增量（单位：0..1）
        private float _animProgressDampingDeltaLoop;
        
        /// <summary>
        /// 设置 动画-进度控制模式
        /// </summary>
        /// <param name="animAction"></param>
        private void SetAnimProgressMode(AnimAction animAction)
        {
#if HAS_SPINE
            if (spineAnimator == false || animAction.animReferenceAsset == false) return;
            // 不循环，播放速度为0。由玩家操作 直接设置动画进度。
            _spineAnimDataCurrent.isLoop = false;
            _spineAnimDataCurrent.speed = 0f;
            // 播放Spine动画。
            spineAnimator.PlaySpineAnim(_spineAnimDataCurrent);
#endif
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
            // 设置了阻尼时间，启动阻尼协程
            if (dampingTime > 0f)
            {
                // 记录 阻尼持续时间
                _animProgressDampingDurationTime = dampingTime;
                // 重置 已经过的时间
                _animProgressDampingElapsedTime = 0f;
                // 不会重复启动协程
                if (_animProgressDampingCor == null)
                {
#if HAS_SPINE
                    // 获取 当前动画轨道
                    var trackEntry = spineAnimator.GetTrackEntry(GetCurrentAnimActionTrackIndex());
                    // Spine动画 进度控制的阻尼协程
                    _animProgressDampingCor = StartCoroutine(CorAnimProgressDamping(trackEntry));
#else
                    // Live2D动画 进度控制的阻尼协程
                    _animProgressDampingCor = StartCoroutine(CorAnimProgressDamping());
#endif
                }
            }
            else
            {
                // 无阻尼时间，直接设置进度，但遵守最小帧阈值，避免小幅更新导致抖动
                _animProgressDampingCurrent = _animProgressDampingTarget; // 直接设置为目标进度值
#if HAS_SPINE
                // 获取 当前动画轨道
                var trackEntry = spineAnimator.GetTrackEntry(GetCurrentAnimActionTrackIndex());
                // 直接设置 Sp动画 进度值
                trackEntry.TrackTime = trackEntry.Animation.Duration * _animProgressDampingTarget;
#else
                // 直接设置 Live2D动画 进度值
#endif
                // 修改 进度条的值
                ModifyProgressBarsValue(_animProgressDampingTarget - _animProgressDampingStart);
            }
        }
        
        /// <summary>
        /// 获取 动画的进度值。
        /// </summary>
        /// <returns>进度值 [0.0, 1.0]</returns>
        private float GetAnimProgress()
        {
            float progress = 0f;
#if HAS_SPINE
            if (spineAnimator)
            {
                // 检查 动画轨道 有效性
                var entry = spineAnimator.GetTrackEntry(GetCurrentAnimActionTrackIndex());
                // 计算 当前进度值
                if (entry != null && entry.Animation != null && entry.Animation.Duration > 0f)
                    progress = Mathf.Clamp01(entry.TrackTime / entry.Animation.Duration);
            }
#else
            // 获取 Live2D动画的当前进度
            progress = 0f;
#endif
            return progress;
        }
        
#if HAS_SPINE
        /// <summary>
        /// 协程 拖拽阻尼。动画进度值 平滑变化。
        /// </summary>
        /// <param name="trackEntry">播放的Spine动画轨道</param>
        private IEnumerator CorAnimProgressDamping(TrackEntry trackEntry)
        {
            if (trackEntry == null || trackEntry.Animation == null)
                yield break;
#else   
        /// <summary>
        /// 协程 拖拽阻尼。动画进度值 平滑变化。
        /// </summary>
        /// <param name="trackEntry">播放的Live2D动画轨道</param>
        private IEnumerator CorAnimProgressDamping()
        {
#endif
            _animProgressDampingElapsedTime = 0f; // 重置 已经过的时间
            // 平滑过渡 到 目标进度值
            while (_animProgressDampingElapsedTime < _animProgressDampingDurationTime)
            {
#if HAS_SPINE
                // 检查 当前轨道 未被替换
                var trackEntryCurrent = spineAnimator.GetTrackEntry(GetCurrentAnimActionTrackIndex());
                if (trackEntryCurrent == null || trackEntryCurrent.Animation == null || !ReferenceEquals(trackEntryCurrent, trackEntry))
                {
                    _animProgressDampingCor = null;
                    yield break;
                }
#endif
                // 累计时间
                _animProgressDampingElapsedTime += Time.deltaTime;
                
                // 计算 新的进度值
                float t = Mathf.Clamp01(_animProgressDampingElapsedTime / _animProgressDampingDurationTime);
                float progressNew;
                // 循环模式：沿最短环绕路径插值，然后规整到 [0,1]
                if (_animProgressIsLoop)
                {
                    // 计算 新的进度值。只保留小数部分，将结果规整到 [0,1]。
                    progressNew = _animProgressDampingStart + _animProgressDampingDeltaLoop * t;
                    progressNew -= Mathf.Floor(progressNew);
                }
                // 非循环模式：线性插值
                else
                {
                    // 计算 新的进度值。到 目标值 的线性插值。
                    progressNew = Mathf.Lerp(_animProgressDampingStart, _animProgressDampingTarget, t);
                }
#if HAS_SPINE
                // 设置 Sp动画 进度值
                trackEntry.TrackTime = progressNew * trackEntry.Animation.Duration;
#else
                // 设置 Live2D动画 进度值
#endif
                // 修改 进度条的值
                ModifyProgressBarsValue(progressNew - _animProgressDampingCurrent);
                // 更新 当前进度值
                _animProgressDampingCurrent = progressNew;

                yield return null;
            }

            _animProgressDampingCor = null;
        }
        #endregion
        #endregion
        
        #region 动画动作 播放
        [Header("动作列表")]
        [Tooltip("动画动作播放器 类型：用于区分不同的 动画动作播放器类别。[Operate玩家操作 / ProgressBar进度条控制]。")]
        [SerializeField] private EAnimActionPlayerType animActionPlayerType;
        [FormerlySerializedAs("animActionPlayerSelectType")]
        [Tooltip("动画动作 选择类型：动作组的选择方式。[Select选择 / Order顺序 / Random随机]。")]
        [SerializeField] private EAnimActionSelectType animActionSelectType;
        [Tooltip("动画动作列表：包含的动画动作。")]
        [SerializeField] private AnimAction[] animActions;
        
        /// <summary>
        /// 动画动作播放器 类型。
        /// [Operate玩家操作 / ProgressBar进度条控制]。
        /// </summary>
        public EAnimActionPlayerType AnimActionPlayerType => animActionPlayerType;
        
        // 当前正在播放的 动画动作
        private AnimAction _animActionCurrent;
#if HAS_SPINE
        // 当前正在播放的 动画数据
        private SpineAnimator.SpineAnimData _spineAnimDataCurrent;
#endif
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
                    // 替换 当前悬停的 动画动作播放器。强制设置为 允许操作。
                    AnimSimulatorManager.Instance.ReplaceAnimActionPlayer(this, true);
                    // 打开 动画动作列表 界面，等待玩家选择动作
                    AnimSimulatorManager.Instance.OpenCloseAnimActionList(this, true, true);
                    // 记录回调
                    if (onActionComplete != null)
                        _onActionCompleteEvent = onActionComplete; // 记录动作完成的回调
                    if (onActionStart != null)
                        _onActionStartEvent = onActionStart; // 记录动作开始的回调
                    break;
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
#if HAS_SPINE
            // 记录 当前正在播放的 Spine动画数据
            _spineAnimDataCurrent = new SpineAnimator.SpineAnimData
            (
                _animActionCurrent.animReferenceAsset,
                GetCurrentAnimActionTrackIndex(),
                _animActionCurrent.isLoop,
                _animActionCurrent.isReverse,
                _animActionCurrent.clickModeAnimPlaySpeed,
                _animActionCurrent.startDelayTime
            );
#endif
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
#if HAS_SPINE
            // 获取 Spine动画组件
            if (spineAnimator)
                // 停止 Spine动画
                spineAnimator.StopSpineAnim(_spineAnimDataCurrent);
            else
                Debug.LogWarning($"[AnimActionPlayer] StopAnimActionByIndex: {gameObject.name}的Spine动画组件 未设置。");
#endif
            // 触发 动作完成的回调
            _onActionCompleteEvent?.Invoke(this);
            _onActionCompleteEvent = null; // 清除回调，避免重复调用

            // 清除 当前正在播放的 动画动作
            _animActionCurrent = null;
#if HAS_SPINE
            // 清除 当前正在播放的 Spine动画数据
            _spineAnimDataCurrent = null;
#endif
            // 重置计时与协程状态
            _animActionStartTime = -1f;
            // 清除 延迟停止 动画动作 的协程
            ClearCorDelayStopAnimAction();
        }
        
        /// <summary>
        /// 清除 延迟停止 动画动作 的协程
        /// </summary>
        private void ClearCorDelayStopAnimAction()
        {
            if (_delayStopAnimActionCor != null)
            {
                try { StopCoroutine(_delayStopAnimActionCor); }
                catch (Exception ex) { Debug.LogWarning($"[AnimActionPlayer] Stop delay-stop coroutine failed: {ex.Message}"); }
                _delayStopAnimActionCor = null;
            }
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
        /// 获取 满足条件的 动画动作 列表
        /// </summary>
        /// <param name="onAllUnmetConditionsMet">不满足时添加监听，满足后触发的回调。</param>
        /// <returns></returns>
        public List<AnimAction> GetAnimActionsMeetConditions(Action<AnimAction> onAllUnmetConditionsMet = null)
        {
            List<AnimAction> animActionList = new List<AnimAction>();
            // 遍历所有 动画动作
            foreach (var animAction in animActions)
            {
                // 检查 是否满足所有条件
                if (animAction.CheckAllConditionsIsMet(onAllUnmetConditionsMet))
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
        /// 获取 选中的动画动作
        /// </summary>
        /// <param name="actionIndex"></param>
        /// <param name="animAction"></param>
        /// <returns></returns>
        private bool GetAnimActionByIndex(int actionIndex, out AnimAction animAction)
        {
            animAction = default;
            // 索引无效，直接返回
            if (actionIndex < 0 || actionIndex >= animActions.Length)
            {
                Debug.LogWarning($"[AnimActionPlayer] PlayAnimActionByIndex: Invalid action index {actionIndex}.");
                return false;
            }
            
            // 获取 动画动作
            animAction = animActions[actionIndex];

            return true;
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
            if (_animActionCurrent.progressBarConfigs == null || _animActionCurrent.progressBarConfigs.Length == 0)
                return;
            
            // 遍历所有 进度条 配置
            foreach (var progressBarConfig in _animActionCurrent.progressBarConfigs)
            {
                // 计算 本次变化值。变化值可以是负数。
                float valueModify = progressBarConfig.progressModifyValue * Mathf.Abs(progressValueModify);
                // 应用 进度变化值
                AnimSimulatorManager.Instance.ModifyProgressBars(progressBarConfig.progressName, valueModify);
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
#if HAS_LOCALIZATION
        [Tooltip("UI中显示的动作名称：多语言Key。")]
        public LocalizedString uiDisplayActionName;
#else
        [Tooltip("UI中显示的动作名称")]
        public string uiDisplayActionName;
#endif
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
#if HAS_SPINE
        [Tooltip("动画资源: 播放的Spine动画资源")]
        public AnimationReferenceAsset animReferenceAsset;
#endif
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
        [Tooltip("条件组：满足所有条件，动作才会在 动画动作列表 中出现。")]
        [SerializeField] private AnimActionCondition[] conditions;
        
        /// <summary>
        /// 未满足条件的 对象-条件 映射表。
        /// 在条件要求值变化时，使用该映射表 查找对应的条件 进行重新检查。
        /// </summary>
        private Dictionary<int, AnimActionCondition> _mapHashcodeToUnmetCondition = 
            new Dictionary<int, AnimActionCondition>();
        
        /// <summary>
        /// 满足所有条件时的回调
        /// </summary>
        public event Action<AnimAction> OnAllUnmetConditionsMet;
        
        /// <summary>
        /// 检查 所有条件 是否满足
        /// </summary>
        /// <param name="onAllUnmetConditionsMet">不满足时添加监听，满足后触发的回调。</param>
        /// <returns></returns>
        public bool CheckAllConditionsIsMet(Action<AnimAction> onAllUnmetConditionsMet = null)
        {
            // 清除 未满足条件的映射表
            _mapHashcodeToUnmetCondition.Clear();
            OnAllUnmetConditionsMet = null;
            
            // 遍历检查 所有条件
            bool isAllMeet = true;
            foreach (var condition in conditions)
            {
                if (CheckConditionIsMet(condition, true) == false)
                    isAllMeet = false;
            }
            
            // 若有未满足的条件，注册回调
            if (isAllMeet == false)
                OnAllUnmetConditionsMet = onAllUnmetConditionsMet;
            
            return isAllMeet;
        }

        /// <summary>
        /// 检查 条件 是否满足
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="registerCallback">条件不满足时，是否监听 条件值变化。</param>
        /// <returns></returns>
        private bool CheckConditionIsMet(AnimActionCondition condition, bool registerCallback = false)
        {
            // 检查条件类型
            switch (condition.conditionType)
            {
                // 等级进度条
                case EAnimActionConditionType.LevelProgress:
                    return CheckConditionLevelProgressBar(condition, registerCallback);
                // 道具持有
                case EAnimActionConditionType.Item:
                    //TODO:接入道具系统，检查是否持有指定道具
                    break;
            }
            
            // 默认返回满足条件
            return true;
        }
        
        /// <summary>
        /// 检查 未满足的条件
        /// </summary>
        /// <param name="hashCode"></param>
        /// <returns></returns>
        private bool CheckUnmetConditionByHashcode(int hashCode)
        {
            // 查找 未满足条件的映射表
            if (_mapHashcodeToUnmetCondition.TryGetValue(hashCode, out var condition))
            {
                // 重新检查 条件
                if (CheckConditionIsMet(condition))
                {
                    // 条件已满足，移除映射表
                    _mapHashcodeToUnmetCondition.Remove(hashCode);
                    // 检查 是否所有条件均已满足
                    if (_mapHashcodeToUnmetCondition.Count == 0)
                    {
                        // 触发 所有条件满足的回调
                        OnAllUnmetConditionsMet?.Invoke(this);
                        OnAllUnmetConditionsMet = null; // 清除回调，避免重复调用
                    }
                    
                    // 条件满足，返回 true
                    return true;
                }
            }
            
            // 条件仍未满足，返回 false
            return false;
        }
        
        /// <summary>
        /// 添加 未满足的条件 到映射表
        /// </summary>
        /// <param name="hashCode"></param>
        /// <param name="condition"></param>
        private void AddUnmetConditionByHashcode(int hashCode, AnimActionCondition condition)
        {
            // 添加到 未满足条件的映射表。不重复添加
            _mapHashcodeToUnmetCondition.TryAdd(hashCode, condition);
        }
        #region 条件 等级进度条

        /// <summary>
        /// 检查 条件-等级进度条
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="registerCallback">条件不满足时，是否监听 条件值变化。</param>
        /// <returns></returns>
        private bool CheckConditionLevelProgressBar(AnimActionCondition condition, bool registerCallback = false)
        {
            // 目标 进度条名称
            string levelProgressName = condition.conditionTargetName;
            // 参数解析。“要求等级”
            var paramArray = condition.conditionTargetParameter.Split('|');
            if (paramArray.Length >= 1 && int.TryParse(paramArray[0], out int requiredLevel))
            {
                // 获取 当前等级进度条 的等级
                var uiLevelProgressBar = AnimSimulatorManager.Instance.GetProgressBar<UILevelProgressBar>(levelProgressName);
                // 检查是否满足要求
                if (uiLevelProgressBar is not null && uiLevelProgressBar.LevelNumber < requiredLevel)
                {
                    // 条件不满足。注册等级变化的回调，等待下次检查
                    if (registerCallback)
                    {
                        int hashcode = uiLevelProgressBar.GetHashCode();
                        // 检查 是否已注册
                        if (_mapHashcodeToUnmetCondition.ContainsKey(hashcode) == false)
                        {
                            // 注册 等级变化的回调
                            uiLevelProgressBar.OnLevelNumberChanged += OnLevelNumberChanged;
                            // 记录 条件映射表
                            AddUnmetConditionByHashcode(hashcode, condition);
                        }
                    }
                    // 不满足条件，返回 false
                    return false;
                }
            }
            
            // 满足条件，返回 true
            return true;
        }
        
        /// <summary>
        /// 等级变化时的回调
        /// </summary>
        /// <param name="uiLevelProgressBar"></param>
        /// <param name="levelNumber"></param>
        private void OnLevelNumberChanged(UILevelProgressBar uiLevelProgressBar, int levelNumber)
        {
            // 重新检查 条件
            if (CheckUnmetConditionByHashcode(uiLevelProgressBar.GetHashCode()))
                // 条件已满足，取消注册回调
                uiLevelProgressBar.OnLevelNumberChanged -= OnLevelNumberChanged;
        }
        #endregion
        
        /// <summary>
        /// 动画动作 条件
        /// </summary>
        [Serializable]
        public struct AnimActionCondition
        {
            [Tooltip("条件类型：指定条件的检查方式。")]
            public EAnimActionConditionType conditionType;
            [Tooltip("条件目标名称：根据条件类型，指定条件检查的目标对象名称。例如，等级进度条的名称，道具的名称(ID)。")]
            public string conditionTargetName;
            [Tooltip("条件目标参数：根据条件类型，设置相应的参数值。")]
            public string conditionTargetParameter;
        }
        
        /// <summary>
        /// 动画动作 条件类型。
        /// </summary>
        [Serializable]
        public enum EAnimActionConditionType
        {
            /// <summary>
            /// 等级进度条。
            /// 参数为 "等级进度条名称|要求等级"。大于等于 指定等级时，条件满足。
            /// </summary>
            LevelProgress,
            
            /// <summary>
            /// 道具持有。
            /// 参数为 "道具ID"。持有该道具时，条件满足。
            /// </summary>
            Item,
        }
        #endregion
    }

    /// <summary>
    /// 动画动作 操作类型。
    /// 操作方式 建议与动画的表现相匹配。
    /// 例如，来回拖拽模拟 锯木头 的动作。
    /// </summary>
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
