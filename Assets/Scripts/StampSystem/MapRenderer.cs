// --- 파일 10: MapRenderer.cs (강화된 디버그 버전) ---

using UnityEngine;
using UnityEngine.Tilemaps;

public class MapRenderer : MonoBehaviour
{
    [Header("필수 연결")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Transform entityParent;

    private GameMap _gameMap;
    private ResourceManager _resourceManager;
    
    public void Initialize(GameMap gameMap, ResourceManager resourceManager)
    {
        _gameMap = gameMap;
        _resourceManager = resourceManager;
    }
    public void RenderMap(GameMap map)
    {
        if (_gameMap == null || _resourceManager == null)
        {
            Debug.LogError("[MapRenderer] 렌더링할 GameMap 또는 ResourceManager가 없습니다! (Initialize 실패)");
            return;
        }
            
        _gameMap = map; // MapGenerator.Start()에서 다시 호출될 때 맵 데이터 갱신

        Debug.Log("--- 맵 렌더링 시작 ---");
        RenderTiles();
        RenderEntities();
        Debug.Log("--- 맵 렌더링 완료 ---");
    }

    // --- 이 코드로 MapRenderer.cs의 RenderTiles 함수를 교체하세요 ---

    private void RenderTiles()
    {
        // (강제 렌더링 코드가 아닌, 원본 맵 렌더링 코드로 복구합니다)
        Vector3Int[] positions = new Vector3Int[GameMap.MAP_WIDTH * GameMap.MAP_HEIGHT];
        TileBase[] tiles = new TileBase[GameMap.MAP_WIDTH * GameMap.MAP_HEIGHT];
        TileBase[] wallTiles = new TileBase[GameMap.MAP_WIDTH * GameMap.MAP_HEIGHT];
            
        int index = 0;
        for (int y = 0; y < GameMap.MAP_HEIGHT; y++)
        {
            for (int x = 0; x < GameMap.MAP_WIDTH; x++)
            {
                positions[index] = new Vector3Int(x, y, 0);
                int tileId = _gameMap.TileGrid[x, y];
                tiles[index] = _resourceManager.GetTileAsset(tileId); // _resourceManager 사용
                int wallId = _gameMap.WallGrid[x, y];
                wallTiles[index] = _resourceManager.GetTileAsset(wallId); // _resourceManager 사용
                index++;
            }
        }
            
        if (tilemap == null) Debug.LogError("MapRenderer: 'tilemap'이 연결되지 않았습니다!");
        else tilemap.SetTiles(positions, tiles);
            
        if (wallTilemap == null) Debug.LogError("MapRenderer: 'wallTilemap'이 연결되지 않았습니다!");
        else wallTilemap.SetTiles(positions, wallTiles);
    }
    
    private void RenderEntities()
    {
        if (entityParent == null) { /* ... */ return; }
        Debug.Log($"[RenderEntities] {_gameMap.Entities.Count}개의 개체 렌더링을 시도합니다...");
            
        foreach (var entity in _gameMap.Entities)
        {
            GameObject prefab = _resourceManager.GetEntityPrefab(entity.id);
            if (prefab != null)
            {
                Vector3 worldPos = new Vector3(entity.position.x, entity.position.y, 0);
                Instantiate(prefab, worldPos, Quaternion.identity, entityParent);
            }
            else
            {
                Debug.LogWarning($"[RenderEntities] 맵 데이터에서 개체 ID '{entity.id}'({entity.type})를 찾았으나, MyResourceManager에 프리팹이 없습니다.");
            }
        }
    }
        
    public ResourceManager GetResourceManager()
    {
        return _resourceManager;
    }

    public void UpdateTileVisual(int x, int y)
    {
        if (_gameMap == null || _resourceManager == null) return;
            
        int newTileId = _gameMap.TileGrid[x, y];
        TileBase tileAsset = _resourceManager.GetTileAsset(newTileId);
        tilemap.SetTile(new Vector3Int(x, y, 0), tileAsset);
    }
}
