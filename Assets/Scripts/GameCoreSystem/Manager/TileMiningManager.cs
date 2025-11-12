// --- 파일 12: TileMiningManager.cs (아이템 들기 로직 제거 버전) ---

using UnityEngine;
using UnityEngine.Tilemaps;

namespace StampSystem
{
    // ★★★ 싱글턴(DestroySingleton) 상속 제거 -> 일반 MonoBehaviour ★★★
    // (ClickableItem이 더 이상 이 스크립트를 호출하지 않으므로 싱글턴일 필요가 없습니다)
    public class TileMiningManager : MonoBehaviour 
    {
        [Header("필수 연결 (씬)")]
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Transform itemDropParent;

        // --- 시스템 참조 ---
        private GameMap _gameMap;
        private MapRenderer _mapRenderer;
        private ResourceManager _resourceManager;
        private CameraController _cameraController;

        void Start()
        {
            if (MapGenerator.instance == null)
            {
                Debug.LogError("TileMiningManager: 씬에 MapGenerator가 없습니다!");
                return;
            }

            _gameMap = MapGenerator.instance.GameMapInstance;
            _mapRenderer = MapGenerator.instance.MapRendererInstance;
            _resourceManager = MapGenerator.instance.ResourceManagerInstance;
            _cameraController = Camera.main.GetComponent<CameraController>();

            if (_gameMap == null || _mapRenderer == null || _resourceManager == null || _cameraController == null)
            {
                Debug.LogError("TileMiningManager: 핵심 시스템 참조에 실패했습니다!");
            }
        }

        void Update()
        {
            if (_gameMap == null) return; 

            // ★★★ 이제 이 스크립트는 '타일 채광'만 신경 씁니다 ★★★
            if (Input.GetMouseButtonDown(0))
            {
                // (아이템 위에서 클릭하면 ClickableItem.OnMouseDown이 먼저 실행되어
                // 아이템이 주워지고, 채광 로직은 실행되지 않습니다.)
                
                Vector3 worldPos = _cameraController.GetMouseWorldPosition();
                Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);
                
                // ★ 혹시 모르니 아이템이 있는지 한 번 더 확인
                RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
                if (hit.collider != null && hit.collider.GetComponent<ClickableItem>() != null)
                {
                    // 아이템을 클릭했으므로, 타일 채광을 중단
                    return;
                }
                
                MineTile(cellPos.x, cellPos.y);
            }
        }
        
        // --- [PickUpItem, HandleHoldingItem, DropHeldItem 함수 모두 삭제됨] ---

        
        public void MineTile(int x, int y)
        {
            // ... (MineTile 함수 코드는 변경 없이 그대로 유지) ...
            if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT) return;
            
            int tileID = _gameMap.TileGrid[x, y];
            if (tileID == AIR_ID || tileID == PROCESSED_DIRT_ID) return;
            if (_resourceManager == null) return;
            
            GameObject dropPrefab = _resourceManager.GetDropPrefab(tileID);
            if (dropPrefab != null)
            {
                Vector3 dropPos = groundTilemap.CellToWorld(new Vector3Int(x, y, 0)) + new Vector3(0.5f, 0.5f, 0);
                Instantiate(dropPrefab, dropPos, Quaternion.identity, itemDropParent);
            }

            _gameMap.SetTile(x, y, AIR_ID);
            _mapRenderer.UpdateTileVisual(x, y);
        }
        
        private const int AIR_ID = 0;
        private const int PROCESSED_DIRT_ID = 7;
    }
}