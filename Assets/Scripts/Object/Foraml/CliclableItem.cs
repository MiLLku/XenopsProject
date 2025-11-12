// --- 파일 14: ClickableItem.cs (수정본) ---
// 하이라이트(OnMouseEnter/Exit)와 픽업(OnMouseDown)을 모두 담당합니다.

using UnityEngine;

namespace StampSystem
{
    [RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
    public class ClickableItem : MonoBehaviour
    {
        [Header("아이템 정보")]
        [Tooltip("이 아이템의 데이터(ID, 이름 등) ScriptableObject")]
        [SerializeField] private ItemData itemData;

        [Header("시각 효과")]
        [Tooltip("마우스를 올렸을 때 켜질 하이라이트 오브젝트 (자식 오브젝트)")]
        [SerializeField] private GameObject highlightObject;

        private void Start()
        {
            // 시작할 때 하이라이트가 꺼져 있는지 확인
            if (highlightObject != null)
            {
                highlightObject.SetActive(false);
            }
        }

        /// <summary>
        /// 이 아이템의 데이터(정보)를 반환합니다.
        /// </summary>
        public ItemData GetItemData()
        {
            return itemData;
        }

        // --- 마우스 상호작용 함수 ---

        // 마우스가 이 오브젝트의 Collider 위로 올라왔을 때
        private void OnMouseEnter()
        {
            if (highlightObject != null)
            {
                highlightObject.SetActive(true);
            }
        }

        // 마우스가 이 오브젝트의 Collider 밖으로 나갔을 때
        private void OnMouseExit()
        {
            if (highlightObject != null)
            {
                highlightObject.SetActive(false);
            }
        }

        // 마우스가 이 오브젝트를 클릭했을 때
        private void OnMouseDown()
        {
            // 1. 아이템 데이터가 있는지 확인
            if (itemData == null)
            {
                Debug.LogError($"아이템 {name}에 ItemData가 없습니다!", this.gameObject);
                return;
            }

            // 2. 글로벌 인벤토리 매니저를 찾아서 아이템 추가
            if (InventoryManager.instance != null)
            {
                InventoryManager.instance.AddItem(itemData);
                
                // 3. 인벤토리에 성공적으로 추가했으면, 씬에서 오브젝트 파괴
                Destroy(this.gameObject);
            }
            else
            {
                Debug.LogError("씬에 InventoryManager가 없습니다! 아이템을 주울 수 없습니다.");
            }
        }
    }
}