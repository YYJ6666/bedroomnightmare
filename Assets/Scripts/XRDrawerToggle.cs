using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class XRDrawerToggle : MonoBehaviour
{
    [Header("Drawer")]
    [SerializeField] private Transform drawerToMove;
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 0f, 0.25f);
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private bool startOpened;

    [Header("Optional")]
    [SerializeField] private Animator animatorToDisable;

    private XRSimpleInteractable interactable;
    private Vector3 closedLocalPosition;
    private Vector3 openedLocalPosition;
    private Coroutine moveRoutine;
    private bool isOpen;

    private void Reset()
    {
        drawerToMove = transform;
        animatorToDisable = GetComponent<Animator>();
    }

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        if (drawerToMove == null)
            drawerToMove = transform;

        if (animatorToDisable == null)
            animatorToDisable = GetComponent<Animator>();

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
        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        ToggleDrawer();
    }

    public void ToggleDrawer()
    {
        Vector3 target = isOpen ? closedLocalPosition : openedLocalPosition;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveDrawer(target));
        isOpen = !isOpen;
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
