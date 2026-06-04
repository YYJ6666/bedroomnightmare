using NavKeypad;
using UnityEngine;

public sealed class KeypadDoorOpen : MonoBehaviour
{
    [SerializeField] private Keypad keypad;
    [SerializeField] private Transform doorRoot;
    [SerializeField] private bool useLocalRotation = true;
    [SerializeField] private float openYaw = 0f;

    private void Reset()
    {
        keypad = GetComponent<Keypad>();
    }

    private void OnEnable()
    {
        if (keypad == null)
            keypad = GetComponent<Keypad>();

        if (keypad != null)
            keypad.OnAccessGranted.AddListener(OpenDoor);
    }

    private void OnDisable()
    {
        if (keypad != null)
            keypad.OnAccessGranted.RemoveListener(OpenDoor);
    }

    public void OpenDoor()
    {
        if (doorRoot == null)
            return;

        if (useLocalRotation)
        {
            Vector3 euler = doorRoot.localEulerAngles;
            doorRoot.localRotation = Quaternion.Euler(euler.x, openYaw, euler.z);
        }
        else
        {
            Vector3 euler = doorRoot.eulerAngles;
            doorRoot.rotation = Quaternion.Euler(euler.x, openYaw, euler.z);
        }
    }
}

