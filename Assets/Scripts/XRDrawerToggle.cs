using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class XRDrawerToggle : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private bool allowSelectToggle = true;

    [Header("Drawer")]
    [SerializeField] private Transform drawerToMove;
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 0f, 0.25f);
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private bool startOpened;

    [Header("Glow")]
    [SerializeField] private TouchGlowObject glowObject;

    [Header("Optional")]
    [SerializeField] private Animator animatorToDisable;

    [Header("Proximity Open")]
    [SerializeField] private bool openOnProximity = false;
    [SerializeField] private Transform proximityTarget;
    [SerializeField] private string proximityTargetTag = "Key";
    [SerializeField] private float proximityDistance = 0.25f;
    [SerializeField] private float proximityCheckInterval = 0.1f;

    [Header("Proximity Unlock")]
    [SerializeField] private bool unlockOnProximity = false;
    [SerializeField] private GameObject lockObjectToHide;

    private XRSimpleInteractable interactable;
    private Vector3 closedLocalPosition;
    private Vector3 openedLocalPosition;
    private Coroutine moveRoutine;
    private bool isOpen;
    private bool isUnlocked;
    private float nextProximityCheckTime;

    private void Reset()
    {
        drawerToMove = transform;
        animatorToDisable = GetComponent<Animator>();
        glowObject = GetComponent<TouchGlowObject>();
    }

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        if (drawerToMove == null)
            drawerToMove = transform;

        if (animatorToDisable == null)
            animatorToDisable = GetComponent<Animator>();

        if (glowObject == null)
            glowObject = GetComponent<TouchGlowObject>();

        if (animatorToDisable != null)
            animatorToDisable.enabled = false;

        closedLocalPosition = drawerToMove.localPosition;
        openedLocalPosition = closedLocalPosition + openOffset;

        isOpen = startOpened;
        drawerToMove.localPosition = isOpen ? openedLocalPosition : closedLocalPosition;
    }

    private void OnEnable()
    {
        interactable = GetComponent<XRSimpleInteractable>();
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
        if (!allowSelectToggle)
            return;

        if (glowObject != null)
        {
            glowObject.Reveal();
        }

        ToggleDrawer();
    }

    private void Update()
    {
        if ((!openOnProximity && !unlockOnProximity) || (isOpen && isUnlocked))
            return;

        if (proximityCheckInterval > 0f && Time.time < nextProximityCheckTime)
            return;

        nextProximityCheckTime = proximityCheckInterval > 0f ? Time.time + proximityCheckInterval : Time.time;

        if (proximityTarget == null && !string.IsNullOrWhiteSpace(proximityTargetTag))
        {
            GameObject go = GameObject.FindGameObjectWithTag(proximityTargetTag);
            if (go != null)
                proximityTarget = go.transform;
        }

        if (proximityTarget == null)
            return;

        float sqrDistance = (proximityTarget.position - drawerToMove.position).sqrMagnitude;
        float threshold = Mathf.Max(0f, proximityDistance);

        if (sqrDistance <= threshold * threshold)
        {
            if (unlockOnProximity && !isUnlocked)
            {
                Unlock();
                return;
            }

            if (openOnProximity && !isOpen)
            {
                OpenDrawer();
            }
        }
    }

    public void Unlock()
    {
        if (isUnlocked)
            return;

        isUnlocked = true;

        if (lockObjectToHide != null)
            lockObjectToHide.SetActive(false);

        allowSelectToggle = true;

        if (glowObject != null)
            glowObject.Reveal();
    }

    public void ToggleDrawer()
    {
        if (isOpen)
            CloseDrawer();
        else
            OpenDrawer();
    }

    public void OpenDrawer()
    {
        if (isOpen)
            return;

        StartMove(openedLocalPosition);
        isOpen = true;

        if (glowObject != null)
            glowObject.Reveal();
    }

    public void CloseDrawer()
    {
        if (!isOpen)
            return;

        StartMove(closedLocalPosition);
        isOpen = false;
    }

    private void StartMove(Vector3 target)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveDrawer(target));
    }

    private IEnumerator MoveDrawer(Vector3 target)
    {
        Vector3 start = drawerToMove.localPosition;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            t = t * t * (3f - 2f * t);
            drawerToMove.localPosition = Vector3.Lerp(start, target, t);
            yield return null;
        }

        drawerToMove.localPosition = target;
        moveRoutine = null;
    }
}
