using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRBaseInteractable))]
public class XRCompleteTaskOnSelect : MonoBehaviour
{
    [Header("Task")]
    [SerializeField] private string taskId = "touch_tv";

    [Header("Behavior")]
    [SerializeField] private bool completeOnlyOnce = true;
    [SerializeField] private bool ignoreSocketSelect = true;

    [Header("Optional")]
    [SerializeField] private bool requireCurrentTask = true;

    [Header("Complete Audio")]
    [SerializeField] private bool playAudioOnComplete = false;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip completeAudioClip;

    [Tooltip("完成任务后，延迟多少秒再播放音频。")]
    [SerializeField] private float audioDelay = 0f;

    [SerializeField] private bool stopCurrentAudioBeforePlay = true;
    [SerializeField] private bool useOneShot = true;

    [Range(0f, 1f)]
    [SerializeField] private float audioVolume = 1f;

    private XRBaseInteractable interactable;
    private bool hasCompleted = false;
    private Coroutine audioRoutine;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        if (interactable == null)
            interactable = GetComponent<XRBaseInteractable>();

        if (interactable != null)
            interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelected);

        if (audioRoutine != null)
        {
            StopCoroutine(audioRoutine);
            audioRoutine = null;
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
            Debug.LogWarning($"{name}: 场景里没有 TaskChainManager。");
            return;
        }

        if (requireCurrentTask && !TaskChainManager.Instance.IsCurrentTask(taskId))
        {
            Debug.Log($"{name}: 当前任务不是 {taskId}，所以不会完成。");
            return;
        }

        hasCompleted = true;

        PlayCompleteAudioWithDelay();

        TaskChainManager.Instance.CompleteTask(taskId);
    }

    private void PlayCompleteAudioWithDelay()
    {
        if (!playAudioOnComplete)
            return;

        if (audioRoutine != null)
            StopCoroutine(audioRoutine);

        audioRoutine = StartCoroutine(PlayAudioRoutine());
    }

    private IEnumerator PlayAudioRoutine()
    {
        if (audioDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(audioDelay);
        }

        PlayCompleteAudioNow();
        audioRoutine = null;
    }

    private void PlayCompleteAudioNow()
    {
        if (audioSource == null)
        {
            Debug.LogWarning($"{name}: 想播放完成音频，但没有设置 AudioSource。");
            return;
        }

        if (completeAudioClip == null)
        {
            Debug.LogWarning($"{name}: 想播放完成音频，但没有设置 Complete Audio Clip。");
            return;
        }

        if (stopCurrentAudioBeforePlay)
        {
            audioSource.Stop();
        }

        if (useOneShot)
        {
            audioSource.PlayOneShot(completeAudioClip, audioVolume);
        }
        else
        {
            audioSource.clip = completeAudioClip;
            audioSource.volume = audioVolume;
            audioSource.loop = false;
            audioSource.Play();
        }
    }
}