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
    }
}
