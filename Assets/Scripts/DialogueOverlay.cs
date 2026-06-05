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
    [SerializeField] private bool showOnStart = true;
    [SerializeField] private string startupText = "You wake from a deep sleep.\nEverything is pitch black.";

    [Header("Layout")]
    [SerializeField] private float distanceFromCamera = 0.75f;
    [SerializeField] private Vector2 canvasSize = new Vector2(800f, 100f);
    [SerializeField] private float canvasScale = 0.0012f;
    [SerializeField] private float verticalOffsetMeters = -0.25f;

    [Header("Style")]
    [SerializeField] private float backgroundAlpha = 0.75f;
    [SerializeField] private Color frameColor = Color.white;
    [SerializeField] private float frameAlpha = 0.9f;
    [SerializeField] private float fontSize = 25f;
    [SerializeField] private Color textColor = Color.white;

    [Header("Rounded Frame")]
    [SerializeField] private float cornerRadiusPixels = 36f;
    [SerializeField] private float frameThicknessPixels = 6f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float visibleSeconds = 3f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    private Transform cameraTransform;
    private CanvasGroup canvasGroup;
    private TMP_Text dialogueText;
    private Coroutine fadeRoutine;
    private Coroutine autoHideRoutine;
    private Sprite roundedSprite;
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

        if (roundedSprite != null)
        {
            if (roundedSprite.texture != null)
                Destroy(roundedSprite.texture);
            Destroy(roundedSprite);
            roundedSprite = null;
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
            cameraTransform.rotation);
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

        canvasGo.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;

        canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (roundedSprite == null)
            roundedSprite = CreateRoundedSprite(256, Mathf.Max(2f, cornerRadiusPixels));

        GameObject frameGo = new GameObject("Frame");
        frameGo.transform.SetParent(canvasGo.transform, false);
        Image frame = frameGo.AddComponent<Image>();
        frame.sprite = roundedSprite;
        frame.type = Image.Type.Sliced;
        frame.color = new Color(frameColor.r, frameColor.g, frameColor.b, Mathf.Clamp01(frameAlpha));
        RectTransform frameRect = frameGo.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(frameGo.transform, false);
        Image bg = bgGo.AddComponent<Image>();
        bg.sprite = roundedSprite;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, Mathf.Clamp01(backgroundAlpha));
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        float inset = Mathf.Max(0f, frameThicknessPixels);
        bgRect.offsetMin = new Vector2(inset, inset);
        bgRect.offsetMax = new Vector2(-inset, -inset);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(bgGo.transform, false);
        dialogueText = textGo.AddComponent<TextMeshProUGUI>();
        dialogueText.alignment = TextAlignmentOptions.Center;
        dialogueText.fontSize = fontSize;
        dialogueText.color = textColor;
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.08f, 0.12f);
        textRect.anchorMax = new Vector2(0.92f, 0.88f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private static Sprite CreateRoundedSprite(int size, float radiusPixels)
    {
        int s = Mathf.Clamp(size, 64, 1024);
        float r = Mathf.Clamp(radiusPixels, 2f, s * 0.49f);

        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[s * s];
        float half = (s - 1) * 0.5f;
        float innerHalf = half - r;
        float rSq = r * r;

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Abs(x - half) - innerHalf;
                float dy = Mathf.Abs(y - half) - innerHalf;
                float cx = Mathf.Max(dx, 0f);
                float cy = Mathf.Max(dy, 0f);
                bool inside = (cx * cx + cy * cy) <= rSq;
                pixels[y * s + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        Vector4 border = new Vector4(r, r, r, r);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    public static void Show(string text, bool instant = false)
    {
        if (instance == null)
            return;

        instance.ShowInternal(text, instant);
    }

    public static void Hide(bool instant = false)
    {
        if (instance == null)
            return;

        instance.HideInternal(instant);
    }

    private void ShowInternal(string text, bool instant)
    {
        if (dialogueText == null || canvasGroup == null)
            return;

        dialogueText.text = text ?? string.Empty;
        StartFade(1f, instant ? 0f : fadeInDuration);

        if (autoHideRoutine != null)
            StopCoroutine(autoHideRoutine);
        autoHideRoutine = StartCoroutine(AutoHideRoutine());
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

    private IEnumerator AutoHideRoutine()
    {
        if (visibleSeconds > 0f)
            yield return new WaitForSecondsRealtime(visibleSeconds);

        StartFade(0f, fadeOutDuration);
        autoHideRoutine = null;
    }
}
