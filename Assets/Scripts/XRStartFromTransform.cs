using UnityEngine;

public class XRStartFromTransform : MonoBehaviour
{
    [SerializeField] private Transform rigRoot;
    [SerializeField] private Transform startPoint;
    [SerializeField] private bool matchYawOnly = true;

    private void Awake()
    {
        if (rigRoot == null || startPoint == null)
            return;

        rigRoot.position = startPoint.position;

        if (matchYawOnly)
        {
            Vector3 euler = rigRoot.eulerAngles;
            rigRoot.rotation = Quaternion.Euler(euler.x, startPoint.eulerAngles.y, euler.z);
        }
        else
        {
            rigRoot.rotation = startPoint.rotation;
        }
    }
}
