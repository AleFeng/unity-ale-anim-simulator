using System;
using UnityEngine;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// Live2D 皮肤定义。
    ///
    /// <para><b>Cubism 没有「皮肤」这个概念</b>——Spine 的 Skin 是骨架数据里的一等公民，可以按名取用并合并；
    /// Cubism 只有部件（<c>CubismPart</c>）与可绘制对象（<c>CubismDrawable</c>）。所以 Live2D 侧的「一件皮肤」
    /// 只能是一份<b>配置出来的映射</b>：皮肤名 → 该皮肤要显示的部件 ID 集合。</para>
    ///
    /// <para>换装时，<c>Live2DAnimator</c> 取「基础皮肤 + 应用中皮肤」的并集展开成部件集合，
    /// 集合内的部件不透明度置 1、其余<b>被本组件管理的</b>部件置 0——未在任何皮肤里出现过的部件不受影响，
    /// 避免误伤模型的固有部件（身体、脸等）。</para>
    ///
    /// <para>皮肤名与 Spine 侧使用<b>相同的命名规则</b>（含路径时用 '/' 分隔），
    /// 这样 <see cref="AnimActor"/> 上的一份皮肤组配置对两个后端都适用。</para>
    /// </summary>
    [Serializable]
    public class Live2dSkinData
    {
        [Tooltip("皮肤名称：与 Spine 侧使用相同的命名规则。有文件夹路径时，一般使用 '/' 作为分隔符。")]
        public string skinName;

        [Tooltip("部件 ID 列表：该皮肤显示哪些部件（CubismPart.Id，即 Cubism Editor 中的部件 ID）。")]
        public string[] partIds;

        [Tooltip("贴图替换（可选）：把指定可绘制对象的贴图换成另一张。留空则不做替换。")]
        public FLive2dSkinTexture[] textures;
    }

    /// <summary>
    /// Live2D 皮肤的贴图替换项：把某个可绘制对象（<c>CubismDrawable.Id</c>）的主贴图替换为指定贴图。
    /// 用于「同一套部件、不同花色」的场合，避免为每个花色都做一份部件。
    /// </summary>
    [Serializable]
    public struct FLive2dSkinTexture
    {
        [Tooltip("可绘制对象 ID（CubismDrawable.Id）")]
        public string drawableId;
        [Tooltip("替换用的贴图")]
        public Texture2D texture;
    }
}
