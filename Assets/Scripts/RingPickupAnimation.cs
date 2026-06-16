using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class RingPickupAnimation : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    [Header("Animation")]
    [SerializeField] private float moveTime = 0.35f;
    [SerializeField] private float holdTime = 0.2f;

    [Header("Wear Ring Motion")]
    [SerializeField] private float leftDistance = 0.18f;   // 向左移动距离
    [SerializeField] private float sinkDistance = 0.12f;   // 向下移动距离

    private XRGrabInteractable grab;
    private Rigidbody rb;

    private bool isPlaying;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
    }

    private void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (isPlaying) return;

        StartCoroutine(Play(args.interactorObject));
    }

    private IEnumerator Play(IXRSelectInteractor interactor)
    {
        isPlaying = true;

        if (cameraTransform == null)
        {
            Debug.LogWarning("RingPickupAnimation: Camera Transform is missing.");
            isPlaying = false;
            yield break;
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        // 1. 强制释放 XR 控制
        if (grab.interactionManager != null && grab.isSelected)
        {
            grab.interactionManager.SelectExit(interactor, grab);
        }

        yield return null;

        // 2. 关闭 XR 控制
        grab.enabled = false;

        // 3. 锁定物理
        if (!rb.isKinematic)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.useGravity = false;
        rb.isKinematic = true;

        // 4. 飞到玩家视野前方
        Vector3 facePos =
            cameraTransform.position
            + cameraTransform.forward * 0.4f
            - cameraTransform.up * 0.1f;

        // 5. 戒指飞到相机前方
        yield return Move(
            startPos,
            facePos,
            startRot,
            Quaternion.identity
        );

        yield return new WaitForSeconds(holdTime);

        // 6. 往左下方移动，模仿戴到左手上
        Vector3 wearPos =
            facePos
            - cameraTransform.right * leftDistance
            - cameraTransform.up * sinkDistance;

        yield return Move(
            facePos,
            wearPos,
            transform.rotation,
            transform.rotation
        );

        // 7. 消失
        gameObject.SetActive(false);
    }

    private IEnumerator Move(Vector3 startPos, Vector3 endPos, Quaternion startRot, Quaternion endRot)
    {
        float timer = 0f;

        while (timer < moveTime)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / moveTime);

            // 平滑缓动，避免动作太生硬
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(startPos, endPos, smoothT);
            transform.rotation = Quaternion.Slerp(startRot, endRot, smoothT);

            yield return null;
        }

        transform.position = endPos;
        transform.rotation = endRot;
    }
}
