using System;
using Ale.Toolkit.Runtime;
using UnityEngine;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动作模拟器 系统配置
    /// </summary>
    [CreateAssetMenu(fileName = "AnimSimulatorConfig", menuName = "Ale/AnimSimulator System/AnimSimulator Config")]
    public class AnimSimulatorConfig : ScriptableObject
    {
        [Header("背景")]
        [Tooltip("背景资产 的 Addressable 地址前缀：加载时按 『前缀 + 背景名 + .prefab』 组合地址。\n" +
                 "默认值对应演示样例的登记地址（欢迎窗口的「登记样例到 Addressables」按钮会把样例文件夹登记为 AnimSimulator）。\n" +
                 "正式项目请按自己资产的 Addressable 地址填写。")]
        public string backgroundAddressableFolder = "AnimSimulator/Assets/Backgrounds/";

        [Header("角色")]
        [Tooltip("角色资产 的 Addressable 地址前缀：加载时按 『前缀 + 角色名 + .prefab』 组合地址。\n" +
                 "默认值对应演示样例的登记地址（欢迎窗口的「登记样例到 Addressables」按钮会把样例文件夹登记为 AnimSimulator）。\n" +
                 "正式项目请按自己资产的 Addressable 地址填写。")]
        public string actorAddressableFolder = "AnimSimulator/Assets/Actors/";
        
        [Header("动画动作播放器 配置")]
        [Tooltip("动画动作播放器 UI列表：用于在 动画角色 身上的各个 动画动作播放器 的位置，显示动画动作的 UI列表。")]
        public UIAnimActionList uiAnimActionListPrefab;
        
        [Header("动画角色皮肤组 配置")]
        [Tooltip("动画角色皮肤组 UI列表：用于将 动画角色 上配置的 皮肤组，显示在UI列表中 供用户选择。")]
        public UIAnimActorSkinGroupList uiAnimActorSkinGroupListPrefab;
        
        [Header("进度条 配置")]
        [Tooltip("进度条UI视口 预制体：用于在UI画布中 显示进度条。")]
        public UIProgressBarView uiProgressBarViewPrefab;
        
        [Header("进度条 配置 - 等级进度条")]
        [Tooltip("等级进度条 默认UI预制体：用于在UI画布中 显示等级进度条。")]
        public UILevelProgressBar uiLevelProgressBarDefault;
        [Tooltip("等级经验曲线 默认配置：当等级进度条 未指定经验曲线配置时，使用此默认配置。")]
        public ExpCurveConfig[] levelExpCurveConfigsDefault;
        [Tooltip("等级进度条 配置组")]
        public LevelProgressBarConfig[] levelProgressBarConfigs;
        
        [Header("进度条 配置 - 动作进度条")]
        [Tooltip("动作进度条 默认UI预制体：用于在UI画布中 显示动作进度条。")]
        public UIActionProgressBar uiActionProgressBarDefault;
        [Tooltip("动作进度条 配置组")]
        public ActionProgressBarConfig[] actionProgressBarConfigs;
        
        #region 功能接口
        /// <summary>
        /// 获取 指定等级 所需的经验值
        /// </summary>
        /// <param name="levelProgressBarConfig">等级进度条配置</param>
        /// <param name="levelCurrent">当前等级。</param>
        /// <returns>升级到 下一级 所需的经验值。</returns>
        public int GetExpForLevel(LevelProgressBarConfig levelProgressBarConfig, int levelCurrent)
        {
            if (levelProgressBarConfig == null)
            {
                AnimSimLog.Warn(this, "传入的 LevelProgressBarConfig 为空，返回0。");
                return 0;
            }
            
            // 获取 经验曲线配置组
            var expCurveConfigs = levelProgressBarConfig.expCurveConfigs;
            if (expCurveConfigs == null || expCurveConfigs.Length == 0)
            {
                // 使用默认配置
                expCurveConfigs = levelExpCurveConfigsDefault;
            }
            // 如果没有配置，返回0并警告
            if (expCurveConfigs == null || expCurveConfigs.Length == 0)
            {
                AnimSimLog.Warn(this, "未配置 ExpCurveConfigs，返回0。");
                return 0;
            }
            
            // 等级不能小于0
            if (levelCurrent < 0)
            {
                AnimSimLog.Warn(this, $"参数等级不能小于0，已自动修正为0。");
                levelCurrent = 0;
            }
            // 计算 下一级 等级
            int levelNext = levelCurrent + 1;
            // 选择适用的 ExpCurveConfig：选择 LevelRangeStart <= levelNext 中最大的一个
            int selectedIndex = -1;
            int bestLevelStart = int.MinValue;
            for (int i = 0; i < expCurveConfigs.Length; i++)
            {
                var cfg = expCurveConfigs[i];
                if (levelNext >= cfg.levelRangeStart && cfg.levelRangeStart > bestLevelStart)
                {
                    bestLevelStart = cfg.levelRangeStart;
                    selectedIndex = i;
                }
            }
            // 如果没有找到（所有 LevelRangeStart 都大于 levelNext），则回退到最小的 LevelRangeStart
            if (selectedIndex == -1)
            {
                int minStart = int.MaxValue;
                for (int i = 0; i < expCurveConfigs.Length; i++)
                {
                    var cfg = expCurveConfigs[i];
                    if (cfg.levelRangeStart < minStart)
                    {
                        minStart = cfg.levelRangeStart;
                        selectedIndex = i;
                    }
                }
            }
            
            // 计算 所需经验值
            var expCurveConfig = expCurveConfigs[selectedIndex]; // 选中的 经验曲线配置 
            long expRequired; // 所需经验值
            switch (expCurveConfig.expCurveType)
            {
                // 线性
                case ExpCurveConfig.EExpCurveType.Linear:
                    expRequired = (long)(expCurveConfig.expBase + levelNext * expCurveConfig.expGrowthRate);
                    break;
                // 指数
                case ExpCurveConfig.EExpCurveType.Exponent:
                    expRequired = (long)(expCurveConfig.expBase * Mathf.Pow(levelNext, expCurveConfig.expGrowthRate));
                    break;
                default:
                    expRequired = expCurveConfig.expBase;
                    break;
            }
            // 限制最小值与最大值
            if (expRequired < 0) expRequired = expCurveConfig.expBase;
            if (expRequired > int.MaxValue) return int.MaxValue;
            
            return (int)expRequired;
        }
        #endregion
    }
    
    #region 进度条 配置
    /// <summary>
    /// 进度条 配置。基类
    /// </summary>
    [Serializable]
    public class ProgressBarConfig
    {
        [Header("基础设置")]
        [Tooltip("进度条 名称：在配置中使用的唯一标识。")]
        public string progressName;
#if UNITY_EDITOR
        [Tooltip("备注：仅用于编辑器查看。")]
        public string comment;
#endif
        
        [Header("UI设置")]
        [Tooltip("UI画布中的 分组名称：会将 进度条 添加到指定 分组下。若为空，则不显示UI的进度条，但会保留 进度条功能。")]
        public string uiGroupName;
        
        [Tooltip("UI中显示的名称：填纯文本；启用本地化后还可另选多语言条目，取不到时回退到纯文本。")]
        public TextValue uiDisplayName = new TextValue();

        // ── 由子类落实的两件事 ──────────────────────────────────────────────────────
        // 「用哪个预制体」与「怎么把配置写进实例」是等级条与动作条仅有的差异。把它们收在这里，
        // 管理器那边两段除了类型参数几乎逐字相同的实例化循环就能合成一个。

        /// <summary>
        /// 解析本条配置实际要用的进度条预制体：自身指定的优先，未指定则回退到全局默认。
        /// 两者都为空时返回 <c>null</c>。
        /// </summary>
        public virtual UIBaseProgressBar ResolvePrefab(AnimSimulatorConfig config) => null;

        /// <summary>
        /// 把本配置写入已实例化的进度条。
        /// </summary>
        /// <returns>实例类型与本配置是否匹配。<c>false</c> 表示配错了预制体，初始值未写入。</returns>
        public virtual bool ApplyTo(UIBaseProgressBar progressBar) => false;
    }

    #region 等级进度条
    /// <summary>
    /// 等级进度条 配置
    /// </summary>
    [Serializable]
    public class LevelProgressBarConfig : ProgressBarConfig
    {
        [Header("等级设置")]
        [Tooltip("等级进度条 UI 预制体：若为空，则使用默认 等级进度条预制体。")]
        public UILevelProgressBar uiLevelProgressBar;
        [Tooltip("最大等级：进度条达到该等级时，视为满级。")]
        public int levelMax = 99;
        
        [Tooltip("经验曲线配置组：根据等级范围，使用不同的经验曲线配置。")]
        public ExpCurveConfig[] expCurveConfigs;

        /// <inheritdoc/>
        public override UIBaseProgressBar ResolvePrefab(AnimSimulatorConfig config)
            => uiLevelProgressBar ? uiLevelProgressBar : (config ? config.uiLevelProgressBarDefault : null);

        /// <inheritdoc/>
        public override bool ApplyTo(UIBaseProgressBar progressBar)
        {
            if (!(progressBar is UILevelProgressBar levelProgressBar)) return false;
            levelProgressBar.SetInfo(this, 0, 0);
            return true;
        }
    }

    /// <summary>
    /// 经验曲线 配置
    /// </summary>
    [Serializable]
    public class ExpCurveConfig
    {
        [Tooltip("等级范围起始值：大于等于该值时，使用此配置。")] 
        public int levelRangeStart;
        
        [Tooltip("等级经验曲线类型：决定每一级所需经验的计算方式。" +
                 "[Linear 线性：所需经验 = 经验基础值 + (等级 * 经验增长率)] " +
                 "[Exponential 指数：所需经验 = 经验基础值 * (等级 ^ 经验增长率)。]")]
        public EExpCurveType expCurveType = EExpCurveType.Linear;
            
        [Tooltip("经验基础值：每一级所需经验的基础值。")]
        public int expBase = 100;
        [Tooltip("经验增长率：根据曲线类型 与当前等级，调整每一级 所需经验的增长量。")]
        public float expGrowthRate = 20f;
        
        /// <summary>
        /// 经验曲线 类型
        /// </summary>
        [Serializable]
        public enum EExpCurveType
        {
            /// <summary>
            /// 线性。
            /// 所需经验 = 经验基础值 + (等级 * 经验增长率)。
            /// </summary>
            Linear,
            
            /// <summary>
            /// 指数。
            /// 所需经验 = 经验基础值 * (等级 ^ 经验指数率)。
            /// </summary>
            Exponent
        }
    }
    #endregion

    #region 动作进度条
    /// <summary>
    /// 动作进度条 配置
    /// </summary>
    [Serializable]
    public class ActionProgressBarConfig : ProgressBarConfig
    {
        [Header("动作设置")]
        [Tooltip("动作进度条 UI 预制体：若为空，则使用默认 动作进度条预制体。")]
        public UIActionProgressBar uiActionProgressBar;
        [Tooltip("进度最大值：当前进度值达到该值后，进度条将视为满值，进度达到100%。")]
        public float progressValueMax = 100;
        [Tooltip("进度限制值：进度条的限制值。获取的进度值总和达到该值后，进度条将不再增加。")]
        public float progressValueLimit = 300;
        
        [Tooltip("动作播放 配置组")]
        public ActionPlayConfig[] actionPlayConfigs;

        /// <inheritdoc/>
        public override UIBaseProgressBar ResolvePrefab(AnimSimulatorConfig config)
            => uiActionProgressBar ? uiActionProgressBar : (config ? config.uiActionProgressBarDefault : null);

        /// <inheritdoc/>
        public override bool ApplyTo(UIBaseProgressBar progressBar)
        {
            if (!(progressBar is UIActionProgressBar actionProgressBar)) return false;
            actionProgressBar.SetInfo(this, 0);
            return true;
        }
    }
    
    /// <summary>
    /// 动作播放 配置
    /// </summary>
    [Serializable]
    public class ActionPlayConfig
    {
        [Tooltip("动画动作播放器 名称：对指定的 动画动作播放器 进行操作。")]
        public string actionPlayerName;
        [Tooltip("动作播放类型：动作的播放方式。[Auto 自动 / Manual 手动]。")]
        public EAnimActionPlayType animActionPlayType = EAnimActionPlayType.Auto;
        [Tooltip("动作播放器类型：动作的播放选择方式。[Select选择 / Order顺序 / Random随机]。")]
        public EAnimActionSelectType animActionSelectType = EAnimActionSelectType.Random;
        [Tooltip("动作播放次数：最多可播放的动作次数，0表示不限制次数。达到该次数后，即使进度值满足要求，也无法播放动作。")]
        public int actionPlayCount;
        
        [Tooltip("进度值 要求：达到该进度值，可以播放动作。")]
        public float progressValueRequired = 60;
        [Tooltip("进度值 消耗：播放动作后，减少指定的进度值。")]
        public float progressValueConsumed = 50;
    }
    #endregion
    #endregion
}