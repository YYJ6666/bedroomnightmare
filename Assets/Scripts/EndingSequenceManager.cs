using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndingSequenceManager : MonoBehaviour
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
    public class ImageCue
    {
        [Header("Image")]
        public Sprite sprite;

        [Tooltip("Seconds after the ending starts before this image appears.")]
        public float delay = 0f;

        [Tooltip("How long this image stays visible. Set <= 0 to keep until next image.")]
        public float duration = 0f;
    }

    [System.Serializable]
    public class EndingStep
    {
        [Header("Audio")]
        public AudioClip audioClip;

        [Header("Subtitles")]
        public SubtitleCue[] subtitles;

        [Header("Timing")]
        public float extraWaitAfterAudio = 0.5f;
    }

    [System.Serializable]
    public class EndingConfig
    {
        [Header("Ending Type")]
        public EndingData.EndingType endingType = EndingData.EndingType.None;

        [Header("Images")]
        public ImageCue[] images;

        [Header("Steps")]
        public EndingStep[] steps;
    }

    [Header("Ending Configs")]
    [SerializeField] private EndingConfig[] endings;

    [Header("Black Screen")]
    [SerializeField] private CanvasGroup blackScreenGroup;

    [Header("Image")]
    [SerializeField] private Image endingImage;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Next Scene")]
    [SerializeField] private string creditsSceneName = "CreditsScene";

    [Header("Fallback")]
    [SerializeField] private EndingData.EndingType fallbackEnding = EndingData.EndingType.Bed;

    private void Start()
    {
        StartCoroutine(PlayEndingRoutine());
    }

    private IEnumerator PlayEndingRoutine()
    {
        if (endingImage != null)
        {
            endingImage.enabled = false;
            endingImage.sprite = null;
        }

        if (blackScreenGroup != null)
        {
            blackScreenGroup.gameObject.SetActive(true);
            blackScreenGroup.alpha = 1f;
        }

        yield return null;
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        EndingConfig ending = GetCurrentEnding();

        if (ending == null)
        {
            Debug.LogError($"[EndingSequenceManager] No EndingConfig found for {EndingData.SelectedEnding}.");
            yield break;
        }

        Coroutine imageRoutine = null;

        if (endingImage != null)
        {
            endingImage.enabled = false;
            endingImage.sprite = null;
            endingImage.color = Color.white;
            endingImage.preserveAspect = true;
            endingImage.transform.SetAsLastSibling();

            imageRoutine = StartCoroutine(PlayImageRoutine(ending));
        }

        if (ending.steps != null)
        {
            for (int i = 0; i < ending.steps.Length; i++)
            {
                EndingStep step = ending.steps[i];

                if (step == null)
                    continue;

                Coroutine subtitleRoutine = StartCoroutine(PlaySubtitleRoutine(step));
                Coroutine audioRoutine = StartCoroutine(PlayAudioRoutine(step));

                yield return audioRoutine;
                yield return subtitleRoutine;
            }
        }

        if (imageRoutine != null)
        {
            StopCoroutine(imageRoutine);
        }

        DialogueOverlay.Hide(true);

        if (string.IsNullOrWhiteSpace(creditsSceneName))
        {
            Debug.LogError("[EndingSequenceManager] Credits Scene Name is empty.");
            yield break;
        }

        SceneManager.LoadScene(creditsSceneName);
    }

    private EndingConfig GetCurrentEnding()
    {
        EndingData.EndingType selected = EndingData.SelectedEnding;

        if (selected == EndingData.EndingType.None)
        {
            Debug.LogWarning($"[EndingSequenceManager] SelectedEnding is None. Use fallback: {fallbackEnding}");
            selected = fallbackEnding;
        }

        if (endings == null)
            return null;

        for (int i = 0; i < endings.Length; i++)
        {
            EndingConfig ending = endings[i];

            if (ending == null)
                continue;

            if (ending.endingType == selected)
                return ending;
        }

        return null;
    }

    private IEnumerator PlayImageRoutine(EndingConfig ending)
    {
        if (endingImage == null)
            yield break;

        if (ending == null || ending.images == null || ending.images.Length == 0)
        {
            Debug.LogError("[EndingSequenceManager] 当前结局没有设置图片序列。");
            yield break;
        }

        float currentTime = 0f;

        for (int i = 0; i < ending.images.Length; i++)
        {
            ImageCue cue = ending.images[i];

            if (cue == null || cue.sprite == null)
                continue;

            float delay = Mathf.Max(0f, cue.delay);
            float waitTime = delay - currentTime;

            if (waitTime > 0f)
            {
                yield return new WaitForSecondsRealtime(waitTime);
                currentTime += waitTime;
            }

            endingImage.sprite = cue.sprite;
            endingImage.enabled = true;
            endingImage.color = Color.white;
            endingImage.preserveAspect = true;

            float duration = Mathf.Max(0f, cue.duration);

            if (duration > 0f)
            {
                yield return new WaitForSecondsRealtime(duration);
                currentTime += duration;

                if (i + 1 >= ending.images.Length || ending.images[i + 1].delay > currentTime)
                {
                    endingImage.enabled = false;
                }
            }
        }
    }

    private IEnumerator PlayAudioRoutine(EndingStep step)
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

    private IEnumerator PlaySubtitleRoutine(EndingStep step)
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

    private static bool HasSubtitleCues(EndingStep step)
    {
        return step != null &&
               step.subtitles != null &&
               step.subtitles.Length > 0;
    }

    private static float GetSubtitleTimelineEnd(EndingStep step)
    {
        if (!HasSubtitleCues(step))
            return 0f;

        float latestEnd = 0f;

        for (int i = 0; i < step.subtitles.Length; i++)
        {
            SubtitleCue cue = step.subtitles[i];

            if (cue == null || string.IsNullOrWhiteSpace(cue.subtitle))
                continue;

            latestEnd = Mathf.Max(
                latestEnd,
                Mathf.Max(0f, cue.delay) + Mathf.Max(0f, cue.duration)
            );
        }

        return latestEnd;
    }
}