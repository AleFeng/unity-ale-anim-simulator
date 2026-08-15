using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.AnimSimulatorSystem.Editor.AddressableSupport
{
    /// <summary>
    /// 把演示样例的文件夹一键登记进 Addressables，替代「手工拖进分组再改地址」那一步。
    ///
    /// <para>本类位于独立程序集 <c>Ale.AnimSimulatorSystem.Addressables.Editor</c>，受
    /// <c>ATK_ADDRESSABLE</c> + <c>ASS_HAS_ADDRESSABLES</c> 双重门控：前者是使用者在 Ale Toolkit
    /// 欢迎窗口手工勾的，后者由 <c>versionDefines</c> 从 <c>com.unity.addressables</c> 自动推出。
    /// 只勾宏却没装包时，光靠前者会让 asmdef 的 Addressables 引用解析不到；加上后者才关得严。</para>
    ///
    /// <para>欢迎窗口对 Addressables <b>零引用</b>，只持有两个 <c>Func&lt;string&gt;</c> 钩子；
    /// 宏一关本程序集不参与编译，钩子保持 null，那一整块界面自动消失。</para>
    /// </summary>
    [InitializeOnLoad]
    internal static class AnimSimulatorDemoAddressables
    {
        /// <summary>
        /// 条目地址。<b>不带尾部斜杠</b>——文件夹条目的子地址是 <c>地址 + 相对路径</c>，
        /// 而相对路径本身以 <c>/</c> 开头（见 <c>AddressableAssetEntry.GatherFolderEntries</c>
        /// 里的 <c>Substring(path.Length)</c>）。于是 <c>AnimSimulator</c> +
        /// <c>/Assets/Actors/Koharu/Koharu.prefab</c> 正好拼成运行时要的地址。
        ///
        /// <para>⚠️ 与 <see cref="AnimSimulatorConfig"/> 上两个前缀字段的默认值
        /// （<c>AnimSimulator/Assets/{Backgrounds,Actors}/</c>）保持一致。
        /// 那两个是 <c>public</c> 实例字段、拿不到编译期常量，故此处只能重复一次。</para>
        /// </summary>
        private const string DemoAddress = "AnimSimulator";

        /// <summary>样例落地目录：<c>Assets/Samples/{package.json 的 displayName}/{导入时的版本}/{样例名}</c>。</summary>
        private const string SamplesRoot = "Assets/Samples";
        private const string PackageDisplayName = "Anim Simulator System";
        private const string SampleName = "Anim Simulator System Demo";

        /// <summary>本仓库自带的开发用 Demo 目录（不是经 Package Manager 导入的样例）。</summary>
        private const string EmbeddedDemoFolder = "Assets/Demo";

        // 判定「这个文件夹确实是本插件的样例」用的两个子目录。
        // 只按路径名认会很危险：使用者工程里完全可能有个无关的 Assets/Demo，
        // 把它登记成 AnimSimulator 会平白改掉别人的资产配置。
        private static readonly string[] DemoMarkerFolders = { "Assets/Actors", "Assets/Backgrounds" };

        static AnimSimulatorDemoAddressables()
        {
            AnimSimulatorWelcomeWindow.RegisterDemoAddressables = Register;
            AnimSimulatorWelcomeWindow.DescribeDemoAddressables = Describe;
        }

        #region 样例目录定位

        /// <summary>
        /// 找出所有可登记的样例文件夹。
        ///
        /// <para><b>不能按当前包版本拼路径。</b>落地路径盖的是<b>导入当时</b>的包版本号，
        /// 与升级后的 <c>package.json</c> 不是一回事。同理也不能用
        /// <c>Sample.FindByPackage(name, version).importPath</c>，那条会指向一个不存在的目录。
        /// 所以这里枚举版本目录，把「装了多个版本」的情况也一并暴露出来。</para>
        /// </summary>
        private static List<string> FindDemoFolders()
        {
            var result = new List<string>();

            string root = $"{SamplesRoot}/{PackageDisplayName}";
            if (AssetDatabase.IsValidFolder(root))
            {
                foreach (var versionDir in Directory.GetDirectories(root))
                {
                    string candidate = $"{versionDir.Replace('\\', '/')}/{SampleName}";
                    if (LooksLikeDemoFolder(candidate)) result.Add(candidate);
                }
            }

            // 本仓库把 Demo 直接放在 Assets/Demo 下（Samples~ 是它的镜像），插件开发时用的就是这一份
            if (LooksLikeDemoFolder(EmbeddedDemoFolder)) result.Add(EmbeddedDemoFolder);

            result.Sort(System.StringComparer.Ordinal);
            return result;
        }

        /// <summary>该目录是否确实是本插件的样例——必须同时含有角色与背景两个子目录。</summary>
        private static bool LooksLikeDemoFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath)) return false;
            foreach (var marker in DemoMarkerFolders)
                if (!AssetDatabase.IsValidFolder($"{folderPath}/{marker}")) return false;
            return true;
        }

        #endregion

        #region 现状描述

        /// <summary>欢迎窗口里显示的现状。纯读，不改任何东西。</summary>
        private static string Describe()
        {
            var folders = FindDemoFolders();
            if (folders.Count == 0)
                return Tr("  ⚠ 未找到演示样例。请先在 Package Manager 里导入 Anim Simulator System Demo。");

            if (!AddressableAssetSettingsDefaultObject.SettingsExists)
                return Tr("  ⚠ 本工程尚未初始化 Addressables 设置，点击下方按钮会一并创建。");

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return Tr("  ⚠ 暂时读不到 Addressables 设置（可能正在编译）。稍后重试。");

            if (folders.Count > 1)
                return Fmt("  ⚠ 发现 {0} 个样例目录，它们会争用同一个地址。建议只保留一个。", folders.Count);

            var entry = FindFolderEntry(settings, folders[0]);
            if (entry == null)
                return Fmt("  ⚠ 样例已就位但尚未登记：{0}", folders[0]);

            if (entry.address != DemoAddress)
                return Fmt("  ⚠ 已登记，但地址是「{0}」而不是「{1}」，资源会加载不到。", entry.address, DemoAddress);

            return Fmt("  ✓ 已登记：地址「{0}」，分组「{1}」",
                entry.address, entry.parentGroup != null ? entry.parentGroup.Name : "-");
        }

        private static AddressableAssetEntry FindFolderEntry(AddressableAssetSettings settings, string folderPath)
        {
            string guid = AssetDatabase.AssetPathToGUID(folderPath);
            return string.IsNullOrEmpty(guid) ? null : settings.FindAssetEntry(guid);
        }

        #endregion

        #region 登记

        /// <summary>
        /// 执行登记。幂等：已经登记且地址正确时什么都不做。
        ///
        /// <para>写之前先弹确认框——toolkit 既有的策略是「绝不擅自改写使用者的 Addressables 配置」，
        /// 本按钮是个写入方，因此必须让用户明确点头。</para>
        /// </summary>
        private static string Register()
        {
            var folders = FindDemoFolders();
            if (folders.Count == 0)
            {
                return Tr("未找到演示样例。请先在 Package Manager 里导入 Anim Simulator System Demo，再回来点这个按钮。" +
                          "（判定条件：目标目录下同时存在 Assets/Actors 与 Assets/Backgrounds 两个子目录。）");
            }

            if (folders.Count > 1)
            {
                return Fmt("发现 {0} 个样例目录：\n{1}\n它们会争用同一个地址，请先删掉多余的，只保留一个。",
                    folders.Count, string.Join("\n", folders));
            }

            string folderPath = folders[0];
            string guid = AssetDatabase.AssetPathToGUID(folderPath);
            if (string.IsNullOrEmpty(guid))
                return Fmt("取不到文件夹的 GUID：{0}", folderPath);

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
                return Tr("无法获取或创建 Addressables 设置。请稍后重试，或先手工创建一次 Addressables 配置。");

            // ── 幂等分支 ────────────────────────────────────────────────────
            var existing = settings.FindAssetEntry(guid);
            if (existing != null && existing.address == DemoAddress)
            {
                return Fmt("已经登记好了，无需改动。（分组「{0}」，地址「{1}」）",
                    existing.parentGroup != null ? existing.parentGroup.Name : "-", existing.address);
            }

            // ── 预扫描：单独登记过的资源会被文件夹条目静默跳过 ───────────────
            var conflicts = FindIndividuallyRegistered(settings, folderPath, guid);

            var group = settings.DefaultGroup;
            if (group == null)
                return Tr("Addressables 没有默认分组。请先在 Addressables Groups 窗口里指定一个默认分组。");

            // ── 确认 ────────────────────────────────────────────────────────
            string detail = existing == null
                ? Fmt("将把文件夹\n{0}\n登记到分组「{1}」，并把地址设为「{2}」。", folderPath, group.Name, DemoAddress)
                : Fmt("该文件夹已登记在分组「{0}」，但地址是「{1}」。\n将把地址改为「{2}」（不移动分组）。",
                    existing.parentGroup != null ? existing.parentGroup.Name : "-", existing.address, DemoAddress);

            if (conflicts.Count > 0)
            {
                detail += Fmt("\n\n⚠ 另有 {0} 个资源此前被单独登记过，它们会保留各自的旧地址、不受文件夹条目影响：\n{1}",
                    conflicts.Count, string.Join("\n", conflicts.Take(5)) + (conflicts.Count > 5 ? "\n…" : string.Empty));
            }

            if (!EditorUtility.DisplayDialog(Tr("登记样例到 Addressables"), detail, Tr("确定"), Tr("取消")))
                return Tr("已取消，未做任何改动。");

            return ApplyRegistration(settings, guid, group, existing, conflicts.Count);
        }

        /// <summary>
        /// 真正写入 Addressables 配置。与 <see cref="Register"/> 拆开有两个好处：
        /// 「征得同意」与「执行」各自单一职责；且本方法不含任何模态框，可被自动化测试直接调用。
        /// </summary>
        private static string ApplyRegistration(AddressableAssetSettings settings, string guid,
            AddressableAssetGroup group, AddressableAssetEntry existing, int conflictCount)
        {
            // 已存在时只改地址、不动分组：使用者可能特意把它放在别的分组里，没理由替他搬家。
            var entry = existing ?? settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: true);
            if (entry == null)
                return Tr("创建 Addressables 条目失败。请检查 Addressables 配置是否正常。");

            entry.SetAddress(DemoAddress);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true, true);
            AssetDatabase.SaveAssets();

            string result = Fmt("已登记：分组「{0}」，地址「{1}」。",
                entry.parentGroup != null ? entry.parentGroup.Name : "-", entry.address);
            if (conflictCount > 0)
                result += Fmt("（{0} 个此前单独登记过的资源保留了原地址）", conflictCount);

            Debug.Log("[Anim Simulator System] " + result);
            return result;
        }

        /// <summary>
        /// 找出文件夹内已被<b>单独</b>登记过的资源。
        ///
        /// <para><c>CreateSubEntryIfUnique</c> 遇到这类资源会静默跳过，它们保留旧地址、
        /// 不会变成 <c>AnimSimulator/…</c>，于是运行时按新前缀就是取不到。
        /// 不报出来的话，使用者只会看到「个别资源加载不到」而毫无线索。</para>
        /// </summary>
        private static List<string> FindIndividuallyRegistered(
            AddressableAssetSettings settings, string folderPath, string folderGuid)
        {
            var conflicts = new List<string>();
            foreach (var assetGuid in AssetDatabase.FindAssets(string.Empty, new[] { folderPath }))
            {
                if (assetGuid == folderGuid) continue;

                string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (AssetDatabase.IsValidFolder(path)) continue;

                var e = settings.FindAssetEntry(assetGuid);
                if (e != null) conflicts.Add($"{e.address}  ←  {path}");
            }
            return conflicts;
        }

        #endregion
    }
}
