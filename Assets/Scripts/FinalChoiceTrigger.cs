using UnityEngine;

public class FinalChoiceTrigger : MonoBehaviour
{
    public enum ChoiceType
    {
        DoorEnding,
        BedEnding
    }

    [Header("Choice")]
    [SerializeField] private ChoiceType choiceType = ChoiceType.DoorEnding;

    [Header("Trigger Filter")]
    [SerializeField] private bool onlyPlayerTag = false;
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Enter : " + other.name);
        if (hasTriggered)
            return;

        if (onlyPlayerTag && !other.CompareTag(playerTag))
            return;

        if (FinalChoiceManager.Instance == null)
        {
            Debug.LogWarning($"{name}: 场景里没有 FinalChoiceManager。");
            return;
        }

        hasTriggered = true;

        if (logDebug)
            Debug.Log($"[FinalChoiceTrigger] {name} triggered by {other.name}, Choice={choiceType}");

        switch (choiceType)
        {
            case ChoiceType.DoorEnding:
                FinalChoiceManager.Instance.ChooseDoorEnding();
                break;

            case ChoiceType.BedEnding:
                FinalChoiceManager.Instance.ChooseBedEnding();
                break;
        }
    }
}