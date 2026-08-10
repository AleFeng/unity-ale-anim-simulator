using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if ATK_LOCALIZATION
using UnityEngine.Localization.Components;
#endif

namespace Ale.AnimSimulatorSystem
{
    public class UIBaseProgressBar : MonoBehaviour
    {
        [Header("UI组件")]
#if ATK_LOCALIZATION
        [Tooltip("本地化文字 等级名称")]
        [SerializeField] private LocalizeStringEvent localizeTxtName;
#else
        [Tooltip("文本 名称")]
        [SerializeField] private Text txtName;
#endif
        [Tooltip("滑动条 经验进度")]
        [SerializeField] private Slider sliderProgress;
        
        // 进度条 配置
        private ProgressBarConfig _progressBarConfig;
        
        /// <summary>
        /// 初始化
        /// </summary>
        protected virtual void Awake()
        {
            
        }
        
        /// <summary>
        /// 设置信息
        /// </summary>
        /// <param name="progressBarConfigNew">进度条配置</param>
        protected void SetInfo(ProgressBarConfig progressBarConfigNew)
        {
            // 记录 配置
            _progressBarConfig = progressBarConfigNew;
            if (_progressBarConfig == null)
            {
                Debug.LogWarning("[BaseProgressBar] SetInfo: 传入的 ProgressBarConfig 为空，请检查调用。");
                return;
            }
            
#if ATK_LOCALIZATION
            // 设置 名称的本地化Key
            if (localizeTxtName != null)
            {
                localizeTxtName.StringReference = _progressBarConfig.uiDisplayName;
                localizeTxtName.RefreshString();
            }
#else
            // 设置 名称
            if (txtName != null)
                txtName.text = _progressBarConfig.uiDisplayName;
#endif
        }

        #region 进度条
        #region 设置 进度值
        /// <summary>
        /// 当前 进度值
        /// </summary>
        public float ProgressValueCurrent => _progressValueCurrent;
        private float _progressValueCurrent;
        /// <summary>
        /// 最大 进度值
        /// </summary>
        public float ProgressValueMax => _progressValueMax;
        private float _progressValueMax = 100;
        
        /// <summary>
        /// 初始化 进度值
        /// </summary>
        /// <param name="progressValue">当前 进度值</param>
        /// <param name="progressValueMax">最大 进度值</param>
        protected void InitProgressValue(float progressValue, float progressValueMax)
        {
            // 设置 进度值
            _progressValueCurrent = progressValue;
            // 设置 最大值
            _progressValueMax = progressValueMax;
            
            // 设置 进度百分比。最大值为0时按满值处理，避免除零。
            SetProgressPercent(_progressValueMax > 0f ? _progressValueCurrent / _progressValueMax : 1f, true);
        }
        
        /// <summary>
        /// 修改 进度值
        /// </summary>
        /// <param name="valueModify">增加或减少的进度值。</param>
        public void ModifyProgressValue(float valueModify)
        {
            // 修改 进度值
            _progressValueCurrent += valueModify;
            // 钳制 进度值（子类可覆盖，例如动作进度条的上限限制）
            _progressValueCurrent = ClampProgressValue(_progressValueCurrent);
            // 设置 进度百分比。最大值为0时按满值处理，避免除零。
            SetProgressPercent(_progressValueMax > 0f ? _progressValueCurrent / _progressValueMax : 1f);

            // 调用 进度值修改回调
            OnProgressValueModified(_progressValueCurrent, _progressValueMax);
        }

        /// <summary>
        /// 钳制 进度值。基类不做限制，子类可覆盖以施加上下限。
        /// </summary>
        /// <param name="value">待钳制的进度值</param>
        /// <returns>钳制后的进度值</returns>
        protected virtual float ClampProgressValue(float value) => value;

        /// <summary>
        /// 进度值 修改回调
        /// </summary>
        /// <param name="progressValueCurrent">当前 进度值</param>
        /// <param name="progressValueMax">最大 进度值</param>
        protected virtual void OnProgressValueModified(float progressValueCurrent, float progressValueMax)
        {
            
        }
        #endregion

        #region 设置 进度百分比
        [Tooltip("滑动条 数值平滑过渡的基础时长（秒）：实际时长会根据变化幅度进行调整。")]
        [SerializeField] private float sliderTweenBaseDuration = 0.5f;
        
        // 协程 平滑设置 slider 的值
        private Coroutine _sliderTweenCor;
        // 进度百分比 起始值
        private float _progressPercentStart;
        // 进度百分比 目标值
        private float _progressPercentTarget;
        // 进度时间 经过时间
        private float _progressTimeElapsed;
        // 进度时间 持续时间
        private float _progressTimeDuration;

        /// <summary>
        /// 设置 进度百分比
        /// </summary>
        /// <param name="progressPercent">进度百分比</param>
        /// <param name="isImmediate">立即设置</param>
        protected void SetProgressPercent(float progressPercent, bool isImmediate = false)
        {
            if (sliderProgress != null)
            {
                // 限制 进度百分比 在[0,1]
                progressPercent = Mathf.Clamp01(progressPercent);
                
                // 记录 起始值 与 目标值
                _progressPercentStart = sliderProgress.value;
                _progressPercentTarget = progressPercent;
                
                // 立即设置
                if (isImmediate)
                {
                    // 停止 协程
                    if (_sliderTweenCor != null)
                    {
                        StopCoroutine(_sliderTweenCor);
                        _sliderTweenCor = null;
                    }
                    // 直接设置 目标值
                    sliderProgress.value = _progressPercentTarget;
                }
                // 平滑过渡
                else
                {
                    // 计算 过渡时长
                    float modify = Mathf.Abs(_progressPercentTarget - _progressPercentStart);
                    _progressTimeDuration = Mathf.Clamp(
                        sliderTweenBaseDuration * (0.6f + modify), sliderTweenBaseDuration * 0.1f, sliderTweenBaseDuration * 2f);
                    // 重置 经过时间
                    _progressTimeElapsed = 0f;
                
                    // 不要重复启动协程
                    if (_sliderTweenCor == null)
                        // 启动新的 平滑过渡协程
                        _sliderTweenCor = StartCoroutine(CorTweenSliderValue());
                }
            }
        }

        /// <summary>
        /// 协程 平滑过渡 滑动条的值
        /// </summary>
        private IEnumerator CorTweenSliderValue()
        {
            // 检查 slider有效性
            if (sliderProgress == null)
            {
                _sliderTweenCor = null;
                yield break;
            }
            
            // 几乎相等，直接设置并结束
            if (Mathf.Approximately(_progressPercentStart, _progressPercentTarget))
            {
                sliderProgress.value = _progressPercentTarget;
                _sliderTweenCor = null;
                yield break;
            }
            
            // 平滑过渡 设置 slider 的值
            while (_progressTimeElapsed < _progressTimeDuration)
            {
                // 增加 经过时间
                _progressTimeElapsed += Time.deltaTime;
                // 计算 插值比例（0-1）
                float t = Mathf.Clamp01(_progressTimeElapsed / _progressTimeDuration);
                
                // 设置 slider 的值
                float eased = 1f - Mathf.Pow(1f - t, 3f); // 先快后慢（f(t) = 1 - (1 - t)^3）
                sliderProgress.value = Mathf.Lerp(_progressPercentStart, _progressPercentTarget, eased);
                
                yield return null;
            }
            // 确保 最终值正确
            sliderProgress.value = _progressPercentTarget;
            
            // 结束 协程
            _sliderTweenCor = null;
        }
        #endregion
        #endregion
    }
}