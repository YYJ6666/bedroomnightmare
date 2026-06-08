using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

public sealed class DialogueOverlay : MonoBehaviour
{
    private static DialogueOverlay instance;

    [Header("Startup")]
    [SerializeField] private bool showOnStart = false;
    [SerializeField] private string startupText = "";

    [Header("Layout")]
    [SerializeField] private float distanceFromCamera = 0.9f;
    [SerializeField] private Vector2 canvasSize = new Vector2(900f, 160f);
    [SerializeField] private float canvasScale = 0.0012f;
    [SerializeField] private float verticalOffsetMeters = -0.25f;

    [Header("Text Style")]
    [SerializeField] private float fontSize = 32f;
    [SerializeField] private Color textColor = Color.white;

    [Header("Chinese Font")]
    [SerializeField] private string chineseFontResourcePath = "Fonts/simheiSDF";

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float visibleSeconds = 3f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    private Transform cameraTransform;
    private CanvasGroup canvasGroup;
    private TMP_Text dialogueText;
    private Coroutine fadeRoutine;
    private Coroutine autoHideRoutine;
    private Canvas canvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(nameof(DialogueOverlay));
        instance = go.AddComponent<DialogueOverlay>();
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
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopAllCoroutines();
        fadeRoutine = null;
        autoHideRoutine = null;

        if (canvasGroup != null)
            Destroy(canvasGroup.gameObject);

        canvasGroup = null;
        dialogueText = null;
        canvas = null;
    }

    private void Start()
    {
        StartCoroutine(InitializeRoutine());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(InitializeRoutine());
    }

    private IEnumerator InitializeRoutine()
    {
        float timeoutAt = Time.realtimeSinceStartup + 3f;

        while (Time.realtimeSinceStartup < timeoutAt && cameraTransform == null)
        {
            XROrigin origin = FindObjectOfType<XROrigin>(true);

            if (origin != null && origin.Camera != null)
            {
                cameraTransform = origin.Camera.transform;
                break;
            }

            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                break;
            }

            yield return null;
        }

        if (cameraTransform == null)
            yield break;

        EnsureUi();

        if (showOnStart)
            Show(startupText, true);
    }

    private void LateUpdate()
    {
        if (canvasGroup == null || cameraTransform == null)
            return;

        Transform t = canvasGroup.transform;

        t.SetPositionAndRotation(
            cameraTransform.TransformPoint(0f, verticalOffsetMeters, distanceFromCamera),
            cameraTransform.rotation
        );
    }

    private void EnsureUi()
    {
        if (canvasGroup != null && dialogueText != null)
            return;

        GameObject canvasGo = new GameObject("DialogueCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localScale = Vector3.one * canvasScale;

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cameraTransform.GetComponent<Camera>();
        canvas.sortingOrder = 6000;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(canvasGo.transform, false);

        dialogueText = textGo.AddComponent<TextMeshProUGUI>();
        dialogueText.alignment = TextAlignmentOptions.Center;
        dialogueText.fontSize = fontSize;
        dialogueText.color = textColor;
        dialogueText.enableWordWrapping = true;

        TMP_FontAsset chineseFont = Resources.Load<TMP_FontAsset>(chineseFontResourcePath);
        if (chineseFont != null)
        {
            dialogueText.font = chineseFont;
        }
        else
        {
            Debug.LogWarning($"DialogueOverlay: 没有在 Resources/{chineseFontResourcePath} 找到中文 TMP 字体。中文可能无法显示。");
        }

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    public static void Show(string text, bool instant = false)
    {
        if (instance == null)
            return;

        instance.ShowInternal(text, instant, -1f);
    }

    public static void ShowFor(string text, float visibleSecondsOverride, bool instant = false)
    {
        if (instance == null)
            return;

        instance.ShowInternal(text, instant, visibleSecondsOverride);
    }

    public static void Hide(bool instant = false)
    {
        if (instance == null)
            return;

        instance.HideInternal(instant);
    }

    private void ShowInternal(string text, bool instant, float visibleSecondsOverride)
    {
        if (dialogueText == null || canvasGroup == null)
            return;

        dialogueText.text = text ?? string.Empty;
        StartFade(1f, instant ? 0f : fadeInDuration);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        autoHideRoutine = StartCoroutine(AutoHideRoutine(visibleSecondsOverride));
    }

    private void HideInternal(bool instant)
    {
        if (canvasGroup == null)
            return;

        StartFade(0f, instant ? 0f : fadeOutDuration);
    }

    private void StartFade(float targetAlpha, float duration)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        if (duration <= 0f)
        {
            canvasGroup.alpha = Mathf.Clamp01(targetAlpha);
            fadeRoutine = null;
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeRoutine = null;
    }

    private IEnumerator AutoHideRoutine(float visibleSecondsOverride)
    {
        float duration = visibleSecondsOverride >= 0f ? visibleSecondsOverride : visibleSeconds;

        if (duration > 0f)
            yield return new WaitForSecondsRealtime(duration);

        StartFade(0f, fadeOutDuration);
        autoHideRoutine = null;
    }
}
