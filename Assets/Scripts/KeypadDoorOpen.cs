using NavKeypad;
using UnityEngine;

public sealed class KeypadDoorOpen : MonoBehaviour
{
    [Header("Keypad")]
    [SerializeField] private Keypad keypad;

    [Header("Door")]
    [SerializeField] private Transform doorRoot;
    [SerializeField] private bool useLocalRotation = true;
    [SerializeField] private float openYaw = 0f;

    [Header("Task Complete")]
    [SerializeField] private bool completeTaskOnOpen = true;
    [SerializeField] private string taskId = "unlock_door";
    [SerializeField] private bool requireCurrentTask = true;
    [SerializeField] private bool completeOnlyOnce = true;

    private bool hasCompletedTask = false;

    private void Reset()
    {
        keypad = GetComponent<Keypad>();
    }

    private void OnEnable()
    {
        if (keypad == null)
            keypad = GetComponent<Keypad>();

        if (keypad != null)
            keypad.OnAccessGranted.AddListener(OpenDoor);
    }

    private void OnDisable()
    {
        if (keypad != null)
            keypad.OnAccessGranted.RemoveListener(OpenDoor);
    }

    public void OpenDoor()
    {
        OpenDoorTransform();
        TryCompleteTask();
    }

    private void OpenDoorTransform()
    {
        if (doorRoot == null)
            return;

        if (useLocalRotation)
        {
            Vector3 euler = doorRoot.localEulerAngles;
            doorRoot.localRotation = Quaternion.Euler(euler.x, openYaw, euler.z);
        }
        else
        {
            Vector3 euler = doorRoot.eulerAngles;
            doorRoot.rotation = Quaternion.Euler(euler.x, openYaw, euler.z);
        }
    }

    private void TryCompleteTask()
    {
        if (!completeTaskOnOpen)
            return;

        if (completeOnlyOnce && hasCompletedTask)
            return;

        if (TaskChainManager.Instance == null)
        {
            Debug.LogWarning($"{name}: 场景里没有 TaskChainManager，无法完成任务 {taskId}。");
            return;
        }

        if (requireCurrentTask && !TaskChainManager.Instance.IsCurrentTask(taskId))
        {
            Debug.Log($"{name}: 当前任务不是 {taskId}，所以不会完成该任务。");
            return;
        }

        hasCompletedTask = true;
        TaskChainManager.Instance.CompleteTask(taskId);
    }
}