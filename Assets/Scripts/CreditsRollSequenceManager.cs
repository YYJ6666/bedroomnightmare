using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CreditsRollSequenceManager : MonoBehaviour
{
    [System.Serializable]
    public class CreditEntry
    {
        public string sectionTitle;

        [TextArea(1, 5)]
        public string names;
    }

    [Header("UI")]
    [SerializeField] private CanvasGroup blackScreenGroup;
    [SerializeField] private CanvasGroup creditsCanvasGroup;
    [SerializeField] private RectTransform creditsContainer;
    [SerializeField] private TMP_Text creditsText;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip backgroundMusic;

    [Header("Credits")]
    [SerializeField] private string title = "Credits";
    [SerializeField] private CreditEntry[] entries;

    [Header("Timing")]
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private float scrollSpeed = 60f;
    [SerializeField] private float startOffsetY = -1000f;
    [SerializeField] private bool useAbsoluteStartY = false;
    [SerializeField] private float startContainerY = -800f;
    [SerializeField] private float topPadding = 200f;
    [SerializeField] private float endOffsetY = -500f;
    [SerializeField] private bool useAbsoluteStopY = false;
    [SerializeField] private float stopContainerY = -128f;
    [SerializeField] private float holdDuration = 3f;
    [SerializeField] private float fadeDuration = 2f;

    [Header("Flow")]
    [SerializeField] private bool loadSceneAfterCredits = false;
    [SerializeField] private string nextSceneName;
    [SerializeField] private bool quitApplicationAfterCredits = false;

    private Coroutine playRoutine;

    private void Start()
    {
        playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        ApplyInitialState();
        BuildCreditsText();

        if (startDelay > 0f)
            yield return new WaitForSecondsRealtime(startDelay);

        if (audioSource != null && backgroundMusic != null)
        {
            audioSource.clip = backgroundMusic;
            audioSource.loop = false;
            audioSource.Play();
        }

        yield return FadeCanvasGroup(creditsCanvasGroup, 0f, 1f, Mathf.Min(1f, fadeDuration));
        yield return ScrollCredits();

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        yield return FadeCanvasGroup(creditsCanvasGroup, creditsCanvasGroup != null ? creditsCanvasGroup.alpha : 1f, 0f, fadeDuration);

        if (loadSceneAfterCredits && !string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            yield break;
        }

        if (quitApplicationAfterCredits)
            QuitGame();
    }

    private void ApplyInitialState()
    {
        if (blackScreenGroup != null)
        {
            blackScreenGroup.gameObject.SetActive(true);
            blackScreenGroup.alpha = 1f;
        }

        if (creditsCanvasGroup != null)
        {
            creditsCanvasGroup.gameObject.SetActive(true);
            creditsCanvasGroup.alpha = 0f;
            creditsCanvasGroup.transform.SetAsLastSibling();

            Graphic viewportGraphic = creditsCanvasGroup.GetComponent<Graphic>();
            if (viewportGraphic != null)
            {
                Color color = viewportGraphic.color;
                color.a = 0f;
                viewportGraphic.color = color;
            }
        }

        if (creditsContainer != null)
        {
            Vector2 anchored = creditsContainer.anchoredPosition;
            anchored.y = GetStartY();
            creditsContainer.anchoredPosition = anchored;
        }
    }

    private void BuildCreditsText()
    {
        if (creditsText == null)
            return;

        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(title))
        {
            builder.AppendLine(title.Trim());
            builder.AppendLine();
        }

        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                CreditEntry entry = entries[i];
                if (entry == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(entry.sectionTitle))
                    builder.AppendLine(entry.sectionTitle.Trim());

                if (!string.IsNullOrWhiteSpace(entry.names))
                    builder.AppendLine(entry.names.Trim().Replace("\\n", "\n"));

                if (i < entries.Length - 1)
                    builder.AppendLine().AppendLine();
            }
        }

        creditsText.text = builder.ToString().TrimEnd();
        LayoutRebuilder.ForceRebuildLayoutImmediate(creditsText.rectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(creditsContainer);
    }

    private IEnumerator ScrollCredits()
    {
        if (creditsContainer == null)
            yield break;

        Vector2 anchored = creditsContainer.anchoredPosition;
        float startY = GetStartY();
        float endY = GetEndY();

        anchored.y = startY;
        creditsContainer.anchoredPosition = anchored;

        if (scrollSpeed <= 0f)
        {
            anchored.y = endY;
            creditsContainer.anchoredPosition = anchored;
            yield break;
        }

        while (creditsContainer.anchoredPosition.y < endY)
        {
            anchored = creditsContainer.anchoredPosition;
            anchored.y += scrollSpeed * Time.unscaledDeltaTime;
            creditsContainer.anchoredPosition = anchored;
            yield return null;
        }

        anchored = creditsContainer.anchoredPosition;
        anchored.y = endY;
        creditsContainer.anchoredPosition = anchored;
    }

    private float GetViewportHeight()
    {
        RectTransform parentRect = creditsContainer != null ? creditsContainer.parent as RectTransform : null;
        return parentRect != null ? parentRect.rect.height : Screen.height;
    }

    private float GetContentHeight()
    {
        if (creditsText != null)
            return creditsText.preferredHeight;

        return creditsContainer != null ? creditsContainer.rect.height : Screen.height;
    }

    private float GetStartY()
    {
        if (useAbsoluteStartY)
            return startContainerY;

        float viewportHeight = GetViewportHeight();
        float contentHeight = GetContentHeight();
        return -viewportHeight * 0.5f - contentHeight * 0.5f + startOffsetY;
    }

    private float GetEndY()
    {
        if (useAbsoluteStopY)
            return stopContainerY;

        float viewportHeight = GetViewportHeight();
        float contentHeight = GetContentHeight();
        return viewportHeight * 0.5f - contentHeight * 0.5f - topPadding + endOffsetY;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        group.alpha = from;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
