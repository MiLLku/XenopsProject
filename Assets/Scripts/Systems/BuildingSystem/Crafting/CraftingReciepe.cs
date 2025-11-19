using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewRecipe", menuName = "StampSystem/Crafting Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Header("제작 정보")]
    [Tooltip("제작법의 고유 ID (예: 5001)")]
    public int recipeID;
        
    [Tooltip("생산될 아이템 (예: Part_ItemData)")]
    public ItemData outputItem;
        
    [Tooltip("한 번에 생산될 개수")]
    public int outputAmount = 1;

    [Header("제작 비용")]
    [Tooltip("제작에 필요한 자원 목록")]
    public List<ResourceCost> requiredMaterials; 
        
    [Tooltip("제작에 걸리는 시간(초)")]
    public float craftingTime = 2f;
}