using System;
using System.Collections.Generic;
using UnityEngine;
using Ale.Toolkit.Runtime;

#if ASS_SPINE
using Spine;
using Spine.Unity;
using Spine.Unity.AttachmentTools;
#endif

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// Spine 动画播放器
    /// </summary>
    public class SpineAnimator : MonoBehaviour
    {
#if ASS_SPINE
        [Header("Spine基础设置")]

        [Tooltip("Spine动画组件")]
        [SerializeField] private SkeletonAnimation spineSkeletonAnimation;
        [Tooltip("切换Spine模型的淡入淡出时间（秒）")]
        [SerializeField] private float spineAnimSwitchSpineDuration = 0.2f;
        [Tooltip("初始化时显示：在初始化时，就会显示Spine动画的渲染。")]
        [SerializeField] private bool isDisplayOnInit;
        
#if UNITY_EDITOR
        private void Reset()
        {
            // 尝试自动获取 SkeletonAnimation组件
            if (!spineSkeletonAnimation)
                spineSkeletonAnimation = GetComponent<SkeletonAnimation>();
        }
#endif
        
        /// <summary>
        /// 初始化
        /// </summary>
        private void Awake()
        {
            // 初始化 Spine动画状态
            InitSpineAnimState();
        }

        private void Start()
        {
            // 初始化 Spine皮肤
            InitSkin();
            
            // 设置初始状态
            SwitchStateArray(spineStateInitList);
        }

        #region 清除与销毁
        /// <summary>
        /// 销毁 Spine动画
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        public bool DestroySpineAnim(out float delay)
        {
            delay = -1f;
            if (!spineSkeletonAnimation) return false;
            
            // 淡出隐藏
            FadeSpineAnimator(false, spineSkeletonAnimation);
            // 返回延迟时间
            delay = spineAnimSwitchSpineDuration;
            
            return true;
        }
        
        /// <summary>
        /// 清除所有 Spine动画
        /// </summary>
        /// <param name="spineAnimator"></param>
        private void ClearSpineAnim(SkeletonAnimation spineAnimator)
        {
            // 清除所有Spine动画
            if (spineAnimator && spineAnimator.state != null)
            {
                // 清除所有轨道动画
                spineAnimator.state.ClearTracks();
                // 重置为初始姿势
                spineAnimator.Skeleton.SetToSetupPose();
                // 更新Skeleton以应用更改
                spineAnimator.LateUpdate();
            }
            
            // 清除所有正在运行的 循环随机间隔动画 协程
            StopSpineAnimLoopCoroutineAll(spineAnimator);
        }
        
        /// <summary>
        /// 清除所有 Spine动画
        /// </summary>
        public void ClearAllSpineAnim()
        {
            // 移除所有状态
            _listStateCurrent.Clear();
            
            // 清除所有 正在等待延时播放的 Spine动画
            ClearAllSpineAnimStartDelay();
            
            // 基础 Spine动画组件
            ClearSpineAnim(spineSkeletonAnimation);
            
            // 其他 Spine动画组件
            foreach (var kv in _dicSpineAnimatorUsageCount)
                ClearSpineAnim(kv.Key);
            _dicSpineAnimatorUsageCount.Clear();
            
            // 清除 轨道-动画引用 记录
            _dicSpineAnimTrackToAnimRefLooping.Clear();
        }
        #endregion
        
        #region Spine动画状态 管理与播放
        [Header("Spine动画状态设置")] 
        [Tooltip("初始状态")] 
        [SerializeField] private string[] spineStateInitList = new string[] { "idle" };
        [Tooltip("Spine状态数据列表")]
        [SerializeField] private FSpineStateData[] spineStateData;
        
        /// <summary>
        /// 当前状态 列表
        /// </summary>
        private List<string> _listStateCurrent = new List<string>();
        
        // 状态名称:状态数据 字典。通过 名称 快速查找 对应数据
        private readonly Dictionary<string, FSpineStateData> _dicStateData = 
            new Dictionary<string, FSpineStateData>();
        
        // 轨道ID:正在循环播放的动画引用（用于优化，避免重复设置同一循环动画）
        private readonly Dictionary<int, AnimationReferenceAsset> _dicSpineAnimTrackToAnimRefLooping = 
            new Dictionary<int, AnimationReferenceAsset>();
        // Spine动画组件 计数器
        // 动画状态使用指定的Spine动画组件时，根据计数器引用 淡入/淡出 该组件
        private readonly Dictionary<SkeletonAnimation, int> _dicSpineAnimatorUsageCount = 
            new Dictionary<SkeletonAnimation, int>();
        
        /// <summary>
        /// 初始化 Spine动画状态
        /// </summary>
        private void InitSpineAnimState()
        {
            // 初始化时 设置Spine动画渲染器 隐藏或显示
            if (spineSkeletonAnimation && isDisplayOnInit == false)
            {
                // 隐藏 Spine动画渲染器
                var skeleton = spineSkeletonAnimation.Skeleton;
                if (skeleton != null)
                    skeleton.A = 0f;
                spineSkeletonAnimation.gameObject.SetActive(false);
            }
            
            // 初始化状态数据字典
            foreach (var data in spineStateData)
            {
                // 检查是否已存在相同状态的数据
                if (_dicStateData.ContainsKey(data.stateName))
                {
                    // 重复的状态数据，添加失败
                    Debug.LogWarning($"SpineAnimator >> Awake: 重复的状态数据 State={data.stateName} GameObject={this.gameObject.name}");
                    continue;
                }
                
                // 初始化 Spine 网格渲染器 隐藏
                if (data.spineSkeletonAnimation)
                {
                    var skeleton = data.spineSkeletonAnimation.Skeleton;
                    if (skeleton != null)
                        skeleton.A = 0f;
                    data.spineSkeletonAnimation.gameObject.SetActive(false);
                }
                
                // 添加到字典
                _dicStateData.Add(data.stateName, data);
            }
        }
        
        /// <summary>
        /// 添加 Spine动画状态
        /// </summary>
        /// <param name="state"></param>
        public void AddSpineAnimState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            
            // 如果状态已存在，则不处理
            if (_listStateCurrent.Contains(state)) return;
            // 记录新状态
            _listStateCurrent.Add(state);
            
            // 获取状态数据
            if (_dicStateData.TryGetValue(state, out var stateData) == false) return;
            // 获取对应的 Spine动画组件
            var spineAnimator = stateData.spineSkeletonAnimation;
            if (!spineAnimator) spineAnimator = spineSkeletonAnimation; // 默认的Spine动画组件
            // 检查 Spine动画组件 是否有效
            if (spineAnimator)
            {
                // 计数器增加
                int count = 1;
                if (_dicSpineAnimatorUsageCount.ContainsKey(spineAnimator) == false)
                    _dicSpineAnimatorUsageCount[spineAnimator] = count;
                else
                    count = ++_dicSpineAnimatorUsageCount[spineAnimator];
                
                // 第一次使用该组件，淡入显示
                if (count == 1)
                    FadeSpineAnimator(true, spineAnimator);
            }
            else
            {
                Debug.LogWarning($"SpineAnimator >> AddSpineAnim: SkeletonAnimation 为空，无法添加状态 {state} 的动画，GameObject={this.gameObject.name}");
                return;
            }
            
            // 调用AnimationState可以初始化Spine动画状态机
            if (spineAnimator.AnimationState == null || stateData.spineAnimDatas == null) return;
            // 播放 动画数据。
            PlaySpineAnimDatas(spineAnimator, stateData.spineAnimDatas);
        }
        
        /// <summary>
        /// 播放 动画数据
        /// </summary>
        /// <param name="spineAnimator"></param>
        /// <param name="spineAnimDatas"></param>
        private void PlaySpineAnimDatas(SkeletonAnimation spineAnimator,SpineAnimData[] spineAnimDatas)
        {
            // 播放 状态数据中配置的 Spine动画
            foreach (var animData in spineAnimDatas)
            {
                // 计算轨道索引: 主轨道 + 子轨道
                int trackIndex = animData.AnimTrack;
                    
                // 如果是循环动画，且当前 已经在该轨道上 循环播放 同名动画，则跳过设置
                if (animData.isLoop)
                {
                    if (_dicSpineAnimTrackToAnimRefLooping.TryGetValue(trackIndex, out var existingAnimRef))
                        // 已在循环播放同名动画，跳过设置
                        if (existingAnimRef == animData.animRefAsset) continue;
                        else
                            // 非重复循环动画，记录被覆盖
                            _dicSpineAnimTrackToAnimRefLooping[trackIndex] = animData.animRefAsset;
                }
                else if (_dicSpineAnimTrackToAnimRefLooping.ContainsKey(trackIndex))
                    // 非循环动画，记录被移除
                    _dicSpineAnimTrackToAnimRefLooping.Remove(trackIndex);
                    
                // 根据 是否需要 循环间隔播放，选择不同的播放方式
                // 如果此动画 需要在 每次循环 间隔随机等待（LoopIntervalTimeRange 非零且大于等于0）
                bool hasLoopInterval = 
                    animData.isLoop && 
                    animData.loopIntervalTimeRange != Vector2.zero && 
                    animData.loopIntervalTimeRange.y >= animData.loopIntervalTimeRange.x;
                if (hasLoopInterval)
                    // 协程循环 播放动画，每次循环后 等待 随机间隔时间
                    StartSpineAnimLoopCoroutine(spineAnimator, animData);
                else
                    // 播放 Spine动画
                    PlaySpineAnim(animData);
            }
        }
        
        /// <summary>
        /// 移除 Spine动画状态
        /// </summary>
        /// <param name="state"></param>
        public void RemoveSpineAnimState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            
            // 如果状态不存在，则不处理
            if (!_listStateCurrent.Contains(state)) return;
            // 移除状态
            _listStateCurrent.Remove(state);
            
            // 获取状态数据
            if (_dicStateData.TryGetValue(state, out var stateData) == false) return;
            // 获取对应的 SkeletonAnimation 组件
            var spineAnimator = stateData.spineSkeletonAnimation;
            if (!spineAnimator) spineAnimator = spineSkeletonAnimation; // 默认的Spine动画组件
            if (spineAnimator)
            {
                // 计数器减少
                if (_dicSpineAnimatorUsageCount.TryGetValue(spineAnimator, out var count))
                {
                    count--;
                    if (count == 0)
                        // 不再使用该组件，淡出隐藏
                        FadeSpineAnimator(false, spineAnimator);
                    // 更新计数器
                    count = count < 0 ? 0 : count; // 避免负数
                    _dicSpineAnimatorUsageCount[spineAnimator] = count;
                }
            }
            else
            {
                Debug.LogWarning($"SpineAnimator >> RemoveSpineAnim: SkeletonAnimation 为空，无法移除状态 {state} 的动画，GameObject={this.gameObject.name}");
                return;
            }
            
            // 调用AnimationState可以初始化Spine动画状态机
            if (spineAnimator.AnimationState == null || stateData.spineAnimDatas == null) return;
            // 移除 状态数据中配置的 Spine动画
            foreach (var animData in stateData.spineAnimDatas)
            {
                int trackIndex = animData.AnimTrack;
                    
                // 如果是循环动画，移除记录
                if (animData.isLoop && _dicSpineAnimTrackToAnimRefLooping.ContainsKey(trackIndex))
                    _dicSpineAnimTrackToAnimRefLooping.Remove(trackIndex);
                    
                // 停止 循环随机间隔动画 协程
                StopSpineAnimLoopCoroutine(spineAnimator, trackIndex);
                // 清除该轨道动画
                spineAnimator.state.ClearTrack(trackIndex);
            }
        }
        
        /// <summary>
        /// 切换状态列表：切换整个 状态列表。
        /// </summary>
        /// <param name="actorAnims"></param>
        public void SwitchStateArray(string[] actorAnims)
        {
            if (actorAnims == null) return;

            // 与现有的 状态列表 进行对比，移除不存在的状态，添加新的状态
            var statesToRemove = new List<string>();
            foreach (var existingState in _listStateCurrent)
            {
                if (Array.IndexOf(actorAnims, existingState) < 0)
                    statesToRemove.Add(existingState);
            }
            // 先移除 不存在的状态
            foreach (var stateToRemove in statesToRemove)
                RemoveSpineAnimState(stateToRemove);
            // 再添加 新的状态
            foreach (var newState in actorAnims)
                AddSpineAnimState(newState);
        }
        
        #region 循环动画 随机间隔
        // 正在运行的 循环随机间隔动画 补间句柄表
        private readonly Dictionary<SkeletonAnimation, Dictionary<int, ToolkitTweenHandle>> _mDicSpineAnimTrackToLoopCoroutine =
            new Dictionary<SkeletonAnimation, Dictionary<int, ToolkitTweenHandle>>();

        /// <summary>
        /// 停止所有 Spine动画 的 循环随机间隔动画
        /// </summary>
        /// <param name="spineAnimator"></param>
        private void StopSpineAnimLoopCoroutineAll(SkeletonAnimation spineAnimator)
        {
            if (!spineAnimator) return;
            if (_mDicSpineAnimTrackToLoopCoroutine.Count == 0) return;

            // 停止所有
            if (_mDicSpineAnimTrackToLoopCoroutine.TryGetValue(spineAnimator, out var dicTrackToItem))
            {
                foreach (var kv in dicTrackToItem)
                    kv.Value.Kill();
                dicTrackToItem.Clear();
            }
        }

        /// <summary>
        /// 启动指定轨道的 循环随机间隔动画
        /// </summary>
        /// <param name="spineAnimator"></param>
        /// <param name="animData"></param>
        private void StartSpineAnimLoopCoroutine
        (
            SkeletonAnimation spineAnimator,
            SpineAnimData animData)
        {
            // 停止已有
            StopSpineAnimLoopCoroutine(spineAnimator, animData.AnimTrack);

            // 每次播放后等待一个随机延迟再播放下一次
            PlaySpineAnimLoopIntervalTime(spineAnimator, animData);
        }

        /// <summary>
        /// 停止 指定轨道的 循环随机间隔动画
        /// </summary>
        /// <param name="spineAnimator"></param>
        /// <param name="trackIndex"></param>
        private void StopSpineAnimLoopCoroutine(SkeletonAnimation spineAnimator, int trackIndex)
        {
            if (!spineAnimator) return;
            if (_mDicSpineAnimTrackToLoopCoroutine.TryGetValue(spineAnimator, out var dicTrackToItem))
            {
                if (dicTrackToItem.TryGetValue(trackIndex, out var item))
                {
                    // 停止补间
                    item.Kill();
                    // 移除记录
                    dicTrackToItem.Remove(trackIndex);
                }
            }
        }

        /// <summary>
        /// 播放 Spine 动画，循环播放且在每次播放后等待一个随机间隔时间。
        /// 由 <see cref="ToolkitTween.DelayedCall"/> 递归调度实现。
        /// </summary>
        /// <param name="spineAnimator"></param>
        /// <param name="animData"></param>
        private void PlaySpineAnimLoopIntervalTime
        (
            SkeletonAnimation spineAnimator,
            SpineAnimData animData
        )
        {
            if (!spineAnimator || !animData.animRefAsset || animData.animRefAsset.Animation == null) return;

            // 设置为 非循环播放，由 递归调度 控制 循环和间隔
            animData.isLoop = false;

            // 预计算动画时长（考虑速度倍率）
            float animDuration = animData.animRefAsset.Animation.Duration / Mathf.Max(0.001f, Mathf.Abs(animData.speed));
            int trackIndex = animData.AnimTrack;

            // 获取或创建轨道字典
            if (!_mDicSpineAnimTrackToLoopCoroutine.TryGetValue(spineAnimator, out var dicTrackToTween))
            {
                dicTrackToTween = new Dictionary<int, ToolkitTweenHandle>();
                _mDicSpineAnimTrackToLoopCoroutine.Add(spineAnimator, dicTrackToTween);
            }

            // 递归调度：播放一次动画，等待 动画时长+随机间隔 后，调度下一次
            void ScheduleNextLoop()
            {
                if (!spineAnimator || !spineAnimator.gameObject.activeInHierarchy) return;

                // 播放一次
                PlaySpineAnimWhenTrackEmpty(animData);

                // 计算随机间隔，等待 动画时长 + 间隔时间 后，调度下一次循环
                float intervalDelay = UnityEngine.Random.Range(animData.loopIntervalTimeRange.x, animData.loopIntervalTimeRange.y);
                intervalDelay = Mathf.Max(0.001f, intervalDelay); // 最小等待时间保护
                // owner 传 this：本组件被销毁后调度自动中止，不会再对已失效的骨架下发播放。
                dicTrackToTween[trackIndex] =
                    ToolkitTween.DelayedCall(animDuration + intervalDelay, ScheduleNextLoop, owner: this);
            }

            // 处理初始延迟
            if (animData.startDelayTime > 0f)
            {
                dicTrackToTween[trackIndex] =
                    ToolkitTween.DelayedCall(animData.startDelayTime, ScheduleNextLoop, owner: this);
            }
            else
            {
                ScheduleNextLoop();
            }
        }
        #endregion
        #endregion

        #region Spine动画 播放与停止
        /// <summary>
        /// 轨道ID:动画引用列表。记录 各个轨道上 正在播放的动画数据。
        /// 相同轨道上 被新的动画 覆盖时，将新动画 记录到列表，并播放新动画。
        /// 当新动画 停止时，会从列表中移除，并在列表中找到 最后一个动画继续播放，实现动画的 切换与恢复。
        /// </summary>
        private readonly Dictionary<int, List<SpineAnimData>> _trackToAnimDataListPlayingMap = 
            new Dictionary<int, List<SpineAnimData>>();
        
        /// <summary>
        /// 播放 Spine动画
        /// </summary>
        /// <param name="animData">动画数据</param>
        /// <param name="onOncePlayComplete">单次播放 完成的回调。（循环播放时 不会被调用）</param>
        public void PlaySpineAnim
        (
            SpineAnimData animData,
            Action<TrackEntry> onOncePlayComplete = null
        )
        {
            // 检查 参数有效性
            if (!spineSkeletonAnimation || !animData.animRefAsset || animData.animRefAsset.Animation == null)
            {
                Debug.LogWarning($"SpineAnimator >> PlaySpineAnim: 播放动画失败，GameObject={this.gameObject.name} AnimData={animData} AnimRefAsset={(animData.animRefAsset ? animData.animRefAsset.Animation : null)}");
                return;
            }
            
            if (animData.startDelayTime > 0f)
            {
                if (_spineAnimStartDelayTweenMap.ContainsKey(animData)) return;
                // owner 传 this：本组件被销毁后延时自动作废，不会再对已失效的骨架下发播放。
                var delayHandle = ToolkitTween.DelayedCall(animData.startDelayTime, () =>
                {
                    _spineAnimStartDelayTweenMap.Remove(animData);
                    PlaySpineAnimImmediate(animData, onOncePlayComplete);
                }, owner: this);
                // 仅登记真正在途的延时。句柄无效（运行器不可用等）时不登记，
                // 否则这条记录会永久卡住该动画数据——上面的 ContainsKey 会让后续播放一律提前返回。
                if (delayHandle.IsActive) _spineAnimStartDelayTweenMap.Add(animData, delayHandle);
                return;
            }

            PlaySpineAnimImmediate(animData, onOncePlayComplete);
        }
        
        /// <summary>
        /// 立刻播放 Spine动画。
        /// </summary>
        /// <param name="animData"></param>
        /// <param name="onOncePlayComplete"></param>
        /// <returns></returns>
        private TrackEntry PlaySpineAnimImmediate
        (
            SpineAnimData animData,
            Action<TrackEntry> onOncePlayComplete = null
        )
        {
            // 检查 参数有效性
            if (!spineSkeletonAnimation|| !animData.animRefAsset || animData.animRefAsset.Animation == null)
            {
                Debug.LogWarning($"SpineAnimator >> PlaySpineAnimImmediate: 播放动画失败，GameObject={this.gameObject.name} AnimData={animData} AnimRefAsset={(animData.animRefAsset ? animData.animRefAsset.Animation : null)}");
                return null;
            }
            
            // 获取 当前轨道上 正在播放的动画列表
            int animTrackIndex = animData.AnimTrack;
            if (!_trackToAnimDataListPlayingMap.TryGetValue(animTrackIndex, out var animDataListPlaying))
            {
                animDataListPlaying = new List<SpineAnimData>();
                _trackToAnimDataListPlayingMap.Add(animTrackIndex, animDataListPlaying);
            }
            // 检查，是否 已经在播放 该动画数据。
            // 引用地址作为唯一标识，理论上不会重复。外部调用接口时，一般会new一个新的数据，数据的引用地址是唯一的。
            if (animDataListPlaying.Contains(animData)) return null;
            
            // 记录 新动画。
            animDataListPlaying.Add(animData);
            
            // 设置动画
            var trackEntryPlay = 
                spineSkeletonAnimation.state.SetAnimation(animTrackIndex, animData.animRefAsset, animData.isLoop);
            // 设置 混合时间
            float defaultMixDuration = spineSkeletonAnimation.SkeletonDataAsset.defaultMix; // 默认混合时间
            float maxMixDuration = animData.animRefAsset.Animation.Duration * 0.3f; // 最大混合时间
            if (defaultMixDuration > maxMixDuration)
                // 限制 混合时间 不超过 最大混合时间。避免 动画播放效果异常
                trackEntryPlay.MixDuration = maxMixDuration;
            
            // 设置 初始进度值。若反转，则初始进度为 1，否则为 0。
            if (animData.isReverse)
            {
                // 反转时，设置动画进度为 结束位置，并启用反转播放
                trackEntryPlay.TrackTime = animData.animRefAsset.Animation.Duration;
                // 反转播放，速度为负值
                trackEntryPlay.TimeScale = -Mathf.Abs(animData.speed); 
            }
            else
            {
                // 正常播放，设置动画进度为 起始位置
                trackEntryPlay.TrackTime = 0f;
                // 正常播放，速度为正值
                trackEntryPlay.TimeScale = Mathf.Abs(animData.speed); 
            }
            
            // 非循环播放时，注册 动画完成的回调
            if (animData.isLoop == false)
                OncePlaySpineAnimComplete(animData, trackEntryPlay, onOncePlayComplete);
            
            return trackEntryPlay;
        }

        /// <summary>
        /// 播放 Spine动画，播放完成后 停止动画，并触发回调。
        /// </summary>
        /// <param name="animData"></param>
        /// <param name="trackEntry"></param>
        /// <param name="onOncePlayComplete"></param>
        private void OncePlaySpineAnimComplete
        (
            SpineAnimData animData,
            TrackEntry trackEntry = null,
            Action<TrackEntry> onOncePlayComplete = null
        )
        {
            // 计算动画时长（考虑速度倍率）。
            float speed = Mathf.Abs(animData.speed);
            if (speed == 0f) return; // 速度为0, 动画暂停，无法完成
            float delayTime = animData.animRefAsset.Animation.Duration / speed;

            // 等待指定时间后，触发回调。owner 传 this，组件销毁后不再回调。
            ToolkitTween.DelayedCall(delayTime, () =>
            {
                // 停止播放。从 正在播放的动画列表中移除。
                StopSpineAnim(animData);
                // 触发回调
                onOncePlayComplete?.Invoke(trackEntry);
            }, owner: this);
        }
        
        /// <summary>
        /// 停止 Spine动画
        /// </summary>
        /// <param name="animData"></param>
        public void StopSpineAnim(SpineAnimData animData)
        {
            if (!spineSkeletonAnimation || animData == null) return;
            
            // 如果正在 等待延时播放。则直接清空 延时Tween，取消播放。
            if (CancelSpineAnimStartDelay(animData)) return;
            
            // 获取 当前轨道上 正在播放的动画列表
            int animTrackIndex = animData.AnimTrack;
            if (!_trackToAnimDataListPlayingMap.TryGetValue(animTrackIndex, out var animDataListPlaying)) return;
            // 从列表中 移除 指定动画。从最后面开始查找。
            int index = animDataListPlaying.LastIndexOf(animData);
            if (index >= 0)
            {
                // 从列表中 移除 指定动画
                animDataListPlaying.RemoveAt(index);
                if (index == animDataListPlaying.Count && index > 0)
                {
                    // 被移除的动画 是列表中 最后一个动画，且列表中 还有其他动画，则继续播放 列表中的 最后一个动画
                    var animDataPrevious = animDataListPlaying[^1];
                    spineSkeletonAnimation.state.SetAnimation(animTrackIndex, animDataPrevious.animRefAsset, true);
                }
                else
                {
                    // 否则，直接停止该轨道动画
                    spineSkeletonAnimation.state.SetEmptyAnimation(animTrackIndex, 0.2f);
                }
            }
        }
        
        /// <summary>
        /// 播放 Spine动画。仅当 指定轨道上 没有正在播放的动画。
        /// </summary>
        /// <param name="animData">动画数据</param>
        /// <returns></returns>
        private TrackEntry PlaySpineAnimWhenTrackEmpty(SpineAnimData animData)
        {
            if (!spineSkeletonAnimation || animData == null ) return null;
            
            // 获取 当前轨道上 正在播放的动画列表
            int count = 0;
            if (_trackToAnimDataListPlayingMap.TryGetValue(animData.AnimTrack, out var animRefsListPlaying))
                count = animRefsListPlaying.Count;
            
            // 仅当轨道上 没有正在播放的动画时，才播放新动画
            if (count == 0)
                return PlaySpineAnimImmediate(animData);
            
            return null;
        }
        
        /// <summary>
        /// 获取 动画轨道。
        /// </summary>
        /// <param name="trackIndex">动画轨道的索引</param>
        /// <returns></returns>
        public TrackEntry GetTrackEntry(int trackIndex)
        {
            if (!spineSkeletonAnimation) return null;
            
            return spineSkeletonAnimation.state.GetCurrent(trackIndex);
        }
        
        #region 延时播放动画
        // 映射表 动画数据：延时播放补间句柄。记录正在进行的 延时播放动画，以便在需要时 取消延时播放。
        private readonly Dictionary<SpineAnimData, ToolkitTweenHandle> _spineAnimStartDelayTweenMap =
            new Dictionary<SpineAnimData, ToolkitTweenHandle>();

        /// <summary>
        /// 清除所有 延时播放的 Spine动画。
        /// 用于 切换状态时，清空之前状态的 正在等待延时播放的 Spine动画数据。
        /// </summary>
        private void ClearAllSpineAnimStartDelay()
        {
            foreach (var kv in _spineAnimStartDelayTweenMap)
                kv.Value.Kill();
            _spineAnimStartDelayTweenMap.Clear();
        }

        /// <summary>
        /// 取消 延时播放的 Spine动画。用于 切换状态时，取消之前状态的 正在等待延时播放的 动画数据。
        /// </summary>
        /// <param name="animData"></param>
        /// <returns>是否确实取消了一条在途的延时播放。</returns>
        private bool CancelSpineAnimStartDelay(SpineAnimData animData)
        {
            if (animData == null) return false;

            if (_spineAnimStartDelayTweenMap.TryGetValue(animData, out var handle))
            {
                handle.Kill();
                _spineAnimStartDelayTweenMap.Remove(animData);
                return true;
            }
            return false;
        }
        #endregion
        #endregion
        
        #region Spine组件淡入/淡出
        // 正在进行淡入/淡出的 Spine动画组件:对应的 补间句柄
        private readonly Dictionary<SkeletonAnimation, ToolkitTweenHandle> _mDicSpineAnimFadeHandle =
            new Dictionary<SkeletonAnimation, ToolkitTweenHandle>();

        /// <summary>
        /// 淡入/淡出 Spine动画组件（补间 <c>Skeleton.A</c>）。
        /// </summary>
        /// <param name="isFadeIn">是否为淡入</param>
        /// <param name="spineAnimator">目标 SkeletonAnimation，为空时使用默认组件</param>
        /// <param name="clearAnimOnFadeOut">淡出完成后是否清除动画数据。
        /// true（默认）= 正常销毁流程，清除动画轨道和数据；
        /// false = 临时隐藏，仅禁用对象，保留动画数据以便之后恢复。</param>
        public void FadeSpineAnimator(bool isFadeIn, SkeletonAnimation spineAnimator = null, bool clearAnimOnFadeOut = true)
        {
            if (!spineAnimator) spineAnimator = spineSkeletonAnimation;
            if (!spineAnimator) return;

            // 立刻完成 当前的淡入/淡出：其完成回调会同步执行并把自己从表中移除，
            // 因此下面登记新句柄时表里必定不残留旧记录。必须在 SetActive(true) 之前——
            // 旧的淡出回调里带着 SetActive(false)，顺序反了会把刚激活的对象又关掉。
            if (_mDicSpineAnimFadeHandle.TryGetValue(spineAnimator, out var handleFadeCur))
                handleFadeCur.Complete();

            // 如果是淡入，确保对象是激活状态
            if (isFadeIn)
            {
                spineAnimator.gameObject.SetActive(true);
            }

            var skeleton = spineAnimator.Skeleton;
            if (skeleton == null)
            {
                Debug.LogWarning($"SpineAnimator >> FadeSpineAnimator: SkeletonAnimation 的 Skeleton 为空，无法淡入/淡出，GameObject={spineAnimator.gameObject.name}");
                _mDicSpineAnimFadeHandle.Remove(spineAnimator);
                return;
            }

            float fadeDur = Mathf.Max(0.001f, spineAnimSwitchSpineDuration);
            float targetAlpha = isFadeIn ? 1f : 0f; // 目标透明度
            float startAlpha = skeleton.A;          // 起始透明度

            // fadeDur 恒 > 0，故 To() 必定异步推进、完成回调不会在本方法返回前触发——
            // 下面那行「登记新句柄」才不会被回调里的 Remove 抢先。
            // owner 传 this：本组件被销毁后补间自动作废，不再向已失效的骨架写 alpha。
            var handleFadeNew = ToolkitTween.To(startAlpha, targetAlpha, fadeDur,
                x => skeleton.A = x,
                EToolkitEase.Linear,
                onComplete: () =>
                {
                    // 目标已被销毁，直接移除记录并返回
                    if (!spineAnimator)
                    {
                        _mDicSpineAnimFadeHandle.Remove(spineAnimator);
                        return;
                    }
                    // 立刻设置为目标透明度
                    skeleton.A = targetAlpha;
                    // 如果是淡出，禁用对象
                    if (isFadeIn == false)
                    {
                        if (clearAnimOnFadeOut)
                        {
                            // 正常销毁流程：清除所有动画数据
                            ClearSpineAnim(spineAnimator);
                        }
                        // 禁用对象（临时隐藏 和 正常销毁 都需要）
                        if (spineAnimator) spineAnimator.gameObject.SetActive(false);
                    }

                    // 移除记录
                    _mDicSpineAnimFadeHandle.Remove(spineAnimator);
                },
                owner: this);

            _mDicSpineAnimFadeHandle[spineAnimator] = handleFadeNew;
        }
        #endregion
        
        #region 皮肤管理
        [Header("Spine皮肤设置")]
        [Tooltip("基础皮肤 列表：始终显示的 基础皮肤。")]
        [SpineSkin] public string[] baseSkins;

        // 是否已初始化皮肤
        private bool _isSkinInit;
        // 缓存的 基础皮肤 列表
        private Skin[] _cachedBaseSkins;
        // 缓存的 皮肤数据 字典
        private readonly Dictionary<string, Skin> _cachedSkins = new Dictionary<string, Skin>();
        // 应用中的 皮肤列表
        private readonly List<Skin> _applySkins = new List<Skin>();
        
        /// <summary>
        /// 初始化 Spine皮肤
        /// </summary>
		private void InitSkin()
        {
            // 检查 是否已初始化
            if (_isSkinInit) return;
            _isSkinInit = true;
            
            // 获取Skeleton中的所有Skin并缓存
            if (spineSkeletonAnimation)
            {
                // 缓存 皮肤名称:皮肤数据
                SkeletonData skeletonData = spineSkeletonAnimation.Skeleton.Data;
                foreach (var skin in skeletonData.Skins)
                    _cachedSkins[skin.Name] = skin;
            }
            
            // 设置 基础皮肤
            SetBaseSkin(baseSkins);
		}

        /// <summary>
        /// 设置 基础皮肤组
        /// </summary>
        /// <param name="baseSkinNames">基础皮肤组：始终存在的 皮肤名称列表。</param>
        /// <param name="isRefresh">是否 刷新皮肤 显示。默认为 true。</param>
        public void SetBaseSkin(string[] baseSkinNames, bool isRefresh = true)
        {
            // 检查 是否已经初始化
            InitSkin();
            
            // 设置 基础皮肤 列表
            baseSkins = baseSkinNames;
            
            // 缓存 基础皮肤 列表
            _cachedBaseSkins = new Skin[baseSkins.Length];
            for (int i = 0; i < baseSkins.Length; i++)
            {
                // 获取并记录 基础皮肤
                if (_cachedSkins.TryGetValue(baseSkins[i], out var skin))
                    _cachedBaseSkins[i] = skin;
                else
                    Debug.LogWarning($"SpineAnimator >> StartSkin: 基础皮肤名称 {baseSkins[i]} 不存在，无法添加基础皮肤，GameObject={gameObject.name}");
            }
            
            // 刷新皮肤
            if (isRefresh) RefreshSkin();
        }
        
        /// <summary>
        /// 添加皮肤
        /// </summary>
        /// <param name="skinName">皮肤名称：有文件夹路径时，一般使用 '/' 作为分隔符。</param>
        /// <param name="isRefresh">立即刷新：是否立即刷新皮肤显示。默认为 true。</param>
        public void AddSkin(string skinName, bool isRefresh = true)
        {
            // 检查 是否已经初始化
            InitSkin();
            
            // 获取皮肤数据
            if (!_cachedSkins.TryGetValue(skinName, out var skin))
            {
                Debug.LogWarning($"SpineAnimator >> AddSkin: 皮肤名称 {skinName} 不存在，无法添加皮肤，GameObject={gameObject.name}");
                return;
            }
            
            // 如果皮肤已存在，则不重复添加
            if (!_applySkins.Contains(skin))
            {
                // 添加皮肤名称
                _applySkins.Add(skin);
                // 刷新皮肤
                if (isRefresh) RefreshSkin();
            }
        }

        /// <summary>
        /// 移除皮肤
        /// </summary>
        /// <param name="skinName">皮肤名称：有文件夹路径时，一般使用 '/' 作为分隔符。</param>
        /// <param name="isRefresh">立即刷新：是否立即刷新皮肤显示。默认为 true。</param>
        public void RemoveSkin(string skinName, bool isRefresh = true)
        {
            // 检查 是否已经初始化
            InitSkin();
            
            // 获取皮肤数据
            if (!_cachedSkins.TryGetValue(skinName, out var skin))
            {
                Debug.LogWarning($"SpineAnimator >> RemoveSkin: 皮肤名称 {skinName} 不存在，无法移除皮肤，GameObject={gameObject.name}");
                return;
            }
            
            // 如果皮肤存在，则移除
            if (_applySkins.Contains(skin))
            {
                // 移除皮肤名称
                _applySkins.Remove(skin);
                // 刷新皮肤
                if (isRefresh) RefreshSkin();
            }
        }
        
        /// <summary>
        /// 刷新 皮肤
        /// </summary>
		public void RefreshSkin()
        {
            if (!spineSkeletonAnimation) return;
            
            Skeleton skeleton = spineSkeletonAnimation.Skeleton;
			var combinedSkin = new Skin("Combined Skin");
			
            // 添加基础皮肤
            if (_cachedBaseSkins != null)
            {
                foreach (var baseSkin in _cachedBaseSkins)
                {
                    if (baseSkin != null)
                        combinedSkin.AddSkin(baseSkin);
                }
            }
            
            // 添加应用中的皮肤
            foreach (var skin in _applySkins)
            {
                if (skin != null)
                    combinedSkin.AddSkin(skin);
            }
            
            // 应用合并的皮肤
            skeleton.SetSkin(combinedSkin);
            skeleton.SetSlotsToSetupPose();
		}
        
        // 打包的皮肤。用于优化皮肤，减少绘制调用和内存使用。
        private Material _repackedMaterial;
        // 打包的图集。
        private Texture2D _repackedAtlas;
        
        /// <summary>
        /// 打包皮肤。
        /// 将 所有皮肤 合并重打包，减少 绘制调用 和 内存使用。
        /// 一般在 角色皮肤的 选择与切换 完成后调用。
        /// </summary>
        public void RepackedSkin() 
        {
            if (!spineSkeletonAnimation) return;
            
            // 销毁 旧的打包材质与图集
            if (_repackedMaterial)
                Destroy(_repackedMaterial);
            if (_repackedAtlas)
                Destroy(_repackedAtlas);
            // 获取 当前皮肤
            Skin skinCurrent = spineSkeletonAnimation.Skeleton.Skin;
            // 打包成 新的皮肤
            Skin skinRepacked = skinCurrent.GetRepackedSkin
            (
                "Repacked Skin",
                spineSkeletonAnimation.SkeletonDataAsset.atlasAssets[0].PrimaryMaterial,
                out _repackedMaterial,
                out _repackedAtlas
            );
            // 清除 旧的皮肤数据
            skinCurrent.Clear();

            // 应用 新的打包皮肤
            spineSkeletonAnimation.Skeleton.Skin = skinRepacked;
            spineSkeletonAnimation.Skeleton.SetSlotsToSetupPose();
            spineSkeletonAnimation.AnimationState.Apply(spineSkeletonAnimation.Skeleton);

            // 清理未使用的资源。性能开销较大，建议仅在必要时调用。
            AtlasUtilities.ClearCache();
            Resources.UnloadUnusedAssets();
        }
        #endregion
        
        #region 定义-状态与动画数据
        /// <summary>
        /// 游戏角色 状态数据
        /// </summary>
        [Serializable]
        public struct FSpineStateData
        {
            [Tooltip("状态名称")]
            public string stateName;
            [Tooltip("Spine 动画组件")]
            public SkeletonAnimation spineSkeletonAnimation;
            [Tooltip("Spine 动画数据 列表")]
            public SpineAnimData[] spineAnimDatas;
        }
    
        /// <summary>
        /// 游戏角色 动画数据
        /// </summary>
        [Serializable]
        public class SpineAnimData
        {
            [Tooltip("动画引用资源：Spine动画资产引用。")]
            public AnimationReferenceAsset animRefAsset;
            [Tooltip("动画轨道：用于不同类型的动画的区分。不同轨道的动画 可以同时播放。")]
            [SerializeField] private EAnimTrack animTrack;
            [Tooltip("动画子轨道：用于同一类型动画的区分。不同子轨道的动画 可以同时播放。"), Range(0, 9)]
            [SerializeField] private int animTrackSub;
            [Tooltip("是否循环播放")]
            public bool isLoop;
            [Tooltip("是否反转播放。反转时，动画将从 结束位置开始，并反方向播放。")]
            public bool isReverse;
            [Tooltip("播放速度倍率。默认为 1.0。"), Range(0f, 5f)]
            public float speed;

            [Tooltip("开始播放的延迟时间（秒）：动画将在指定的延迟后开始播放。")]
            public float startDelayTime;
            [Tooltip("循环间隔时间（秒）：仅当动画为 循环播放 时有效。动画播放完成后，在设定的范围内 随机一次 间隔时间。")]
            public Vector2 loopIntervalTimeRange;

            /// <summary>
            /// 动画轨道 默认：主轨道 * 10 + 子轨道，主轨道间隔10，子轨道0-9，保证不同 子轨道的动画 可以同时播放。
            /// </summary>
            public int AnimTrack
            {
                get
                {
                    if (_animTrack > 0)
                        return _animTrack;
                    else
                        return (int)animTrack * 10 + animTrackSub;
                }
            }
            // 动画轨道。构造函数时，由外部传参进行设置。
            private int _animTrack = -1;
            
            /// <summary>
            /// 构造函数：初始化动画数据
            /// </summary>
            public SpineAnimData()
            {
                animRefAsset = null;
                animTrack = EAnimTrack.None;
                animTrackSub = 0;
                isLoop = false;
                isReverse = false;
                speed = 1.0f;
                startDelayTime = 0f;
                loopIntervalTimeRange = Vector2.zero;
            }

            /// <summary>
            /// 构造函数：初始化动画数据
            /// </summary>
            /// <param name="animRefAsset">动画引用资源：Spine动画资产引用。</param>
            /// <param name="animTrack">动画轨道：用于不同类型的动画的区分。不同轨道的动画 可以同时播放。</param>
            /// <param name="isLoop">是否循环播放</param>
            /// <param name="isReverse">是否反转播放。反转时，动画将从 结束位置开始，并反方向播放。</param>
            /// <param name="speed">播放速度倍率。默认为 1.0。</param>
            /// <param name="startDelayTime">开始播放的延迟时间（秒）：动画将在指定的延迟后开始播放。</param>
            /// <param name="loopIntervalTimeRange">循环间隔时间（秒）：仅当动画为 循环播放 时有效。动画播放完成后，在设定的范围内 随机一次 间隔时间。</param>
            public SpineAnimData
            (
                AnimationReferenceAsset animRefAsset,
                int animTrack = 0,
                bool isLoop = false, 
                bool isReverse = false, 
                float speed = 1f,
                float startDelayTime = 0f,
                Vector2 loopIntervalTimeRange = default
            )
            {
                // 设置动画数据
                this.animRefAsset = animRefAsset;
                this._animTrack = animTrack;
                this.isLoop = isLoop;
                this.isReverse = isReverse;
                this.speed = speed;
                this.startDelayTime = startDelayTime;
                this.loopIntervalTimeRange = loopIntervalTimeRange;
            }
        }
        #endregion
#endif
    }
}