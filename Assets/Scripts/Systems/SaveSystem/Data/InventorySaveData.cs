using System;
using System.Collections.Generic;

/// <summary>
/// 인벤토리 저장 데이터
/// </summary>
[Serializable]
public class InventorySaveData
{
    public List<ItemStackSaveData> items;

    public InventorySaveData()
    {
        items = new List<ItemStackSaveData>();
    }
}

/// <summary>
/// 아이템 스택 저장 데이터
/// </summary>
[Serializable]
public class ItemStackSaveData
{
    public int itemId;      // ItemData ScriptableObject ID
    public int amount;
}

/// <summary>
/// 드롭된 아이템 저장 데이터 (바닥에 떨어진 아이템)
/// </summary>
[Serializable]
public class DroppedItemSaveData
{
    public int instanceId;
    public int itemId;      // ItemData ScriptableObject ID
    public int amount;
    public float posX;
    public float posY;
}
