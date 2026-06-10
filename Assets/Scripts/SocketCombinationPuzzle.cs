using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SocketCombinationPuzzle : MonoBehaviour
{
    [System.Serializable]
    public class SocketRequirement
    {
        [Header("Socket")]
        public XRSocketInteractor socket;

        [Header("Required Item")]
        public string requiredItemId = "correct_cloth";

        public bool IsSatisfied()
        {
            if (socket == null)
                return false;

            IXRSelectInteractable selected = socket.GetOldestInteractableSelected();

            if (selected == null)
                return false;

            Transform selectedTransform = selected.transform;

            PuzzleItemId itemId =
                selectedTransform.GetComponentInParent<PuzzleItemId>();

            if (itemId == null)
                itemId = selectedTransform.GetComponentInChildren<PuzzleItemId>();

            if (itemId == null)
                return false;

            return itemId.ItemId == requiredItemId;
        }

        public string GetCurrentItemName()
        {
            if (socket == null)
                return "<No Socket>";

            IXRSelectInteractable selected = socket.GetOldestInteractableSelected();

            if (selected == null)
                return "<Empty>";

            return selected.transform.name;
        }
    }

    [Header("Task")]
    [SerializeField] private string taskId = "find_cloths";
    [SerializeField] private bool requireCurrentTask = true;
    [SerializeField] private bool completeOnlyOnce = true;

    [Header("Requirements")]
    [SerializeField] private SocketRequirement[] requirements;

    [Header("Timing")]
    [SerializeField] private float checkDelayAfterSocketEvent = 0.15f;

    [Header("Dialogue")]
    [SerializeField] private bool showDialogueOnComplete = false;

    [TextArea(2, 5)]
    [SerializeField] private string completeDialogue = "这样才对。";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool hasCompleted;
    private Coroutine checkRoutine;

    private void OnEnable()
    {
        RegisterSocketEvents();
        ScheduleCheck();
    }

    private void OnDisable()
    {
        UnregisterSocketEvents();

        if (checkRoutine != null)
        {
            StopCoroutine(checkRoutine);
            checkRoutine = null;
        }
    }

    private void RegisterSocketEvents()
    {
        if (requirements == null)
            return;

        foreach (SocketRequirement requirement in requirements)
        {
            if (requirement == null || requirement.socket == null)
                continue;

            requirement.socket.selectEntered.AddListener(OnSocketSelectEntered);
            requirement.socket.selectExited.AddListener(OnSocketSelectExited);
        }
    }

    private void UnregisterSocketEvents()
    {
        if (requirements == null)
            return;

        foreach (SocketRequirement requirement in requirements)
        {
            if (requirement == null || requirement.socket == null)
                continue;

            requirement.socket.selectEntered.RemoveListener(OnSocketSelectEntered);
            requirement.socket.selectExited.RemoveListener(OnSocketSelectExited);
        }
    }

    private void OnSocketSelectEntered(SelectEnterEventArgs args)
    {
        ScheduleCheck();
    }

    private void OnSocketSelectExited(SelectExitEventArgs args)
    {
        ScheduleCheck();
    }

    private void ScheduleCheck()
    {
        if (checkRoutine != null)
            StopCoroutine(checkRoutine);

        checkRoutine = StartCoroutine(CheckRoutine());
    }

    private IEnumerator CheckRoutine()
    {
        if (checkDelayAfterSocketEvent > 0f)
            yield return new WaitForSecondsRealtime(checkDelayAfterSocketEvent);

        CheckPuzzle();
        checkRoutine = null;
    }

    public void CheckPuzzle()
    {
        if (completeOnlyOnce && hasCompleted)
            return;

        if (TaskChainManager.Instance == null)
        {
            if (logDebug)
                Debug.LogWarning($"{name}: 场景里没有 TaskChainManager。");

            return;
        }

        if (requireCurrentTask && !TaskChainManager.Instance.IsCurrentTask(taskId))
        {
            if (logDebug)
                Debug.Log($"{name}: 当前任务不是 {taskId}，不会完成。");

            return;
        }

        if (!AllRequirementsSatisfied())
            return;

        CompletePuzzle();
    }

    private bool AllRequirementsSatisfied()
    {
        if (requirements == null || requirements.Length == 0)
            return false;

        foreach (SocketRequirement requirement in requirements)
        {
            if (requirement == null)
                return false;

            bool ok = requirement.IsSatisfied();

            if (logDebug)
            {
                string socketName = requirement.socket != null ? requirement.socket.name : "<No Socket>";
                string current = requirement.GetCurrentItemName();

                Debug.Log(
                    $"[{name}] Socket={socketName}, Current={current}, " +
                    $"Need={requirement.requiredItemId}, OK={ok}"
                );
            }

            if (!ok)
                return false;
        }

        return true;
    }

    private void CompletePuzzle()
    {
        hasCompleted = true;

        if (showDialogueOnComplete && !string.IsNullOrWhiteSpace(completeDialogue))
        {
            DialogueOverlay.Show(completeDialogue.Replace("\\n", "\n"));
        }

        if (logDebug)
            Debug.Log($"[{name}] 完成 Socket 组合谜题：{taskId}");

        TaskChainManager.Instance.CompleteTask(taskId);
    }
}