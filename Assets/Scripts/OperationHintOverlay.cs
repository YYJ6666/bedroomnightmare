using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

public sealed class OperationHintOverlay : MonoBehaviour
{
    private static OperationHintOverlay instance;

    [Header("Startup")]
    [SerializeField] private bool showOnStart = true;

    [TextArea(2, 5)]
    [SerializeField] private string startupHint =
        "[房间里太黑了，用grab键摸索周围。]\n[确认了位置的物体会显示轮廓。在黑暗中，你很快就会失去方向。]\n[再次触摸可以重新确认位置。]";

    [SerializeField] private float startupVisibleSeconds = 60f;
    // 0 表示一直显示，不自动消失

    [Header("Scene Filter")]
    [SerializeField] private string gameSceneName = "bedroom2";

    [Header("Layout")]
    [SerializeField] private float distanceFromCamera = 0.9f;
    [SerializeField] private float horizontalOffsetMeters = 0.36f;
    [SerializeField] private float verticalOffsetMeters = 0.22f;
    [SerializeField] private Vector2 canvasSize = new Vector2(520f, 180f);
    [SerializeField] private float canvasScale = 0.0012f;

    [Header("Text Style")]
    [SerializeField] private float fontSize = 20f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private TextAlignmentOptions alignment = TextAlignmentOptions.TopRight;

    [Header("Chinese Font")]
    [SerializeField] private string chineseFontResourcePath = "Fonts/simheiSDF";

    [Header("Text Quality")]
    [SerializeField] private bool extraPadding = true;
    [SerializeField] private float dynamicPixelsPerUnit = 20f;

    [Header("Always On Top")]
    [SerializeField] private bool alwaysOnTop = true;
    [SerializeField] private int alwaysOnTopRenderQueue = 4100;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    private Transform cameraTransform;
    private CanvasGroup canvasGroup;
    private TMP_Text hintText;
    private Canvas canvas;

    private Coroutine fadeRoutine;
    private Coroutine autoHideRoutine;

    private Material hintRuntimeMaterial;

    private static readonly int ZTestModeId = Shader.PropertyToID("_ZTestMode");

    private static readonly int UnityGuiZTestModeId = Shader.PropertyToID("unity_GUIZTestMode");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(nameof(OperationHintOverlay));
        instance = go.AddComponent<OperationHintOverlay>();
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
        hintText = null;
        canvas = null;

        if (hintRuntimeMaterial != null)
        {
            Destroy(hintRuntimeMaterial);
            hintRuntimeMaterial = null;
        }
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
        if (!IsInGameScene())
        {
            Hide(true);
            yield break;
        }

        cameraTransform = null;

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

