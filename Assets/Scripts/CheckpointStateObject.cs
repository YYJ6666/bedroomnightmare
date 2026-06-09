using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckpointStateObject : MonoBehaviour
{
    [Header("Checkpoint ID")]
    [Tooltip("每个需要保存状态的物体都必须有唯一 ID。建议手动填写。")]
    [SerializeField] private string uniqueId = "";

    public string UniqueId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(uniqueId))
                return uniqueId;

            return SceneManager.GetActiveScene().name + "/" + GetHierarchyPath(transform);
        }
    }

    private string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return "";

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}