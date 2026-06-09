using TMPro;
using UnityEngine;

public class TVScreenTextManager : MonoBehaviour
{
    public static TVScreenTextManager Instance { get; private set; }

    private static string savedText = "";
    private static bool hasSavedText = false;

    [Header("Text Component")]
    [SerializeField] private TMP_Text textComponent;

    [Header("Startup")]
    [SerializeField] private bool hideIfNoSavedText = true;

    private void Awake()
    {
        Instance = this;

        if (textComponent == null)
            textComponent = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        ApplySavedText();
    }

    public static void SetText(string text)
    {
        savedText = FormatTextStatic(text);
        hasSavedText = !string.IsNullOrWhiteSpace(savedText);

        if (Instance != null)
        {
            Instance.ApplySavedText();
        }
    }

    public static void ClearText()
    {
        savedText = "";
        hasSavedText = false;

        if (Instance != null)
        {
            Instance.ApplySavedText();
        }
    }

    public static string GetCurrentText()
    {
        return savedText;
    }

    private void ApplySavedText()
    {
        if (textComponent == null)
            return;

        if (hasSavedText)
        {
            gameObject.SetActive(true);
            textComponent.text = savedText;
        }
        else
        {
            textComponent.text = "";

            if (hideIfNoSavedText)
                gameObject.SetActive(false);
        }
    }

    private static string FormatTextStatic(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Replace("\\n", "\n");
    }
}