        if (showOnStart && !string.IsNullOrWhiteSpace(startupHint))
        {
            Show(startupHint, startupVisibleSeconds);
        }
    }

    private bool IsInGameScene()
    {
        return string.IsNullOrWhiteSpace(gameSceneName) ||
               SceneManager.GetActiveScene().name == gameSceneName;
    }

    private void LateUpdate()
    {
        if (canvasGroup == null || cameraTransform == null)
            return;

        Transform t = canvasGroup.transform;

        Vector3 localOffset = new Vector3(
            horizontalOffsetMeters,
            verticalOffsetMeters,
            distanceFromCamera
        );

        t.SetPositionAndRotation(
            cameraTransform.TransformPoint(localOffset),
            cameraTransform.rotation
        );
    }

    private void EnsureUi()
    {
        if (canvasGroup != null && hintText != null)
            return;

        GameObject canvasGo = new GameObject("OperationHintCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localScale = Vector3.one * canvasScale;

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cameraTransform.GetComponent<Camera>();
        canvas.sortingOrder = 7000;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = dynamicPixelsPerUnit;

        canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;

        GameObject textGo = new GameObject("HintText");
        textGo.transform.SetParent(canvasGo.transform, false);

        hintText = textGo.AddComponent<TextMeshProUGUI>();
        hintText.alignment = alignment;
        hintText.fontSize = fontSize; // 保持你的 15f，不改字号
        hintText.color = textColor;
        hintText.enableWordWrapping = true;

        // 改善小字号 TMP 在 World Space Canvas 下的灰底 / 边缘脏块
        hintText.extraPadding = extraPadding;
        hintText.richText = true;
        hintText.raycastTarget = false;
        hintText.overflowMode = TextOverflowModes.Overflow;
        hintText.isTextObjectScaleStatic = false;

        TMP_FontAsset chineseFont = Resources.Load<TMP_FontAsset>(chineseFontResourcePath);
        if (chineseFont != null)
        {
            hintText.font = chineseFont;
        }
        else
        {
            Debug.LogWarning($"OperationHintOverlay: 没有在 Resources/{chineseFontResourcePath} 找到中文 TMP 字体。中文可能无法显示。");
        }

        ApplyAlwaysOnTop(hintText, ref hintRuntimeMaterial);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void ApplyAlwaysOnTop(TMP_Text text, ref Material runtimeMaterial)
    {
        if (text == null || !alwaysOnTop)
            return;

        if (runtimeMaterial == null)
        {
            runtimeMaterial = new Material(text.fontMaterial);
            runtimeMaterial.name = text.fontMaterial.name + " AlwaysOnTop Overlay Instance";
        }

        Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");

        if (overlayShader != null)
        {
            runtimeMaterial.shader = overlayShader;
        }
        else
        {
            Debug.LogWarning(
                $"{name}: 没有找到 TextMeshPro/Distance Field Overlay shader，字幕可能仍然会被物体遮挡。"
            );
        }

        runtimeMaterial.renderQueue = alwaysOnTopRenderQueue;

        // 强制尝试设置常见的 ZTest 属性，不判断 HasProperty
        runtimeMaterial.SetFloat("_ZTestMode", (float)CompareFunction.Always);
        runtimeMaterial.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);

        text.fontMaterial = runtimeMaterial;
        text.UpdateMeshPadding();
        text.ForceMeshUpdate();
    }

    // =========================
    // Public Static Interfaces
    // =========================

    public static void Show(string text)
    {
        Show(text, 0f, false);
    }

    public static void Show(string text, float visibleSeconds)
    {
        Show(text, visibleSeconds, false);
    }

    public static void Show(string text, float visibleSeconds, bool instant)
    {
        if (instance == null)
            return;

        instance.ShowInternal(text, visibleSeconds, instant);
    }

    public static void SetText(string text)
    {
        if (instance == null)
            return;

        instance.SetTextInternal(text);
    }

    public static void Hide()
    {
        Hide(false);
    }

    public static void Hide(bool instant)
    {
        if (instance == null)
            return;

        instance.HideInternal(instant);
    }

    // =========================
    // Internal Logic
    // =========================

    private void ShowInternal(string text, float visibleSeconds, bool instant)
    {
        if (!IsInGameScene())
            return;

        if (hintText == null || canvasGroup == null)
            return;

        hintText.text = FormatText(text);
        hintText.ForceMeshUpdate();

        StartFade(1f, instant ? 0f : fadeInDuration);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        if (visibleSeconds > 0f)
        {
            autoHideRoutine = StartCoroutine(AutoHideRoutine(visibleSeconds));
        }
        else
        {
            autoHideRoutine = null;
        }
    }

    private void SetTextInternal(string text)
    {
        if (hintText == null)
            return;

        hintText.text = FormatText(text);
        hintText.ForceMeshUpdate();
    }

    private void HideInternal(bool instant)
    {
        if (canvasGroup == null)
            return;

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);

        autoHideRoutine = null;

        StartFade(0f, instant ? 0f : fadeOutDuration);
    }

    private IEnumerator AutoHideRoutine(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        HideInternal(false);
        autoHideRoutine = null;
    }

    private string FormatText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Replace("\\n", "\n");
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
}