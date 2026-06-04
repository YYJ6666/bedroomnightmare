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
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelected);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            return;
        }

        if (glowObject != null)
        {
            glowObject.RevealAndKeep();
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            return;
        }

        if (glowObject != null)
        {
            glowObject.ReleaseKeep();
        }
    }
}