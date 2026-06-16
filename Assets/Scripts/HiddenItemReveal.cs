using UnityEngine;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit;

public class HiddenItemReveal : MonoBehaviour, ICheckpointStateHandler
{
    [System.Serializable]
    private class RevealCheckpointState
    {
        public bool revealed;
    }

    [Header("Task")]
    [SerializeField] private string revealTaskId = "find_ring";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip revealClip;
    [SerializeField] private float audioDelay = 0.5f;

    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;

    [SerializeField] private Rigidbody rb;

    [Header("Drop")]
    [SerializeField] private float dropForce = 0.2f;

    [Header("Glow")]
    [SerializeField] private TouchGlowObject glowObject;

    [Header("Hint")]
    [TextArea]
    [SerializeField]
    private string hint = "有什么东西从衣服里掉出来了……";

    [Header("Hide Settings")]
    [SerializeField] private bool hideByScale = true;

    private Collider[] colliders;
    private Renderer[] renderers;
    private LODGroup[] lodGroups;
    private Light[] lights;
    private XRGrabInteractable[] grabInteractables;

    private Vector3 originalLocalScale;

    private bool revealed = false;
    private bool hasObservedTaskState = false;
    private bool wasCurrentTask = false;

    private void Awake()
    {
        InitComponents();

        originalLocalScale = transform.localScale;

        HideItem();
    }

    private void Start()
    {
        // 再隐藏一次，防止其他脚本在 Awake 之后把模型打开
        HideItem();

        // 延迟一帧后继续隐藏，防止 LODGroup 或 Prefab 初始化后又显示
        StartCoroutine(ForceHideAtStartRoutine());
    }

    private IEnumerator ForceHideAtStartRoutine()
    {
        yield return null;

        if (!revealed)
        {
            HideItem();
        }

        yield return new WaitForEndOfFrame();

        if (!revealed)
        {
            HideItem();
        }
    }

    private void InitComponents()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (glowObject == null)
            glowObject = GetComponent<TouchGlowObject>();

        // 强制自动获取，不依赖 Inspector 里原来拖的数组
        colliders = GetComponentsInChildren<Collider>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
        lodGroups = GetComponentsInChildren<LODGroup>(true);
        lights = GetComponentsInChildren<Light>(true);
        grabInteractables = GetComponentsInChildren<XRGrabInteractable>(true);
    }

    private void Update()
    {
        if (revealed)
            return;

        // 隐藏期间每帧保持隐藏，防止 LODGroup 或其他脚本重新打开模型
        HideItem();

        if (TaskChainManager.Instance == null)
            return;

        bool isCurrentTask = TaskChainManager.Instance.IsCurrentTask(revealTaskId);

        // 第一次检测任务状态时只记录，不立刻显示
        // 这样即使 find_ring 一开场就是当前任务，也不会直接露出来
        if (!hasObservedTaskState)
        {
            wasCurrentTask = isCurrentTask;
            hasObservedTaskState = true;
            return;
        }

        // 只有任务从“不是 find_ring”切换到“find_ring”时才显示
        if (!wasCurrentTask && isCurrentTask)
        {
            RevealItem();
            return;
        }

        wasCurrentTask = isCurrentTask;
    }

    private void HideItem()
    {
        // 1. 关闭 LODGroup
        if (lodGroups != null)
        {
            foreach (var lod in lodGroups)
            {
                if (lod != null)
                    lod.enabled = false;
            }
        }

        // 2. 关闭所有 Renderer，包括 Ring19_LOD0 / Ring19_LOD1
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                    r.enabled = false;
            }
        }

        // 3. 关闭所有 Collider，防止被选中
        if (colliders != null)
        {
            foreach (var c in colliders)
            {
                if (c != null)
                    c.enabled = false;
            }
        }

        // 4. 关闭 Point Light
        if (lights != null)
        {
            foreach (var l in lights)
            {
                if (l != null)
                    l.enabled = false;
            }
        }

        // 5. 关闭 XR 抓取
        if (grabInteractables != null)
        {
            foreach (var grab in grabInteractables)
            {
                if (grab != null)
                    grab.enabled = false;
            }
        }

        // 6. 最保险：直接把根物体缩小到 0
        // 不 SetActive(false)，否则脚本自己也停了
        if (hideByScale)
        {
            transform.localScale = Vector3.zero;
        }

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    private void RevealItem()
    {
        revealed = true;

        // 先恢复缩放
        if (hideByScale)
        {
            transform.localScale = originalLocalScale;
        }

        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }

        // 开启 LODGroup
        if (lodGroups != null)
        {
            foreach (var lod in lodGroups)
            {
                if (lod != null)
                    lod.enabled = true;
            }
        }

        // 开启 Renderer
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                    r.enabled = true;
            }
        }

        // 开启 Collider
        if (colliders != null)
        {
            foreach (var c in colliders)
            {
                if (c != null)
                    c.enabled = true;
            }
        }

        // 开启 Point Light
        if (lights != null)
        {
            foreach (var l in lights)
            {
                if (l != null)
                    l.enabled = true;
            }
        }

        // 开启 XR 抓取
        if (grabInteractables != null)
        {
            foreach (var grab in grabInteractables)
            {
                if (grab != null)
                    grab.enabled = true;
            }
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 force =
                Vector3.down * dropForce +
                Random.insideUnitSphere * dropForce * 0.3f;

            rb.AddForce(force, ForceMode.Impulse);
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
            OperationHintOverlay.Show(hint, 6f);
        }
    }

    public string CaptureCheckpointState()
    {
        RevealCheckpointState state = new RevealCheckpointState();
        state.revealed = revealed || ShouldBeRevealedForCurrentTask();

        return JsonUtility.ToJson(state);
    }

    public void RestoreCheckpointState(string stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
            return;

        RevealCheckpointState state = JsonUtility.FromJson<RevealCheckpointState>(stateJson);

        if (state == null || !state.revealed)
            return;

        revealed = true;

        if (hideByScale)
            transform.localScale = originalLocalScale;

        ApplyRevealedComponentState();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        if (glowObject != null)
            glowObject.RevealAndKeep();
    }

    private bool ShouldBeRevealedForCurrentTask()
    {
        if (TaskChainManager.Instance == null)
            return false;

        return TaskChainManager.Instance.IsCurrentTask(revealTaskId);
    }

    private void ApplyRevealedComponentState()
    {
        if (lodGroups != null)
        {
            foreach (var lod in lodGroups)
            {
                if (lod != null)
                    lod.enabled = true;
            }
        }

        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                    r.enabled = true;
            }
        }

        if (colliders != null)
        {
            foreach (var c in colliders)
            {
                if (c != null)
                    c.enabled = true;
            }
        }

        if (lights != null)
        {
            foreach (var l in lights)
            {
                if (l != null)
                    l.enabled = true;
            }
        }

        if (grabInteractables != null)
        {
            foreach (var grab in grabInteractables)
            {
                if (grab != null)
                    grab.enabled = true;
            }
        }
    }

    private IEnumerator PlayRevealAudioRoutine()
    {
        if (audioDelay > 0f)
            yield return new WaitForSeconds(audioDelay);

        if (audioSource != null && revealClip != null)
            audioSource.PlayOneShot(revealClip);
    }
}
