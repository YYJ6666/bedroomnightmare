using UnityEngine;
using Unity.XR.CoreUtils;

[RequireComponent(typeof(CharacterController))]
public class XRCharacterControllerFollowHMD : MonoBehaviour
{
    [Header("XR")]
    [SerializeField] private XROrigin xrOrigin;

    [Header("Character Controller")]
    [SerializeField] private CharacterController characterController;

    [Header("Body Size")]
    [SerializeField] private float radius = 0.28f;
    [SerializeField] private float minHeight = 1.0f;
    [SerializeField] private float maxHeight = 2.0f;

    [Header("Follow HMD")]
    [Tooltip("让碰撞体水平位置跟随 HMD。")]
    [SerializeField] private bool followHorizontalPosition = true;

    [Tooltip("让碰撞体高度跟随 HMD 高度。")]
    [SerializeField] private bool followHeight = true;

    private void Awake()
    {
        if (xrOrigin == null)
            xrOrigin = GetComponent<XROrigin>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();
    }

    private void LateUpdate()
    {
        if (xrOrigin == null || xrOrigin.Camera == null || characterController == null)
            return;

        Transform hmd = xrOrigin.Camera.transform;

        // 把 HMD 世界坐标转换到 XR Origin 的局部坐标
        Vector3 hmdLocalPosition = transform.InverseTransformPoint(hmd.position);

        float height = characterController.height;

        if (followHeight)
        {
            height = Mathf.Clamp(hmdLocalPosition.y, minHeight, maxHeight);
            characterController.height = height;
        }

        characterController.radius = radius;

        Vector3 center = characterController.center;

        // 胶囊体中心高度
        center.y = height * 0.5f;

        // 核心：让碰撞体的 x/z 跟着 HMD
        if (followHorizontalPosition)
        {
            center.x = hmdLocalPosition.x;
            center.z = hmdLocalPosition.z;
        }

        characterController.center = center;
    }
}