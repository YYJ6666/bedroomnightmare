using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
public class XRGrabRevealOnSelect : MonoBehaviour
{
    [SerializeField] private TouchGlowObject glowObject;

    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (glowObject == null)
        {
            glowObject = GetComponent<TouchGlowObject>();
        }
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelected);
        }
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        // 如果是 Socket 触发的 Select，就忽略
        if (args.interactorObject is XRSocketInteractor)
        {
            return;
        }

        // 只有玩家手柄 / 射线 / Direct Interactor 触发时才发光
        if (glowObject != null)
        {
            glowObject.Reveal();
        }
    }
}