using System;
using UnityEngine;

namespace Ale.AnimSimulatorSystem
{
    /// <summary>
    /// Unity 动画后端的皮肤定义。
    ///
    /// <para><b>Unity 侧同样没有「皮肤」这个概念</b>——Spine 的 Skin 是骨架数据里的一等公民，可以按名取用并合并；
    /// Unity 只有一棵普通的物体树。所以这里的「一件皮肤」与 <see cref="Live2DSkinData"/> 一样，
    /// 是一份<b>配置出来的映射</b>：皮肤名 → 该皮肤要显示的物体集合（外加可选的材质 / 贴图覆盖）。</para>
    ///
    /// <para>换装时，<c>UnityAnimator</c> 取「基础皮肤 + 应用中皮肤」的并集展开成物体集合，集合内的物体
    /// <c>SetActive(true)</c>、其余<b>被本组件管理的</b>物体 <c>SetActive(false)</c>——未在任何皮肤里出现过的
    /// 物体不受影响，避免误伤模型的固有部分（身体、脸等）。</para>
    ///
    /// <para>皮肤名与 Spine / Live2D 侧使用<b>相同的命名规则</b>（含路径时用 '/' 分隔），
    /// 这样 <see cref="AnimActor"/> 上的一份皮肤组配置对三个后端都适用。</para>
    /// </summary>
    [Serializable]
    public class UnityAnimSkinData
    {
        [Tooltip("皮肤名称：与 Spine / Live2D 侧使用相同的命名规则。有文件夹路径时，一般使用 '/' 作为分隔符。")]
        public string skinName;

        [Tooltip("物体列表：该皮肤要显示的物体。\n" +
                 "只有在某件皮肤里出现过的物体才会被换装接管，其余物体一律不动。")]
        public GameObject[] objects;

        [Tooltip("材质 / 贴图覆盖（可选）：用于「同一套物体、不同花色」的场合，避免为每个花色都做一份物体。")]
        public FUnitySkinMaterialOverride[] materials;
    }

    /// <summary>
    /// Unity 皮肤的材质 / 贴图覆盖项。两种覆盖可以只填其一，也可以同时填。
    ///
    /// <para><b>两者的落点刻意不同，这是为了不改动材质资产本身：</b></para>
    /// <list type="bullet">
    /// <item><see cref="material"/> 替换的是 <c>Renderer.sharedMaterials[materialIndex]</c>——
    /// 换的是「用哪个材质资产」，不修改任何资产的内容。</item>
    /// <item><see cref="texture"/> 经 <c>MaterialPropertyBlock</c> 写到渲染器上，<b>不走
    /// <c>sharedMaterial.SetTexture</c></b>——后者会就地改写材质<b>资产</b>，在编辑器里换一次装就把
    /// 磁盘上的 .mat 弄脏了，且同一材质的所有使用者都会跟着变。属性块是逐渲染器的，没有这个问题。</item>
    /// </list>
    /// </summary>
    [Serializable]
    public struct FUnitySkinMaterialOverride
    {
        [Tooltip("目标渲染器")]
        public Renderer renderer;

        [Tooltip("材质槽位下标：材质替换写 sharedMaterials 的第几个槽位。贴图替换不使用本项。"), Min(0)]
        public int materialIndex;

        [Tooltip("替换用的材质（可选）：填了则替换 sharedMaterials[Material Index]。")]
        public Material material;

        [Tooltip("替换用的贴图（可选）：填了则经 MaterialPropertyBlock 写到渲染器上，不改材质资产。")]
        public Texture texture;

        [Tooltip("贴图属性名：留空则用 _BaseMap（URP）。内置管线的材质一般用 _MainTex。")]
        public string texturePropertyName;
    }
}
