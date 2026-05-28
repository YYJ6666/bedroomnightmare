using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class RayGlowController : MonoBehaviour
{
    [Header("Ray Interactor")]
    public XRRayInteractor rayInteractor;

    [Header("Keyboard Test")]
    public Key revealKey = Key.Space;

    private void Reset()
    {
        rayInteractor = GetComponent<XRRayInteractor>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[revealKey].wasPressedThisFrame)
        {
            TryReveal();
        }
    }

    private void TryReveal()
    {
        if (rayInteractor == null) return;

        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            TouchGlowObject glowObject = hit.collider.GetComponentInParent<TouchGlowObject>();

            if (glowObject != null)
            {
                glowObject.Reveal();
            }
        }
    }
}