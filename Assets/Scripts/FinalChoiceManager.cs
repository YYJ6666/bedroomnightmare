using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FinalChoiceManager : MonoBehaviour
{
    public static FinalChoiceManager Instance { get; private set; }

    [Header("Task Stage")]
    [SerializeField] private string finalChoiceTaskId = "final_choice";
    [SerializeField] private bool requireCurrentTask = true;

    [Header("Glow Objects")]
    [SerializeField] private TouchGlowObject bedGlowObject;
    [SerializeField] private TouchGlowObject doorExitGlowObject;

    [Header("Scene Names")]
    [SerializeField] private string ending1SceneName = "Ending1";
    [SerializeField] private string ending2SceneName = "Ending2";

    [Header("Dialogue")]
    [SerializeField] private bool showStartDialogue = true;

    [TextArea(2, 6)]
    [SerializeField] private string startDialogue =
        "门开了。\n可是她的声音又一次响起：\n不要走……回来。";

    [SerializeField] private bool showDoorWarningDialogue = true;

    [TextArea(2, 6)]
    [SerializeField] private string doorWarningDialogue =
        "你还是选择离开了。";

    [SerializeField] private bool showBedDialogue = true;

    [TextArea(2, 6)]
    [SerializeField] private string bedDialogue =
        "你回到了床上。\n这一次，你选择留下。";

    [Header("Timing")]
    [SerializeField] private float sceneLoadDelay = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool hasStartedFinalChoice = false;
    private bool hasChosenEnding = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        TryStartFinalChoice();
    }

    private void Update()
    {
        if (!hasStartedFinalChoice)
            TryStartFinalChoice();
    }

    private void TryStartFinalChoice()
    {
        if (hasStartedFinalChoice)
            return;

        if (requireCurrentTask)
        {
            if (TaskChainManager.Instance == null)
                return;

            if (!TaskChainManager.Instance.IsCurrentTask(finalChoiceTaskId))
                return;
        }

        hasStartedFinalChoice = true;

        if (logDebug)
            Debug.Log($"[FinalChoiceManager] 最终选择阶段开始：{finalChoiceTaskId}");

        if (bedGlowObject != null)
            bedGlowObject.RevealAndKeep();

        if (doorExitGlowObject != null)
            doorExitGlowObject.RevealAndKeep();

        if (showStartDialogue && !string.IsNullOrWhiteSpace(startDialogue))
            DialogueOverlay.Show(startDialogue.Replace("\\n", "\n"));
    }

    public void ChooseDoorEnding()
    {
        if (!CanChoose())
            return;

        hasChosenEnding = true;

        if (logDebug)
            Debug.Log("[FinalChoiceManager] 玩家选择门后结局。");

        if (showDoorWarningDialogue && !string.IsNullOrWhiteSpace(doorWarningDialogue))
            DialogueOverlay.Show(doorWarningDialogue.Replace("\\n", "\n"));

        StartCoroutine(LoadSceneAfterDelay(ending1SceneName));
    }

    public void ChooseBedEnding()
    {
        if (!CanChoose())
            return;

        hasChosenEnding = true;

        if (logDebug)
            Debug.Log("[FinalChoiceManager] 玩家选择回到床上结局。");

        if (showBedDialogue && !string.IsNullOrWhiteSpace(bedDialogue))
            DialogueOverlay.Show(bedDialogue.Replace("\\n", "\n"));

        StartCoroutine(LoadSceneAfterDelay(ending2SceneName));
    }

    private bool CanChoose()
    {
        if (hasChosenEnding)
            return false;

        if (!hasStartedFinalChoice)
            TryStartFinalChoice();

        if (!hasStartedFinalChoice)
            return false;

        if (requireCurrentTask)
        {
            if (TaskChainManager.Instance == null)
                return false;

            if (!TaskChainManager.Instance.IsCurrentTask(finalChoiceTaskId))
                return false;
        }

        return true;
    }

    private IEnumerator LoadSceneAfterDelay(string sceneName)
    {
        if (sceneLoadDelay > 0f)
            yield return new WaitForSeconds(sceneLoadDelay);

        SceneManager.LoadScene(sceneName);
    }
}