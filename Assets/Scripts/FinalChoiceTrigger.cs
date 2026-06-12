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
        if (logDebug)
            Debug.Log("Enter : " + other.name);

        if (hasTriggered)
            return;

        if (!IsPlayer(other))
        {
            if (logDebug)
                Debug.Log($"[FinalChoiceTrigger] 忽略非玩家物体: {other.name}");
            return;
        }

        if (FinalChoiceManager.Instance == null)
        {
            Debug.LogWarning($"{name}: 场景里没有 FinalChoiceManager。");
            return;
        }

        bool success = false;

        if (logDebug)
            Debug.Log($"[FinalChoiceTrigger] {name} triggered by {other.name}, Choice={choiceType}");

        switch (choiceType)
        {
            case ChoiceType.DoorEnding:
                success = FinalChoiceManager.Instance.ChooseDoorEnding();
                break;

            case ChoiceType.BedEnding:
                success = FinalChoiceManager.Instance.ChooseBedEnding();
                break;
        }

        if (success)
        {
            hasTriggered = true;
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (onlyPlayerTag)
            return other.CompareTag(playerTag);

        if (other.GetComponent<CharacterController>() != null)
            return true;

        if (other.GetComponentInParent<CharacterController>() != null)
            return true;

        return other.name.Contains("XR Origin");
    }
}