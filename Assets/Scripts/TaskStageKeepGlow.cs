using UnityEngine;

public class TaskStageKeepGlow : MonoBehaviour
{
    [Header("Task")]
    [SerializeField] private string targetTaskId = "restore_memory";

    [Header("Glow")]
    [SerializeField] private TouchGlowObject glowObject;

    [Header("Behavior")]
    [Tooltip("离开该任务阶段后是否关闭发光。")]
    [SerializeField] private bool hideWhenTaskEnds = true;

    [Tooltip("如果物体一开始不在目标阶段，是否先强制关闭发光。")]
    [SerializeField] private bool hideOnAwake = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool isGlowing = false;

    private void Awake()
    {
        if (glowObject == null)
            glowObject = GetComponent<TouchGlowObject>();

        if (hideOnAwake && glowObject != null)
        {
            glowObject.Hide();
            isGlowing = false;
        }
    }

    private void Update()
    {
        if (TaskChainManager.Instance == null)
            return;

        bool isInTargetTask = TaskChainManager.Instance.IsCurrentTask(targetTaskId);

        if (isInTargetTask)
        {
            if (!isGlowing)
                StartGlow();
        }
        else
        {
            if (isGlowing && hideWhenTaskEnds)
                StopGlow();
        }
    }

    private void StartGlow()
    {
        if (glowObject == null)
        {
            Debug.LogWarning($"{name}: 没有找到 TouchGlowObject，无法恒亮。");
            return;
        }

        isGlowing = true;
        glowObject.RevealAndKeep();

        if (logDebug)
            Debug.Log($"[TaskStageKeepGlow] {name} 在任务阶段 {targetTaskId} 开始恒亮。", this);
    }

    private void StopGlow()
    {
        if (glowObject == null)
            return;

        isGlowing = false;
        glowObject.Hide();

        if (logDebug)
            Debug.Log($"[TaskStageKeepGlow] {name} 离开任务阶段 {targetTaskId}，关闭发光。", this);
    }
}