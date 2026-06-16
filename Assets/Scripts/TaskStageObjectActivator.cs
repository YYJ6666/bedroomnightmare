using UnityEngine;

public class TaskStageRevealObject : MonoBehaviour, ICheckpointStateHandler
{
    [System.Serializable]
    private class StageRevealCheckpointState
    {
        public bool hasRevealed;
    }

    [Header("Task")]
    [SerializeField] private string revealTaskId = "find_ring";

    [Header("Behaviour")]
    [SerializeField] private bool hideOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool hasRevealed = false;

    private Renderer[] renderers;
    private Collider[] colliders;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        if (hideOnStart)
            SetVisible(false);
    }

    private void Update()
    {
        if (hasRevealed)
            return;

        if (TaskChainManager.Instance == null)
            return;

        if (!TaskChainManager.Instance.IsCurrentTask(revealTaskId))
            return;

        Reveal();
    }

    private void Reveal()
    {
        hasRevealed = true;

        SetVisible(true);

        if (logDebug)
        {
            Debug.Log($"[TaskStageRevealObject] {name} revealed at {revealTaskId}");
        }
    }

    public string CaptureCheckpointState()
    {
        StageRevealCheckpointState state = new StageRevealCheckpointState();
        state.hasRevealed = hasRevealed || ShouldBeRevealedForCurrentTask();

        return JsonUtility.ToJson(state);
    }

    public void RestoreCheckpointState(string stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
            return;

        StageRevealCheckpointState state = JsonUtility.FromJson<StageRevealCheckpointState>(stateJson);

        if (state == null)
            return;

        hasRevealed = state.hasRevealed;
        SetVisible(hasRevealed);
    }

    private bool ShouldBeRevealedForCurrentTask()
    {
        if (TaskChainManager.Instance == null)
            return false;

        return TaskChainManager.Instance.IsCurrentTask(revealTaskId);
    }

    private void SetVisible(bool visible)
    {
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                    r.enabled = visible;
            }
        }

        if (colliders != null)
        {
            foreach (var c in colliders)
            {
                if (c != null)
                    c.enabled = visible;
            }
        }
    }
}
