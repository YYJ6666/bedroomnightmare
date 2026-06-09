using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRBaseInteractable))]
public class XRCompleteTaskOnSelect : MonoBehaviour
{
    [Header("Task")]
    [SerializeField] private string taskId = "touch_tv";

    [Header("Behavior")]
    [SerializeField] private bool completeOnlyOnce = true;
    [SerializeField] private bool ignoreSocketSelect = true;

    [Header("Optional")]
    [SerializeField] private bool requireCurrentTask = true;

    private XRBaseInteractable interactable;
    private bool hasCompleted = false;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
    }

    private void OnEnable()
    {
        if (interactable == null)
            interactable = GetComponent<XRBaseInteractable>();

        if (interactable != null)
            interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (ignoreSocketSelect && args.interactorObject is XRSocketInteractor)
            return;

        if (completeOnlyOnce && hasCompleted)
            return;

        if (TaskChainManager.Instance == null)
        {
            Debug.LogWarning($"{name}: 场景里没有 TaskChainManager。");
            return;
        }

        if (requireCurrentTask && !TaskChainManager.Instance.IsCurrentTask(taskId))
        {
            Debug.Log($"{name}: 当前任务不是 {taskId}，所以不会完成。");
            return;
        }

        hasCompleted = true;

        TaskChainManager.Instance.CompleteTask(taskId);
    }
}