using UnityEngine;

public class TouchGlowObject : MonoBehaviour
{
    [Header("Outlines")]
    public Outline[] outlines;

    [Header("Settings")]
    public float glowDuration = 10f;

    private Light[] lights;
    private float timer;
    private bool isRevealed;
    private bool keepRevealed;

    private void Awake()
    {
        if (outlines == null || outlines.Length == 0)
        {
            outlines = GetComponentsInChildren<Outline>(true);
        }

        lights = GetComponentsInChildren<Light>(true);

        SetGlow(false);
    }

    private void Update()
    {
        if (!isRevealed) return;

        // 被抓住或被强制保持发光时，不倒计时
        if (keepRevealed) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            Hide();
        }
    }

    public void Reveal()
    {
        SetGlow(true);
        timer = glowDuration;
        isRevealed = true;
    }

    public void RevealAndKeep()
    {
        keepRevealed = true;
        SetGlow(true);
        isRevealed = true;
    }

    public void ReleaseKeep()
    {
        keepRevealed = false;
        timer = glowDuration;
        isRevealed = true;
    }

    public void Hide()
    {
        keepRevealed = false;
        SetGlow(false);
        isRevealed = false;
        timer = 0f;
    }

    private void SetGlow(bool enabled)
    {
        if (outlines != null)
        {
            foreach (Outline outline in outlines)
            {
                if (outline != null)
                {
                    outline.enabled = enabled;
                }
            }
        }

        if (lights != null)
        {
            foreach (Light light in lights)
            {
                if (light != null)
                {
                    light.enabled = enabled;
                }
            }
        }
    }
}