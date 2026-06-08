using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroSequenceManager : MonoBehaviour
{
    [System.Serializable]
    public class SubtitleCue
    {
        [TextArea(2, 5)]
        public string subtitle;

        [Tooltip("Seconds after the audio starts before this subtitle appears.")]
        public float delay = 0f;

        [Tooltip("How long this subtitle stays visible after it appears.")]
        public float duration = 3f;
    }

    [System.Serializable]
    public class IntroStep
    {
        [Header("Audio")]
        public AudioClip audioClip;

        [Header("Subtitles")]
        public SubtitleCue[] subtitles;

        [Header("Timing")]
        public float extraWaitAfterAudio = 0.5f;
    }

    [Header("Intro")]
    [SerializeField] private IntroStep[] steps;

    [Header("Black Screen")]
    [SerializeField] private CanvasGroup blackScreenGroup;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Next Scene")]
    [SerializeField] private string gameSceneName = "BedroomScene";

    private void Start()
    {
        StartCoroutine(PlayIntroRoutine());
    }

    private IEnumerator PlayIntroRoutine()
    {
        if (blackScreenGroup != null)
        {
            blackScreenGroup.gameObject.SetActive(true);
            blackScreenGroup.alpha = 1f;
        }

        // 等待 DialogueOverlay 初始化
        yield return null;
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        if (steps != null)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                IntroStep step = steps[i];

                if (step == null)
                    continue;

                Coroutine subtitleRoutine = StartCoroutine(PlaySubtitleRoutine(step));
                Coroutine audioRoutine = StartCoroutine(PlayAudioRoutine(step));

                yield return audioRoutine;
                yield return subtitleRoutine;
            }
        }

        DialogueOverlay.Hide(true);

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError("[IntroSequenceManager] Game Scene Name is empty.");
            yield break;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    private IEnumerator PlayAudioRoutine(IntroStep step)
    {
        if (audioSource != null && step.audioClip != null)
        {
            audioSource.clip = step.audioClip;
            audioSource.Play();

            yield return new WaitWhile(() => audioSource != null && audioSource.isPlaying);
        }
        else
        {
            float fallbackSeconds = GetSubtitleTimelineEnd(step);

            yield return new WaitForSecondsRealtime(Mathf.Max(2f, fallbackSeconds));
        }

        if (step.extraWaitAfterAudio > 0f)
            yield return new WaitForSecondsRealtime(step.extraWaitAfterAudio);
    }

    private IEnumerator PlaySubtitleRoutine(IntroStep step)
    {
        if (!HasSubtitleCues(step))
            yield break;

        float latestEnd = 0f;
        int startedCueCount = 0;

        for (int i = 0; i < step.subtitles.Length; i++)
        {
            SubtitleCue cue = step.subtitles[i];

            if (cue == null || string.IsNullOrWhiteSpace(cue.subtitle))
                continue;

            float delay = Mathf.Max(0f, cue.delay);
            float cueDuration = Mathf.Max(0f, cue.duration);

            latestEnd = Mathf.Max(latestEnd, delay + cueDuration);
            startedCueCount++;

            StartCoroutine(ShowSubtitleCueRoutine(cue, delay, cueDuration));
        }

        if (startedCueCount > 0 && latestEnd > 0f)
            yield return new WaitForSecondsRealtime(latestEnd);
    }

    private IEnumerator ShowSubtitleCueRoutine(SubtitleCue cue, float delay, float duration)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        DialogueOverlay.ShowFor(cue.subtitle.Replace("\\n", "\n"), duration);
    }

    private static bool HasSubtitleCues(IntroStep step)
    {
        return step.subtitles != null && step.subtitles.Length > 0;
    }

    private static float GetSubtitleTimelineEnd(IntroStep step)
    {
        if (!HasSubtitleCues(step))
            return 0f;

        float latestEnd = 0f;

        for (int i = 0; i < step.subtitles.Length; i++)
        {
            SubtitleCue cue = step.subtitles[i];

            if (cue == null || string.IsNullOrWhiteSpace(cue.subtitle))
                continue;

            latestEnd = Mathf.Max(latestEnd, Mathf.Max(0f, cue.delay) + Mathf.Max(0f, cue.duration));
        }

        return latestEnd;
    }
}
