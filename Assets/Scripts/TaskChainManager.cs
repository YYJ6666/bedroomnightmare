using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TaskChainManager : MonoBehaviour
{
    public static TaskChainManager Instance { get; private set; }

    [System.Serializable]
    public class StartDialogueEntry
    {
        [TextArea(2, 5)]
        public string dialogue = "";

        [Tooltip("留空或 <= 0 时，使用 DialogueOverlay 的默认显示时长。")]
        public float visibleSeconds = -1f;
    }

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

        [Header("Start Dialogue Sequence")]
        public StartDialogueEntry[] startDialogues;

        [Header("TV Screen Text On Task Start")]
        public bool setTVTextOnTaskStart = false;

        [TextArea(2, 5)]
        public string tvScreenTextOnTaskStart = "";

        [Tooltip("任务开始后，延迟多少秒修改电视文字。")]
        public float tvScreenTextDelay = 0f;

        [Header("On Complete Optional")]
        public bool showDialogueOnComplete = false;
        [Tooltip("任务完成后，延迟多少秒再开始播放 Complete Dialogue。")]
        public float completeDialogueDelay = 0f;

        [Tooltip("完成任务时，如果要播放 Complete Dialogue，先临时屏蔽 XRDialogueOnSelect，避免它把 Complete Dialogue 顶掉。")]
        public bool suppressXRDialogueOnSelectOnComplete = true;

        [TextArea(2, 5)]
        public string completeDialogue = "";

        [Header("Complete Dialogue Sequence")]
        public StartDialogueEntry[] completeDialogues;
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
    private Coroutine completeTaskRoutine;
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

        if (completeTaskRoutine != null)
        {
            StopCoroutine(completeTaskRoutine);
            completeTaskRoutine = null;
        }

        completeTaskRoutine = StartCoroutine(CompleteTaskRoutine(currentTask, taskId));
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

        if (completeTaskRoutine != null)
        {
            StopCoroutine(completeTaskRoutine);
            completeTaskRoutine = null;
        }
    }

    private IEnumerator TaskRoutine(TaskStep task, bool replayStartDialogue)
    {
        if (replayStartDialogue && task.showStartDialogue && HasStartDialogues(task))
        {
            if (task.startDialogueDelay > 0f)
                yield return new WaitForSecondsRealtime(task.startDialogueDelay);

            if (!isRunning)
                yield break;

            yield return PlayStartDialogueSequence(task);
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
        ShowText(text, task, -1f);
    }

    private IEnumerator CompleteTaskRoutine(TaskStep currentTask, string taskId)
    {
        if (HasCompleteDialogues(currentTask))
        {
            float suppressSeconds = GetDialogueSequenceTotalDuration(currentTask.completeDialogues, currentTask.completeDialogue)
                + Mathf.Max(0f, currentTask.completeDialogueDelay)
                + 0.2f;

            if (currentTask.suppressXRDialogueOnSelectOnComplete)
            {
                XRDialogueOnSelect.SuppressFor(suppressSeconds);
            }

            if (currentTask.completeDialogueDelay > 0f)
                yield return new WaitForSecondsRealtime(currentTask.completeDialogueDelay);

            yield return PlayCompleteDialogueSequence(currentTask);
        }

        if (logDebug)
            Debug.Log($"[TaskChainManager] Complete Task: {currentTask.taskId}, index={currentTaskIndex}");

        currentTaskIndex++;

        if (saveCheckpointOnTaskComplete)
            CheckpointManager.SaveCheckpoint(checkpointPrefix + taskId);

        if (currentTaskIndex >= tasks.Length)
        {
            if (logDebug)
                Debug.Log("[TaskChainManager] 所有任务已完成。");

            completeTaskRoutine = null;
            yield break;
        }

        completeTaskRoutine = null;
        StartCurrentTask(true);
    }

    private void ShowText(string text, TaskStep task, float dialogueVisibleSecondsOverride)
    {
        string finalText = FormatText(text);

        if (task.useDialogueOverlay)
        {
            if (dialogueVisibleSecondsOverride >= 0f)
                DialogueOverlay.ShowFor(finalText, dialogueVisibleSecondsOverride);
            else
                DialogueOverlay.Show(finalText);
        }

        if (task.useOperationHintOverlay)
        {
            float operationHintVisibleSeconds = dialogueVisibleSecondsOverride >= 0f
                ? dialogueVisibleSecondsOverride
                : task.visibleSeconds;

            OperationHintOverlay.Show(finalText, operationHintVisibleSeconds);
        }
    }

    private string FormatText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Replace("\\n", "\n");
    }

    private static bool HasStartDialogues(TaskStep task)
    {
        if (task == null || !task.showStartDialogue)
            return false;

        if (task.startDialogues != null)
        {
            for (int i = 0; i < task.startDialogues.Length; i++)
            {
                StartDialogueEntry entry = task.startDialogues[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.dialogue))
                    return true;
            }
        }

        return !string.IsNullOrWhiteSpace(task.startDialogue);
    }

    private static bool HasCompleteDialogues(TaskStep task)
    {
        if (task == null || !task.showDialogueOnComplete)
            return false;

        if (task.completeDialogues != null)
        {
            for (int i = 0; i < task.completeDialogues.Length; i++)
            {
                StartDialogueEntry entry = task.completeDialogues[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.dialogue))
                    return true;
            }
        }

        return !string.IsNullOrWhiteSpace(task.completeDialogue);
    }

    private IEnumerator PlayStartDialogueSequence(TaskStep task)
    {
        bool playedAny = false;

        if (task.startDialogues != null && task.startDialogues.Length > 0)
        {
            for (int i = 0; i < task.startDialogues.Length; i++)
            {
                StartDialogueEntry entry = task.startDialogues[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.dialogue))
                    continue;

                yield return PlaySingleStartDialogue(task, entry.dialogue, entry.visibleSeconds);
                playedAny = true;

                if (!isRunning)
                    yield break;
            }
        }

        if (!playedAny && !string.IsNullOrWhiteSpace(task.startDialogue))
            yield return PlaySingleStartDialogue(task, task.startDialogue, -1f);
    }

    private IEnumerator PlayCompleteDialogueSequence(TaskStep task)
    {
        bool playedAny = false;

        if (task.completeDialogues != null && task.completeDialogues.Length > 0)
        {
            for (int i = 0; i < task.completeDialogues.Length; i++)
            {
                StartDialogueEntry entry = task.completeDialogues[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.dialogue))
                    continue;

                yield return PlaySingleStartDialogue(task, entry.dialogue, entry.visibleSeconds);
                playedAny = true;

                if (!isRunning && completeTaskRoutine == null)
                    yield break;
            }
        }

        if (!playedAny && !string.IsNullOrWhiteSpace(task.completeDialogue))
            yield return PlaySingleStartDialogue(task, task.completeDialogue, -1f);
    }

    private IEnumerator PlaySingleStartDialogue(TaskStep task, string text, float visibleSecondsOverride)
    {
        float visibleSeconds = visibleSecondsOverride > 0f
            ? visibleSecondsOverride
            : DialogueOverlay.DefaultVisibleSeconds;

        ShowText(text, task, visibleSeconds);

        float waitSeconds = visibleSeconds + DialogueOverlay.DefaultFadeOutDuration;
        if (waitSeconds > 0f)
            yield return new WaitForSecondsRealtime(waitSeconds);
    }

    private float GetDialogueSequenceTotalDuration(StartDialogueEntry[] entries, string fallbackDialogue)
    {
        float total = 0f;
        bool countedAny = false;

        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                StartDialogueEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.dialogue))
                    continue;

                float visibleSeconds = entry.visibleSeconds > 0f
                    ? entry.visibleSeconds
                    : DialogueOverlay.DefaultVisibleSeconds;

                total += visibleSeconds + DialogueOverlay.DefaultFadeOutDuration;
                countedAny = true;
            }
        }

        if (!countedAny && !string.IsNullOrWhiteSpace(fallbackDialogue))
            total = DialogueOverlay.DefaultVisibleSeconds + DialogueOverlay.DefaultFadeOutDuration;

        return total;
    }

    private bool IsInTargetScene()
    {
        if (!onlyInGameScene)
            return true;

        return SceneManager.GetActiveScene().name == gameSceneName;
    }
}
