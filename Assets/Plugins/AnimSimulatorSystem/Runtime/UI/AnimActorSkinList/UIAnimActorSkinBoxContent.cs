namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// 动画皮肤 UI列表项目的内容。一件皮肤在列表里所需的全部展示与操作数据。
    /// <para>由 <see cref="UIAnimActorSkinGroupList"/> 从当前角色与皮肤组组装，
    /// 喂给 <see cref="UIAnimActorSkinScrollList"/> 作为虚拟滚动的数据源。</para>
    /// </summary>
    public class UIAnimActorSkinBoxContent
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
        /// <param name="animActorSkinGroup">动画角色的 皮肤组</param>
        /// <param name="animActorSkin">动画角色的 皮肤数据</param>
        public UIAnimActorSkinBoxContent(AnimActor animActor, AnimActorSkinGroup animActorSkinGroup, AnimActorSkin animActorSkin)
        {
            AnimActor = animActor;
            AnimActorSkinGroup = animActorSkinGroup;
            AnimActorSkin = animActorSkin;
        }
    }
}
