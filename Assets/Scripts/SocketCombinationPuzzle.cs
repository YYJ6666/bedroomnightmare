using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SocketCombinationPuzzle : MonoBehaviour
{
    [Header("Task")]
    [SerializeField] private string taskId = "find_cloths";
    [SerializeField] private bool requireCurrentTask = true;
    [SerializeField] private bool completeOnlyOnce = true;

    [Header("Sockets")]
    [Tooltip("参与这个谜题的所有 Socket。物品放在哪个 Socket 不重要，只要总体满足即可。")]
    [SerializeField] private XRSocketInteractor[] sockets;

    [Header("Required Items")]
    [Tooltip("需要出现的所有物品 ID。顺序不重要。")]
    [SerializeField] private string[] requiredItemIds =
    {
        "correct_cloth",
        "correct_accessory"
    };

    [Header("Behavior")]
    [Tooltip("如果为 true，则每个 requiredItemId 只需要出现一次。")]
    [SerializeField] private bool requireEachItemOnce = true;

    [Tooltip("如果为 true，Socket 里出现额外物品也不影响完成。")]
    [SerializeField] private bool allowExtraItems = true;

    [Header("Timing")]
    [SerializeField] private float checkDelayAfterSocketEvent = 0.15f;

    [Header("Dialogue")]
    [SerializeField] private bool showDialogueOnComplete = false;

    [TextArea(2, 5)]
    [SerializeField] private string completeDialogue = "这样才对。";

    [Header("Complete Audio")]
    [SerializeField] private bool playAudioOnComplete = false;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip completeAudioClip;
    [SerializeField] private float audioDelay = 0f;
    [SerializeField] private bool useOneShot = true;
    [Range(0f, 1f)]
    [SerializeField] private float audioVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool hasCompleted;
    private Coroutine checkRoutine;
    private Coroutine audioRoutine;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

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

        if (audioRoutine != null)
        {
            StopCoroutine(audioRoutine);
            audioRoutine = null;
        }
    }

    private void RegisterSocketEvents()
    {
        if (sockets == null)
            return;

        foreach (XRSocketInteractor socket in sockets)
        {
            if (socket == null)
                continue;

            socket.selectEntered.AddListener(OnSocketSelectEntered);
            socket.selectExited.AddListener(OnSocketSelectExited);
        }
    }

    private void UnregisterSocketEvents()
    {
        if (sockets == null)
            return;

        foreach (XRSocketInteractor socket in sockets)
        {
            if (socket == null)
                continue;

            socket.selectEntered.RemoveListener(OnSocketSelectEntered);
            socket.selectExited.RemoveListener(OnSocketSelectExited);
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
        List<string> currentItemIds = GetCurrentSocketItemIds();

        if (logDebug)
        {
            Debug.Log($"[{name}] 当前 Socket 中的物品: {string.Join(", ", currentItemIds)}");
            Debug.Log($"[{name}] 需要的物品: {string.Join(", ", requiredItemIds)}");
        }

        if (requiredItemIds == null || requiredItemIds.Length == 0)
            return false;

        if (requireEachItemOnce)
        {
            foreach (string requiredId in requiredItemIds)
            {
                if (string.IsNullOrWhiteSpace(requiredId))
                    continue;

                if (!currentItemIds.Contains(requiredId))
                {
                    if (logDebug)
                        Debug.Log($"[{name}] 缺少物品: {requiredId}");

                    return false;
                }
            }
        }
        else
        {
            Dictionary<string, int> currentCounts = CountItems(currentItemIds);
            Dictionary<string, int> requiredCounts = CountItems(requiredItemIds);

            foreach (KeyValuePair<string, int> pair in requiredCounts)
            {
                string id = pair.Key;
                int needCount = pair.Value;

                currentCounts.TryGetValue(id, out int haveCount);

                if (haveCount < needCount)
                {
                    if (logDebug)
                        Debug.Log($"[{name}] 物品数量不足: {id}, Need={needCount}, Have={haveCount}");

                    return false;
                }
            }
        }

        if (!allowExtraItems)
        {
            foreach (string currentId in currentItemIds)
            {
                if (!ContainsId(requiredItemIds, currentId))
                {
                    if (logDebug)
                        Debug.Log($"[{name}] 出现了额外物品: {currentId}");

                    return false;
                }
            }
        }

        return true;
    }

    private List<string> GetCurrentSocketItemIds()
    {
        List<string> result = new List<string>();

        if (sockets == null)
            return result;

        foreach (XRSocketInteractor socket in sockets)
        {
            if (socket == null)
                continue;

            IXRSelectInteractable selected = socket.GetOldestInteractableSelected();

            if (selected == null)
                continue;

            Transform selectedTransform = selected.transform;

            PuzzleItemId itemId = selectedTransform.GetComponentInParent<PuzzleItemId>();

            if (itemId == null)
                itemId = selectedTransform.GetComponentInChildren<PuzzleItemId>();

            if (itemId == null)
            {
                if (logDebug)
                    Debug.Log($"[{name}] Socket {socket.name} 中的物体 {selectedTransform.name} 没有 PuzzleItemId。");

                continue;
            }

            result.Add(itemId.ItemId);
        }

        return result;
    }

    private Dictionary<string, int> CountItems(IEnumerable<string> ids)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();

        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!counts.ContainsKey(id))
                counts[id] = 0;

            counts[id]++;
        }

        return counts;
    }

    private bool ContainsId(string[] array, string id)
    {
        if (array == null)
            return false;

        foreach (string item in array)
        {
            if (item == id)
                return true;
        }

        return false;
    }

    private void CompletePuzzle()
    {
        hasCompleted = true;

        if (showDialogueOnComplete && !string.IsNullOrWhiteSpace(completeDialogue))
        {
            DialogueOverlay.Show(completeDialogue.Replace("\\n", "\n"));
        }

        PlayCompleteAudioWithDelay();

        if (logDebug)
            Debug.Log($"[{name}] 完成 Socket 组合谜题：{taskId}");

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
            yield return new WaitForSecondsRealtime(audioDelay);

        if (audioSource == null || completeAudioClip == null)
        {
            Debug.LogWarning($"{name}: 想播放完成音频，但 AudioSource 或 CompleteAudioClip 没有设置。");
            audioRoutine = null;
            yield break;
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

        audioRoutine = null;
    }

    public void ResetPuzzle()
    {
        hasCompleted = false;
        ScheduleCheck();
    }
}
