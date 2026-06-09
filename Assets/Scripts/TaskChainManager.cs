using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TaskChainManager : MonoBehaviour
{
    public static TaskChainManager Instance { get; private set; }

    [System.Serializable]
    public class TaskStep
    {
        [Header("Task")]
        public string taskId = "touch_tv";

        [TextArea(2, 5)]
        public string repeatingHint = "这里写这个任务未完成时的重复提示。";

        [Header("Hint Timing")]
        public float firstDelay = 3f;
        public float repeatInterval = 10f;
        public float visibleSeconds = 4f;

        [Header("Overlay")]
        public bool useDialogueOverlay = true;
        public bool useOperationHintOverlay = false;

        [Header("On Start Optional")]
        public bool showStartDialogue = false;

        public float startDialogueDelay = 0f;

        [TextArea(2, 5)]
        public string startDialogue = "";

        [Header("On Complete Optional")]
        public bool showDialogueOnComplete = false;

        [TextArea(2, 5)]
        public string completeDialogue = "";
    }

    [Header("Scene Filter")]
    [SerializeField] private bool onlyInGameScene = true;
    [SerializeField] private string gameSceneName = "bedroom2";

    [Header("Tasks")]
    [SerializeField] private TaskStep[] tasks;

    [Header("Runtime")]
    [SerializeField] private int currentTaskIndex = 0;
    [SerializeField] private bool autoStart = true;

    private Coroutine hintRoutine;
    private bool isRunning;

    public int CurrentTaskIndex => currentTaskIndex;

    public string CurrentTaskId
    {
        get
        {
            if (tasks == null || currentTaskIndex < 0 || currentTaskIndex >= tasks.Length)
                return "";

            return tasks[currentTaskIndex].taskId;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (autoStart)
        {
            StartCurrentTask();
        }
    }

    public void StartCurrentTask()
    {
        if (!IsInTargetScene())
            return;

        if (tasks == null || tasks.Length == 0)
        {
            Debug.LogWarning("[TaskChainManager] 没有设置任何任务。");
            return;
        }

        if (currentTaskIndex < 0 || currentTaskIndex >= tasks.Length)
        {
            Debug.Log("[TaskChainManager] 所有任务已完成。");
            return;
        }

        StopCurrentHint();

        TaskStep task = tasks[currentTaskIndex];

        isRunning = true;

        hintRoutine = StartCoroutine(TaskHintRoutine(task));

        Debug.Log($"[TaskChainManager] Start Task: {task.taskId}");
    }

    public void CompleteCurrentTask()
    {
        if (tasks == null || currentTaskIndex < 0 || currentTaskIndex >= tasks.Length)
            return;

        TaskStep task = tasks[currentTaskIndex];

        CompleteTask(task.taskId);
    }

    public void CompleteTask(string taskId)
    {
        if (tasks == null || tasks.Length == 0)
            return;

        if (currentTaskIndex < 0 || currentTaskIndex >= tasks.Length)
            return;

        TaskStep currentTask = tasks[currentTaskIndex];

        if (currentTask.taskId != taskId)
        {
            Debug.Log($"[TaskChainManager] 当前任务是 {currentTask.taskId}，收到完成 {taskId}，已忽略。");
            return;
        }

        StopCurrentHint();

        if (currentTask.showDialogueOnComplete && !string.IsNullOrWhiteSpace(currentTask.completeDialogue))
        {
            ShowText(currentTask.completeDialogue, currentTask);
        }

        Debug.Log($"[TaskChainManager] Complete Task: {currentTask.taskId}");

        currentTaskIndex++;

        if (currentTaskIndex >= tasks.Length)
        {
            Debug.Log("[TaskChainManager] 所有任务完成。");
            return;
        }

        StartCurrentTask();
    }

    public bool IsCurrentTask(string taskId)
    {
        return CurrentTaskId == taskId;
    }

    public void SetTaskIndex(int index)
    {
        if (tasks == null || tasks.Length == 0)
            return;

        currentTaskIndex = Mathf.Clamp(index, 0, tasks.Length);
        StartCurrentTask();
    }

    public void StopCurrentHint()
    {
        isRunning = false;

        if (hintRoutine != null)
        {
            StopCoroutine(hintRoutine);
            hintRoutine = null;
        }
    }

    private IEnumerator TaskHintRoutine(TaskStep task)
    {
        if (task.showStartDialogue && !string.IsNullOrWhiteSpace(task.startDialogue))
        {
            if (task.startDialogueDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(task.startDialogueDelay);
            }

            if (isRunning)
            {
                ShowText(task.startDialogue, task);
            }
        }

        if (task.firstDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(task.firstDelay);
        }

        while (isRunning)
        {
            if (!string.IsNullOrWhiteSpace(task.repeatingHint))
            {
                ShowText(task.repeatingHint, task);
            }

            if (task.repeatInterval <= 0f)
                yield break;

            yield return new WaitForSecondsRealtime(task.repeatInterval);
        }
    }

    private void ShowText(string text, TaskStep task)
    {
        string finalText = FormatText(text);

        if (task.useDialogueOverlay)
        {
            DialogueOverlay.Show(finalText);
        }

        if (task.useOperationHintOverlay)
        {
            OperationHintOverlay.Show(finalText, task.visibleSeconds);
        }
    }

    private string FormatText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Replace("\\n", "\n");
    }

    private bool IsInTargetScene()
    {
        if (!onlyInGameScene)
            return true;

        return SceneManager.GetActiveScene().name == gameSceneName;
    }
}