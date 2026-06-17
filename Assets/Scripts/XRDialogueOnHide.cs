using UnityEngine;

public class XRDialogueOnHide : MonoBehaviour
{
    [Header("Dialogue")]
    [TextArea(2, 6)]
    [SerializeField] private string dialogueText = "这里写锁消失后要显示的文字。";

    [SerializeField] private bool instant = false;
    [SerializeField] private bool showOnlyOnce = true;

    private bool hasShown;

    public void TryShowDialogue()
    {
        if (showOnlyOnce && hasShown)
            return;

        hasShown = true;

        string finalText = dialogueText.Replace("\\n", "\n");
        DialogueOverlay.Show(finalText, instant);
    }

    public void ResetShownState()
    {
        hasShown = false;
    }
}
