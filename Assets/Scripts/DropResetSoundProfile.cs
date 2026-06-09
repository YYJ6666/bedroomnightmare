using UnityEngine;

public class DropResetSoundProfile : MonoBehaviour
{
    [Header("Drop Sound")]
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private float volume = 1f;

    [Tooltip("音效播放多久后开始黑屏。0 表示自动按音频长度等待一小段。")]
    [SerializeField] private float waitBeforeFade = 0f;

    public AudioClip DropSound => dropSound;
    public float Volume => volume;

    public float GetWaitBeforeFade()
    {
        if (waitBeforeFade > 0f)
            return waitBeforeFade;

        if (dropSound != null)
            return Mathf.Min(dropSound.length, 1.2f);

        return 0.2f;
    }
}