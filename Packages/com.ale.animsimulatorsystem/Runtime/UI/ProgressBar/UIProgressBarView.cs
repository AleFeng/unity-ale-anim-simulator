using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 进度条 UI
    /// </summary>
    public class UIProgressBarView : MonoBehaviour
    {
        [Header("UI分组设置")]
        [Tooltip("UI分组 配置组：用于将 进度条 添加到 指定 分组下。")]
        [SerializeField] private FUiGroup[] uiGroups;
        
        // 是否已初始化
        private bool _isInited;
        // UI分组 字典
        private Dictionary<string, FUiGroup> _uiGroupDictionary;
        
        /// <summary>
        /// 初始化
        /// </summary>
        private void Init()
        {
            if (_isInited)
                return;
            _isInited = true;
            
            // 初始化 UI分组 字典
            _uiGroupDictionary = new Dictionary<string, FUiGroup>();
            if (uiGroups == null) return;
            foreach (var uiGroup in uiGroups)
            {
                // 尝试添加到 字典。TryAdd 成功即已写入，原先在成功分支里又赋了一次值（多余），
                // 重名的告警反倒落在了 else 上——逻辑是对的，但那次重复赋值容易读反。
                if (!_uiGroupDictionary.TryAdd(uiGroup.uiGroupName, uiGroup))
                    Debug.LogWarning($"[ProgressBarUI] Init: UI分组'{uiGroup.uiGroupName}'名称重复，请确保 分组名称 唯一。", this);
            }
        }
        
        /// <summary>
        /// 获取 UI分组 根节点
        /// </summary>
        /// <param name="uiGroupName"></param>
        /// <returns></returns>
        public Transform GetUiGroupRoot(string uiGroupName)
        {
            // 确保已初始化
            Init();
            
            if (_uiGroupDictionary != null && _uiGroupDictionary.TryGetValue(uiGroupName, out var uiGroup))
            {
                // 返回 分组根节点
                return uiGroup.uiGroupRoot;
            }
            else
            {
                Debug.LogWarning($"[ProgressBarUI] GetUiGroupRoot: 未找到名称为 '{uiGroupName}' 的 UI分组，请检查 配置。");
                return this.transform;
            }
        }
        
        /// <summary>
        /// UI分组
        /// </summary>
        [Serializable]
        public struct FUiGroup
        {
            [Tooltip("UI画布中的 分组名称：唯一标识。用于将 进度条 添加到指定 分组下。")]
            public string uiGroupName;
            [Tooltip("UI画布中的 分组根节点：用于将 进度条 添加到指定 分组下。")]
            public Transform uiGroupRoot;
        }
    }
}