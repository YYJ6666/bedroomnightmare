using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRBaseInteractable))]
public class XRDialogueOnSelect : MonoBehaviour
{
    private static float suppressUntilUnscaledTime = -1f;

    [Header("Dialogue")]
    [TextArea(2, 6)]
    [SerializeField] private string dialogueText = "这里写要显示的文字。";

    [SerializeField] private bool instant = false;
    [SerializeField] private bool showOnlyOnce = false;

    [Header("Grab / Socket")]
    [SerializeField] private bool ignoreSocketSelect = true;

    private XRBaseInteractable interactable;
    private bool hasShown = false;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
    }

    private void OnEnable()
    {
        if (interactable == null)
        {
            interactable = GetComponent<XRBaseInteractable>();
        }

        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelected);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelected);
        }
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (Time.unscaledTime < suppressUntilUnscaledTime)
        {
            return;
        }

        // 如果是 Socket 自动吸附触发的 Select，不显示对话
        if (ignoreSocketSelect && args.interactorObject is XRSocketInteractor)
        {
            return;
        }

        if (showOnlyOnce && hasShown)
        {
            return;
        }

        hasShown = true;

        string finalText = dialogueText.Replace("\\n", "\n");
        DialogueOverlay.Show(finalText, instant);
    }

    public static void SuppressFor(float seconds)
    {
        if (seconds <= 0f)
            return;

        suppressUntilUnscaledTime = Mathf.Max(suppressUntilUnscaledTime, Time.unscaledTime + seconds);
    }
}
