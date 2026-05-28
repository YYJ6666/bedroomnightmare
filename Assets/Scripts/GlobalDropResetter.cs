using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

public sealed class GlobalDropResetter : MonoBehaviour
{
    private static GlobalDropResetter instance;

    [Header("Detection")]
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float minWorldYToTrigger = -0.15f;
    [SerializeField] private float minSecondsAfterRelease = 0.05f;
    [SerializeField] private float cooldownSeconds = 0.5f;

    [Header("Reset")]
    [SerializeField] private float resetDelaySeconds = 0.1f;

    private bool resetting;
    private float lastTriggerTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(nameof(GlobalDropResetter));
        instance = go.AddComponent<GlobalDropResetter>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        resetting = false;

        if (groundLayers.value == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                groundLayers = 1 << groundLayer;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        AttachWatchersInScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines();
        resetting = false;
        AttachWatchersInScene();
    }

    private void AttachWatchersInScene()
    {
        XRGrabInteractable[] grabbables = FindObjectsOfType<XRGrabInteractable>(true);
        for (int i = 0; i < grabbables.Length; i++)
        {
            XRGrabInteractable grabbable = grabbables[i];
            if (grabbable == null)
                continue;

            DropWatcher watcher = grabbable.GetComponent<DropWatcher>();
            if (watcher == null)
                watcher = grabbable.gameObject.AddComponent<DropWatcher>();

            watcher.Bind(this, grabbable);
        }
    }

    internal bool IsGroundCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.GetComponentInParent<XROrigin>(true) != null)
            return false;

        if (!string.IsNullOrWhiteSpace(groundTag) && other.CompareTag(groundTag))
            return true;

        if (groundLayers.value != 0 && (groundLayers.value & (1 << other.gameObject.layer)) != 0)
            return true;

        return false;
    }

    internal bool ShouldTriggerByWorldY(float worldY)
    {
        return worldY <= minWorldYToTrigger;
    }

    internal float MinSecondsAfterRelease => minSecondsAfterRelease;

    internal void TriggerReset()
    {
        if (resetting)
            return;

        float now = Time.unscaledTime;
        if (now - lastTriggerTime < cooldownSeconds)
            return;

        lastTriggerTime = now;
        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        resetting = true;
        if (resetDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(resetDelaySeconds);

        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    [DisallowMultipleComponent]
    private sealed class DropWatcher : MonoBehaviour
    {
        private GlobalDropResetter controller;
        private XRGrabInteractable grabbable;
        private Rigidbody rb;

        private bool wasReleased;
        private float releasedAtUnscaledTime;

        public void Bind(GlobalDropResetter newController, XRGrabInteractable newGrabbable)
        {
            if (grabbable != null)
            {
                grabbable.selectExited.RemoveListener(OnSelectExited);
                grabbable.selectEntered.RemoveListener(OnSelectEntered);
            }

            controller = newController;
            grabbable = newGrabbable;

            if (grabbable != null)
            {
                grabbable.selectEntered.AddListener(OnSelectEntered);
                grabbable.selectExited.AddListener(OnSelectExited);
            }

            rb = GetComponent<Rigidbody>();
        }

        private void OnDisable()
        {
            if (grabbable != null)
            {
                grabbable.selectExited.RemoveListener(OnSelectExited);
                grabbable.selectEntered.RemoveListener(OnSelectEntered);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            wasReleased = false;
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            wasReleased = true;
            releasedAtUnscaledTime = Time.unscaledTime;
        }

        private void Update()
        {
            if (!wasReleased || controller == null)
                return;

            if (Time.unscaledTime - releasedAtUnscaledTime < controller.MinSecondsAfterRelease)
                return;

            if (controller.ShouldTriggerByWorldY(transform.position.y))
                controller.TriggerReset();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!wasReleased || controller == null)
                return;

            if (Time.unscaledTime - releasedAtUnscaledTime < controller.MinSecondsAfterRelease)
                return;

            if (collision == null)
                return;

            Collider other = collision.collider;
            if (other == null)
                return;

            if (other.transform.IsChildOf(transform))
                return;

            if (controller.IsGroundCollider(other))
                controller.TriggerReset();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!wasReleased || controller == null)
                return;

            if (Time.unscaledTime - releasedAtUnscaledTime < controller.MinSecondsAfterRelease)
                return;

            if (other == null)
                return;

            if (other.transform.IsChildOf(transform))
                return;

            if (controller.IsGroundCollider(other))
                controller.TriggerReset();
        }
    }
}
