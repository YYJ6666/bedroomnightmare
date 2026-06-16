using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

public sealed class GlobalDropResetter : MonoBehaviour
{
    private static GlobalDropResetter instance;

    [Header("Scene Filter")]
    [SerializeField] private string gameSceneName = "bedroom2";

    [Header("Detection")]
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float minWorldYToTrigger = -0.15f;
    [SerializeField] private bool enableWorldYFallback = false;
    [SerializeField] private float minDownwardSpeedToTrigger = 0.5f;
    [SerializeField] private float minSecondsAfterRelease = 0.05f;
    [SerializeField] private float ignoreSecondsAfterSceneLoad = 1f;
    [SerializeField] private bool ignoreWhileSelected = true;
    [SerializeField] private float cooldownSeconds = 0.5f;
    [SerializeField] private float rescanIntervalSeconds = 1f;
    [SerializeField] private bool logResetCause = false;
    [Header("Scream Before Reset")]
    [SerializeField] private AudioClip screamBeforeResetClip;
    [SerializeField] private float screamVolume = 1f;

    [Tooltip("落地音效结束/等待后，再延迟多久播放尖叫。")]
    [SerializeField] private float waitBeforeScream = 0.1f;

    [Tooltip("尖叫播放多久后开始黑屏。0 表示自动按尖叫音频长度等待一小段。")]
    [SerializeField] private float waitAfterScream = 0f;
    [Header("Sound")]
    [Tooltip("如果物体没有 DropResetSoundProfile，就播放这个默认声音。")]
    [SerializeField] private AudioClip defaultDropSound;
    [SerializeField] private float defaultVolume = 1f;

    [Header("Black Screen")]
    [SerializeField] private bool useBlackScreenBeforeReset = true;
    [SerializeField] private float fadeToBlackTime = 0.8f;
    [SerializeField] private float blackHoldTime = 0.35f;

    [Header("Reset")]
    [SerializeField] private float resetDelaySeconds = 0.1f;

    [Header("Checkpoint Reset")]
    [Tooltip("如果场景里存在 CheckpointManager，Reset 时优先回到最近一次检查点。")]
    [SerializeField] private bool resetToCheckpointIfAvailable = true;

    [Header("Reset Dialogue / Task Prompt")]
    [Tooltip("Reset 后重新播放当前 Task 的开始提示。需要场景里有 TaskChainManager。")]
    [SerializeField] private bool replayCurrentTaskStartPromptOnReset = true;

    [Tooltip("重新进入场景后，等待多久再尝试重放当前 Task 的开始提示。建议给 CheckpointManager 一点时间恢复任务阶段。")]
    [SerializeField] private float waitBeforeReplayTaskPromptAfterLoad = 0.25f;

    [Tooltip("如果没有找到 TaskChainManager 或无法重放当前任务提示，则使用下面的旧 Reset 台词。")]
    [SerializeField] private bool showOldResetDialogueIfTaskReplayFails = true;

    [Tooltip("Reset 重新进入场景后，先播放 Reset 开场白，再重放当前 Task 的 startDialogue，避免两段字幕互相覆盖。")]
    [SerializeField] private bool showResetDialogueBeforeTaskPrompt = true;

    [Tooltip("Reset 开场白显示后，等待多久再重放当前 Task 的 startDialogue。建议设置成和开场白可读时间接近。")]
    [SerializeField] private float waitAfterResetDialogueBeforeTaskPrompt = 3f;

    [Header("Start Dialogue")]
    [SerializeField] private bool showDialogueOnStart = true;

    [TextArea(2, 5)]
    [SerializeField] private string wakeUpDialogue =
        "......醒了？还是在做梦？ \n 房间里怎么这么黑，我好渴......";

    [TextArea(2, 6)]
    [SerializeField] private string firstRunHintDialogue =
        "太黑了，先开灯吧 \n我记得睡前在床头放了一杯水……\n （灯的开关右手边的床头柜上，先打开灯然后喝口水吧）";

    [SerializeField] private float timeBetweenFirstRunDialogues = 3f;

