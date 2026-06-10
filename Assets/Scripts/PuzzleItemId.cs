using UnityEngine;

public class PuzzleItemId : MonoBehaviour
{
    [Header("Puzzle Item ID")]
    [SerializeField] private string itemId = "item_id";

    public string ItemId => itemId;
}