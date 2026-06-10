using UnityEditor;
using UnityEngine;

public static class FindMissingScriptsInPrefabs
{
    [MenuItem("Tools/Find Missing Scripts In All Prefabs")]
    public static void FindInAllPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

        int count = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            Component[] components = prefab.GetComponentsInChildren<Component>(true);

            foreach (Component component in components)
            {
                if (component == null)
                {
                    Debug.LogWarning($"Missing Script in prefab: {path}", prefab);
                    count++;
                    break;
                }
            }
        }

        Debug.Log($"Prefab Missing Script search finished. Count = {count}");
    }
}