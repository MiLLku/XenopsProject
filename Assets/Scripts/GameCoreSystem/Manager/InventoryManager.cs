using UnityEngine;
using System.Collections.Generic;

public class InventoryManager : DestroySingleton<InventoryManager>
{
    // 글로벌 인벤토리: (Key: 아이템 데이터, Value: 아이템 개수)
    public Dictionary<ItemData, int> globalInventory = new Dictionary<ItemData, int>();

    /// <summary>
    /// 글로벌 인벤토리에 지정된 수량의 아이템을 추가합니다.
    /// </summary>
    public void AddItem(ItemData itemData, int amount = 1) // ★ 수량(amount) 파라미터 추가
    {
        if (itemData == null || amount <= 0) return;

        if (globalInventory.ContainsKey(itemData))
        {
            globalInventory[itemData] += amount; // 수량 증가
        }
        else
        {
            globalInventory[itemData] = amount; // 새로 등록
        }
        
        Debug.Log($"[InventoryManager] '{itemData.itemName}' {amount}개 추가. (현재 총: {globalInventory[itemData]}개)");
        
        // (나중에 여기에 인벤토리 UI를 새로고침하는 코드를 추가)
        // UpdateInventoryUI();
    }

    /// <summary>
    /// 인벤토리에 레시피에 필요한 재료가 '모두' 있는지 확인합니다.
    /// </summary>
    public bool HasItems(List<ResourceCost> requiredMaterials)
    {
        foreach (var cost in requiredMaterials)
        {
            // 1. 인벤토리에 해당 아이템이 아예 없거나
            if (!globalInventory.ContainsKey(cost.item))
            {
                return false; // 재료 부족
            }
            
            // 2. 아이템은 있지만 수량이 부족할 때
            if (globalInventory[cost.item] < cost.amount)
            {
                return false; // 재료 부족
            }
        }
        return true; // 모든 재료를 통과
    }
    
    /// <summary>
    /// 인벤토리에서 레시피에 필요한 재료를 '모두' 제거합니다. (먼저 HasItems로 검사해야 함)
    /// </summary>
    public bool RemoveItems(List<ResourceCost> requiredMaterials)
    {
        // (안전장치) 한 번 더 확인
        if (!HasItems(requiredMaterials))
        {
            Debug.LogWarning("[InventoryManager] 재료가 부족하여 아이템을 제거할 수 없습니다.");
            return false;
        }
        
        // 재료가 충분하므로 실제로 제거
        foreach (var cost in requiredMaterials)
        {
            globalInventory[cost.item] -= cost.amount;
            Debug.Log($"[InventoryManager] '{cost.item.itemName}' {cost.amount}개 사용. (남은 수량: {globalInventory[cost.item]})");
            
            // (선택) 만약 수량이 0이 되면 딕셔너리에서 키를 삭제
            // if (globalInventory[cost.item] == 0)
            // {
            //     globalInventory.Remove(cost.item);
            // }
        }
        return true;
    }
}