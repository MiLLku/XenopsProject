// --- 파일 12: TileMiningManager.cs (수정본) ---
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StampSystem
{
    public class TileMiningManager : MonoBehaviour
    {
        [Header("필수 연결 (씬)")]
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Transform itemDropParent;
        
        // --- 내부 참조 ---
        private GameMap _gameMap;
        private MapRenderer _mapRenderer;
        private ResourceManager _resourceManager;

        // ★★★ [수정된 Start() 함수] ★★★
        void Start()
        {
            // MapGenerator.Awake()가 먼저 실행되어 모든 것을 준비했는지 확인
            if (MapGenerator.instance == null)
            {
                Debug.LogError("TileMiningManager: 씬에 MapGenerator가 없습니다!");
                return;
            }

            // MapGenerator의 공개 프로퍼티에서 모든 참조를 가져옴
            _gameMap = MapGenerator.instance.GameMapInstance;
            _mapRenderer = MapGenerator.instance.MapRendererInstance;
            _resourceManager = MapGenerator.instance.ResourceManagerInstance;

            // (MapGenerator.Awake에서 이미 null 체크를 했지만, 한 번 더 확인)
            if (_gameMap == null || _mapRenderer == null || _resourceManager == null)
            {
                Debug.LogError("TileMiningManager: MapGenerator가 시스템을 제대로 초기화하지 못했습니다! (Awake 실패)");
            }
        }

        // ... (Update, MineTile 함수는 기존과 동일하게 유지) ...
        void Update()
        {
            // _gameMap이 null이면 Update를 실행하지 않음 (안전장치)
            if (_gameMap == null) return; 
        
            if (Input.GetMouseButtonDown(0))
            {
                // ★★★ [수정된 부분 1] ★★★
                // 마우스 위치를 Vector3 변수로 먼저 가져옵니다.
                Vector3 mousePos = Input.mousePosition;
            
                // ★★★ [수정된 부분 2] ★★★
                // Z 좌표(깊이)를 카메라와 타일맵(Z=0) 사이의 거리로 설정합니다.
                // (카메라가 -10에 있으므로 거리는 10입니다.)
                mousePos.z = -Camera.main.transform.position.z; 

                // 이제 z=10으로 설정된 마우스 위치를 월드 좌표로 변환합니다.
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
            
                // 월드 좌표를 타일맵 셀 좌표로 변환
                Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);

                // ★★★ 디버그 로그 추가 (확인용) ★★★
                Debug.Log($"[MineTile] 마우스 클릭! -> Cell: ({cellPos.x}, {cellPos.y})");
            
                MineTile(cellPos.x, cellPos.y);
            }
        }
        
        // --- [교체할 MineTile 함수] ---

        public void MineTile(int x, int y)
        {
            if (_gameMap == null) return; // (Start에서 실패 시)

            // 1. 맵 경계 확인
            if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT)
                return;

            // 2. 맵 데이터에서 타일 ID 가져오기
            int tileID = _gameMap.TileGrid[x, y];
            
            // ★★★ 디버그 로그 1: 클릭한 좌표와 타일 ID 확인 ★★★
            Debug.Log($"[MineTile] 클릭 좌표: ({x}, {y}), 타일 ID: {tileID}");

            // 3. 채광 가능한 타일인지 확인


            // 4. 리소스 매니저 확인
            if (_resourceManager == null)
            {
                Debug.LogError("[MineTile] _resourceManager가 null입니다. Start()에서 할당 실패!");
                return;
            }
            
            // 5. 드랍 아이템 가져오기
            GameObject dropPrefab = _resourceManager.GetDropPrefab(tileID);

            if (dropPrefab != null)
            {
                Debug.Log($"[MineTile] ID {tileID}에 해당하는 아이템 '{dropPrefab.name}'을 드랍합니다.");
                Vector3 dropPos = groundTilemap.CellToWorld(new Vector3Int(x, y, 0)) + new Vector3(0.5f, 0.5f, 0);
                Instantiate(dropPrefab, dropPos, Quaternion.identity, itemDropParent);
            }
            else
            {
                // ★★★ 디버그 로그 3: 아이템은 없지만 타일은 제거
                Debug.LogWarning($"[MineTile] ID {tileID}에 해당하는 드랍 아이템이 MyResourceManager에 없습니다. 타일만 제거합니다.");
            }

            // 6. 맵 데이터 변경 (공기로)
            _gameMap.SetTile(x, y, 0);
            
            // 7. 맵 시각적 업데이트
            _mapRenderer.UpdateTileVisual(x, y);
        }
    }
}