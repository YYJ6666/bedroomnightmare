using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class XRPresentToCameraOnSelect : MonoBehaviour
{
    [Header("Glow")]
    [SerializeField] private TouchGlowObject glowObject;

    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Vector3 cameraLocalOffset = new Vector3(0f, 0f, 0.45f);
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private Vector3 cameraFacingEulerOffset = Vector3.zero;
    [SerializeField] private bool keepWorldRotation = false;

    [Header("Timing")]
    [SerializeField] private float moveToCameraDuration = 0.35f;
    [SerializeField] private float stayDuration = 3f;
    [SerializeField] private float moveBackDuration = 0.35f;

    [Header("Physics")]
    [SerializeField] private bool disablePhysicsWhilePresenting = true;

    private XRSimpleInteractable interactable;
    private Rigidbody rb;

    private Coroutine routine;
    private bool isPresenting;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalWorldPosition;
    private Quaternion originalWorldRotation;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        rb = GetComponent<Rigidbody>();

        if (glowObject == null)
        {
            glowObject = GetComponent<TouchGlowObject>();
        }

        if (glowObject == null)
        {
            glowObject = GetComponentInParent<TouchGlowObject>();
        }

        if (glowObject == null)
        {
            glowObject = GetComponentInChildren<TouchGlowObject>(true);
        }
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelected);

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        isPresenting = false;
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (isPresenting)
            return;

        if (glowObject != null)
        {
            glowObject.Reveal();
        }

        Transform anchor = ResolvePresentationAnchor();
        if (anchor == null)
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PresentRoutine(anchor));
    }

    private IEnumerator PresentRoutine(Transform anchor)
    {
        isPresenting = true;

        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalWorldPosition = transform.position;
        originalWorldRotation = transform.rotation;

        bool changedPhysics = false;
        bool originalUseGravity = false;
        bool originalIsKinematic = false;

        if (disablePhysicsWhilePresenting && rb != null)
        {
            originalUseGravity = rb.useGravity;
            originalIsKinematic = rb.isKinematic;
            changedPhysics = true;

            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.useGravity = false;
            rb.isKinematic = true;
            rb.WakeUp();
        }

        transform.SetParent(null, true);

        Vector3 targetPosition =
            anchor.position +
            anchor.right * cameraLocalOffset.x +
            anchor.up * cameraLocalOffset.y +
            anchor.forward * cameraLocalOffset.z;

        Quaternion targetRotation;

        if (keepWorldRotation)
        {
            targetRotation = originalWorldRotation;
        }
        else if (faceCamera)
        {
            Vector3 lookDirection = anchor.position - targetPosition;
            if (lookDirection.sqrMagnitude <= 0.0001f)
                lookDirection = -anchor.forward;

            targetRotation = Quaternion.LookRotation(lookDirection.normalized, anchor.up) *
                             Quaternion.Euler(cameraFacingEulerOffset);
        }
        else
        {
            targetRotation = anchor.rotation;
        }

        yield return MoveTo(transform.position, transform.rotation, targetPosition, targetRotation, moveToCameraDuration);

        if (stayDuration > 0f)
            yield return new WaitForSeconds(stayDuration);

        if (originalParent != null)
        {
            transform.SetParent(originalParent, true);
            yield return MoveTo(transform.position, transform.rotation, originalParent.TransformPoint(originalLocalPosition), originalParent.rotation * originalLocalRotation, moveBackDuration);
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
        }
        else
        {
            yield return MoveTo(transform.position, transform.rotation, originalWorldPosition, originalWorldRotation, moveBackDuration);
        }

        if (changedPhysics && rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = originalUseGravity;
            rb.isKinematic = originalIsKinematic;
            rb.WakeUp();
        }

        routine = null;
        isPresenting = false;
    }

    private Transform ResolvePresentationAnchor()
    {
        if (cameraTransform != null)
        {
            Camera direct = cameraTransform.GetComponent<Camera>();
            if (direct != null)
                return direct.transform;

            Camera child = cameraTransform.GetComponentInChildren<Camera>(true);
            if (child != null)
                return child.transform;
        }

        XROrigin origin = FindObjectOfType<XROrigin>(true);
        if (origin != null && origin.Camera != null)
            return origin.Camera.transform;

        if (Camera.main != null)
            return Camera.main.transform;

        return cameraTransform;
    }

    private IEnumerator MoveTo(
        Vector3 startPosition,
        Quaternion startRotation,
        Vector3 targetPosition,
        Quaternion targetRotation,
        float duration
    )
    {
        if (duration <= 0f)
        {
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);
    }
}
