using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

public sealed class GlobalDropResetter : MonoBehaviour
{
    private static GlobalDropResetter instance;

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

    [Header("Scene Filter")]
    [SerializeField] private string gameSceneName = "bedroom2";

    [Header("Reset")]
    [SerializeField] private float resetDelaySeconds = 0.1f;

    [Header("Dialogue")]
    [SerializeField] private bool showDialogueOnStart = true;

    [TextArea(2, 5)]
    [SerializeField] private string wakeUpDialogue =
        "又做了一场梦吗。\n房间里好黑，我好渴。";
    
    [TextArea(2, 5)]
    [SerializeField] private string repeatedResetDialogue =
    "她回来了。\n她不喜欢我把东西丢在地上，\n别让她发现。";

    [TextArea(2, 5)]
    [SerializeField] private string firstRunHintDialogue =
        "灯的开关在右手边床头柜上，\n我记得睡前我还放了一杯水……\n把灯打开然后喝水吧。";

    [SerializeField] private float timeBetweenFirstRunDialogues = 4f;

    private bool resetting;
    private int resetCount = 0;
    private float lastTriggerTime;
    private float sceneLoadedAtUnscaledTime;
    private Coroutine rescanRoutine;
    private Coroutine startDialogueRoutine;
    private string pendingResetLog;

    private bool IsInGameScene()
    {
        return string.IsNullOrWhiteSpace(gameSceneName) ||
            SceneManager.GetActiveScene().name == gameSceneName;
    }

    private bool hasShownFirstRunHint = false;
    private bool showResetDialogueAfterLoad = false;

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

        resetting = false;
        sceneLoadedAtUnscaledTime = Time.unscaledTime;

        if (!IsInGameScene())
        {
            return;
        }

        AttachWatchersInScene();
        StartRescanRoutine();

        if (showResetDialogueAfterLoad)
        {
            showResetDialogueAfterLoad = false;
            ShowResetDialogueByCount();
        }
        else
        {
            TryShowStartDialogue();
        }
    }

    private void AttachWatchersInScene()
    {
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

    private void TryShowStartDialogue()
    {
        if (!showDialogueOnStart)
            return;

        if (startDialogueRoutine != null)
            return;

        startDialogueRoutine = StartCoroutine(ShowStartDialogueRoutine());
    }

    private IEnumerator ShowStartDialogueRoutine()
    {
        yield return null;
        yield return null;
        yield return null;

        ShowWakeUpDialogueOnly();

        if (!hasShownFirstRunHint)
        {
            hasShownFirstRunHint = true;

            if (timeBetweenFirstRunDialogues > 0f)
                yield return new WaitForSecondsRealtime(timeBetweenFirstRunDialogues);

            if (!string.IsNullOrWhiteSpace(firstRunHintDialogue))
            {
                DialogueOverlay.Show(firstRunHintDialogue);
            }
        }

        startDialogueRoutine = null;
    }

    private void ShowWakeUpDialogueOnly()
    {
        if (!string.IsNullOrWhiteSpace(wakeUpDialogue))
        {
            DialogueOverlay.Show(wakeUpDialogue);
        }
    }
    private void ShowResetDialogueByCount()
    {
        if (resetCount >= 2)
        {
            if (!string.IsNullOrWhiteSpace(repeatedResetDialogue))
            {
                DialogueOverlay.Show(repeatedResetDialogue);
            }
        }
        else
        {
            ShowWakeUpDialogueOnly();
        }
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

        float now = Time.unscaledTime;

        if (now - lastTriggerTime < cooldownSeconds)
            return;

        lastTriggerTime = now;

        if (logResetCause)
            pendingResetLog = BuildResetLog(source, ground, reason);

        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        resetting = true;

        if (resetDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(resetDelaySeconds);

        if (logResetCause && !string.IsNullOrWhiteSpace(pendingResetLog))
            Debug.LogWarning(pendingResetLog);
        resetCount++;
        showResetDialogueAfterLoad = true;

        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
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