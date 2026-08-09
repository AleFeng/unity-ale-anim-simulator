using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fs.GameFramework.Gameplay.AnimSimulatorSystem
{
    /// <summary>
    /// 动作进度条 UI
    /// </summary>
    public class UIActionProgressBar : UIBaseProgressBar
    {
        [Header("动作进度条")]
        [SerializeField] private Button button;

        // 当前使用的 动作进度条 配置
        private ActionProgressBarConfig _actionProgressBarConfig;
        // 本地记录每个 actionPlayConfig 已播放的次数（与 actionPlayConfigs 索引一一对应）
        private Dictionary<ActionPlayConfig, int> _dicActionPlayCounter = new Dictionary<ActionPlayConfig, int>();
        
        /// <summary>
        /// 初始化
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            
            // 注册 按钮点击事件
            if (button != null)
            {
                button.onClick.AddListener(OnButtonClick);
            }
        }
        
        /// <summary>
        /// 设置信息
        /// </summary>
        /// <param name="actionProgressBarConfigNew"></param>
        /// <param name="progressValue">当前的进度值</param>
        public void SetInfo(ActionProgressBarConfig actionProgressBarConfigNew, int progressValue)
        {
            // 设置 基类信息
            SetInfo(actionProgressBarConfigNew);
            
            // 记录 配置
            _actionProgressBarConfig = actionProgressBarConfigNew;
            // 检查 配置有效性
            if (_actionProgressBarConfig == null)
            {
                _dicActionPlayCounter.Clear();
                SetProgressPercent(0f, true);
                Debug.LogWarning("[ActionProgressBar] SetInfo: 传入的 ActionProgressBarConfig 为空，请检查调用。");
                return;
            }

            // 初始化 播放计数器
            if (_actionProgressBarConfig.actionPlayConfigs != null)
            {
                foreach (var actionPlayConfig in _actionProgressBarConfig.actionPlayConfigs)
                    _dicActionPlayCounter[actionPlayConfig] = 0;
            }

            // 初始化 进度值与最大值（使 progressValueMax 生效）
            float max = Mathf.Max(1f, _actionProgressBarConfig.progressValueMax);
            InitProgressValue(progressValue, max);
        }

        /// <summary>
        /// 钳制 进度值：限制在 [0, 进度限制值] 内。
        /// 进度限制值 &lt;= 0 表示不限制上限。
        /// </summary>
        /// <param name="value">待钳制的进度值</param>
        /// <returns>钳制后的进度值</returns>
        protected override float ClampProgressValue(float value)
        {
            // 施加 进度限制值 上限
            if (_actionProgressBarConfig != null && _actionProgressBarConfig.progressValueLimit > 0f)
                value = Mathf.Min(value, _actionProgressBarConfig.progressValueLimit);
            // 进度值 不低于 0
            return Mathf.Max(0f, value);
        }

        /// <summary>
        /// 进度值 修改回调
        /// </summary>
        /// <param name="progressValueCurrent">当前 进度值</param>
        /// <param name="progressValueMax">最大 进度值</param>
        protected override void OnProgressValueModified(float progressValueCurrent, float progressValueMax)
        {
            base.OnProgressValueModified(progressValueCurrent, progressValueMax);
            
            // 尝试播放 消耗值最大且可播放的 动画动作播放配置。自动播放
            if (GetAnimActionConfigMaxConsumeCanPlay(out var actionPlayConfig, EAnimActionPlayType.Auto))
                PlayAnimAction(actionPlayConfig);
        }

        /// <summary>
        /// 按钮点击事件
        /// </summary>
        private void OnButtonClick()
        {
            // 检查 配置有效性
            // 尝试播放 消耗值最大且可播放的 动画动作播放配置。手动播放
            if (GetAnimActionConfigMaxConsumeCanPlay(out var actionPlayConfig, EAnimActionPlayType.Manual))
                PlayAnimAction(actionPlayConfig);
        }

        #region 播放动作
        /// <summary>
        /// 获取 消耗值最大且可播放的 动画动作播放配置
        /// </summary>
        /// <param name="selectedActionPlayConfig"></param>
        /// <param name="animActionPlayType"></param>
        /// <returns></returns>
        private bool GetAnimActionConfigMaxConsumeCanPlay
        (
            out ActionPlayConfig selectedActionPlayConfig, 
            EAnimActionPlayType animActionPlayType
        )
        {
            selectedActionPlayConfig = null;
            // 检查 配置有效性
            if (_actionProgressBarConfig == null || _actionProgressBarConfig.actionPlayConfigs == null) return false;
            // 遍历检查 所有动作播放配置
            for (int i = 0; i < _actionProgressBarConfig.actionPlayConfigs.Length; i++)
            {
                // 获取 当前动作播放配置
                var actionPlayConfig = _actionProgressBarConfig.actionPlayConfigs[i];
                if (actionPlayConfig == null) continue;
                // 检查 播放类型 是否匹配
                if (actionPlayConfig.animActionPlayType != animActionPlayType) continue;
                // 检查 是否可播放
                if (CheckAnimActionCanPlay(actionPlayConfig))
                {
                    // 选择 消耗值最大 的配置
                    if (selectedActionPlayConfig == null ||
                        actionPlayConfig.progressValueConsumed >= selectedActionPlayConfig.progressValueConsumed)
                    {
                        // 记录 选中的配置
                        selectedActionPlayConfig = actionPlayConfig;
                    }
                }
            }
            
            return selectedActionPlayConfig != null;
        }
        
        /// <summary>
        /// 检查 动画动作 是否可播放
        /// </summary>
        /// <param name="actionPlayConfig"></param>
        /// <returns></returns>
        private bool CheckAnimActionCanPlay(ActionPlayConfig actionPlayConfig)
        {
            // 检查 是否已经达到 最大播放次数（0表示不限制）
            if (actionPlayConfig.actionPlayCount > 0)
            {
                // 获取 已播放次数
                if (_dicActionPlayCounter.TryGetValue(actionPlayConfig, out int playedCount))
                {
                    // 已达到最大播放次数，跳过
                    if (playedCount >= actionPlayConfig.actionPlayCount) return false;
                }
            }
            // 检查 进度值 是否达到要求
            if (ProgressValueCurrent >= actionPlayConfig.progressValueRequired)
                return true;

            return false;
        }
        
        /// <summary>
        /// 播放 动画动作
        /// </summary>
        /// <param name="actionPlayConfig"></param>
        /// <returns></returns>
        private void PlayAnimAction(ActionPlayConfig actionPlayConfig)
        {
            // 检查 进度值 是否达到要求
            if (CheckAnimActionCanPlay(actionPlayConfig))
            {
                // 获取 选择类型
                EAnimActionSelectType actionSelectType = actionPlayConfig.animActionSelectType;
                
                // 判断 播放类型
                switch (actionPlayConfig.animActionPlayType)
                {
                    case EAnimActionPlayType.Auto:
                        // Auto自动 模式下，选择类型不能是 手动选择。
                        if (actionPlayConfig.animActionSelectType == EAnimActionSelectType.Select)
                            // 强制改为 随机选择
                            actionSelectType = EAnimActionSelectType.Random;
                        break;
                    case EAnimActionPlayType.Manual:
                        // 将AnimActionPlayer移动到鼠标的位置。
                        AnimSimulatorManager.Instance.MoveAnimActionPlayerToCursor(actionPlayConfig.actionPlayerName);
                        break;
                }
                
                // 自动播放动作
                AnimSimulatorManager.Instance.PlayAnimActionPlayerByType
                (
                    actionPlayConfig.actionPlayerName,
                    actionSelectType,
                    (animActionPlayer) =>
                    {
                        if (!animActionPlayer)
                        {
                            Debug.LogWarning($"[ActionProgressBar] PlayAnimAction: 无法找到名为 {actionPlayConfig.actionPlayerName} 的 AnimActionPlayer，请检查配置。");
                            return;
                        }
                        
                        // 消耗 进度值
                        ModifyProgressValue(-actionPlayConfig.progressValueConsumed);
                        // 累计 播放次数
                        if (!_dicActionPlayCounter.TryAdd(actionPlayConfig, 1))
                            _dicActionPlayCounter[actionPlayConfig] += 1;
                    }
                );
            }
        }
        #endregion
    }
}