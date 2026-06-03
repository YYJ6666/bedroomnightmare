using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class XRRevealOnSelect : MonoBehaviour
{
    private XRSimpleInteractable interactable;
    private TouchGlowObject glowObject;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        glowObject = GetComponent<TouchGlowObject>();

        if (glowObject == null)
        {
            glowObject = GetComponentInParent<TouchGlowObject>();
        }

        if (glowObject == null)
        {
            glowObject = GetComponentInChildren<TouchGlowObject>();
        }
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelected);
        }
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (glowObject != null)
        {
            glowObject.Reveal();
        }
    }
}