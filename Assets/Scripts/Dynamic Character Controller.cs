using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class XRCharacterControllerUpdater : MonoBehaviour
{
    public Transform head;

    private CharacterController cc;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    private void Update()
    {
        float height = Mathf.Clamp(head.localPosition.y, 1f, 2f);

        cc.height = height;

        Vector3 center = head.localPosition;
        center.y = height / 2f;

        cc.center = center;
    }
}