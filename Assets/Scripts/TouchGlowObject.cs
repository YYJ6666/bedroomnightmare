using UnityEngine;

public class TouchGlowObject : MonoBehaviour
{
    [Header("Outlines")]
    public Outline[] outlines;

    [Header("Settings")]
    public float glowDuration = 3f;

    private float timer;
    private bool isRevealed;

    private void Awake()
    {
        SetOutlines(false);
    }

    private void Update()
    {
        if (!isRevealed) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            SetOutlines(false);
            isRevealed = false;
        }
    }

    public void Reveal()
    {
        if (outlines == null || outlines.Length == 0)
        {
            Debug.LogWarning($"{name}: No outlines assigned.");
            return;
        }

        SetOutlines(true);
        timer = glowDuration;
        isRevealed = true;
    }

    private void SetOutlines(bool enabled)
    {
        if (outlines == null) return;

        foreach (Outline outline in outlines)
        {
            if (outline != null)
            {
                outline.enabled = enabled;
            }
        }
    }
}