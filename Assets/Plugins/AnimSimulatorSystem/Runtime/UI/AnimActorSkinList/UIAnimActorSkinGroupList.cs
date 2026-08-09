using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fs.GameFramework.Gameplay.AnimSimulatorSystem
{
    /// <summary>
    /// 动画角色 UI皮肤组列表。
    /// 显示当前角色 所有可用的皮肤组，可在 不同皮肤组间 切换，并显示选中的 皮肤组内 的所有 皮肤项目。
    /// </summary>
    public class UIAnimActorSkinGroupList : MonoBehaviour
    {
        [Header("UI页签")]
        [Tooltip("皮肤组页签 预制体")]
        [SerializeField] private UIAnimActorSkinGroupTab uiAnimActorSkinGroupTabPrefab;
        [Tooltip("皮肤组页签 根节点")]
        [SerializeField] private Transform uiAnimActorSkinGroupTabRoot;
        
        [FormerlySerializedAs("scrollingListSkinGroups")]
        [Header("UI列表")]
        [Tooltip("列表组件")]
        [SerializeField] private UIAnimActorSkinScrollList scrollingListSkinGroup;

        // 当前 动画角色
        private AnimActor _animActor;
        // 皮肤组页签 列表
        private List<UIAnimActorSkinGroupTab> _uiAnimActorSkinGroupTabList = new List<UIAnimActorSkinGroupTab>();
        // 皮肤数据 列表。作为滚动列表的数据源。
        private readonly List<UIAnimActorSkinBoxContent> _uiAnimActorSkinBoxContentList =
            new List<UIAnimActorSkinBoxContent>();

        /// <summary>
        /// 设置 皮肤组数据
        /// </summary>
        /// <param name="animActor"></param>
        public void SetAnimActor(AnimActor animActor)
        {
            // 检查 是否相同角色
            if (_animActor == animActor) return;
            // 记录当前角色
            _animActor = animActor;

            // 刷新 皮肤组列表UI
            if (_animActor && _animActor.AnimActorSkinGroups != null)
            {
                // 获取 皮肤组数量
                var skinGroupsCount = _animActor.AnimActorSkinGroups.Length;
                
                // 实例化 数量不足的页签
                for (int i = _uiAnimActorSkinGroupTabList.Count; i < skinGroupsCount; i++)
                {
                    // 实例化 页签
                    var tabInstance = Instantiate(uiAnimActorSkinGroupTabPrefab, uiAnimActorSkinGroupTabRoot);
                    tabInstance.OnClickSkinGroupTab = OnSelectSkinGroupTab; // 监听 页签点击 事件
                    // 添加到 列表
                    _uiAnimActorSkinGroupTabList.Add(tabInstance);
                }
                // 设置 页签数据
                for (int i = 0; i < skinGroupsCount; i++)
                {
                    // 显示 页签
                    var tabInstance = _uiAnimActorSkinGroupTabList[i];
                    tabInstance.gameObject.SetActive(true);
                    // 设置 皮肤组数据
                    var animActorSkinGroup = _animActor.AnimActorSkinGroups[i];
                    tabInstance.SetAnimActorSkinGroup(animActorSkinGroup);
                }
                // 多余页签 隐藏
                for (int i = skinGroupsCount; i < _uiAnimActorSkinGroupTabList.Count; i++)
                {
                    _uiAnimActorSkinGroupTabList[i].gameObject.SetActive(false);
                }
                
                // 默认选中 第一个 皮肤组页签
                if (skinGroupsCount > 0)
                    OnSelectSkinGroupTab(_uiAnimActorSkinGroupTabList[0]);
            }
            // 清空 皮肤组列表UI
            else
            {
                // 所有页签 隐藏
                for (int i = 0; i < _uiAnimActorSkinGroupTabList.Count; i++)
                {
                    _uiAnimActorSkinGroupTabList[i].gameObject.SetActive(false);
                }
                
                // 清空 列表的数据内容
                RebuildSkinListContents(null);
            }
        }

        /// <summary>
        /// 依据指定皮肤组重建列表数据并推给列表组件。皮肤组为空时即为清空。
        /// </summary>
        /// <param name="animActorSkinGroup">皮肤组</param>
        private void RebuildSkinListContents(AnimActorSkinGroup animActorSkinGroup)
        {
            // 清空 现有数据
            _uiAnimActorSkinBoxContentList.Clear();

            // 检查 皮肤组数据
            var animActorSkins = animActorSkinGroup?.animActorSkins;
            if (animActorSkins != null)
            {
                // 填充 动画皮肤 数据列表
                for (int i = 0; i < animActorSkins.Length; i++)
                    _uiAnimActorSkinBoxContentList.Add(
                        new UIAnimActorSkinBoxContent(_animActor, animActorSkinGroup, animActorSkins[i]));
            }

            // 推给 列表组件。切换皮肤组属于换内容，回到起点是期望行为。
            if (scrollingListSkinGroup)
                scrollingListSkinGroup.SetItems(_uiAnimActorSkinBoxContentList);
        }
        
        // 当前 选择的 皮肤组页签
        private UIAnimActorSkinGroupTab _skinGroupTabSelected;
        
        /// <summary>
        /// 选择 皮肤组页签
        /// </summary>
        /// <param name="selectedTab"></param>
        private void OnSelectSkinGroupTab(UIAnimActorSkinGroupTab selectedTab)
        {
            // 检查 是否变化
            if (selectedTab == null || _skinGroupTabSelected == selectedTab) return;
            
            // 取消 之前选择的 页签状态
            if (_skinGroupTabSelected) 
                _skinGroupTabSelected.SetIsSelected(false);
            // 记录 当前选择的 页签
            _skinGroupTabSelected = selectedTab;
            // 设置 当前选择的 页签状态
            _skinGroupTabSelected.SetIsSelected(true);
            
            // 更新 列表数据内容
            RebuildSkinListContents(_skinGroupTabSelected.AnimActorSkinGroup);
        }
    }
}
