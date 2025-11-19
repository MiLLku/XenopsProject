using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class InventoryManager : DestroySingleton<InventoryManager>
{
    // 글로벌 인벤토리: (Key: 아이템 데이터, Value: 아이템 개수)
    public Dictionary<ItemData, int> globalInventory = new Dictionary<ItemData, int>();
    
    // 인벤토리 변경 이벤트
    public delegate void InventoryChangedDelegate(ItemData item, int changeAmount);
    public event InventoryChangedDelegate OnInventoryChanged;
    
    // 인벤토리 제한 (선택사항)
    [Header("인벤토리 설정")]
    [SerializeField] private int maxStackSize = 999; // 아이템당 최대 스택
    [SerializeField] private bool showDebugLogs = true;

    /// <summary>
    /// 글로벌 인벤토리에 지정된 수량의 아이템을 추가합니다.
    /// </summary>
    public bool AddItem(ItemData itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0) return false;

        int currentAmount = GetItemCount(itemData);
        int newAmount = currentAmount + amount;
        
        // 최대 스택 체크
        if (newAmount > maxStackSize)
        {
            int actualAdded = maxStackSize - currentAmount;
            if (actualAdded <= 0)
            {
                if (showDebugLogs) Debug.LogWarning($"[InventoryManager] '{itemData.itemName}' 스택이 가득 참 (최대: {maxStackSize})");
                return false;
            }
            amount = actualAdded;
            newAmount = maxStackSize;
        }
        
        globalInventory[itemData] = newAmount;
        
        if (showDebugLogs) 
            Debug.Log($"[InventoryManager] '{itemData.itemName}' {amount}개 추가. (현재 총: {newAmount}개)");
        
        // 이벤트 발생
        OnInventoryChanged?.Invoke(itemData, amount);
        
        return true;
    }
    
    /// <summary>
    /// 특정 아이템의 현재 개수를 반환합니다.
    /// </summary>
    public int GetItemCount(ItemData itemData)
    {
        if (itemData == null) return 0;
        return globalInventory.TryGetValue(itemData, out int count) ? count : 0;
    }

    /// <summary>
    /// 인벤토리에 레시피에 필요한 재료가 '모두' 있는지 확인합니다.
    /// </summary>
    public bool HasItems(List<ResourceCost> requiredMaterials)
    {
        if (requiredMaterials == null) return true;
        
        foreach (var cost in requiredMaterials)
        {
            if (GetItemCount(cost.item) < cost.amount)
            {
                return false;
            }
        }
        return true;
    }
    
    /// <summary>
    /// 특정 아이템이 지정된 개수만큼 있는지 확인합니다.
    /// </summary>
    public bool HasItem(ItemData item, int amount = 1)
    {
        return GetItemCount(item) >= amount;
    }
    
    /// <summary>
    /// 인벤토리에서 레시피에 필요한 재료를 '모두' 제거합니다.
    /// </summary>
    public bool RemoveItems(List<ResourceCost> requiredMaterials)
    {
        if (requiredMaterials == null) return true;
        
        // 안전장치: 먼저 모든 아이템이 충분한지 확인
        if (!HasItems(requiredMaterials))
        {
            if (showDebugLogs) 
                Debug.LogWarning("[InventoryManager] 재료가 부족하여 아이템을 제거할 수 없습니다.");
            return false;
        }
        
        // 재료가 충분하므로 실제로 제거
        foreach (var cost in requiredMaterials)
        {
            RemoveItem(cost.item, cost.amount);
        }
        return true;
    }
    
    /// <summary>
    /// 특정 아이템을 지정된 개수만큼 제거합니다.
    /// </summary>
    public bool RemoveItem(ItemData itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0) return false;
        
        int currentAmount = GetItemCount(itemData);
        if (currentAmount < amount)
        {
            if (showDebugLogs) 
                Debug.LogWarning($"[InventoryManager] '{itemData.itemName}' 제거 실패. 보유: {currentAmount}, 요청: {amount}");
            return false;
        }
        
        int newAmount = currentAmount - amount;
        
        if (newAmount == 0)
        {
            globalInventory.Remove(itemData);
            if (showDebugLogs) 
                Debug.Log($"[InventoryManager] '{itemData.itemName}' 모두 소진됨");
        }
        else
        {
            globalInventory[itemData] = newAmount;
            if (showDebugLogs) 
                Debug.Log($"[InventoryManager] '{itemData.itemName}' {amount}개 사용. (남은 수량: {newAmount})");
        }
        
        // 이벤트 발생 (음수로 제거 표시)
        OnInventoryChanged?.Invoke(itemData, -amount);
        
        return true;
    }
    
    /// <summary>
    /// 인벤토리를 완전히 비웁니다.
    /// </summary>
    public void ClearInventory()
    {
        globalInventory.Clear();
        if (showDebugLogs) Debug.Log("[InventoryManager] 인벤토리가 비워졌습니다.");
        OnInventoryChanged?.Invoke(null, 0); // 전체 초기화 신호
    }
    
    /// <summary>
    /// 현재 인벤토리의 모든 아이템 목록을 반환합니다.
    /// </summary>
    public List<KeyValuePair<ItemData, int>> GetAllItems()
    {
        return globalInventory.ToList();
    }
    
    /// <summary>
    /// 인벤토리에 있는 아이템의 총 개수를 반환합니다.
    /// </summary>
    public int GetTotalItemCount()
    {
        int total = 0;
        foreach (var kvp in globalInventory)
        {
            total += kvp.Value;
        }
        return total;
    }
    
    /// <summary>
    /// 인벤토리에 있는 아이템 종류의 개수를 반환합니다.
    /// </summary>
    public int GetUniqueItemCount()
    {
        return globalInventory.Count;
    }
    
    /// <summary>
    /// 디버그용: 인벤토리 내용을 콘솔에 출력합니다.
    /// </summary>
    [ContextMenu("Print Inventory")]
    public void PrintInventory()
    {
        if (globalInventory.Count == 0)
        {
            Debug.Log("[InventoryManager] 인벤토리가 비어있습니다.");
            return;
        }
        
        string inventoryLog = "[InventoryManager] 현재 인벤토리:\n";
        foreach (var kvp in globalInventory)
        {
            inventoryLog += $"- {kvp.Key.itemName}: {kvp.Value}개\n";
        }
        Debug.Log(inventoryLog);
    }
    
    // 저장/불러오기를 위한 직렬화 가능한 구조
    [System.Serializable]
    public class InventoryData
    {
        public List<int> itemIds = new List<int>();
        public List<int> itemCounts = new List<int>();
    }
    
    /// <summary>
    /// 저장을 위해 인벤토리 데이터를 직렬화합니다.
    /// </summary>
    public InventoryData GetSaveData()
    {
        InventoryData data = new InventoryData();
        foreach (var kvp in globalInventory)
        {
            data.itemIds.Add(kvp.Key.itemID);
            data.itemCounts.Add(kvp.Value);
        }
        return data;
    }
    
    /// <summary>
    /// 저장된 데이터로부터 인벤토리를 복원합니다.
    /// </summary>
    public void LoadSaveData(InventoryData data, ItemDatabase itemDatabase)
    {
        if (data == null || itemDatabase == null) return;
        
        ClearInventory();
        
        for (int i = 0; i < data.itemIds.Count; i++)
        {
            ItemData item = itemDatabase.GetItemByID(data.itemIds[i]);
            if (item != null)
            {
                globalInventory[item] = data.itemCounts[i];
            }
        }
        
        if (showDebugLogs) Debug.Log($"[InventoryManager] {data.itemIds.Count}개 아이템 불러오기 완료");
    }
}

// 아이템 데이터베이스 인터페이스 (나중에 구현 필요)
public interface ItemDatabase
{
    ItemData GetItemByID(int id);
}