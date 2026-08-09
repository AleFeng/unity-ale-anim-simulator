#if HAS_LOCALIZATION
using UnityEngine.Localization;
#endif

using System.Collections.Generic;
using UnityEngine;
using AirFishLab.ScrollingList;
using AirFishLab.ScrollingList.ContentManagement;

namespace Fs.GameFramework.Gameplay.AnimSimulatorSystem
{
    /// <summary>
    /// 动画动作 列表数据 组件。
    /// 用于存储所有的 动画动作UI数据，并为 CircularScrollingList 提供数据支持。
    /// </summary>
    public class UIAnimActionListBank : BaseListBank
    {
        // 动画动作数据 列表
        private readonly List<UIAnimActionListBoxContent> _animActionContentList = new List<UIAnimActionListBoxContent>();
        // 关联的 CircularScrollingList列表组件
        private CircularScrollingList _scrollingList;
        
        /// <summary>
        /// 设置 列表内容
        /// </summary>
        /// <param name="animActionPlayer">动画动作播放器</param>
        /// <param name="scrollingList">UI列表组件</param>
        public void SetListContents(AnimActionPlayer animActionPlayer, CircularScrollingList scrollingList)
        {
            // 清空 现有数据
            _animActionContentList.Clear();
            
            // 检查 角色动作播放器
            if (animActionPlayer)
            {
                // 获取 所有动画动作 名称和图标
                var allAnimActions = animActionPlayer.GetAnimActionsMeetConditions(OnUnmetAnimActionIsMet);
                if (allAnimActions == null) return;
                // 填充 动画动作 数据列表
                for (int i = 0; i < allAnimActions.Count; i++)
                {
                    // 获取 当前动画动作
                    var animAction = allAnimActions[i];
                    // 创建 动画动作数据
                    _animActionContentList.Add(new UIAnimActionListBoxContent(animAction));
                }
            }
            
            // 记录 关联的 列表组件
            _scrollingList = scrollingList;
            _scrollingList?.Initialize();
            _scrollingList?.Refresh(); // 刷新 列表显示
        }

        /// <summary>
        /// 获取 列表项目的内容
        /// 由 列表系统 调用
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public override IListContent GetListContent(int index)
        {
            // 无对应数据时，返回 空内容
            if (_animActionContentList == null || index < 0 || index >= _animActionContentList.Count)
            {
                // 返回 空内容
                return new UIAnimActionListBoxContent();
            }
            
            // 返回 对应数据
            return _animActionContentList[index];
        }
        
        /// <summary>
        /// 获取 内容数量
        /// 由 列表系统 调用
        /// </summary>
        /// <returns></returns>
        public override int GetContentCount()
        {
            return _animActionContentList?.Count ?? 0;
        }
        
        /// <summary>
        /// 当 不满足的动画动作，条件被满足时 调用
        /// </summary>
        /// <param name="animAction"></param>
        private void OnUnmetAnimActionIsMet(AnimAction animAction)
        {
            if (animAction == null) return;
            
            // 将 新满足条件的 动画动作，添加到 列表数据中。
            _animActionContentList.Add(new UIAnimActionListBoxContent(animAction));
            // 刷新 列表显示
            _scrollingList?.Refresh();
        }
    }
    
    /// <summary>
    /// 动画动作 UI列表项目的内容
    /// </summary>
    public class UIAnimActionListBoxContent : IListContent
    {
#if HAS_LOCALIZATION
        [Tooltip("UI中显示的动作名称：多语言Key。")]
        public LocalizedString UIDisplayActionName;
#else
        [Tooltip("UI中显示的动作名称")]
        public string UIDisplayActionName;
#endif
        
        /// <summary>
        /// 动画动作 图标
        /// </summary>
        public readonly Sprite ActionIcon;
        
        /// <summary>
        /// 动画动作
        /// </summary>
        public AnimAction AnimAction => _animAction;
        private AnimAction _animAction;

        /// <summary>
        /// 构造函数
        /// </summary>
        public UIAnimActionListBoxContent()
        {
            
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="animAction">动画动作</param>
        public UIAnimActionListBoxContent(AnimAction animAction)
        {
            // 设置 关联的 动画动作
            _animAction = animAction;

            // 设置 动画动作 名称
#if HAS_LOCALIZATION
            UIDisplayActionName = animAction.uiDisplayActionName;
#else
            UIDisplayActionName = animAction.uiDisplayActionName;
#endif
            // 设置 动画动作 图标
            ActionIcon = _animAction.actionIcon;
        }
    }
}
