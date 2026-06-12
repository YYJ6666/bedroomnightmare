using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class UnusedAssetCleanerWindow : EditorWindow
{
    private class AssetItem
    {
        public bool selected;
        public string path;
        public long size;
        public UnityEngine.Object asset;
    }

    private readonly List<AssetItem> unusedAssets = new List<AssetItem>();
    private Vector2 scrollPos;

    private bool protectResources = true;
    private bool protectStreamingAssets = true;
    private bool protectPlugins = true;
    private bool protectEditor = true;
    private bool protectAddressables = true;
    private bool protectScripts = true;
    private bool protectShaders = true;
    private bool protectScenes = true;

    // 新增：保护所有可能和材质有关的资源
    private bool protectMaterialRelatedAssets = true;

    // 更安全的默认设置：不扫描贴图和材质
    private bool includeTextures = false;
    private bool includeAudio = true;
    private bool includeModels = true;
    private bool includeMaterials = false;
    private bool includePrefabs = true;
    private bool includeAnimations = true;
    private bool includeOtherAssets = false;

    private string backupFolder = "Assets/_UnusedAssetsBackup";

    [MenuItem("Tools/Unused Asset Cleaner")]
    public static void ShowWindow()
    {
        GetWindow<UnusedAssetCleanerWindow>("Unused Asset Cleaner");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Unused Asset Cleaner", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "这个工具会根据 Build Settings 中启用的 Scene 扫描依赖，并列出未被这些 Scene 引用的资源。\n\n" +
            "当前版本默认保护所有材质相关资源，包括 .mat、shader、png、jpg、tga、psd、rendertexture 等。\n\n" +
            "注意：通过 Resources.Load、Addressables、StreamingAssets、代码字符串路径加载的资源可能无法准确判断。\n\n" +
            "如果已经把资源移动到了 _UnusedAssetsBackup，建议优先使用 Force Restore All From Backup 恢复。",
            MessageType.Warning
        );

        EditorGUILayout.Space();

        DrawProtectionSettings();
        DrawIncludeSettings();

        EditorGUILayout.Space();

        backupFolder = EditorGUILayout.TextField("Backup Folder", backupFolder);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Scan Unused Assets", GUILayout.Height(30)))
        {
            ScanUnusedAssets();
        }

        if (GUILayout.Button("Select All", GUILayout.Height(30)))
        {
            SetAllSelected(true);
        }

        if (GUILayout.Button("Deselect All", GUILayout.Height(30)))
        {
            SetAllSelected(false);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        DrawSummary();

        EditorGUILayout.Space();

        DrawAssetList();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = unusedAssets.Count > 0;

        if (GUILayout.Button("Move Selected To Backup Folder", GUILayout.Height(35)))
        {
            MoveSelectedToBackup();
        }

        GUI.enabled = true;

        GUI.backgroundColor = Color.green;

        if (GUILayout.Button("Force Restore All From Backup", GUILayout.Height(35)))
        {
            ForceRestoreAllFromBackup();
        }

        GUI.backgroundColor = Color.red;

        GUI.enabled = unusedAssets.Count > 0;

        if (GUILayout.Button("Delete Selected Permanently", GUILayout.Height(35)))
        {
            DeleteSelectedAssets();
        }

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawProtectionSettings()
    {
        EditorGUILayout.LabelField("Protected Folders / Types", EditorStyles.boldLabel);

        protectResources = EditorGUILayout.ToggleLeft("Protect Resources folder", protectResources);
        protectStreamingAssets = EditorGUILayout.ToggleLeft("Protect StreamingAssets folder", protectStreamingAssets);
        protectPlugins = EditorGUILayout.ToggleLeft("Protect Plugins folder", protectPlugins);
        protectEditor = EditorGUILayout.ToggleLeft("Protect Editor folder", protectEditor);
        protectAddressables = EditorGUILayout.ToggleLeft("Protect Addressables folders", protectAddressables);
        protectScripts = EditorGUILayout.ToggleLeft("Protect C# scripts / DLL / asmdef", protectScripts);
        protectShaders = EditorGUILayout.ToggleLeft("Protect Shaders", protectShaders);
        protectScenes = EditorGUILayout.ToggleLeft("Protect Scene files", protectScenes);

        protectMaterialRelatedAssets = EditorGUILayout.ToggleLeft(
            "Protect ALL material-related assets (.mat / shader / textures / png / jpg / tga / psd / rendertexture)",
            protectMaterialRelatedAssets
        );
    }

    private void DrawIncludeSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scan Asset Types", EditorStyles.boldLabel);

        includeTextures = EditorGUILayout.ToggleLeft("Textures / Sprites", includeTextures);
        includeAudio = EditorGUILayout.ToggleLeft("Audio", includeAudio);
        includeModels = EditorGUILayout.ToggleLeft("Models", includeModels);
        includeMaterials = EditorGUILayout.ToggleLeft("Materials", includeMaterials);
        includePrefabs = EditorGUILayout.ToggleLeft("Prefabs", includePrefabs);
        includeAnimations = EditorGUILayout.ToggleLeft("Animations", includeAnimations);
        includeOtherAssets = EditorGUILayout.ToggleLeft("Other Assets", includeOtherAssets);

        EditorGUILayout.HelpBox(
            "建议保持 Textures / Sprites 和 Materials 关闭。\n" +
            "即使你打开它们，只要 Protect ALL material-related assets 是开启的，它们也不会被加入清理列表。",
            MessageType.Info
        );
    }

    private void DrawSummary()
    {
        long totalSize = 0;
        int selectedCount = 0;
        long selectedSize = 0;

        foreach (AssetItem item in unusedAssets)
        {
            totalSize += item.size;

            if (item.selected)
            {
                selectedCount++;
                selectedSize += item.size;
            }
        }

        EditorGUILayout.LabelField($"Found: {unusedAssets.Count} unused assets");
        EditorGUILayout.LabelField($"Total Size: {FormatSize(totalSize)}");
        EditorGUILayout.LabelField($"Selected: {selectedCount} assets, {FormatSize(selectedSize)}");
    }

    private void DrawAssetList()
    {
        if (unusedAssets.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "还没有扫描结果。\n\n" +
                "如果你是想恢复 _UnusedAssetsBackup 里的资源，不需要先扫描，直接点击 Force Restore All From Backup。",
                MessageType.Info
            );
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (AssetItem item in unusedAssets)
        {
            EditorGUILayout.BeginHorizontal();

            item.selected = EditorGUILayout.Toggle(item.selected, GUILayout.Width(20));

            EditorGUILayout.ObjectField(item.asset, typeof(UnityEngine.Object), false, GUILayout.Width(180));

            EditorGUILayout.LabelField(FormatSize(item.size), GUILayout.Width(80));

            EditorGUILayout.SelectableLabel(item.path, GUILayout.Height(18));

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void ScanUnusedAssets()
    {
        unusedAssets.Clear();

        string[] scenePaths = GetEnabledBuildScenePaths();

        if (scenePaths.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "No Build Scenes",
                "Build Settings 中没有启用的 Scene。\n\n请先打开 File > Build Settings，把正式使用的 Scene 加进去并勾选。",
                "OK"
            );
            return;
        }

        HashSet<string> usedAssets = new HashSet<string>();

        string[] dependencies = AssetDatabase.GetDependencies(scenePaths, true);

        foreach (string dependency in dependencies)
        {
            usedAssets.Add(NormalizePath(dependency));
        }

        string[] allGuids = AssetDatabase.FindAssets("");

        int count = 0;

        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            path = NormalizePath(path);

            if (string.IsNullOrEmpty(path))
                continue;

            if (!path.StartsWith("Assets/"))
                continue;

            if (AssetDatabase.IsValidFolder(path))
                continue;

            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                continue;

            if (path.StartsWith(NormalizePath(backupFolder) + "/"))
                continue;

            if (usedAssets.Contains(path))
                continue;

            if (ShouldProtectAsset(path))
                continue;

            if (!ShouldIncludeAssetType(path))
                continue;

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

            AssetItem item = new AssetItem
            {
                selected = false,
                path = path,
                size = GetFileSize(path),
                asset = asset
            };

            unusedAssets.Add(item);
            count++;
        }

        unusedAssets.Sort((a, b) => b.size.CompareTo(a.size));

        EditorUtility.DisplayDialog(
            "Scan Complete",
            $"扫描完成。\n找到 {count} 个疑似未使用资源。\n\n" +
            "这个版本已经默认跳过材质、Shader、贴图等资源。\n" +
            "建议先 Move Selected To Backup Folder，不要直接永久删除。",
            "OK"
        );
    }

    private string[] GetEnabledBuildScenePaths()
    {
        List<string> scenePaths = new List<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene == null)
                continue;

            if (!scene.enabled)
                continue;

            if (string.IsNullOrEmpty(scene.path))
                continue;

            scenePaths.Add(NormalizePath(scene.path));
        }

        return scenePaths.ToArray();
    }

    private bool ShouldProtectAsset(string path)
    {
        string lower = path.ToLowerInvariant();

        if (protectResources && lower.Contains("/resources/"))
            return true;

        if (protectStreamingAssets && lower.StartsWith("assets/streamingassets/"))
            return true;

        if (protectPlugins && lower.StartsWith("assets/plugins/"))
            return true;

        if (protectEditor && lower.Contains("/editor/"))
            return true;

        if (protectAddressables)
        {
            if (lower.Contains("addressableassetsdata"))
                return true;

            if (lower.Contains("/addressables/"))
                return true;
        }

        if (protectScenes && lower.EndsWith(".unity"))
            return true;

        if (protectScripts)
        {
            if (lower.EndsWith(".cs"))
                return true;

            if (lower.EndsWith(".dll"))
                return true;

            if (lower.EndsWith(".asmdef"))
                return true;

            if (lower.EndsWith(".asmref"))
                return true;
        }

        if (protectShaders)
        {
            if (lower.EndsWith(".shader"))
                return true;

            if (lower.EndsWith(".shadergraph"))
                return true;

            if (lower.EndsWith(".compute"))
                return true;

            if (lower.EndsWith(".hlsl"))
                return true;

            if (lower.EndsWith(".cginc"))
                return true;
        }

        if (protectMaterialRelatedAssets)
        {
            // Materials
            if (lower.EndsWith(".mat"))
                return true;

            if (lower.EndsWith(".physicmaterial"))
                return true;

            if (lower.EndsWith(".physicsmaterial2d"))
                return true;

            // Shaders and shader dependencies
            if (lower.EndsWith(".shader"))
                return true;

            if (lower.EndsWith(".shadergraph"))
                return true;

            if (lower.EndsWith(".compute"))
                return true;

            if (lower.EndsWith(".hlsl"))
                return true;

            if (lower.EndsWith(".cginc"))
                return true;

            // Texture files commonly used by materials / sprites / UI
            if (lower.EndsWith(".png"))
                return true;

            if (lower.EndsWith(".jpg"))
                return true;

            if (lower.EndsWith(".jpeg"))
                return true;

            if (lower.EndsWith(".tga"))
                return true;

            if (lower.EndsWith(".psd"))
                return true;

            if (lower.EndsWith(".tif"))
                return true;

            if (lower.EndsWith(".tiff"))
                return true;

            if (lower.EndsWith(".bmp"))
                return true;

            if (lower.EndsWith(".exr"))
                return true;

            if (lower.EndsWith(".hdr"))
                return true;

            // Unity texture-like assets
            if (lower.EndsWith(".cubemap"))
                return true;

            if (lower.EndsWith(".rendertexture"))
                return true;
        }

        return false;
    }

    private bool ShouldIncludeAssetType(string path)
    {
        string lower = path.ToLowerInvariant();

        if (includeTextures)
        {
            if (lower.EndsWith(".png") ||
                lower.EndsWith(".jpg") ||
                lower.EndsWith(".jpeg") ||
                lower.EndsWith(".tga") ||
                lower.EndsWith(".psd") ||
                lower.EndsWith(".tif") ||
                lower.EndsWith(".tiff") ||
                lower.EndsWith(".bmp") ||
                lower.EndsWith(".exr") ||
                lower.EndsWith(".hdr"))
            {
                return true;
            }
        }

        if (includeAudio)
        {
            if (lower.EndsWith(".wav") ||
                lower.EndsWith(".mp3") ||
                lower.EndsWith(".ogg") ||
                lower.EndsWith(".aiff"))
            {
                return true;
            }
        }

        if (includeModels)
        {
            if (lower.EndsWith(".fbx") ||
                lower.EndsWith(".obj") ||
                lower.EndsWith(".blend") ||
                lower.EndsWith(".dae") ||
                lower.EndsWith(".3ds"))
            {
                return true;
            }
        }

        if (includeMaterials)
        {
            if (lower.EndsWith(".mat") ||
                lower.EndsWith(".physicmaterial") ||
                lower.EndsWith(".physicsmaterial2d"))
            {
                return true;
            }
        }

        if (includePrefabs)
        {
            if (lower.EndsWith(".prefab"))
                return true;
        }

        if (includeAnimations)
        {
            if (lower.EndsWith(".anim") ||
                lower.EndsWith(".controller") ||
                lower.EndsWith(".overridecontroller") ||
                lower.EndsWith(".mask"))
            {
                return true;
            }
        }

        if (includeOtherAssets)
        {
            if (lower.EndsWith(".asset") ||
                lower.EndsWith(".playable") ||
                lower.EndsWith(".rendertexture") ||
                lower.EndsWith(".cubemap"))
            {
                return true;
            }
        }

        return false;
    }

    private void SetAllSelected(bool selected)
    {
        foreach (AssetItem item in unusedAssets)
        {
            item.selected = selected;
        }
    }

    private void MoveSelectedToBackup()
    {
        List<AssetItem> selectedItems = GetSelectedItems();

        if (selectedItems.Count == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "没有选中任何资源。", "OK");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "Move To Backup",
            $"确定要把 {selectedItems.Count} 个资源移动到备份文件夹吗？\n\n{backupFolder}\n\n建议移动后先测试项目。",
            "Move",
            "Cancel"
        );

        if (!confirm)
            return;

        EnsureFolderExists(backupFolder);

        int movedCount = 0;
        List<AssetItem> movedItems = new List<AssetItem>();

        foreach (AssetItem item in selectedItems)
        {
            string targetPath = backupFolder + "/" + item.path.Substring("Assets/".Length);
            string targetFolder = Path.GetDirectoryName(targetPath);
            targetFolder = NormalizePath(targetFolder);

            EnsureFolderExists(targetFolder);

            string uniqueTargetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);

            string error = AssetDatabase.MoveAsset(item.path, uniqueTargetPath);

            if (string.IsNullOrEmpty(error))
            {
                movedCount++;
                movedItems.Add(item);
            }
            else
            {
                Debug.LogError($"[UnusedAssetCleaner] Failed to move asset: {item.path}\n{error}");
            }
        }

        foreach (AssetItem item in movedItems)
        {
            unusedAssets.Remove(item);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Move Complete",
            $"已移动 {movedCount} 个资源到备份文件夹。\n\n请现在运行项目、打开场景、Build 测试。",
            "OK"
        );
    }

    private void ForceRestoreAllFromBackup()
    {
        backupFolder = NormalizePath(backupFolder);

        if (string.IsNullOrEmpty(backupFolder))
        {
            EditorUtility.DisplayDialog("Error", "Backup Folder 路径为空。", "OK");
            return;
        }

        if (!backupFolder.StartsWith("Assets/"))
        {
            EditorUtility.DisplayDialog(
                "Error",
                "Backup Folder 必须在 Assets 文件夹里面。\n\n例如：Assets/_UnusedAssetsBackup",
                "OK"
            );
            return;
        }

        string projectRoot = Directory.GetCurrentDirectory();
        string fullBackupFolder = Path.Combine(projectRoot, backupFolder);

        if (!Directory.Exists(fullBackupFolder))
        {
            EditorUtility.DisplayDialog(
                "Backup Folder Not Found",
                $"找不到备份文件夹：\n\n{backupFolder}\n\n请确认它在 Assets 目录下。",
                "OK"
            );
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "Force Restore All From Backup",
            "确定要强制恢复备份文件夹中的所有资源吗？\n\n" +
            "它会把：\n" +
            backupFolder + "/xxx\n\n" +
            "恢复到：\n" +
            "Assets/xxx\n\n" +
            "建议恢复后关闭 Unity 再重新打开项目。",
            "Restore",
            "Cancel"
        );

        if (!confirm)
            return;

        AssetDatabase.Refresh();

        string[] files = Directory.GetFiles(fullBackupFolder, "*", SearchOption.AllDirectories);

        int restoredCount = 0;
        int skippedMetaCount = 0;
        int skippedConflictCount = 0;
        int failedCount = 0;

        foreach (string fullFilePath in files)
        {
            string normalizedFullFilePath = NormalizePath(fullFilePath);

            if (normalizedFullFilePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                skippedMetaCount++;
                continue;
            }

            string backupAssetPath = AbsolutePathToAssetPath(normalizedFullFilePath);

            if (string.IsNullOrEmpty(backupAssetPath))
            {
                failedCount++;
                Debug.LogError("[UnusedAssetCleaner] Cannot convert to asset path: " + normalizedFullFilePath);
                continue;
            }

            if (!backupAssetPath.StartsWith(backupFolder + "/"))
                continue;

            string relativePath = backupAssetPath.Substring((backupFolder + "/").Length);
            string originalAssetPath = "Assets/" + relativePath;

            string originalFolder = Path.GetDirectoryName(originalAssetPath);
            originalFolder = NormalizePath(originalFolder);

            EnsureFolderExists(originalFolder);

            string fullOriginalPath = Path.Combine(projectRoot, originalAssetPath);

            if (File.Exists(fullOriginalPath))
            {
                skippedConflictCount++;
                Debug.LogWarning(
                    "[UnusedAssetCleaner] Original path already exists, skipped:\n" +
                    originalAssetPath
                );
                continue;
            }

            string error = AssetDatabase.MoveAsset(backupAssetPath, originalAssetPath);

            if (string.IsNullOrEmpty(error))
            {
                restoredCount++;
            }
            else
            {
                try
                {
                    string fullOriginalDir = Path.GetDirectoryName(fullOriginalPath);

                    if (!Directory.Exists(fullOriginalDir))
                        Directory.CreateDirectory(fullOriginalDir);

                    File.Move(normalizedFullFilePath, fullOriginalPath);

                    string sourceMeta = normalizedFullFilePath + ".meta";
                    string targetMeta = fullOriginalPath + ".meta";

                    if (File.Exists(sourceMeta) && !File.Exists(targetMeta))
                    {
                        File.Move(sourceMeta, targetMeta);
                    }

                    restoredCount++;
                }
                catch (Exception e)
                {
                    failedCount++;
                    Debug.LogError(
                        "[UnusedAssetCleaner] Failed to restore asset:\n" +
                        "From: " + backupAssetPath + "\n" +
                        "To: " + originalAssetPath + "\n" +
                        "AssetDatabase Error: " + error + "\n" +
                        "Exception: " + e
                    );
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TryDeleteEmptyBackupFolders(backupFolder);

        unusedAssets.Clear();

        EditorUtility.DisplayDialog(
            "Restore Complete",
            "恢复完成。\n\n" +
            $"成功恢复：{restoredCount} 个资源\n" +
            $"跳过 meta：{skippedMetaCount} 个\n" +
            $"原位置已有文件，跳过：{skippedConflictCount} 个\n" +
            $"失败：{failedCount} 个\n\n" +
            "接下来建议：\n" +
            "1. 关闭 Unity\n" +
            "2. 重新打开项目\n" +
            "3. 打开场景检查是否还品红\n" +
            "4. 如果还有问题，看 Console 里的 Missing Shader / Missing Material 报错",
            "OK"
        );
    }

    private void DeleteSelectedAssets()
    {
        List<AssetItem> selectedItems = GetSelectedItems();

        if (selectedItems.Count == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "没有选中任何资源。", "OK");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "Permanent Delete",
            $"确定要永久删除 {selectedItems.Count} 个资源吗？\n\n这个操作不可撤销，建议先使用 Move Selected To Backup Folder。",
            "Delete",
            "Cancel"
        );

        if (!confirm)
            return;

        int deletedCount = 0;
        List<AssetItem> deletedItems = new List<AssetItem>();

        foreach (AssetItem item in selectedItems)
        {
            bool deleted = AssetDatabase.DeleteAsset(item.path);

            if (deleted)
            {
                deletedCount++;
                deletedItems.Add(item);
            }
            else
            {
                Debug.LogError($"[UnusedAssetCleaner] Failed to delete asset: {item.path}");
            }
        }

        foreach (AssetItem item in deletedItems)
        {
            unusedAssets.Remove(item);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Delete Complete",
            $"已永久删除 {deletedCount} 个资源。",
            "OK"
        );
    }

    private List<AssetItem> GetSelectedItems()
    {
        List<AssetItem> selectedItems = new List<AssetItem>();

        foreach (AssetItem item in unusedAssets)
        {
            if (item.selected)
                selectedItems.Add(item);
        }

        return selectedItems;
    }

    private void EnsureFolderExists(string folderPath)
    {
        folderPath = NormalizePath(folderPath);

        if (string.IsNullOrEmpty(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');

        if (parts.Length == 0)
            return;

        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private void TryDeleteEmptyBackupFolders(string folderPath)
    {
        folderPath = NormalizePath(folderPath);

        if (!AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] subFolders = AssetDatabase.GetSubFolders(folderPath);

        foreach (string subFolder in subFolders)
        {
            TryDeleteEmptyBackupFolders(subFolder);
        }

        string[] assets = AssetDatabase.FindAssets("", new[] { folderPath });

        bool hasRealAsset = false;

        foreach (string guid in assets)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
                continue;

            if (path == folderPath)
                continue;

            if (!AssetDatabase.IsValidFolder(path))
            {
                hasRealAsset = true;
                break;
            }
        }

        string[] remainingSubFolders = AssetDatabase.GetSubFolders(folderPath);

        if (!hasRealAsset && remainingSubFolders.Length == 0)
        {
            AssetDatabase.DeleteAsset(folderPath);
        }
    }

    private long GetFileSize(string assetPath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);

        if (!File.Exists(fullPath))
            return 0;

        FileInfo fileInfo = new FileInfo(fullPath);
        return fileInfo.Length;
    }

    private string AbsolutePathToAssetPath(string absolutePath)
    {
        absolutePath = NormalizePath(absolutePath);

        string projectRoot = NormalizePath(Directory.GetCurrentDirectory());

        if (!absolutePath.StartsWith(projectRoot))
            return string.Empty;

        string relativePath = absolutePath.Substring(projectRoot.Length);

        if (relativePath.StartsWith("/"))
            relativePath = relativePath.Substring(1);

        return NormalizePath(relativePath);
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        return path.Replace("\\", "/");
    }

    private string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return bytes + " B";

        float kb = bytes / 1024f;

        if (kb < 1024)
            return kb.ToString("F1") + " KB";

        float mb = kb / 1024f;

        if (mb < 1024)
            return mb.ToString("F1") + " MB";

        float gb = mb / 1024f;
        return gb.ToString("F2") + " GB";
    }
}