    [TextArea(2, 5)]
    [SerializeField] private string repeatedResetDialogue =
        "是她！是她回来了！\n （她不喜欢“我”把东西丢在地上，嘘！小心！别让她生气！）";   

    private bool resetting;
    private float lastTriggerTime;
    private float sceneLoadedAtUnscaledTime;
    private Coroutine rescanRoutine;
    private Coroutine startDialogueRoutine;
    private Coroutine resetAfterLoadRoutine;
    private string pendingResetLog;

    private CanvasGroup blackCanvasGroup;

    private int resetCount = 0;
    private bool showResetDialogueAfterLoad = false;
    private bool hasShownFirstRunHint = false;

    private XRGrabInteractable pendingResetSource;
    private Collider pendingResetGround;
    private string pendingResetReason;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(nameof(GlobalDropResetter));
        instance = go.AddComponent<GlobalDropResetter>();
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
        resetting = false;

        if (groundLayers.value == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                groundLayers = 1 << groundLayer;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (rescanRoutine != null)
        {
            StopCoroutine(rescanRoutine);
            rescanRoutine = null;
        }

        if (startDialogueRoutine != null)
        {
            StopCoroutine(startDialogueRoutine);
            startDialogueRoutine = null;
        }

        if (resetAfterLoadRoutine != null)
        {
            StopCoroutine(resetAfterLoadRoutine);
            resetAfterLoadRoutine = null;
        }
    }

    private void Start()
    {
        sceneLoadedAtUnscaledTime = Time.unscaledTime;

        if (!IsInGameScene())
            return;

        AttachWatchersInScene();
        StartRescanRoutine();
        TryShowStartDialogue();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines();

        rescanRoutine = null;
        startDialogueRoutine = null;
        resetAfterLoadRoutine = null;

        resetting = false;
        pendingResetSource = null;
        pendingResetGround = null;
        pendingResetReason = null;

        sceneLoadedAtUnscaledTime = Time.unscaledTime;

        if (blackCanvasGroup != null)
        {
            Destroy(blackCanvasGroup.gameObject);
            blackCanvasGroup = null;
        }

        if (!IsInGameScene())
            return;

        AttachWatchersInScene();
        StartRescanRoutine();

        if (showResetDialogueAfterLoad)
        {
            showResetDialogueAfterLoad = false;
            resetAfterLoadRoutine = StartCoroutine(HandleAfterResetSceneLoadedRoutine());
        }
        else
        {
            TryShowStartDialogue();
        }
    }

    private void AttachWatchersInScene()
    {
        if (!IsInGameScene())
            return;

        XRGrabInteractable[] grabbables = FindObjectsOfType<XRGrabInteractable>(true);

        for (int i = 0; i < grabbables.Length; i++)
        {
            XRGrabInteractable grabbable = grabbables[i];

            if (grabbable == null)
                continue;

            DropWatcher watcher = grabbable.GetComponent<DropWatcher>();

            if (watcher == null)
                watcher = grabbable.gameObject.AddComponent<DropWatcher>();

            watcher.Bind(this, grabbable);
        }
    }

    private void StartRescanRoutine()
    {
        if (!IsInGameScene())
            return;

        if (rescanIntervalSeconds <= 0f)
            return;

        if (rescanRoutine != null)
            StopCoroutine(rescanRoutine);

        rescanRoutine = StartCoroutine(RescanRoutine());
    }

    private IEnumerator RescanRoutine()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(rescanIntervalSeconds);

