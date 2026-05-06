using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥에 떨어진 아이템(ClickableItem)의 저장/복원 모듈.
///
/// 저장: 씬 내 모든 ClickableItem을 DroppedItemSaveData로 캡처.
/// 복원: 아이템을 인벤토리에 직접 추가 (월드 스폰 대신).
///       향후 월드 스폰이 필요하면 Restore에서 프리팹 인스턴스화 추가 가능.
/// </summary>
public class DroppedItemSaveModule : MonoBehaviour, ISaveModule
{
    public int SaveOrder => 70;

    public void Capture(SaveData data)
    {
        data.droppedItems = new List<DroppedItemSaveData>();

        var items = FindObjectsByType<ClickableItem>(FindObjectsSortMode.None);
        foreach (var item in items)
        {
            ItemData itemData = item.GetItemData();
            if (itemData == null) continue;

            data.droppedItems.Add(new DroppedItemSaveData
            {
                itemId = itemData.itemID,
                amount = 1,
                posX = item.transform.position.x,
                posY = item.transform.position.y
            });
        }

        Debug.Log($"[DroppedItemSaveModule] Capture: {data.droppedItems.Count}개 드롭 아이템 저장");
    }

    public void Restore(SaveData data)
    {
        // 기존 드롭 아이템 제거
        var existingItems = FindObjectsByType<ClickableItem>(FindObjectsSortMode.None);
        foreach (var item in existingItems)
        {
            Destroy(item.gameObject);
        }

        if (data.droppedItems == null || data.droppedItems.Count == 0) return;

        // 드롭 아이템을 인벤토리에 추가 (월드 스폰 대신)
        if (InventoryManager.instance == null || GameDatabase.Instance == null) return;

        int restoredCount = 0;
        foreach (var droppedData in data.droppedItems)
        {
            ItemData itemData = GameDatabase.Instance.GetItemData(droppedData.itemId);
            if (itemData == null) continue;

            InventoryManager.instance.AddItem(itemData, droppedData.amount);
            restoredCount++;
        }

        Debug.Log($"[DroppedItemSaveModule] Restore: {restoredCount}개 드롭 아이템 → 인벤토리 복원");
    }

    public void PostRestore(SaveData data) { }
}
