using UnityEngine;

public class TouchGlowObject : MonoBehaviour
{
    public Outline outline;
    public float glowDuration = 3f;

    private float timer;
    private bool isRevealed;

    private void Awake()
    {
        if (outline != null)
            outline.enabled = false;
    }

    private void Update()
    {
        if (!isRevealed) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            outline.enabled = false;
            isRevealed = false;
        }
    }

    public void Reveal()
    {
        if (outline == null) return;

        outline.enabled = true;
        timer = glowDuration;
        isRevealed = true;
    }
}