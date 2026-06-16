using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckpointManager : MonoBehaviour
{
    private static CheckpointManager instance;

    public static CheckpointManager Instance => instance;

    [System.Serializable]
    private class ComponentSnapshot
    {
        public string typeName;
        public string stateJson;
    }

    [System.Serializable]
    private class ObjectSnapshot
    {
        public string id;
        public bool activeSelf;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;

        public bool hasRigidbody;
        public bool useGravity;
        public bool isKinematic;
        public Vector3 velocity;
        public Vector3 angularVelocity;

        public List<ComponentSnapshot> componentSnapshots = new List<ComponentSnapshot>();
    }

    private class CheckpointSnapshot
    {
        public string checkpointId;
        public string sceneName;

        public int taskIndex;

        public string tvScreenText = "";
        public bool hasTVScreenText = false;

        public List<ObjectSnapshot> objectSnapshots = new List<ObjectSnapshot>();
    }

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    [Header("Restore")]
    [Tooltip("场景加载后等待几帧再恢复，避免被其它脚本的 Start 覆盖。")]
    [SerializeField] private int restoreDelayFrames = 3;

    private CheckpointSnapshot latestCheckpoint;
    private bool shouldRestoreAfterSceneLoad = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(nameof(CheckpointManager));
        instance = go.AddComponent<CheckpointManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!shouldRestoreAfterSceneLoad)
            return;

        if (latestCheckpoint == null)
            return;

        if (scene.name != latestCheckpoint.sceneName)
            return;

        StartCoroutine(RestoreAfterSceneLoadRoutine());
    }

    private IEnumerator RestoreAfterSceneLoadRoutine()
    {
        int frames = Mathf.Max(1, restoreDelayFrames);

        for (int i = 0; i < frames; i++)
            yield return null;

        RestoreCheckpointInternal();

        shouldRestoreAfterSceneLoad = false;
    }

    // =========================
    // Public Static Interfaces
    // =========================

    public static void SaveCheckpoint(string checkpointId)
    {
        if (instance == null)
        {
            Debug.LogWarning("[CheckpointManager] Instance 不存在，无法保存 checkpoint。");
            return;
        }

        instance.SaveCheckpointInternal(checkpointId);
    }

    public static void ResetToCheckpointOrReloadCurrentScene()
    {
        if (instance == null)
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
            return;
        }

        instance.ResetToCheckpointOrReloadCurrentSceneInternal();
    }

    public static bool HasCheckpoint()
    {
        return instance != null && instance.latestCheckpoint != null;
    }

    public static string GetLatestCheckpointId()
    {
        if (instance == null || instance.latestCheckpoint == null)
            return "";

        return instance.latestCheckpoint.checkpointId;
    }

    public static int GetLatestTaskIndex()
    {
        if (instance == null || instance.latestCheckpoint == null)
            return 0;

        return instance.latestCheckpoint.taskIndex;
    }

    public static void ClearCheckpoint()
    {
        if (instance == null)
            return;

        instance.latestCheckpoint = null;
        instance.shouldRestoreAfterSceneLoad = false;
    }

    // =========================
    // Save
    // =========================

    private void SaveCheckpointInternal(string checkpointId)
    {
        CheckpointSnapshot snapshot = new CheckpointSnapshot();

        snapshot.checkpointId = checkpointId;
        snapshot.sceneName = SceneManager.GetActiveScene().name;

        if (TaskChainManager.Instance != null)
        {
            snapshot.taskIndex = TaskChainManager.Instance.GetCurrentTaskIndex();
        }

        SaveTVScreenText(snapshot);
        SaveObjects(snapshot);

        latestCheckpoint = snapshot;

        if (logDebug)
        {
            Debug.Log(
                $"[CheckpointManager] Saved checkpoint: {checkpointId}, " +
                $"scene={snapshot.sceneName}, " +
                $"taskIndex={snapshot.taskIndex}, " +
                $"objects={snapshot.objectSnapshots.Count}"
            );
        }
    }

    private void SaveObjects(CheckpointSnapshot snapshot)
    {
        CheckpointStateObject[] objects = FindObjectsOfType<CheckpointStateObject>(true);

        foreach (CheckpointStateObject obj in objects)
        {
            if (obj == null)
                continue;

            if (string.IsNullOrWhiteSpace(obj.UniqueId))
                continue;

            Rigidbody rb = obj.GetComponent<Rigidbody>();

            ObjectSnapshot objectSnapshot = new ObjectSnapshot();

            objectSnapshot.id = obj.UniqueId;
            objectSnapshot.activeSelf = obj.gameObject.activeSelf;

            objectSnapshot.position = obj.transform.position;
            objectSnapshot.rotation = obj.transform.rotation;
            objectSnapshot.localScale = obj.transform.localScale;

            objectSnapshot.hasRigidbody = rb != null;

            if (rb != null)
            {
                objectSnapshot.useGravity = rb.useGravity;
                objectSnapshot.isKinematic = rb.isKinematic;
                objectSnapshot.velocity = rb.velocity;
                objectSnapshot.angularVelocity = rb.angularVelocity;
            }

            SaveComponentStates(obj, objectSnapshot);

            snapshot.objectSnapshots.Add(objectSnapshot);
        }
    }

    private void SaveComponentStates(CheckpointStateObject obj, ObjectSnapshot objectSnapshot)
    {
        MonoBehaviour[] behaviours = obj.GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (!(behaviours[i] is ICheckpointStateHandler handler))
                continue;

            string stateJson = handler.CaptureCheckpointState();

            if (string.IsNullOrWhiteSpace(stateJson))
                continue;

            ComponentSnapshot componentSnapshot = new ComponentSnapshot();
            componentSnapshot.typeName = behaviours[i].GetType().AssemblyQualifiedName;
            componentSnapshot.stateJson = stateJson;

            objectSnapshot.componentSnapshots.Add(componentSnapshot);
        }
    }

    private void SaveTVScreenText(CheckpointSnapshot snapshot)
    {
        // 用反射，避免你暂时没有 TVScreenTextManager 时编译报错
        System.Type type = System.Type.GetType("TVScreenTextManager");

        if (type == null)
            return;

        MethodInfo getCurrentText = type.GetMethod(
            "GetCurrentText",
            BindingFlags.Public | BindingFlags.Static
        );

        if (getCurrentText == null)
            return;

        object result = getCurrentText.Invoke(null, null);

        string text = result as string;

        snapshot.tvScreenText = text ?? "";
        snapshot.hasTVScreenText = !string.IsNullOrWhiteSpace(snapshot.tvScreenText);
    }

    // =========================
    // Reset / Restore
    // =========================

    private void ResetToCheckpointOrReloadCurrentSceneInternal()
    {
        Scene currentScene = SceneManager.GetActiveScene();

        if (latestCheckpoint == null)
        {
            if (logDebug)
                Debug.Log("[CheckpointManager] No checkpoint. Reload current scene.");

            SceneManager.LoadScene(currentScene.buildIndex);
            return;
        }

        shouldRestoreAfterSceneLoad = true;

        if (logDebug)
        {
            Debug.Log(
                $"[CheckpointManager] Reset to checkpoint: {latestCheckpoint.checkpointId}, " +
                $"scene={latestCheckpoint.sceneName}, taskIndex={latestCheckpoint.taskIndex}"
            );
        }

        SceneManager.LoadScene(latestCheckpoint.sceneName);
    }

    private void RestoreCheckpointInternal()
    {
        if (latestCheckpoint == null)
            return;

        RestoreObjects();
        RestoreTVScreenText();
        RestoreTaskIndex();

        if (logDebug)
        {
            Debug.Log(
                $"[CheckpointManager] Restored checkpoint: {latestCheckpoint.checkpointId}, " +
                $"taskIndex={latestCheckpoint.taskIndex}"
            );
        }
    }

    private void RestoreObjects()
    {
        Dictionary<string, ObjectSnapshot> snapshotMap = new Dictionary<string, ObjectSnapshot>();

        foreach (ObjectSnapshot snapshot in latestCheckpoint.objectSnapshots)
        {
            if (snapshot == null)
                continue;

            if (string.IsNullOrWhiteSpace(snapshot.id))
                continue;

            snapshotMap[snapshot.id] = snapshot;
        }

        CheckpointStateObject[] objects = FindObjectsOfType<CheckpointStateObject>(true);

        foreach (CheckpointStateObject obj in objects)
        {
            if (obj == null)
                continue;

            string id = obj.UniqueId;

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!snapshotMap.TryGetValue(id, out ObjectSnapshot snapshot))
                continue;

            obj.gameObject.SetActive(snapshot.activeSelf);

            obj.transform.position = snapshot.position;
            obj.transform.rotation = snapshot.rotation;
            obj.transform.localScale = snapshot.localScale;

            Rigidbody rb = obj.GetComponent<Rigidbody>();

            if (rb != null && snapshot.hasRigidbody)
            {
                rb.isKinematic = false;
                rb.velocity = snapshot.velocity;
                rb.angularVelocity = snapshot.angularVelocity;
                rb.useGravity = snapshot.useGravity;
                rb.isKinematic = snapshot.isKinematic;
                rb.WakeUp();
            }

            RestoreComponentStates(obj, snapshot);
        }
    }

    private void RestoreComponentStates(CheckpointStateObject obj, ObjectSnapshot snapshot)
    {
        if (snapshot.componentSnapshots == null || snapshot.componentSnapshots.Count == 0)
            return;

        MonoBehaviour[] behaviours = obj.GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (!(behaviours[i] is ICheckpointStateHandler handler))
                continue;

            string componentTypeName = behaviours[i].GetType().AssemblyQualifiedName;

            for (int j = 0; j < snapshot.componentSnapshots.Count; j++)
            {
                ComponentSnapshot componentSnapshot = snapshot.componentSnapshots[j];

                if (componentSnapshot == null)
                    continue;

                if (componentSnapshot.typeName != componentTypeName)
                    continue;

                handler.RestoreCheckpointState(componentSnapshot.stateJson);
                break;
            }
        }
    }

    private void RestoreTaskIndex()
    {
        if (TaskChainManager.Instance == null)
        {
            Debug.LogWarning("[CheckpointManager] 没有找到 TaskChainManager，无法恢复任务阶段。");
            return;
        }

        TaskChainManager.Instance.RestoreTaskIndex(latestCheckpoint.taskIndex);
    }

    private void RestoreTVScreenText()
    {
        System.Type type = System.Type.GetType("TVScreenTextManager");

        if (type == null)
            return;

        if (latestCheckpoint.hasTVScreenText)
        {
            MethodInfo setText = type.GetMethod(
                "SetText",
                BindingFlags.Public | BindingFlags.Static
            );

            if (setText != null)
                setText.Invoke(null, new object[] { latestCheckpoint.tvScreenText });
        }
        else
        {
            MethodInfo clearText = type.GetMethod(
                "ClearText",
                BindingFlags.Public | BindingFlags.Static
            );

            if (clearText != null)
                clearText.Invoke(null, null);
        }
    }
}

public interface ICheckpointStateHandler
{
    string CaptureCheckpointState();
    void RestoreCheckpointState(string stateJson);
}
