using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class XRDrawerToggle : MonoBehaviour, ICheckpointStateHandler
{
    [System.Serializable]
    private class DrawerCheckpointState
    {
        public bool isOpen;
        public bool isUnlocked;
        public bool allowSelectToggle;
        public bool hasLockObject;
        public bool lockObjectActiveSelf;
        public Vector3 drawerLocalPosition;
    }

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

    [Header("Linked Interactables")]
    [SerializeField] private Behaviour[] enableWhenDrawerOpen;
    [SerializeField] private Collider[] enableCollidersWhenDrawerOpen;

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

        ApplyDrawerOpenLinkedState();
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
        if (glowObject != null)
        {
            glowObject.Reveal();
        }

        if (!allowSelectToggle)
            return;

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

    public string CaptureCheckpointState()
    {
        DrawerCheckpointState state = new DrawerCheckpointState();
        state.isOpen = isOpen;
        state.isUnlocked = isUnlocked;
        state.allowSelectToggle = allowSelectToggle;
        state.hasLockObject = lockObjectToHide != null;
        state.lockObjectActiveSelf = lockObjectToHide != null && lockObjectToHide.activeSelf;
        state.drawerLocalPosition = drawerToMove != null ? drawerToMove.localPosition : transform.localPosition;

        return JsonUtility.ToJson(state);
    }

    public void RestoreCheckpointState(string stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
            return;

        DrawerCheckpointState state = JsonUtility.FromJson<DrawerCheckpointState>(stateJson);

        if (state == null)
            return;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        isOpen = state.isOpen;
        isUnlocked = state.isUnlocked;
        allowSelectToggle = state.allowSelectToggle;

        if (drawerToMove == null)
            drawerToMove = transform;

        drawerToMove.localPosition = state.drawerLocalPosition;

        if (lockObjectToHide != null)
            lockObjectToHide.SetActive(state.hasLockObject ? state.lockObjectActiveSelf : !isUnlocked);

        ApplyDrawerOpenLinkedState();
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
        ApplyDrawerOpenLinkedState();

        if (glowObject != null)
            glowObject.Reveal();
    }

    public void CloseDrawer()
    {
        if (!isOpen)
            return;

        StartMove(closedLocalPosition);
        isOpen = false;
        ApplyDrawerOpenLinkedState();
    }

    private void ApplyDrawerOpenLinkedState()
    {
        bool shouldEnable = isOpen;

        if (enableWhenDrawerOpen != null)
        {
            for (int i = 0; i < enableWhenDrawerOpen.Length; i++)
            {
                if (enableWhenDrawerOpen[i] != null)
                    enableWhenDrawerOpen[i].enabled = shouldEnable;
            }
        }

        if (enableCollidersWhenDrawerOpen != null)
        {
            for (int i = 0; i < enableCollidersWhenDrawerOpen.Length; i++)
            {
                if (enableCollidersWhenDrawerOpen[i] != null)
                    enableCollidersWhenDrawerOpen[i].enabled = shouldEnable;
            }
        }
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
