using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
namespace NavKeypad
{
    public class KeypadButton : MonoBehaviour
    {
        [Header("Value")]
        [SerializeField] private string value;
        [Header("Button Animation Settings")]
        [SerializeField] private float bttnspeed = 0.1f;
        [SerializeField] private float moveDist = 0.0025f;
        [SerializeField] private float buttonPressedTime = 0.1f;
        [Header("Component References")]
        [SerializeField] private Keypad keypad;

        private XRSimpleInteractable xrInteractable;
        private bool xrWired;
        private XRInteractionManager xrManager;

        private void OnEnable()
        {
            EnsureXrInteractable();
        }

        private void OnDisable()
        {
            if (xrInteractable != null && xrWired)
            {
                xrInteractable.selectEntered.RemoveListener(OnSelected);
                xrWired = false;
            }
        }

        private void EnsureXrInteractable()
        {
            xrInteractable = GetComponent<XRSimpleInteractable>();
            if (xrInteractable == null)
                xrInteractable = gameObject.AddComponent<XRSimpleInteractable>();

            if (xrManager == null)
                xrManager = FindObjectOfType<XRInteractionManager>(true);

            if (xrInteractable.interactionManager == null && xrManager != null)
                xrInteractable.interactionManager = xrManager;

            Collider[] cols = GetComponentsInChildren<Collider>(true);
            if (cols != null && cols.Length > 0)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null)
                        cols[i].isTrigger = false;
                }

                xrInteractable.colliders.Clear();
                xrInteractable.colliders.AddRange(cols);
            }

            if (!xrWired)
            {
                xrInteractable.selectEntered.AddListener(OnSelected);
                xrWired = true;
            }
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            PressButton();
        }

        private bool moving;

        public void PressButton()
        {
            if (!moving)
            {
                keypad.AddInput(value);
                StartCoroutine(MoveSmooth());
            }
        }

        private IEnumerator MoveSmooth()
        {

            moving = true;
            Vector3 startPos = transform.localPosition;
            Vector3 endPos = transform.localPosition + new Vector3(0, 0, moveDist);

            float elapsedTime = 0;
            while (elapsedTime < bttnspeed)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / bttnspeed);

                transform.localPosition = Vector3.Lerp(startPos, endPos, t);

                yield return null;
            }
            transform.localPosition = endPos;
            yield return new WaitForSeconds(buttonPressedTime);
            startPos = transform.localPosition;
            endPos = transform.localPosition - new Vector3(0, 0, moveDist);

            elapsedTime = 0;
            while (elapsedTime < bttnspeed)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / bttnspeed);

                transform.localPosition = Vector3.Lerp(startPos, endPos, t);

                yield return null;
            }
            transform.localPosition = endPos;

            moving = false;
        }
    }
}
