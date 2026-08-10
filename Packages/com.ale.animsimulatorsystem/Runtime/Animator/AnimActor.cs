using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if ATK_LOCALIZATION
using UnityEngine.Localization;
#endif

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画角色。
    /// 不一定要是人形角色，可以是 洗衣机、摇摆的植物等 任何可执行动画 的对象。
    /// </summary>
    public class AnimActor : MonoBehaviour
    {
        [Header("动画设置")]
        // 面向基类编程：挂 SpineAnimator 还是 Live2dAnimator 由角色预制体决定，本类对后端无感。
        // FormerlySerializedAs 保住旧字段名 spineAnimator 上已配置的引用。
        [FormerlySerializedAs("spineAnimator")]
        [Tooltip("动画控制器：Spine Animator 或 Live2D Animator。留空时自动从自身或子物体查找。")]
        [SerializeField] private AnimatorBase animator;
        [Tooltip("初始状态列表：根据 动画制作时的 状态名称 设置，填写一个或多个 状态名称。")]
        [SerializeField] private string[] stateInitList = new string[] { "idle" };
        
        /// <summary>
        /// 初始化完成 回调
        /// </summary>
        public Action<AnimActor> OnInitComplete
        {
            get => _onInitComplete;
            set
            {
                _onInitComplete = value;
                // 如果已经完成初始化，则立即调用回调
                if (_isInitComplete && _onInitComplete != null)
                {
                    _onInitComplete.Invoke(this);
                }
            }
        }
        private Action<AnimActor> _onInitComplete;
        
        // 是否完成初始化
        private bool _isInitComplete;
        
#if UNITY_EDITOR
        private void Reset()
        {
            // 从自身或子物体上获取动画控制器。多态查找天然认得两种后端，无需按宏分支。
            if (!animator)
            {
                animator = GetComponent<AnimatorBase>();
                if (!animator)
                    animator = GetComponentInChildren<AnimatorBase>();
            }
        }
#endif

        private void Start()
        {
            // 初始化 皮肤
            InitSkin();

            // 设置 初始状态
            if (animator)
                animator.SwitchAnimStateArray(stateInitList);

            _isInitComplete = true;
            // 调用 初始化完成 回调
            OnInitComplete?.Invoke(this);
        }

        #region 淡入淡出
        /// <summary>
        /// 淡入（恢复显示）。
        /// 有动画控制器则淡入其渲染器；否则直接激活对象。
        /// </summary>
        public void FadeIn()
        {
            if (animator)
            {
                // 淡入 动画渲染器
                animator.FadeAnimator(true);
                return;
            }
            // 无淡入淡出，直接激活对象
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 淡出（临时隐藏，不销毁）。
        /// 有动画控制器则淡出并禁用，同时保留动画数据以便 FadeIn() 恢复；
        /// 否则直接非激活对象。
        /// </summary>
        public void FadeOut()
        {
            if (animator)
            {
                // clearAnimOnFadeOut=false：临时隐藏，仅禁用对象，保留动画数据
                animator.FadeAnimator(false, null, clearAnimOnFadeOut: false);
                return;
            }
            // 无淡入淡出，直接非激活对象
            gameObject.SetActive(false);
        }
        #endregion
        
        #region 存档与加载
        /// <summary>
        /// 加载 数据
        /// </summary>
        /// <param name="animActorSaveData">存档数据</param>
        public void LoadData(AnimActorSaveData animActorSaveData)
        {
            // 加载 皮肤组:已选择皮肤 映射表
            foreach (var skinGroupToSelectedSkinNameList in animActorSaveData.SkinGroupToSelectedSkinMap)
            {
                // 查找 皮肤组
                var skinGroupName = skinGroupToSelectedSkinNameList.Key;
                var animActorSkinGroup = Array.Find(animActorSkinGroups, 
                    group => group.skinGroupName == skinGroupName);
                if (animActorSkinGroup == null) continue;
                
                // 初始化 皮肤组:已选择皮肤 队列
                _skinGroupToSelectedSkinListMap[animActorSkinGroup] = new List<AnimActorSkin>();
                // 加载 已选择皮肤 列表
                var selectedSkinNameList = skinGroupToSelectedSkinNameList.Value;
                foreach (var selectedSkinName in selectedSkinNameList)
                {
                    // 查找 角色皮肤
                    var animActorSkin = Array.Find(animActorSkinGroup.animActorSkins, 
                        skin => skin.skinName == selectedSkinName);
                    if (animActorSkin == null) continue;
                    
                    // 添加到 已选择皮肤 队列
                    _skinGroupToSelectedSkinListMap[animActorSkinGroup].Add(animActorSkin);
                    
                    // 添加 皮肤。等待初始化后 刷新。
                    AddSkin(animActorSkinGroup, animActorSkin, false);
                }
            }
        }
        
        /// <summary>
        /// 保存 数据
        /// </summary>
        public AnimActorSaveData SaveData()
        {
            AnimActorSaveData animActorSaveData = new AnimActorSaveData();
            
            // 保存 皮肤组:已选择皮肤名称 列表 映射表
            Dictionary<string, string[]> skinGroupToSelectedSkinListNameMap = new Dictionary<string, string[]>();
            foreach (var skinGroupToSelectedSkin in _skinGroupToSelectedSkinListMap)
            {
                // 皮肤组 名称
                var animActorSkinGroupName = skinGroupToSelectedSkin.Key;
                // 记录 皮肤组:已选择皮肤名称 列表
                var selectedSkinNameList = skinGroupToSelectedSkin.Value;
                skinGroupToSelectedSkinListNameMap[animActorSkinGroupName.skinGroupName] = 
                    Array.ConvertAll(selectedSkinNameList.ToArray(), skin => skin.skinName);
            }
            animActorSaveData.SkinGroupToSelectedSkinMap = skinGroupToSelectedSkinListNameMap;

            return animActorSaveData;
        }
        #endregion

        #region 皮肤管理
        [Header("皮肤设置")]
        // 皮肤名对两个后端使用相同的命名规则，故不再按宏区分；
        // 下拉选择由包内 Editor 程序集的皮肤名 Drawer 按角色实际后端提供。
        [Tooltip("基础皮肤列表：始终显示的 基础皮肤名称列表。")]
        [AnimSkinName] [SerializeField] private string[] baseSkins;
        [Tooltip("皮肤组 列表：用于定义角色 可切换的皮肤组。")]
        [SerializeField] private AnimActorSkinGroup[] animActorSkinGroups;
        
        /// <summary>
        /// 皮肤组 列表
        /// </summary>
        public AnimActorSkinGroup[] AnimActorSkinGroups => animActorSkinGroups;
        
        /// <summary>
        /// 添加或移除 皮肤 回调。参数：是否添加、皮肤组、角色皮肤
        /// </summary>
        public Action<AnimActorSkinGroup, AnimActorSkin, bool> OnSkinAddOrRemove;
        
        // 皮肤组:已选择皮肤 映射表
        private Dictionary<AnimActorSkinGroup, List<AnimActorSkin>> _skinGroupToSelectedSkinListMap = 
            new Dictionary<AnimActorSkinGroup, List<AnimActorSkin>>();
        
        /// <summary>
        /// 初始化 皮肤
        /// </summary>
        private void InitSkin()
        {
            // 初始化 皮肤组:已选择皮肤 映射表
            if (animActorSkinGroups != null && animActorSkinGroups.Length > 0)
            {
                foreach (var animActorSkinGroup in animActorSkinGroups)
                {
                    // 初始化 皮肤组:已选择皮肤 队列
                    List<AnimActorSkin> selectedSkinList;
                    if (_skinGroupToSelectedSkinListMap.TryGetValue(animActorSkinGroup, out selectedSkinList) == false)
                    {
                        selectedSkinList = new List<AnimActorSkin>();
                        _skinGroupToSelectedSkinListMap[animActorSkinGroup] = selectedSkinList;
                    }
                    
                    // 检查 是否必须选择皮肤，且 当前未选择任何皮肤
                    if (animActorSkinGroup.isMustSelectSkin &&
                        selectedSkinList.Count == 0 &&
                        animActorSkinGroup.animActorSkins != null &&
                        animActorSkinGroup.animActorSkins.Length > 0)
                    {
                        // 获取 默认皮肤：序号从1开始，钳制到有效范围 [1, Length]，避免 0/负数/越界
                        int defaultSkinIndex = Mathf.Clamp(animActorSkinGroup.defaultSkinNumber, 1, animActorSkinGroup.animActorSkins.Length) - 1;
                        AnimActorSkin defaultSkin = animActorSkinGroup.animActorSkins[defaultSkinIndex];
                        // 添加 皮肤
                        AddSkin(animActorSkinGroup, defaultSkin, false);
                    }
                }
            }
            
            // 设置 基础皮肤
            if (animator)
            {
                // 设置基础皮肤
                if (baseSkins != null && baseSkins.Length > 0)
                {
                    animator.SetBaseSkin(baseSkins, false);
                }

                // 刷新 皮肤
                animator.RefreshSkin();
                // 所有皮肤添加完成后，可重新打包皮肤（Spine 专有优化）：
                // (animator as SpineAnimator)?.RepackedSkin();
            }
        }
        
        /// <summary>
        /// 检查 皮肤是否已选择
        /// </summary>
        /// <param name="animActorSkinGroup"></param>
        /// <param name="animActorSkin"></param>
        /// <returns></returns>
        public bool CheckIsSelectedSkin(AnimActorSkinGroup animActorSkinGroup, AnimActorSkin animActorSkin)
        {
            // 获取 皮肤组:已选择皮肤 列表
            if (_skinGroupToSelectedSkinListMap.TryGetValue(animActorSkinGroup, out var selectedSkinList) == false)
                return false;

            // 检查 皮肤是否已选择
            return selectedSkinList.Contains(animActorSkin);
        }
        
        /// <summary>
        /// 添加 皮肤
        /// </summary>
        /// <param name="animActorSkinGroup">添加到的皮肤组</param>
        /// <param name="animActorSkin">添加的角色皮肤</param>
        /// <param name="isRefresh">是否 立刻刷新</param>
        /// <returns>是否 成功添加</returns>
        public bool AddSkin(AnimActorSkinGroup animActorSkinGroup, AnimActorSkin animActorSkin, bool isRefresh = true)
        {
            // 获取 皮肤组:已选择皮肤 列表（不存在则懒创建，避免外部以未初始化的皮肤组调用时抛 KeyNotFoundException）
            if (!_skinGroupToSelectedSkinListMap.TryGetValue(animActorSkinGroup, out var selectedSkinList))
            {
                selectedSkinList = new List<AnimActorSkin>();
                _skinGroupToSelectedSkinListMap[animActorSkinGroup] = selectedSkinList;
            }
            // 检查 皮肤是否 未选择
            if (selectedSkinList.Contains(animActorSkin)) return false;
            
            // 检查 是否超过 皮肤选择数量上限
            if (animActorSkinGroup.skinSelectCountMax > 0)
            {
                // 超过上限时，移除 最早选择的 皮肤
                if (selectedSkinList.Count >= animActorSkinGroup.skinSelectCountMax)
                {
                    // 移除 最早选择的 皮肤
                    var removedSkin = selectedSkinList[0];
                    // 移除 动画播放器中的 皮肤。等待操作全部完成后 刷新。
                    RemoveSkin(animActorSkinGroup, removedSkin, false, true);
                }
            }
            
            // 记录到 皮肤组:已选择皮肤 列表
            selectedSkinList.Add(animActorSkin);
            
            // 添加皮肤到 动画播放器
            if (animator)
                animator.AddSkin(animActorSkin.skinName, isRefresh);
            // 调用 添加皮肤 回调
            OnSkinAddOrRemove?.Invoke(animActorSkinGroup, animActorSkin, true);
            
            return true;
        }

        /// <summary>
        /// 移除 皮肤
        /// </summary>
        /// <param name="animActorSkinGroup">添加到的皮肤组</param>
        /// <param name="animActorSkin">添加的角色皮肤</param>
        /// <param name="isRefresh">是否 立刻刷新</param>
        /// <param name="isForce">是否 强制移除</param>
        public bool RemoveSkin
        (
            AnimActorSkinGroup animActorSkinGroup, 
            AnimActorSkin animActorSkin, 
            bool isRefresh = true, 
            bool isForce = false
        )
        {
            // 获取 皮肤组:已选择皮肤 列表。不存在则无可移除。
            if (!_skinGroupToSelectedSkinListMap.TryGetValue(animActorSkinGroup, out var selectedSkinList)) return false;
            // 检查 皮肤是否 已选择
            if (selectedSkinList.Contains(animActorSkin) == false) return false;
            
            // 检查 是否必须选择皮肤，且 当前仅选择了该皮肤
            if (isForce == false &&
                animActorSkinGroup.isMustSelectSkin &&
                selectedSkinList.Count == 1 &&
                selectedSkinList[0] == animActorSkin)
            {
                // 不允许移除
                return false;
            }
            
            // 从 皮肤组:已选择皮肤 列表 移除
            selectedSkinList.Remove(animActorSkin);
            
            // 从 动画播放器中 移除皮肤
            if (animator)
                animator.RemoveSkin(animActorSkin.skinName, isRefresh);
            // 调用 移除皮肤 回调
            OnSkinAddOrRemove?.Invoke(animActorSkinGroup, animActorSkin, false);
            
            return true;
        }
        #endregion
    }
    
    #region 类定义-角色皮肤
    /// <summary>
    /// 角色皮肤组
    /// </summary>
    [Serializable]
    public class AnimActorSkinGroup
    {
        [Tooltip("皮肤组 名称")]
        public string skinGroupName;
        [Tooltip("皮肤组 图标:页签上显示的图标。例如，衣服、裤子、饰品等皮肤组的图标。")]
        public Sprite skinGroupIcon;
        [Tooltip("皮肤选择 最大数量：同时可应用的皮肤 最大数量，0则不限制。例如，饰品的皮肤组，可以同时选择多个饰品皮肤。")] 
        public int skinSelectCountMax = 1;
        [Tooltip("必须选择皮肤：是否 必须选择 至少一个皮肤。例如，眼睛的皮肤组，必须选择一个眼睛皮肤。")]
        public bool isMustSelectSkin;
        [Tooltip("默认皮肤序号：在必须选择皮肤时，默认选择的皮肤序号。从1开始计数。")]
        public int defaultSkinNumber = 1;
        [Tooltip("皮肤列表")]
        public AnimActorSkin[] animActorSkins;
    }
    
    /// <summary>
    /// 角色皮肤 数据定义
    /// </summary>
    [Serializable]
    public class AnimActorSkin
    {
        // 皮肤名对 Spine 与 Live2D 使用相同的命名规则；下拉选择由 Editor 程序集的 Drawer 按后端提供。
        [Tooltip("皮肤: 在动画软件中制作时的名称，用于指定皮肤。有文件夹路径时，一般使用 '/' 进行分隔。")]
        [AnimSkinName] public string skinName;
#if ATK_LOCALIZATION
        [Tooltip("UI中显示的皮肤名称：多语言Key。")]
        public LocalizedString uiDisplaySkinName;
#else
        [Tooltip("UI中显示的皮肤名称")]
        public string uiDisplaySkinName;
#endif
        [Tooltip("皮肤图片")]
        public Sprite skinImage;
    }
    #endregion
    
    #region 类定义-角色数据
    /// <summary>
    /// 角色 存档数据
    /// </summary>
    public class AnimActorSaveData
    {
        /// <summary>
        /// 皮肤组名称:已选择皮肤名称 映射表
        /// </summary>
        public Dictionary<string, string[]> SkinGroupToSelectedSkinMap =
            new Dictionary<string, string[]>();
    }
    #endregion

    #region 枚举定义-动画轨道
    /// <summary>
    /// 动画轨道。
    /// 定义了常见的动画轨道类型，例如身体、头部、面部等。便于在 配置时进行 分类与管理。
    /// 分配到不同的 动画轨道 上的动画，可以同时进行播放。
    /// 需要 指定位置的动画 被 其他动画 覆盖时，则将这些动画 分配到 相同的动画轨道上。
    /// </summary>
    [Serializable]
    public enum EAnimTrack
    {
        /// <summary>
        /// 无。未指定动画轨道，或不需要区分动画轨道的情况。
        /// </summary>
        None = 0,

        /// <summary>
        /// 身体。整体的基础动画，例如 站立、走路、跑步等 循环播放的动画。
        /// </summary>
        Body,
        /// <summary>
        /// 头部。例如 点头、摇头等动画。
        /// </summary>
        Head,
        /// <summary>
        /// 面部。例如 面部表情的切换等动画。
        /// </summary>
        Face,
        /// <summary>
        /// 眼睛。例如 眨眼、看向不同方向等动画。
        /// </summary>
        Eyes,
        /// <summary>
        /// 嘴巴。例如 说话时的 张合动画。
        /// </summary>
        Mouth,
        /// <summary>
        /// 眉毛。例如 眉毛的上扬、下垂等动画。
        /// </summary>
        Brows,
        /// <summary>
        /// 鼻子。例如 鼻子的缩放、晃动等动画。
        /// </summary>
        Nose,
        /// <summary>
        /// 耳朵。例如 兽人的大耳朵 随机间隔的 摆动动画。
        /// </summary>
        Ears,
        /// <summary>
        /// 头发。例如 头发飘动、被风吹起等 动画。
        /// </summary>
        Hair,
        /// <summary>
        /// 手臂。例如 手臂的挥动、摆动等动画。
        /// </summary>
        Arms,
        /// <summary>
        /// 腿部。例如 站立、走路、跑步等 动画。
        /// </summary>
        Legs,
        /// <summary>
        /// 胸部。例如 胸部的起伏、晃动等动画。
        /// </summary>
        Breast,
        /// <summary>
        /// 腰部。例如 腰部的扭动、摆动等动画。
        /// </summary>
        Waist,
        /// <summary>
        /// 腹部。例如 腹部的起伏、晃动等动画。
        /// </summary>
        Belly,
        /// <summary>
        /// 臀部。例如 臀部的晃动、摆动等动画。
        /// </summary>
        Buttock,
        /// <summary>
        /// 背部。例如 翅膀、披风等，随机间隔的摆动动画。
        /// </summary>
        Back,
        /// <summary>
        /// 尾巴。例如 尾巴的摆动、卷曲等动画。
        /// </summary>
        Tail,
        /// <summary>
        /// 配件。例如 发饰、帽子、耳坠 等，随机间隔的摆动动画。
        /// </summary>
        Parts,
        
        /// <summary>
        /// 动作。一般用于整体、复杂的复合动作，可能会覆盖到 之前分配的 其他轨道的动画。
        /// 例如，惊吓、攻击等动作，可能会覆盖到 之前分配在 身体、头部、面部等轨道的动画。
        /// </summary>
        Action = 900,
        
        /// <summary>
        /// 其他。不易分类 或 无需区分的动画。
        /// </summary>
        Other = 999,
    }
    #endregion
}