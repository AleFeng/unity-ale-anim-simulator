using System.Collections.Generic;
using AirFishLab.ScrollingList;
using AirFishLab.ScrollingList.ContentManagement;

namespace Fs.GameFramework.Gameplay.AnimSimulatorSystem
{
    /// <summary>
    /// 动画皮肤 列表数据。
    /// 用于存储所有的 动画皮肤UI数据，并为 CircularScrollingList 提供数据支持。
    /// </summary>
    public class UIAnimActorSkinListBank : BaseListBank
    {
        // 动画皮肤 数据列表
        private readonly List<UIAnimActorSkinBoxContent> _uiAnimActorSkinBoxContentList = new List<UIAnimActorSkinBoxContent>();
        // 关联的 CircularScrollingList列表组件
        private CircularScrollingList _scrollingList;
        // 当前 动画角色
        private AnimActor _animActorCurrent;
        // 当前动画角色的 皮肤组
        private AnimActorSkinGroup _animActorSkinGroupCurrent;
        
        /// <summary>
        /// 设置 列表内容
        /// </summary>
        /// <param name="animActor">动画角色</param>
        /// <param name="animActorSkinGroup">皮肤组</param>
        /// <param name="scrollingList">UI列表组件</param>
        public void SetListContents(AnimActor animActor, AnimActorSkinGroup animActorSkinGroup, CircularScrollingList scrollingList)
        {
            // 清空 现有数据
            _uiAnimActorSkinBoxContentList.Clear();
            // 记录 当前角色
            _animActorCurrent = animActor;
            // 记录 当前皮肤组
            _animActorSkinGroupCurrent = animActorSkinGroup;
            
            // 检查 皮肤组数据
            if (animActorSkinGroup != null)
            {
                // 获取 所有动画皮肤 数据
                var animActorSkins = animActorSkinGroup.animActorSkins;
                if (animActorSkins == null) return;
                // 填充 动画皮肤 数据列表
                for (int i = 0; i < animActorSkins.Length; i++)
                {
                    // 获取 当前动画皮肤 数据
                    var animActorSkin = animActorSkins[i];
                    // 添加 动画皮肤 列表项目内容
                    _uiAnimActorSkinBoxContentList.Add(new UIAnimActorSkinBoxContent(_animActorCurrent, _animActorSkinGroupCurrent, animActorSkin));
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
            if (_uiAnimActorSkinBoxContentList == null || index < 0 || index >= _uiAnimActorSkinBoxContentList.Count)
            {
                // 返回 空内容
                return new UIAnimActorSkinBoxContent();
            }
            
            // 返回 对应数据
            return _uiAnimActorSkinBoxContentList[index];
        }
        
        /// <summary>
        /// 获取 内容数量
        /// 由 列表系统 调用
        /// </summary>
        /// <returns></returns>
        public override int GetContentCount()
        {
            return _uiAnimActorSkinBoxContentList?.Count ?? 0;
        }
        
        /// <summary>
        /// 当 不满足的动画动作，条件被满足时 调用
        /// </summary>
        /// <param name="animActorSkin"></param>
        private void OnUnmetAnimActorSkinIsMet(AnimActorSkin animActorSkin)
        {
            // 将 新满足条件的 动画动作，添加到 列表数据中。
            _uiAnimActorSkinBoxContentList.Add(new UIAnimActorSkinBoxContent(_animActorCurrent, _animActorSkinGroupCurrent, animActorSkin));
            // 刷新 列表显示
            _scrollingList?.Refresh();
        }
    }
    
    /// <summary>
    /// 动画皮肤 列表项目的内容
    /// </summary>
    public class UIAnimActorSkinBoxContent : IListContent
    {
        /// <summary>
        /// 动画角色
        /// </summary>
        public AnimActor AnimActor;

        /// <summary>
        /// 动画角色的 皮肤组
        /// </summary>
        public AnimActorSkinGroup AnimActorSkinGroup;
        /// <summary>
        /// 动画角色的 皮肤数据
        /// </summary>
        public AnimActorSkin AnimActorSkin;
        
        /// <summary>
        /// 构造函数 - 空内容
        /// </summary>
        public UIAnimActorSkinBoxContent()
        {
            AnimActorSkin = default;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="animActor">动画角色</param>
        /// /// <param name="animActorSkinGroup">动画角色的 皮肤组</param>
        /// <param name="animActorSkin">动画角色的 皮肤数据</param>
        public UIAnimActorSkinBoxContent(AnimActor animActor, AnimActorSkinGroup animActorSkinGroup, AnimActorSkin animActorSkin)
        {
            AnimActor = animActor;
            AnimActorSkinGroup = animActorSkinGroup;
            AnimActorSkin = animActorSkin;
        }
    }
}
