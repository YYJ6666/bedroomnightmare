using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ScenePeriodicSoundPlayer : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("不填则使用 AudioSource 自己的 Clip。")]
    [SerializeField] private AudioClip audioClip;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Header("Timing")]
    [Tooltip("进入场景后，第一次播放前等待多久。")]
    [SerializeField] private float firstDelay = 1f;

    [Tooltip("每次音频播放结束后，再等待多久播放下一次。")]
    [SerializeField] private float intervalAfterClip = 5f;

    [Header("Behavior")]
    [SerializeField] private bool playOnStart = true;

    private Coroutine playRoutine;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (playOnStart)
            StartLoop();
    }

    public void StartLoop()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(LoopRoutine());
    }

    public void StopLoop()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (audioSource != null)
            audioSource.Stop();
    }

    private IEnumerator LoopRoutine()
    {
        if (firstDelay > 0f)
            yield return new WaitForSeconds(firstDelay);

        while (true)
        {
            AudioClip clip = audioClip != null ? audioClip : audioSource.clip;

            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
                yield return new WaitForSeconds(clip.length);
            }

            if (intervalAfterClip > 0f)
                yield return new WaitForSeconds(intervalAfterClip);
            else
                yield return null;
        }
    }
}