using System;
using UnityEngine;
using TMPro;

namespace Fs.GameFramework.Gameplay.AnimSimulatorSystem
{
    /// <summary>
    /// 等级进度条 UI
    /// </summary>
    public class UILevelProgressBar : UIBaseProgressBar
    {
        [Header("等级进度条")]
        [Tooltip("文字 等级数字")]
        [SerializeField] private TextMeshProUGUI txtLevelNumber;
        
        // 当前使用的 等级进度条 配置
        private LevelProgressBarConfig _levelProgressBarConfig;
        
        /// <summary>
        /// 当前 等级数
        /// </summary>
        public int LevelNumber => _levelNumber;
        private int _levelNumber = -1;
        
        /// <summary>
        /// 事件 等级数字 变更
        /// </summary>
        public event Action<UILevelProgressBar, int> OnLevelNumberChanged;
        
        /// <summary>
        /// 设置信息
        /// </summary>
        /// <param name="levelProgressBarConfigNew">等级进度条 配置</param>
        /// <param name="levelNumber">当前 等级数</param>
        /// <param name="expCurrent">当前 经验值</param>
        public void SetInfo(LevelProgressBarConfig levelProgressBarConfigNew, int levelNumber,  int expCurrent)
        {
            if (levelProgressBarConfigNew == null)
            {
                Debug.LogWarning("[LevelProgressBar] SetInfo: 传入的 LevelProgressBarConfig 为空，请检查调用。");
                return;
            }
            
            // 设置 基类信息
            SetInfo(levelProgressBarConfigNew);
            
            // 记录 配置
            _levelProgressBarConfig = levelProgressBarConfigNew;
            if (_levelProgressBarConfig == null)
            {
                SetLevelNumber(0);
                SetProgressPercent(0f, true);
                Debug.LogWarning("[LevelProgressBar] SetInfo: 传入的 LevelProgressBarConfig 为空，请检查调用。");
                return;
            }
            
            // 设置 等级数字
            SetLevelNumber(levelNumber);

            // 升级曲线由管理器提供，没有管理器就算不出升级所需经验值。
            var manager = AnimSimulatorManager.Instance;
            if (!manager)
            {
                Debug.LogWarning("[LevelProgressBar] SetInfo: 场景中没有 AnimSimulatorManager，无法取得升级所需经验值。");
                return;
            }

            // 获取 升级所需经验值
            int expRequire = manager.GetExpRequireForLevel(_levelProgressBarConfig, levelNumber);
            // 设置 当前经验值 与 最大经验值
            InitProgressValue(expCurrent, expRequire);
        }
        
        /// <summary>
        /// 设置 等级数
        /// </summary>
        /// <param name="levelNumber"></param>
        private void SetLevelNumber(int levelNumber = -1)
        {
            // 设置 等级数字
            if (levelNumber >= 0 && levelNumber != _levelNumber)
            {
                // 更新 等级数字
                _levelNumber = levelNumber;
                // 刷新 等级数字UI显示
                if (txtLevelNumber != null)
                    txtLevelNumber.text = _levelNumber.ToString();
                // 触发 等级数字 变更 事件
                OnLevelNumberChanged?.Invoke(this, _levelNumber);
            }
        }
        
        /// <summary>
        /// 进度值 被修改
        /// </summary>
        /// <param name="expCurrent">当前 经验值</param>
        /// <param name="expRequire">最大 经验值</param>
        protected override void OnProgressValueModified(float expCurrent, float expRequire)
        {
            base.OnProgressValueModified(expCurrent, expRequire);

            // 未达到升级所需经验，直接返回（进度条已由基类更新）
            if (expCurrent < expRequire) return;

            // 到这里说明确实要结算升级，而升级曲线由管理器提供。在循环外取一次并判空——
            // 若放进循环里退化为 expRequire = 0，会被下方的「所需经验不可小于等于 0」误报为配置错误。
            var manager = AnimSimulatorManager.Instance;
            if (!manager)
            {
                Debug.LogWarning("[LevelProgressBar] OnProgressValueModified: 场景中没有 AnimSimulatorManager，无法计算升级所需经验值，已跳过本次升级结算。");
                return;
            }

            // 最大等级：达到后视为满级，不再升级
            int levelMax = _levelProgressBarConfig != null ? _levelProgressBarConfig.levelMax : int.MaxValue;

            int levelNumberNew = _levelNumber;
            while (expCurrent >= expRequire)
            {
                if (expRequire <= 0)
                {
                    Debug.LogWarning("[LevelProgressBar] OnProgressValueModified: 升级所需经验值 不可小于等于0，请检查设置。");
                    break;
                }

                // 已达最大等级：满级，清空溢出经验、进度条显示为满，并停止升级
                if (levelNumberNew >= levelMax)
                {
                    levelNumberNew = levelMax;
                    expCurrent = expRequire;
                    break;
                }

                // 消耗 升级所需经验值
                expCurrent -= expRequire;
                // 升级
                levelNumberNew += 1;
                // 获取新的升级所需经验值（按升级后的等级计算）
                expRequire = manager.GetExpRequireForLevel(_levelProgressBarConfig, levelNumberNew);
            }
            // 更新 等级数
            SetLevelNumber(levelNumberNew);
            // 设置 当前经验值 与 最大经验值
            InitProgressValue(expCurrent, expRequire);
        }
    }
}