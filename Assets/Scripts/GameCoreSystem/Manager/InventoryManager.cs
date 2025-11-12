// --- 파일 17: InventoryManager.cs ---
// 모든 아이템을 총괄하는 글로벌 인벤토리입니다.

using UnityEngine;
using System.Collections.Generic;

namespace StampSystem
{
    // 씬에 하나만 존재해야 하는 싱글턴
    public class InventoryManager : DestroySingleton<InventoryManager>
    {
        // 글로벌 인벤토리: (Key: 아이템 데이터, Value: 아이템 개수)
        public Dictionary<ItemData, int> globalInventory = new Dictionary<ItemData, int>();

        /// <summary>
        /// 글로벌 인벤토리에 아이템을 1개 추가합니다.
        /// </summary>
        public void AddItem(ItemData itemData)
        {
            if (itemData == null) return;

            if (globalInventory.ContainsKey(itemData))
            {
                globalInventory[itemData]++; // 수량 1 증가
            }
            else
            {
                globalInventory[itemData] = 1; // 새로 1개 등록
            }
            
            Debug.Log($"[InventoryManager] '{itemData.itemName}' 1개 추가. (현재 총: {globalInventory[itemData]}개)");
        }
    }
}