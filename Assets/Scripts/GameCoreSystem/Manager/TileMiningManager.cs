using StampSystem;
using UnityEngine;
using UnityEngine.Tilemaps;


public class TileMiningManager : MonoBehaviour
{
    [Header("필수 연결")]
    [Tooltip("좌표 변환을 위해 GroundTilemap을 연결해야 합니다.")]
    [SerializeField] private Tilemap groundTilemap;

    [Tooltip("자원 드랍 아이템을 담을 부모 (없으면 씬 루트에 생성됨)")]
    [SerializeField] private Transform itemDropParent;
    
    // 사용할 시스템 참조 (싱글턴을 통해 가져옴)
    private GameMap _gameMap;
    private MapRenderer _mapRenderer;
    private ResourceManager _resourceManager; // 리소스 매니저 참조

    void Start()
    {
        // MapGenerator가 생성한 인스턴스들을 가져옵니다.
        _gameMap = MapGenerator.instance.GameMapInstance;
        _mapRenderer = MapGenerator.instance.MapRendererInstance;
        
        // ★★★ [수정된 부분 1] ★★★
        // 맵 렌더러의 public 필드에서 리소스 매니저를 직접 가져옵니다.
        if (_mapRenderer != null)
        {
            _resourceManager = _mapRenderer.GetResourceManager();
        }
        else
        {
            Debug.LogError("TileMiningManager: MapRenderer 인스턴스를 찾을 수 없습니다!");
        }
    }

    void Update()
    {
        // 왼쪽 마우스 버튼을 클릭했을 때
        if (Input.GetMouseButtonDown(0))
        {
            // 마우스 위치를 월드 좌표로 변환
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            // 월드 좌표를 타일맵 셀 좌표로 변환
            Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);

            // 타일 채광 시도
            MineTile(cellPos.x, cellPos.y);
        }
    }

    /// <summary>
    /// (x, y) 좌표의 타일을 채광합니다.
    /// </summary>
    public void MineTile(int x, int y)
    {
        // 맵 경계 밖이면 무시
        if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT)
            return;

        // 1. 맵 데이터에서 현재 타일 ID를 가져옵니다.
        int tileID = _gameMap.TileGrid[x, y];

        // 2. 채광 불가능한 타일인지 확인 (공기, 가공된 흙 등)
        // (참고: ID 99(예약됨)은 이제 OccupiedGrid로 대체되어 검사할 필요 없음)
        if (tileID == AIR_ID || tileID == PROCESSED_DIRT_ID) 
        {
            return; // 채광 불가
        }

        // ★★★ [수정된 부분 2] ★★★
        // 3. 리소스 매니저가 있는지 확인 (Start에서 이미 할당됨)
        if (_resourceManager == null)
        {
            Debug.LogError("TileMiningManager: ResourceManager가 할당되지 않았습니다!");
            return;
        }
        
        // 4. 드랍할 아이템 프리팹을 가져옵니다.
        GameObject dropPrefab = _resourceManager.GetDropPrefab(tileID);

        // 5. 드랍 아이템이 있으면 생성(Instantiate)합니다.
        if (dropPrefab != null)
        {
            // 타일의 중앙 위치 계산
            Vector3 dropPos = groundTilemap.CellToWorld(new Vector3Int(x, y, 0)) 
                            + new Vector3(0.5f, 0.5f, 0); // 타일 중앙
                            
            Instantiate(dropPrefab, dropPos, Quaternion.identity, itemDropParent);
        }

        // 6. 맵 데이터를 공기(AIR_ID = 0)로 변경합니다.
        _gameMap.SetTile(x, y, AIR_ID);
        
        // 7. 맵 렌더러에게 해당 타일을 새로고침(지우기)하라고 알립니다.
        _mapRenderer.UpdateTileVisual(x, y);
    }
    
    // (ID 상수 정의 - 가독성을 위해 추가)
    private const int AIR_ID = 0;
    private const int PROCESSED_DIRT_ID = 7;
}