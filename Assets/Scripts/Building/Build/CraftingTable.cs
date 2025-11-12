using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
    public class CraftingTable : MonoBehaviour
    {
        [Header("제작 설정")]
        [Tooltip("이 작업대에서 만들 수 있는 레시피 목록")]
        [SerializeField] private List<CraftingRecipe> availableRecipes;

        private bool _isCrafting = false; 
        
        private void OnMouseDown()
        {
            if (_isCrafting)
            {
                Debug.LogWarning("작업대가 현재 작동 중입니다.");
                return;
            }

            if (availableRecipes != null && availableRecipes.Count > 0)
            {
                Debug.Log($"[CraftingStation] '{availableRecipes[0].outputItem.itemName}' 제작을 시도합니다...");
                StartCrafting(availableRecipes[0]);
            }
            else
            {
                Debug.LogWarning($"[CraftingStation] 이 작업대에 할당된 레시피가 없습니다.");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void StartCrafting(CraftingRecipe recipe)
        {
            if (_isCrafting) return; // 이미 제작 중이면 반환

            // 1. 인벤토리 매니저에서 재료를 확인
            if (InventoryManager.instance.HasItems(recipe.requiredMaterials))
            {
                // 2. 재료가 있으면, 재료 소모
                InventoryManager.instance.RemoveItems(recipe.requiredMaterials);
                
                // 3. 제작 시작 (코루틴 사용)
                StartCoroutine(CraftingCoroutine(recipe));
            }
            else
            {
                // 4. 재료가 없으면 알림
                Debug.LogError($"[CraftingStation] 재료 부족: '{recipe.outputItem.itemName}'을(를) 만들 수 없습니다.");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private IEnumerator CraftingCoroutine(CraftingRecipe recipe)
        {
            _isCrafting = true;
            Debug.Log($"[CraftingStation] '{recipe.outputItem.itemName}' 제작 중... ({recipe.craftingTime}초 소요)");

            yield return new WaitForSeconds(recipe.craftingTime);

            if (InventoryManager.instance != null)
            {
                InventoryManager.instance.AddItem(recipe.outputItem, recipe.outputAmount);
            }
            
            Debug.Log($"[CraftingStation] '{recipe.outputItem.itemName}' 제작 완료!");
            _isCrafting = false;
        }
    }
