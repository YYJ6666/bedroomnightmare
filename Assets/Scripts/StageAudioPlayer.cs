using System.Collections;
using UnityEngine;

public class MultiTaskStageAudioSequencePlayer : MonoBehaviour
{
    [System.Serializable]
    public class AudioStep
    {
        [Header("Audio")]
        public AudioClip audioClip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Header("Timing")]
        [Tooltip("播放这段音频前等待多久。")]
        public float delayBefore = 0f;

        [Tooltip("是否等待这段音频播完后再进入下一段。")]
        public bool waitUntilAudioEnds = true;

        [Tooltip("这段音频结束后额外等待多久。")]
        public float delayAfter = 0f;
    }

    [System.Serializable]
    public class TaskAudioSequence
    {
        [Header("Task")]
        public string taskId = "restore_memory";

        [Header("Audio Source Override")]
        [Tooltip("可不填。不填时使用管理器上的 Default Audio Source。")]
        public AudioSource audioSourceOverride;

        [Header("Audio Sequence")]
        public AudioStep[] audioSteps;

        [Header("Behavior")]
        [Tooltip("勾选后，这个任务阶段在本次场景运行中只播放一次。")]
        public bool playOnlyOnce = true;

        [Tooltip("如果离开该任务阶段，是否立刻停止当前音频序列。")]
        public bool stopWhenTaskEnds = true;

        [Tooltip("如果该阶段音频正在播放，又重新进入该阶段，是否从头重播。")]
        public bool restartIfAlreadyPlaying = false;

        [Header("Debug")]
        public bool logDebug = true;

        [System.NonSerialized] public bool hasPlayed;
        [System.NonSerialized] public bool wasInTask;
        [System.NonSerialized] public Coroutine routine;
    }

    [Header("Audio")]
    [SerializeField] private AudioSource defaultAudioSource;

    [Header("Task Audio Sequences")]
    [SerializeField] private TaskAudioSequence[] taskSequences;

    [Header("Debug")]
    [SerializeField] private bool logManagerDebug = true;

    private void Awake()
    {
        if (defaultAudioSource == null)
            defaultAudioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (TaskChainManager.Instance == null)
            return;

        if (taskSequences == null || taskSequences.Length == 0)
            return;

        for (int i = 0; i < taskSequences.Length; i++)
        {
            TaskAudioSequence sequence = taskSequences[i];

            if (sequence == null)
                continue;

            bool isInTask = TaskChainManager.Instance.IsCurrentTask(sequence.taskId);

            if (isInTask && !sequence.wasInTask)
            {
                OnEnterTask(sequence);
            }

            if (!isInTask && sequence.wasInTask)
            {
                OnExitTask(sequence);
            }

            sequence.wasInTask = isInTask;
        }
    }

    private void OnEnterTask(TaskAudioSequence sequence)
    {
        if (sequence.playOnlyOnce && sequence.hasPlayed)
            return;

        if (sequence.routine != null)
        {
            if (!sequence.restartIfAlreadyPlaying)
                return;

            StopCoroutine(sequence.routine);
            sequence.routine = null;

            AudioSource source = GetAudioSource(sequence);
            if (source != null)
                source.Stop();
        }

        sequence.routine = StartCoroutine(PlaySequenceRoutine(sequence));
    }

    private void OnExitTask(TaskAudioSequence sequence)
    {
        if (!sequence.stopWhenTaskEnds)
            return;

        if (sequence.routine != null)
        {
            StopCoroutine(sequence.routine);
            sequence.routine = null;
        }

        AudioSource source = GetAudioSource(sequence);
        if (source != null)
            source.Stop();

        if (sequence.logDebug)
            Debug.Log($"[MultiTaskStageAudioSequencePlayer] 离开任务阶段 {sequence.taskId}，停止音频序列。");
    }

    private IEnumerator PlaySequenceRoutine(TaskAudioSequence sequence)
    {
        AudioSource source = GetAudioSource(sequence);

        if (source == null)
        {
            Debug.LogWarning($"{name}: 没有设置 AudioSource。");
            sequence.routine = null;
            yield break;
        }

        if (sequence.audioSteps == null || sequence.audioSteps.Length == 0)
        {
            Debug.LogWarning($"{name}: 任务 {sequence.taskId} 没有设置 Audio Steps。");
            sequence.routine = null;
            yield break;
        }

        sequence.hasPlayed = true;

        if (sequence.logDebug)
            Debug.Log($"[MultiTaskStageAudioSequencePlayer] 进入任务阶段 {sequence.taskId}，开始播放音频序列。");

        for (int i = 0; i < sequence.audioSteps.Length; i++)
        {
            AudioStep step = sequence.audioSteps[i];

            if (step == null)
                continue;

            if (sequence.stopWhenTaskEnds && !IsStillInTask(sequence))
                break;

            if (step.delayBefore > 0f)
            {
                yield return WaitRealtimeInterruptible(step.delayBefore, sequence);

                if (sequence.stopWhenTaskEnds && !IsStillInTask(sequence))
                    break;
            }

            if (step.audioClip == null)
            {
                if (sequence.logDebug)
                    Debug.LogWarning($"{name}: 任务 {sequence.taskId} 的 Audio Step {i} 没有设置 AudioClip。");

                continue;
            }

            source.Stop();
            source.clip = step.audioClip;
            source.volume = step.volume;
            source.loop = false;
            source.Play();

            if (sequence.logDebug)
            {
                Debug.Log(
                    $"[MultiTaskStageAudioSequencePlayer] 任务 {sequence.taskId} 播放第 {i + 1} 段音频：{step.audioClip.name}"
                );
            }

            if (step.waitUntilAudioEnds)
            {
                while (source != null && source.isPlaying)
                {
                    if (sequence.stopWhenTaskEnds && !IsStillInTask(sequence))
                    {
                        source.Stop();
                        sequence.routine = null;
                        yield break;
                    }

                    yield return null;
                }
            }

            if (step.delayAfter > 0f)
            {
                yield return WaitRealtimeInterruptible(step.delayAfter, sequence);

                if (sequence.stopWhenTaskEnds && !IsStillInTask(sequence))
                    break;
            }
        }

        if (sequence.logDebug)
            Debug.Log($"[MultiTaskStageAudioSequencePlayer] 任务阶段 {sequence.taskId} 的音频序列播放结束。");

        sequence.routine = null;
    }

    private IEnumerator WaitRealtimeInterruptible(float seconds, TaskAudioSequence sequence)
    {
        float endTime = Time.realtimeSinceStartup + seconds;

        while (Time.realtimeSinceStartup < endTime)
        {
            if (sequence.stopWhenTaskEnds && !IsStillInTask(sequence))
                yield break;

            yield return null;
        }
    }

    private bool IsStillInTask(TaskAudioSequence sequence)
    {
        return TaskChainManager.Instance != null &&
               TaskChainManager.Instance.IsCurrentTask(sequence.taskId);
    }

    private AudioSource GetAudioSource(TaskAudioSequence sequence)
    {
        if (sequence != null && sequence.audioSourceOverride != null)
            return sequence.audioSourceOverride;

        return defaultAudioSource;
    }

    public void StopAllSequences()
    {
        if (taskSequences == null)
            return;

        for (int i = 0; i < taskSequences.Length; i++)
        {
            TaskAudioSequence sequence = taskSequences[i];

            if (sequence == null)
                continue;

            if (sequence.routine != null)
            {
                StopCoroutine(sequence.routine);
                sequence.routine = null;
            }

            AudioSource source = GetAudioSource(sequence);
            if (source != null)
                source.Stop();
        }
    }

    public void ResetAllPlayedStates()
    {
        if (taskSequences == null)
            return;

        for (int i = 0; i < taskSequences.Length; i++)
        {
            if (taskSequences[i] != null)
                taskSequences[i].hasPlayed = false;
        }

        if (logManagerDebug)
            Debug.Log("[MultiTaskStageAudioSequencePlayer] 已重置所有任务阶段音频播放状态。");
    }
}