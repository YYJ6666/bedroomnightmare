using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRBaseInteractable))]
public class XRRevealOnSelect : MonoBehaviour
{
    [Header("Glow")]
    [SerializeField] private TouchGlowObject glowObject;

    [Header("Filter")]
    [Tooltip("忽略 Socket 自动选中。一般建议勾选。")]
    [SerializeField] private bool ignoreSocketSelect = true;

    [Tooltip("如果勾选，只允许玩家手部 Direct Interactor 触发。")]
    [SerializeField] private bool onlyAllowDirectInteractor = false;

    [Tooltip("如果勾选，忽略 Ray Interactor 触发。")]
    [SerializeField] private bool ignoreRayInteractor = false;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private XRBaseInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();

        if (glowObject == null)
            glowObject = GetComponent<TouchGlowObject>();
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
        IXRSelectInteractor interactor = args.interactorObject;

        if (debugLog)
        {
            string interactorName = interactor != null && interactor.transform != null
                ? interactor.transform.name
                : "null";

            string interactorType = interactor != null
                ? interactor.GetType().Name
                : "null";

            Debug.Log(
                $"[XRRevealOnSelect] {name} 被 Select。\n" +
                $"Interactor Name = {interactorName}\n" +
                $"Interactor Type = {interactorType}\n" +
                $"Frame = {Time.frameCount}",
                this
            );
        }

        if (ignoreSocketSelect && interactor is XRSocketInteractor)
        {
            if (debugLog)
                Debug.Log($"[XRRevealOnSelect] {name}: 忽略 Socket Select。", this);

            return;
        }

        if (ignoreRayInteractor && interactor is XRRayInteractor)
        {
            if (debugLog)
                Debug.Log($"[XRRevealOnSelect] {name}: 忽略 Ray Select。", this);

            return;
        }

        if (onlyAllowDirectInteractor && !(interactor is XRDirectInteractor))
        {
            if (debugLog)
                Debug.Log($"[XRRevealOnSelect] {name}: 不是 XRDirectInteractor，忽略。", this);

            return;
        }

        if (glowObject == null)
        {
            Debug.LogWarning($"{name}: 没有设置 TouchGlowObject。", this);
            return;
        }

        glowObject.Reveal();
    }
}