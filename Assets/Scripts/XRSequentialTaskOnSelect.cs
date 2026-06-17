using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class XRSequentialTaskOnSelect : MonoBehaviour
{
    [System.Serializable]
    public class SequenceItem
    {
        [Header("Object")]
        public XRBaseInteractable interactable;

        [Header("Optional Dialogue")]
        [TextArea(2, 5)]
        public string correctDialogue = "";

        [Header("Optional Audio")]
        public AudioClip correctAudioClip;
        public float correctAudioDelay = 0f;
    }

    [Header("Task")]
    [SerializeField] private string taskId = "touch_three_objects";
    [SerializeField] private bool requireCurrentTask = true;
    [SerializeField] private bool completeOnlyOnce = true;

    [Header("Sequence")]
    [Tooltip("按顺序拖入三个要点击的物品。例如：照片、粉裙、医院通知单。")]
    [SerializeField] private SequenceItem[] sequence = new SequenceItem[3];

    [Tooltip("点错后是否把进度清零。开：必须从第一个重新点；关：只是提示点错，进度不变。")]
    [SerializeField] private bool resetProgressOnWrongSelect = true;

    [Tooltip("是否忽略 XR Socket 自动吸附造成的 Select。")]
    [SerializeField] private bool ignoreSocketSelect = true;

    [Header("Dialogue")]
    [TextArea(2, 5)]
    [SerializeField] private string wrongOrderDialogue = "不是这个顺序。你得按记忆发生的顺序来。";

    [TextArea(2, 5)]
    [SerializeField] private string completeDialogue = "这些记忆终于连起来了。";

    [SerializeField] private bool showDialogue = true;

    [Header("Complete Audio")]
    [SerializeField] private bool playAudioOnComplete = false;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip completeAudioClip;
    [SerializeField] private float audioDelay = 0f;
    [SerializeField] private bool useOneShot = true;
    [Range(0f, 1f)]
    [SerializeField] private float audioVolume = 1f;

    [Header("Sequence Step Audio")]
    [SerializeField] private AudioSource stepAudioSource;
    [SerializeField] private bool stopCurrentStepAudioBeforePlay = false;
    [SerializeField] private bool useOneShotForStepAudio = true;
    [Range(0f, 1f)]
    [SerializeField] private float stepAudioVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private int currentIndex = 0;
    private bool hasCompleted = false;
    private Coroutine audioRoutine;
    private Coroutine stepAudioRoutine;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (stepAudioSource == null)
            stepAudioSource = audioSource;
    }

    private void OnEnable()
    {
        RegisterListeners(true);
    }

    private void OnDisable()
    {
        RegisterListeners(false);

        if (audioRoutine != null)
        {
            StopCoroutine(audioRoutine);
            audioRoutine = null;
        }

        if (stepAudioRoutine != null)
        {
            StopCoroutine(stepAudioRoutine);
            stepAudioRoutine = null;
        }
    }

    private void RegisterListeners(bool register)
    {
        if (sequence == null)
            return;

        foreach (SequenceItem item in sequence)
        {
            if (item == null || item.interactable == null)
                continue;

            if (register)
                item.interactable.selectEntered.AddListener(OnSelected);
            else
                item.interactable.selectEntered.RemoveListener(OnSelected);
        }
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (ignoreSocketSelect && args.interactorObject is XRSocketInteractor)
            return;

        if (completeOnlyOnce && hasCompleted)
            return;

        if (TaskChainManager.Instance == null)
        {
            Debug.LogWarning($"{name}: 场景里没有 TaskChainManager，无法完成任务。 ");
            return;
        }

        if (requireCurrentTask && !TaskChainManager.Instance.IsCurrentTask(taskId))
        {
            if (logDebug)
                Debug.Log($"{name}: 当前任务不是 {taskId}，忽略本次顺序点击。 ");
            return;
        }

        if (sequence == null || sequence.Length == 0)
        {
            Debug.LogWarning($"{name}: Sequence 没有配置任何物品。 ");
            return;
        }

        XRBaseInteractable selected = args.interactableObject as XRBaseInteractable;
        if (selected == null)
            return;

        if (currentIndex < 0 || currentIndex >= sequence.Length)
            currentIndex = 0;

        int selectedIndex = FindSequenceIndex(selected);
        if (selectedIndex < 0)
            return;

        if (selectedIndex == currentIndex)
        {
            HandleCorrectSelect(sequence[currentIndex]);
            return;
        }

        if (selectedIndex < currentIndex)
        {
            HandleAlreadyCompletedSelect(sequence[selectedIndex]);
            return;
        }

        HandleWrongSelect(selected);
    }

    private void HandleCorrectSelect(SequenceItem item)
    {
        if (showDialogue && item != null && !string.IsNullOrWhiteSpace(item.correctDialogue))
            DialogueOverlay.Show(FormatText(item.correctDialogue));

        PlayStepAudioWithDelay(item);

        currentIndex++;

        if (logDebug)
            Debug.Log($"{name}: 顺序点击正确，进度 {currentIndex}/{sequence.Length}");

        if (currentIndex >= sequence.Length)
            CompleteSequenceTask();
    }

    private void HandleAlreadyCompletedSelect(SequenceItem item)
    {
        if (showDialogue && item != null && !string.IsNullOrWhiteSpace(item.correctDialogue))
            DialogueOverlay.Show(FormatText(item.correctDialogue));

        PlayStepAudioWithDelay(item);

        if (logDebug)
            Debug.Log($"{name}: 点击了已完成的顺序物品，当前进度保持 {currentIndex}/{sequence.Length}");
    }

    private void HandleWrongSelect(XRBaseInteractable selected)
    {
        if (showDialogue && !string.IsNullOrWhiteSpace(wrongOrderDialogue))
            DialogueOverlay.Show(FormatText(wrongOrderDialogue));

        if (logDebug)
            Debug.Log($"{name}: 点错顺序。当前需要第 {currentIndex + 1} 个物品，但点到了 {selected.name}");

        if (resetProgressOnWrongSelect)
            currentIndex = 0;
    }

    private void CompleteSequenceTask()
    {
        hasCompleted = true;

        if (showDialogue && !string.IsNullOrWhiteSpace(completeDialogue))
            DialogueOverlay.Show(FormatText(completeDialogue));

        PlayCompleteAudioWithDelay();

        TaskChainManager.Instance.CompleteTask(taskId);

        if (logDebug)
            Debug.Log($"{name}: 三个物品顺序点击完成，已完成 task: {taskId}");
    }

    public void ResetSequenceProgress()
    {
        currentIndex = 0;
        hasCompleted = false;
    }

    private int FindSequenceIndex(XRBaseInteractable selected)
    {
        if (selected == null || sequence == null)
            return -1;

        for (int i = 0; i < sequence.Length; i++)
        {
            SequenceItem item = sequence[i];
            if (item != null && item.interactable == selected)
                return i;
        }

        return -1;
    }

    private string FormatText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Replace("\\n", "\n");
    }

    private void PlayCompleteAudioWithDelay()
    {
        if (!playAudioOnComplete)
            return;

        if (audioRoutine != null)
            StopCoroutine(audioRoutine);

        audioRoutine = StartCoroutine(PlayAudioRoutine());
    }

    private void PlayStepAudioWithDelay(SequenceItem item)
    {
        if (item == null || item.correctAudioClip == null)
            return;

        if (stepAudioRoutine != null)
            StopCoroutine(stepAudioRoutine);

        stepAudioRoutine = StartCoroutine(PlayStepAudioRoutine(item.correctAudioClip, item.correctAudioDelay));
    }

    private IEnumerator PlayStepAudioRoutine(AudioClip clip, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (stepAudioSource == null || clip == null)
        {
            Debug.LogWarning($"{name}: 想播放步骤音频，但 Step Audio Source 或 Correct Audio Clip 没有设置。 ");
            stepAudioRoutine = null;
            yield break;
        }

        if (stopCurrentStepAudioBeforePlay)
            stepAudioSource.Stop();

        if (useOneShotForStepAudio)
            stepAudioSource.PlayOneShot(clip, stepAudioVolume);
        else
        {
            stepAudioSource.clip = clip;
            stepAudioSource.volume = stepAudioVolume;
            stepAudioSource.loop = false;
            stepAudioSource.Play();
        }

        stepAudioRoutine = null;
    }

    private IEnumerator PlayAudioRoutine()
    {
        if (audioDelay > 0f)
            yield return new WaitForSecondsRealtime(audioDelay);

        if (audioSource == null || completeAudioClip == null)
        {
            Debug.LogWarning($"{name}: 想播放完成音频，但 AudioSource 或 CompleteAudioClip 没有设置。 ");
            yield break;
        }

        if (useOneShot)
            audioSource.PlayOneShot(completeAudioClip, audioVolume);
        else
        {
            audioSource.clip = completeAudioClip;
            audioSource.volume = audioVolume;
            audioSource.loop = false;
            audioSource.Play();
        }

        audioRoutine = null;
    }
}
