using UnityEngine;

namespace Object.Plant
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class BerryBush : MonoBehaviour
    {
        [Header("상태별 스프라이트")]
        [SerializeField] private Sprite emptySprite;   
        [SerializeField] private Sprite halfSprite;   
        [SerializeField] private Sprite fullSprite;   

        [Header("생산물")]
        [Tooltip("이 나무를 수확했을 때 인벤토리에 들어갈 아이템")]
        [SerializeField] private ItemData itemToProduce;
        
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            if (emptySprite != null)
            {
                spriteRenderer.sprite = emptySprite;
            }
            else
            {
                Debug.LogError("BerryBush 프리팹에 'Empty Sprite'가 설정되지 않았습니다!");
            }
        }
        
        public ItemData Harvest()
        {
            // (나중에 여기에 'fullSprite' 상태일 때만 수확 가능하도록 로직 추가)
            
            // 빈 나무로 상태 변경
            spriteRenderer.sprite = emptySprite; 
            
            // 인벤토리로 보낼 아이템 데이터 반환
            return itemToProduce;
        }
    }
}