        while (true)
        {
            AttachWatchersInScene();
            yield return wait;
        }
    }

    private bool IsInGameScene()
    {
        return string.IsNullOrWhiteSpace(gameSceneName) ||
               SceneManager.GetActiveScene().name == gameSceneName;
    }

    internal bool IsGroundCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.GetComponentInParent<XROrigin>(true) != null)
            return false;

        if (!string.IsNullOrWhiteSpace(groundTag) && other.CompareTag(groundTag))
            return true;

        if (groundLayers.value != 0 && (groundLayers.value & (1 << other.gameObject.layer)) != 0)
            return true;

        return false;
    }

    internal bool ShouldTriggerByWorldY(float worldY)
    {
        return worldY <= minWorldYToTrigger;
    }

    internal float MinSecondsAfterRelease => minSecondsAfterRelease;
    internal float SceneLoadedAtUnscaledTime => sceneLoadedAtUnscaledTime;
    internal float IgnoreSecondsAfterSceneLoad => ignoreSecondsAfterSceneLoad;
    internal bool IgnoreWhileSelected => ignoreWhileSelected;
    internal bool EnableWorldYFallback => enableWorldYFallback;
    internal float MinDownwardSpeedToTrigger => minDownwardSpeedToTrigger;

    internal void TriggerReset(XRGrabInteractable source, Collider ground, string reason)
    {
        if (resetting)
            return;

        if (!IsInGameScene())
            return;

        float now = Time.unscaledTime;

        if (now - lastTriggerTime < cooldownSeconds)
            return;

        lastTriggerTime = now;

        pendingResetSource = source;
        pendingResetGround = ground;
        pendingResetReason = reason;

        if (logResetCause)
            pendingResetLog = BuildResetLog(source, ground, reason);

        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        resetting = true;

        XRGrabInteractable source = pendingResetSource;
        Collider ground = pendingResetGround;

        DisableDroppedObject(source);

        AudioClip clip = defaultDropSound;
        float volume = defaultVolume;
        float waitBeforeFade = 0.2f;

        if (source != null)
        {
            DropResetSoundProfile profile = source.GetComponent<DropResetSoundProfile>();

            if (profile != null)
            {
                if (profile.DropSound != null)
                    clip = profile.DropSound;

                volume = profile.Volume;
                waitBeforeFade = profile.GetWaitBeforeFade();
            }
            else if (clip != null)
            {
                waitBeforeFade = Mathf.Min(clip.length, 1.2f);
            }
        }

        // 1. 播放物体自己的落地声音
        PlayOneShotSound(clip, volume, "DropResetSound");

        if (waitBeforeFade > 0f)
            yield return new WaitForSecondsRealtime(waitBeforeFade);

        // 2. 播放 reset 前的尖叫声
        if (waitBeforeScream > 0f)
            yield return new WaitForSecondsRealtime(waitBeforeScream);

        PlayOneShotSound(screamBeforeResetClip, screamVolume, "ScreamBeforeResetSound");

        float screamWait = GetScreamWaitTime();

        if (screamWait > 0f)
            yield return new WaitForSecondsRealtime(screamWait);

        // 3. 黑屏
        if (useBlackScreenBeforeReset)
        {
            yield return FadeToBlack();

            if (blackHoldTime > 0f)
                yield return new WaitForSecondsRealtime(blackHoldTime);
        }

        if (resetDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(resetDelaySeconds);

        if (logResetCause && !string.IsNullOrWhiteSpace(pendingResetLog))
            Debug.LogWarning(pendingResetLog);

        resetCount++;
        showResetDialogueAfterLoad = true;

        if (resetToCheckpointIfAvailable && TryResetWithCheckpointManager())
            yield break;

        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    private IEnumerator HandleAfterResetSceneLoadedRoutine()
    {
        // 等待场景物体、TaskChainManager、CheckpointManager 都完成 Awake / Start。
        // CheckpointManager 通常会在 sceneLoaded 后再等几帧恢复 currentTaskIndex。
        yield return null;
        yield return null;
        yield return null;

        if (waitBeforeReplayTaskPromptAfterLoad > 0f)
            yield return new WaitForSecondsRealtime(waitBeforeReplayTaskPromptAfterLoad);

        bool hasShownResetDialogue = false;

        // 先播放 reset 开场白，再重放当前 task 的 startDialogue，避免两段字幕互相覆盖。
        if (showResetDialogueBeforeTaskPrompt && showDialogueOnStart)
        {
            ShowResetDialogueByCount();
            hasShownResetDialogue = true;

            if (waitAfterResetDialogueBeforeTaskPrompt > 0f)
                yield return new WaitForSecondsRealtime(waitAfterResetDialogueBeforeTaskPrompt);
        }

        bool replayedTaskPrompt = false;

        if (replayCurrentTaskStartPromptOnReset)
            replayedTaskPrompt = TryReplayCurrentTaskStartPrompt();

        // 如果没有成功重放 task 提示，而且前面还没播过 reset 开场白，就回退到旧 reset 台词。
        if (!replayedTaskPrompt && !hasShownResetDialogue && showOldResetDialogueIfTaskReplayFails)
            ShowResetDialogueByCount();

        resetAfterLoadRoutine = null;
    }

    private bool TryResetWithCheckpointManager()
    {
        Type checkpointType = FindTypeByName("CheckpointManager");

        if (checkpointType == null)
            return false;

        MethodInfo resetMethod = checkpointType.GetMethod(
            "ResetToCheckpointOrReloadCurrentScene",
            BindingFlags.Public | BindingFlags.Static
        );

        if (resetMethod == null)
            return false;

        try
        {
            resetMethod.Invoke(null, null);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GlobalDropResetter] 调用 CheckpointManager.ResetToCheckpointOrReloadCurrentScene 失败，将退回普通重载。\n{e}");
            return false;
        }
    }

    private bool TryReplayCurrentTaskStartPrompt()
    {
        Type taskManagerType = FindTypeByName("TaskChainManager");

        if (taskManagerType == null)
            return false;

        object taskManager = GetTaskManagerInstance(taskManagerType);

        if (taskManager == null)
            return false;

        // 1. 优先调用 TaskChainManager 里专门用于 reset 后重放开始提示的方法。
        //    这些方法没有参数，因此不会发生 AmbiguousMatchException。
        string[] noParameterCandidateMethodNames =
        {
            "ReplayCurrentTaskStartDialogueAfterReset",
            "ReplayCurrentTaskStartDialogue",
            "ReplayStartDialogueForCurrentTask"
        };

        for (int i = 0; i < noParameterCandidateMethodNames.Length; i++)
        {
            MethodInfo method = taskManagerType.GetMethod(
                noParameterCandidateMethodNames[i],
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null
            );

            if (method == null)
                continue;

            try
            {
                method.Invoke(taskManager, null);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GlobalDropResetter] 调用 TaskChainManager.{noParameterCandidateMethodNames[i]} 失败。\n{e}");
                return false;
            }
        }

        // 2. 如果没有专门接口，就明确调用 RestoreTaskIndex(int, bool)。
        //    这里显式指定参数类型，修复 AmbiguousMatchException。
        MethodInfo getCurrentTaskIndexMethod = taskManagerType.GetMethod(
            "GetCurrentTaskIndex",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null
        );

        MethodInfo restoreTaskIndexWithReplayMethod = taskManagerType.GetMethod(
            "RestoreTaskIndex",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new Type[] { typeof(int), typeof(bool) },
            null
        );

        if (getCurrentTaskIndexMethod != null && restoreTaskIndexWithReplayMethod != null)
        {
            try
            {
                int currentTaskIndex = (int)getCurrentTaskIndexMethod.Invoke(taskManager, null);
                restoreTaskIndexWithReplayMethod.Invoke(taskManager, new object[] { currentTaskIndex, true });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GlobalDropResetter] 调用 TaskChainManager.RestoreTaskIndex(int, bool) 失败。\n{e}");
                return false;
            }
        }

        // 3. 最后回退到 StartCurrentTask()。它会重启当前 task 的提示流程。
        MethodInfo startCurrentTaskMethod = taskManagerType.GetMethod(
            "StartCurrentTask",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null
        );

        if (startCurrentTaskMethod != null)
        {
            try
            {
                startCurrentTaskMethod.Invoke(taskManager, null);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GlobalDropResetter] 调用 TaskChainManager.StartCurrentTask 失败。\n{e}");
                return false;
            }
        }

        return false;
    }

    private object GetTaskManagerInstance(Type taskManagerType)
    {
        PropertyInfo instanceProperty = taskManagerType.GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static
        );

        if (instanceProperty != null)
        {
            object instanceValue = instanceProperty.GetValue(null, null);

            if (instanceValue != null)
                return instanceValue;
        }

        UnityEngine.Object found = UnityEngine.Object.FindObjectOfType(taskManagerType, true);
        return found;
    }

    private static Type FindTypeByName(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName);

            if (type != null)
                return type;
        }

        return null;
    }

    private void DisableDroppedObject(XRGrabInteractable source)
    {
        if (source == null)
            return;

        source.enabled = false;

        Rigidbody rb = source.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    private void PlayOneShotSound(AudioClip clip, float volume, string objectName)
    {
        if (clip == null)
            return;

        GameObject audioGo = new GameObject(objectName);
        DontDestroyOnLoad(audioGo);

        AudioSource source = audioGo.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.clip = clip;
        source.Play();

        Destroy(audioGo, clip.length + 0.5f);
    }

    private float GetScreamWaitTime()
    {
        if (screamBeforeResetClip == null)
            return 0f;

        if (waitAfterScream > 0f)
            return waitAfterScream;

        return Mathf.Min(screamBeforeResetClip.length, 1.5f);
    }

    private IEnumerator FadeToBlack()
    {
        EnsureBlackScreen();

        if (blackCanvasGroup == null)
            yield break;

        blackCanvasGroup.gameObject.SetActive(true);

        float startAlpha = blackCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeToBlackTime)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / fadeToBlackTime);
            t = t * t * (3f - 2f * t);

            blackCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);

            yield return null;
        }

        blackCanvasGroup.alpha = 1f;
    }

    private void EnsureBlackScreen()
    {
        if (blackCanvasGroup != null)
            return;

        GameObject canvasGo = new GameObject("GlobalResetBlackScreenCanvas");
        DontDestroyOnLoad(canvasGo);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99999;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        blackCanvasGroup = canvasGo.AddComponent<CanvasGroup>();
        blackCanvasGroup.alpha = 0f;
        blackCanvasGroup.interactable = false;
        blackCanvasGroup.blocksRaycasts = false;

        GameObject imageGo = new GameObject("BlackImage");
        imageGo.transform.SetParent(canvasGo.transform, false);

        Image image = imageGo.AddComponent<Image>();
        image.color = Color.black;

        RectTransform rect = imageGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // =========================
    // Dialogue Logic
    // =========================

    private void TryShowStartDialogue()
    {
        if (!showDialogueOnStart)
            return;

        if (!IsInGameScene())
            return;

        if (hasShownFirstRunHint)
            return;

        if (startDialogueRoutine != null)
            StopCoroutine(startDialogueRoutine);

        startDialogueRoutine = StartCoroutine(ShowStartDialogueRoutine());
    }

    private IEnumerator ShowStartDialogueRoutine()
    {
        hasShownFirstRunHint = true;

        yield return null;
        yield return null;
        yield return null;

        if (!string.IsNullOrWhiteSpace(wakeUpDialogue))
            DialogueOverlay.Show(wakeUpDialogue.Replace("\\n", "\n"));

        if (timeBetweenFirstRunDialogues > 0f)
            yield return new WaitForSecondsRealtime(timeBetweenFirstRunDialogues);

        if (!string.IsNullOrWhiteSpace(firstRunHintDialogue))
            DialogueOverlay.Show(firstRunHintDialogue.Replace("\\n", "\n"));

        startDialogueRoutine = null;
    }

    private void ShowWakeUpDialogueOnly()
    {
        if (!showDialogueOnStart)
            return;

        if (!IsInGameScene())
            return;

        if (!string.IsNullOrWhiteSpace(wakeUpDialogue))
            DialogueOverlay.Show(wakeUpDialogue.Replace("\\n", "\n"));
    }

    private void ShowResetDialogueByCount()
    {
        if (!showDialogueOnStart)
            return;

        if (!IsInGameScene())
            return;

        if (resetCount >= 2)
        {
            if (!string.IsNullOrWhiteSpace(repeatedResetDialogue))
                DialogueOverlay.Show(repeatedResetDialogue.Replace("\\n", "\n"));
        }
        else
        {
            ShowWakeUpDialogueOnly();
        }
    }

    private static string BuildResetLog(XRGrabInteractable source, Collider ground, string reason)
    {
        string sourceName = source != null ? GetHierarchyPath(source.transform) : "<null>";
        Vector3 pos = source != null ? source.transform.position : Vector3.zero;
        string groundName = ground != null ? GetHierarchyPath(ground.transform) : "<null>";
        int frame = Time.frameCount;
        float time = Time.unscaledTime;

        return $"[GlobalDropResetter] Reset triggered. reason={reason}, source={sourceName}, pos={pos}, ground={groundName}, frame={frame}, time={time:F3}";
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return "<null>";

        string path = t.name;
        Transform current = t.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    [DisallowMultipleComponent]
    private sealed class DropWatcher : MonoBehaviour
    {
        private GlobalDropResetter controller;
        private XRGrabInteractable grabbable;
        private Rigidbody rb;

        private float releasedAtUnscaledTime;

        public void Bind(GlobalDropResetter newController, XRGrabInteractable newGrabbable)
        {
            if (grabbable != null)
            {
                grabbable.selectExited.RemoveListener(OnSelectExited);
                grabbable.selectEntered.RemoveListener(OnSelectEntered);
            }

            controller = newController;
            grabbable = newGrabbable;

            if (grabbable != null)
            {
                grabbable.selectEntered.AddListener(OnSelectEntered);
                grabbable.selectExited.AddListener(OnSelectExited);
            }

            rb = GetComponent<Rigidbody>();
        }

        private void OnDisable()
        {
            if (grabbable != null)
            {
                grabbable.selectExited.RemoveListener(OnSelectExited);
                grabbable.selectEntered.RemoveListener(OnSelectEntered);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            releasedAtUnscaledTime = Time.unscaledTime;
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            releasedAtUnscaledTime = Time.unscaledTime;
        }

        private void Update()
        {
            if (controller == null)
                return;

            if (controller.IgnoreWhileSelected && grabbable != null && grabbable.isSelected)
                return;

            if (Time.unscaledTime - controller.SceneLoadedAtUnscaledTime < controller.IgnoreSecondsAfterSceneLoad)
                return;

            if (Time.unscaledTime - releasedAtUnscaledTime < controller.MinSecondsAfterRelease)
                return;

            if (controller.EnableWorldYFallback && controller.ShouldTriggerByWorldY(transform.position.y))
            {
                if (rb != null && rb.velocity.y < -controller.MinDownwardSpeedToTrigger)
                    controller.TriggerReset(grabbable, null, "WorldY");
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (controller == null)
                return;

            if (controller.IgnoreWhileSelected && grabbable != null && grabbable.isSelected)
                return;

            if (Time.unscaledTime - controller.SceneLoadedAtUnscaledTime < controller.IgnoreSecondsAfterSceneLoad)
                return;

            if (Time.unscaledTime - releasedAtUnscaledTime < controller.MinSecondsAfterRelease)
                return;

            if (collision == null)
                return;

            Collider other = collision.collider;

            if (other == null)
                return;

            if (other.transform.IsChildOf(transform))
                return;

            if (controller.IsGroundCollider(other))
                controller.TriggerReset(grabbable, other, "CollisionEnter");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (controller == null)
                return;

            if (controller.IgnoreWhileSelected && grabbable != null && grabbable.isSelected)
                return;

            if (Time.unscaledTime - controller.SceneLoadedAtUnscaledTime < controller.IgnoreSecondsAfterSceneLoad)
                return;

            if (Time.unscaledTime - releasedAtUnscaledTime < controller.MinSecondsAfterRelease)
                return;

            if (other == null)
                return;

            if (other.transform.IsChildOf(transform))
                return;

            if (controller.IsGroundCollider(other))
                controller.TriggerReset(grabbable, other, "TriggerEnter");
        }
    }
}