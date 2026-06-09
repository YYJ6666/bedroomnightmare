using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RepeatingHintUntilCompleted : MonoBehaviour
{
    [Header("Objective")]
    [Tooltip("目标 ID。比如 touch_tv、find_dress、find_ring。")]
    [SerializeField] private string objectiveId = "touch_tv";

    [Header("Scene Filter")]
    [SerializeField] private bool onlyInGameScene = true;
    [SerializeField] private string gameSceneName = "bedroom2";

    [Header("Hint")]
    [TextArea(2, 5)]
    [SerializeField] private string hintText = "电视似乎在响，确认一下怎么了。";

    [SerializeField] private float firstDelay = 3f;
    [SerializeField] private float repeatInterval = 10f;
    [SerializeField] private float visibleSeconds = 4f;

    [Header("Behavior")]
    [SerializeField] private bool startOnEnable = true;
    [SerializeField] private bool useDialogueOverlay = true;
    [SerializeField] private bool useOperationHintOverlay = false;

    private static readonly HashSet<string> completedObjectives = new HashSet<string>();

    private Coroutine hintRoutine;
    private bool isRunning;

    private void OnEnable()
    {
        if (startOnEnable)
        {
            StartRepeating();
        }
    }

    private void OnDisable()
    {
        StopRepeating();
    }

    public void StartRepeating()
    {
        if (!IsInTargetScene())
            return;

        if (string.IsNullOrWhiteSpace(objectiveId))
        {
            Debug.LogWarning($"{name}: Objective Id 为空，无法启动重复提示。");
            return;
        }

        if (IsCompleted(objectiveId))
            return;

        StopRepeating();

        isRunning = true;
        hintRoutine = StartCoroutine(HintRoutine());
    }

    public void StopRepeating()
    {
        isRunning = false;

        if (hintRoutine != null)
        {
            StopCoroutine(hintRoutine);
            hintRoutine = null;
        }
    }

    private IEnumerator HintRoutine()
    {
        if (firstDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(firstDelay);
        }

        while (isRunning && !IsCompleted(objectiveId))
        {
            ShowHint();

            if (repeatInterval <= 0f)
                yield break;

            yield return new WaitForSecondsRealtime(repeatInterval);
        }
    }

    private void ShowHint()
    {
        string finalText = FormatText(hintText);

        if (useDialogueOverlay)
        {
            DialogueOverlay.Show(finalText);
        }

        if (useOperationHintOverlay)
        {
            OperationHintOverlay.Show(finalText, visibleSeconds);
        }
    }

    private string FormatText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Replace("\\n", "\n");
    }

    private bool IsInTargetScene()
    {
        if (!onlyInGameScene)
            return true;

        return SceneManager.GetActiveScene().name == gameSceneName;
    }

    // =========================
    // Static Interfaces
    // =========================

    public static void Complete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        completedObjectives.Add(id);
    }

    public static void Uncomplete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        completedObjectives.Remove(id);
    }

    public static bool IsCompleted(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return completedObjectives.Contains(id);
    }

    public static void ClearAllCompleted()
    {
        completedObjectives.Clear();
    }
}