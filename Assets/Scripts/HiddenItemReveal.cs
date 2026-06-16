using UnityEngine;
using System.Collections;

public class HiddenItemReveal : MonoBehaviour
{
    [Header("Task")]
    
    [SerializeField]
    private string revealTaskId = "find_ring";
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip revealClip;
    [SerializeField] private float audioDelay = 0.5f;

    [Header("Spawn")]
    [SerializeField]
    private Transform spawnPoint;

    [SerializeField]
    private Rigidbody rb;

    [SerializeField]
    private Collider[] colliders;

    [SerializeField]
    private Renderer[] renderers;

    [Header("Drop")]
    [SerializeField]
    private float dropForce = 0.2f;

    [Header("Glow")]
    [SerializeField]
    private TouchGlowObject glowObject;

    [Header("Hint")]
    [TextArea]
    [SerializeField]
    private string hint =
        "有什么东西从衣服里掉出来了……";

    private bool revealed = false;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (colliders == null || colliders.Length == 0)
            colliders = GetComponentsInChildren<Collider>(true);

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        if (glowObject == null)
            glowObject = GetComponent<TouchGlowObject>();

        HideItem();
    }
    private IEnumerator PlayRevealAudioRoutine()
    {
        if (audioDelay > 0f)
            yield return new WaitForSeconds(audioDelay);

        if (audioSource != null && revealClip != null)
            audioSource.PlayOneShot(revealClip);
    }

    private void Update()
    {
        if (revealed)
            return;

        if (TaskChainManager.Instance == null)
            return;

        if (!TaskChainManager.Instance.IsCurrentTask(revealTaskId))
            return;

        RevealItem();
    }

    private void HideItem()
    {
        foreach (var r in renderers)
        {
            if (r != null)
                r.enabled = false;
        }

        foreach (var c in colliders)
        {
            if (c != null)
                c.enabled = false;
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void RevealItem()
    {
        revealed = true;

        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }

        foreach (var r in renderers)
        {
            if (r != null)
                r.enabled = true;
        }

        foreach (var c in colliders)
        {
            if (c != null)
                c.enabled = true;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            rb.AddForce(
                Random.insideUnitSphere * dropForce,
                ForceMode.Impulse);
        }

        if (glowObject != null)
        {
            glowObject.RevealAndKeep();
        }
        if (revealClip != null)
        {
            StartCoroutine(PlayRevealAudioRoutine());
        }

        if (!string.IsNullOrWhiteSpace(hint))
        {
            OperationHintOverlay.Show(
                hint,
                6f);
        }
    }
}