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

        [Tooltip("任务开始后，延迟多少秒显示 Start Dialogue。")]
        public float startDialogueDelay = 0f;

        [TextArea(2, 5)]
        public string startDialogue = "";

        [Header("TV Screen Text On Task Start")]
        public bool setTVTextOnTaskStart = false;

        [TextArea(2, 5)]
        public string tvScreenTextOnTaskStart = "";

        [Tooltip("任务开始后，延迟多少秒修改电视文字。")]
        public float tvScreenTextDelay = 0f;

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

    [Header("Checkpoint")]
    [SerializeField] private bool saveCheckpointOnTaskComplete = true;
    [SerializeField] private string checkpointPrefix = "after_";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private Coroutine taskRoutine;
    private Coroutine tvTextRoutine;
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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (autoStart)
            StartCurrentTask();
    }

    public void StartCurrentTask()
    {
        StartCurrentTask(true);
    }

    public void StartCurrentTask(bool replayStartDialogue)
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
            StopCurrentTaskRuntime();

            if (logDebug)
                Debug.Log("[TaskChainManager] 所有任务已完成。");

            return;
        }

        StopCurrentTaskRuntime();

        TaskStep task = tasks[currentTaskIndex];
        isRunning = true;

        if (task.setTVTextOnTaskStart)
            tvTextRoutine = StartCoroutine(SetTVTextRoutine(task));

        taskRoutine = StartCoroutine(TaskRoutine(task, replayStartDialogue));

        if (logDebug)
            Debug.Log($"[TaskChainManager] Start Task: {task.taskId}, index={currentTaskIndex}, replayStartDialogue={replayStartDialogue}");
    }

    public void ReplayCurrentTaskStartPrompt()
    {
        StartCurrentTask(true);
    }

    public void CompleteCurrentTask()
    {
        if (tasks == null || currentTaskIndex < 0 || currentTaskIndex >= tasks.Length)
            return;

        CompleteTask(tasks[currentTaskIndex].taskId);
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
            if (logDebug)
                Debug.Log($"[TaskChainManager] 当前任务是 {currentTask.taskId}，收到完成 {taskId}，已忽略。");

            return;
        }

        StopCurrentTaskRuntime();

        if (currentTask.showDialogueOnComplete && !string.IsNullOrWhiteSpace(currentTask.completeDialogue))
            ShowText(currentTask.completeDialogue, currentTask);

        if (logDebug)
            Debug.Log($"[TaskChainManager] Complete Task: {currentTask.taskId}, index={currentTaskIndex}");

        currentTaskIndex++;

        // 注意：必须在 currentTaskIndex++ 之后保存，这样 checkpoint 才会保存到“下一个任务阶段”。
        if (saveCheckpointOnTaskComplete)
            CheckpointManager.SaveCheckpoint(checkpointPrefix + taskId);

        if (currentTaskIndex >= tasks.Length)
        {
            if (logDebug)
                Debug.Log("[TaskChainManager] 所有任务已完成。");

            return;
        }

        StartCurrentTask(true);
    }

    public bool IsCurrentTask(string taskId)
    {
        return CurrentTaskId == taskId;
    }

    public int GetCurrentTaskIndex()
    {
        return currentTaskIndex;
    }

    public void RestoreTaskIndex(int index)
    {
        RestoreTaskIndex(index, false);
    }

    public void RestoreTaskIndex(int index, bool replayStartDialogue)
    {
        StopCurrentTaskRuntime();

        if (tasks == null || tasks.Length == 0)
        {
            currentTaskIndex = index;
            return;
        }

        currentTaskIndex = Mathf.Clamp(index, 0, tasks.Length);

        if (currentTaskIndex < tasks.Length)
            StartCurrentTask(replayStartDialogue);
    }

    public void SetTaskIndex(int index)
    {
        RestoreTaskIndex(index, true);
    }

    public void StopCurrentHint()
    {
        StopCurrentTaskRuntime();
    }

    private void StopCurrentTaskRuntime()
    {
        isRunning = false;

        if (taskRoutine != null)
        {
            StopCoroutine(taskRoutine);
            taskRoutine = null;
        }

        if (tvTextRoutine != null)
        {
            StopCoroutine(tvTextRoutine);
            tvTextRoutine = null;
        }
    }

    private IEnumerator TaskRoutine(TaskStep task, bool replayStartDialogue)
    {
        if (replayStartDialogue && task.showStartDialogue && !string.IsNullOrWhiteSpace(task.startDialogue))
        {
            if (task.startDialogueDelay > 0f)
                yield return new WaitForSecondsRealtime(task.startDialogueDelay);

            if (!isRunning)
                yield break;

            ShowText(task.startDialogue, task);
        }

        if (task.firstDelay > 0f)
            yield return new WaitForSecondsRealtime(task.firstDelay);

        if (!isRunning)
            yield break;

        while (isRunning)
        {
            if (!string.IsNullOrWhiteSpace(task.repeatingHint))
                ShowText(task.repeatingHint, task);

            if (task.repeatInterval <= 0f)
                yield break;

            yield return new WaitForSecondsRealtime(task.repeatInterval);
        }
    }

    private IEnumerator SetTVTextRoutine(TaskStep task)
    {
        if (task.tvScreenTextDelay > 0f)
            yield return new WaitForSecondsRealtime(task.tvScreenTextDelay);

        if (!isRunning)
            yield break;

        TVScreenTextManager.SetText(task.tvScreenTextOnTaskStart);
    }

    private void ShowText(string text, TaskStep task)
    {
        string finalText = FormatText(text);

        if (task.useDialogueOverlay)
            DialogueOverlay.Show(finalText);

        if (task.useOperationHintOverlay)
            OperationHintOverlay.Show(finalText, task.visibleSeconds);
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
