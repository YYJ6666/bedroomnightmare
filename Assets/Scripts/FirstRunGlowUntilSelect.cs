using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

public class FirstRunGlowUntilSelect : MonoBehaviour
{
    [Header("Scene Filter")]
    [SerializeField] private string gameSceneName = "bedroom2";

    [Header("Glow")]
    [SerializeField] private TouchGlowObject glowObject;

    [Header("First Run")]
    [SerializeField] private bool onlyFirstRun = true;

    [Tooltip("每个需要开场常亮的物体都应该有不同的 ID。留空则自动使用场景路径。")]
    [SerializeField] private string uniqueId = "";

    [Header("Socket")]
    [SerializeField] private bool ignoreSocketSelect = true;

    [Header("After First Select")]
    [SerializeField] private bool callRevealOnFirstSelect = true;

    private static readonly HashSet<string> activatedIds = new HashSet<string>();

    private XRBaseInteractable interactable;
    private bool waitingForFirstSelect = false;
    private string runtimeId;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();

        if (glowObject == null)
        {
            glowObject = GetComponent<TouchGlowObject>();
        }

        runtimeId = GetRuntimeId();
    }

    private void Start()
    {
        if (!IsInGameScene())
            return;

        if (interactable == null)
        {
            Debug.LogWarning($"{name}: 没有找到 XRBaseInteractable，无法监听 Select。");
            return;
        }

        if (glowObject == null)
        {
            Debug.LogWarning($"{name}: 没有找到 TouchGlowObject，无法开场常亮。");
            return;
        }

        if (onlyFirstRun && activatedIds.Contains(runtimeId))
            return;

        if (onlyFirstRun)
        {
            activatedIds.Add(runtimeId);
        }

        waitingForFirstSelect = true;
        SetOutlines(true);
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
        {
            return;
        }

        if (!waitingForFirstSelect)
            return;

        waitingForFirstSelect = false;

        // 结束“开场强制常亮”
        SetOutlines(false);

        // 之后交给原本 TouchGlowObject 的计时逻辑
        if (callRevealOnFirstSelect && glowObject != null)
        {
            glowObject.Reveal();
        }
    }

    private void SetOutlines(bool enabled)
    {
        if (glowObject == null || glowObject.outlines == null)
            return;

        foreach (Outline outline in glowObject.outlines)
        {
            if (outline != null)
            {
                outline.enabled = enabled;
            }
        }
    }

    private bool IsInGameScene()
    {
        return string.IsNullOrWhiteSpace(gameSceneName) ||
               SceneManager.GetActiveScene().name == gameSceneName;
    }

    private string GetRuntimeId()
    {
        if (!string.IsNullOrWhiteSpace(uniqueId))
            return uniqueId;

        return SceneManager.GetActiveScene().name + "/" + GetHierarchyPath(transform);
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return "";

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}