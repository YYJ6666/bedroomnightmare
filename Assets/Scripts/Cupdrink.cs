using System;
using System.Collections;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class CupDrinkOnce : MonoBehaviour
{
    [Header("Drink Once")]
    [SerializeField] private bool drinkOnlyOnce = true;
    [SerializeField] private bool hasDrunk = false;

    [Header("Camera Target")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("杯子移动到相机前方的位置，单位是米。x右正，y上正，z前正。")]
    [SerializeField] private Vector3 localDrinkPosition = new Vector3(0.12f, -0.18f, 0.35f);

    [Tooltip("杯子靠近嘴边时的局部旋转角度。")]
    [SerializeField] private Vector3 localDrinkEuler = new Vector3(-25f, 0f, 0f);

    [Header("Timing")]
    [SerializeField] private float moveToMouthTime = 0.45f;
    [SerializeField] private float drinkHoldTime = 1.2f;
    [SerializeField] private float moveBackTime = 0.35f;

    [Header("Audio")]
    [SerializeField] private AudioSource drinkAudio;

    [Header("Physics Restore")]
    [Tooltip("喝完后强制重新开启重力，避免杯子悬空。")]
    [SerializeField] private bool forceGravityAfterDrink = true;

    [Tooltip("喝完后强制关闭 Kinematic，避免杯子悬空。")]
    [SerializeField] private bool forceNonKinematicAfterDrink = true;

    [Tooltip("喝完后清空速度，避免杯子飞出去。")]
    [SerializeField] private bool stopVelocityAfterDrink = true;

    public bool HasDrunk => hasDrunk;
    public bool IsDrinking => isDrinking;

    public event Action OnDrinkStarted;
    public event Action OnDrinkFinished;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    private bool isDrinking;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private Coroutine drinkRoutine;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        if (cameraTransform == null)
        {
            cameraTransform = FindCameraTransform();
        }
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        grabInteractable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelected);
        }

        if (drinkRoutine != null)
        {
            StopCoroutine(drinkRoutine);
            drinkRoutine = null;
        }

        RestorePhysics();

        if (grabInteractable != null)
        {
            grabInteractable.enabled = true;
        }

        isDrinking = false;
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (isDrinking)
            return;

        if (args.interactorObject is XRSocketInteractor)
            return;

        if (drinkOnlyOnce && hasDrunk)
            return;

        drinkRoutine = StartCoroutine(DrinkRoutine(args.interactorObject));
    }

    private IEnumerator DrinkRoutine(IXRSelectInteractor interactor)
    {
        isDrinking = true;
        hasDrunk = true;

        OnDrinkStarted?.Invoke();

        if (cameraTransform == null)
        {
            cameraTransform = FindCameraTransform();
        }

        if (cameraTransform == null)
        {
            Debug.LogWarning("CupDrinkOnce: 没有找到 Camera，无法执行喝水动画。");

            isDrinking = false;
            drinkRoutine = null;

            yield break;
        }

        originalPosition = transform.position;
        originalRotation = transform.rotation;

        // 先强制释放
        ForceRelease(interactor);

        // 等一帧，让 XR 完成释放
        yield return null;

        // 禁用抓取，避免动画期间手柄继续控制杯子
        grabInteractable.enabled = false;

        // 释放后立刻锁住物理，防止掉落
        DisablePhysicsForAnimation();

        Vector3 drinkWorldPosition = cameraTransform.TransformPoint(localDrinkPosition);
        Quaternion drinkWorldRotation = cameraTransform.rotation * Quaternion.Euler(localDrinkEuler);

        yield return MoveTo(
            transform.position,
            transform.rotation,
            drinkWorldPosition,
            drinkWorldRotation,
            moveToMouthTime
        );

        if (drinkAudio != null)
        {
            drinkAudio.Play();
        }

        yield return new WaitForSeconds(drinkHoldTime);

        yield return MoveTo(
            transform.position,
            transform.rotation,
            originalPosition,
            originalRotation,
            moveBackTime
        );

        RestorePhysics();

        grabInteractable.enabled = true;

        isDrinking = false;
        drinkRoutine = null;

        OnDrinkFinished?.Invoke();
    }

    private void DisablePhysicsForAnimation()
    {
        if (rb == null)
            return;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private void RestorePhysics()
    {
        if (rb == null)
            return;

        if (forceGravityAfterDrink)
        {
            rb.useGravity = true;
        }

        if (forceNonKinematicAfterDrink)
        {
            rb.isKinematic = false;
        }

        if (stopVelocityAfterDrink)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.WakeUp();
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

    private void ForceRelease(IXRSelectInteractor interactor)
    {
        if (interactor == null)
            return;

        if (grabInteractable == null)
            return;

        if (grabInteractable.interactionManager == null)
            return;

        if (!grabInteractable.isSelected)
            return;

        grabInteractable.interactionManager.SelectExit(interactor, grabInteractable);
    }

    private Transform FindCameraTransform()
    {
        XROrigin origin = FindObjectOfType<XROrigin>(true);

        if (origin != null && origin.Camera != null)
        {
            return origin.Camera.transform;
        }

        if (Camera.main != null)
        {
            return Camera.main.transform;
        }

        return null;
    }

    // =========================
    // Public Interfaces
    // =========================

    public void ResetDrinkState()
    {
        hasDrunk = false;
    }

    public void ForceSetDrunk()
    {
        hasDrunk = true;
    }

    public void ForceDrinkNow()
    {
        if (isDrinking)
            return;

        if (drinkOnlyOnce && hasDrunk)
            return;

        drinkRoutine = StartCoroutine(DrinkRoutine(null));
    }
}