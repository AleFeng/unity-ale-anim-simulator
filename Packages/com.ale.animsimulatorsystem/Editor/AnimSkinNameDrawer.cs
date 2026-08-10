using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.AnimSimulatorSystem.Editor
{
    /// <summary>
    /// <see cref="AnimSkinNameAttribute"/> 的绘制器：把皮肤名字段画成下拉选择。
    ///
    /// <para>候选来自角色实际挂着的动画控制器（经 <c>AnimatorBase.EditorCollectSkinNames()</c>），
    /// 因此 Spine 角色列出的是骨架里的皮肤名、Live2D 角色列出的是自身配置的皮肤名——
    /// <b>同一个字段在两个后端下给出各自正确的候选</b>，这正是原先的 Spine 专有特性做不到的。</para>
    ///
    /// <para>取不到候选（未指定控制器、骨架数据缺失、Live2D 尚未配置皮肤）时退化为普通文本框，
    /// 不阻断手工填写；已填但不在候选内的值会被保留并标注「缺失」，避免打开 Inspector 就把配置洗掉。</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(AnimSkinNameAttribute))]
    public class AnimSkinNameDrawer : PropertyDrawer
    {
        private const string NoneLabel = "(无)";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 非字符串字段：老实画默认样式，不做任何加工
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var names = ResolveSkinNames(property);
            if (names == null || names.Count == 0)
            {
                // 无候选：退化为文本框
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            string current = property.stringValue;
            var options = new List<string>(names.Count + 2) { NoneLabel };
            options.AddRange(names);

            int selected = string.IsNullOrEmpty(current) ? 0 : options.IndexOf(current);
            if (selected < 0)
            {
                // 已填的值不在候选里：保留它并标注，绝不静默改写用户的配置
                options.Add(current + "   ← 缺失");
                selected = options.Count - 1;
            }

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(position, label.text, selected, options.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                // 选中「缺失」那一项等于不改动；选 (无) 则清空
                if (picked == 0) property.stringValue = string.Empty;
                else if (picked <= names.Count) property.stringValue = names[picked - 1];
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 找出该字段所属对象对应的动画控制器，并取其皮肤名候选。
        /// </summary>
        private static IReadOnlyList<string> ResolveSkinNames(SerializedProperty property)
        {
            var animator = ResolveAnimator(property);
            return animator ? animator.EditorCollectSkinNames() : null;
        }

        private static AnimatorBase ResolveAnimator(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;

            // ① 字段就在动画控制器自己身上（AnimatorBase.baseSkins）
            if (target is AnimatorBase self) return self;

            // ② 字段在动画角色上（AnimActorSkin.skinName）：
            //    直接读它序列化的控制器引用，比在层级里瞎找准确
            if (target is AnimActor)
            {
                var p = property.serializedObject.FindProperty("animator");
                if (p != null && p.objectReferenceValue is AnimatorBase bound) return bound;
            }

            // ③ 兜底：在自身子树 / 父级链上找一个
            if (target is Component comp)
            {
                var found = comp.GetComponentInChildren<AnimatorBase>(true);
                if (!found) found = comp.GetComponentInParent<AnimatorBase>();
                return found;
            }

            return null;
        }
    }
}
