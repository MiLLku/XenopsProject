using UnityEngine;

[System.Serializable]
public struct ResourceCost
{
    // 어떤 아이템이 필요한지 (예: Stone_ItemData 애셋)
    public ItemData item;
    
    // 몇 개가 필요한지
    public int amount;
}
