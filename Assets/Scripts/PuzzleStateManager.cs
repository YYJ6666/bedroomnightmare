using System;
using UnityEngine;

public class PuzzleStateManager : MonoBehaviour
{
    public static PuzzleStateManager Instance { get; private set; }

    public enum PuzzleStage
    {
        WakeUp = 0,            // 醒来
        FindPhoto = 1,         // 找照片
        FindDress = 2,         // 找正确衣服
        FindRing = 3,          // 找婚戒
        FindDeathNotice = 4,   // 找医院单据
        RestoreMemory = 5,     // 还原记忆顺序
        DoorUnlocked = 6,      // 门已解锁
        Ending = 7             // 结局
    }

    [Header("Current Main Stage")]
    [SerializeField] private PuzzleStage currentStage = PuzzleStage.WakeUp;

    [Header("Detailed Flags")]
    public bool hasFoundPhoto = false;
    public bool hasFoundDress = false;
    public bool hasFoundRing = false;
    public bool hasFoundDeathNotice = false;
    public bool hasCompletedMemorySequence = false;
    public bool isDoorUnlocked = false;

    public PuzzleStage CurrentStage => currentStage;

    public event Action<PuzzleStage> OnStageChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // 直接设置到某个阶段
    public void SetStage(PuzzleStage newStage)
    {
        if (currentStage == newStage) return;

        currentStage = newStage;
        Debug.Log($"Puzzle Stage changed to: {currentStage}");
        OnStageChanged?.Invoke(currentStage);
    }

    // 推进到下一个阶段
    public void AdvanceToNextStage()
    {
        int next = (int)currentStage + 1;

        if (Enum.IsDefined(typeof(PuzzleStage), next))
        {
            SetStage((PuzzleStage)next);
        }
    }

    // 判断当前是不是某阶段
    public bool IsCurrentStage(PuzzleStage stage)
    {
        return currentStage == stage;
    }

    // 判断是否已经推进到某阶段或之后
    public bool HasReachedStage(PuzzleStage stage)
    {
        return currentStage >= stage;
    }

    // ===== 以下是一些封装好的完成接口 =====

    public void CompletePhotoPuzzle()
    {
        hasFoundPhoto = true;
        SetStage(PuzzleStage.FindDress);
    }

    public void CompleteDressPuzzle()
    {
        hasFoundDress = true;
        SetStage(PuzzleStage.FindRing);
    }

    public void CompleteRingPuzzle()
    {
        hasFoundRing = true;
        SetStage(PuzzleStage.FindDeathNotice);
    }

    public void CompleteDeathNoticePuzzle()
    {
        hasFoundDeathNotice = true;
        SetStage(PuzzleStage.RestoreMemory);
    }

    public void CompleteMemorySequencePuzzle()
    {
        hasCompletedMemorySequence = true;
        isDoorUnlocked = true;
        SetStage(PuzzleStage.DoorUnlocked);
    }

    public void CompleteEnding()
    {
        SetStage(PuzzleStage.Ending);
    }
}