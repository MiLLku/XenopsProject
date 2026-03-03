using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 맵 렌더러.
/// GameMap 데이터를 Unity Tilemap에 시각화하고, 개체(나무, 광물 등)를 인스턴스화합니다.
/// </summary>
public class MapRenderer : MonoBehaviour
{
    #region 필드

    [Header("필수 연결")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] public Transform entityParent;

    private GameMap _gameMap;
    private ResourceManager _resourceManager;

    #endregion

    #region 초기화

    /// <summary>
    /// 맵 렌더러를 초기화합니다.
    /// </summary>
    /// <param name="gameMap">렌더링할 게임 맵</param>
    /// <param name="resourceManager">타일/프리팹 에셋 매니저</param>
    public void Initialize(GameMap gameMap, ResourceManager resourceManager)
    {
        _gameMap = gameMap;
        _resourceManager = resourceManager;
    }

    #endregion

    #region 렌더링

    /// <summary>
    /// 전체 맵을 렌더링합니다.
    /// </summary>
    /// <param name="map">렌더링할 GameMap</param>
    public void RenderMap(GameMap map)
    {
        if (_gameMap == null || _resourceManager == null)
        {
            Debug.LogError("[MapRenderer] GameMap 또는 ResourceManager가 없습니다!");
            return;
        }

        _gameMap = map;

        Debug.Log("--- 맵 렌더링 시작 ---");
        RenderTiles();
        RenderEntities();
        Debug.Log("--- 맵 렌더링 완료 ---");
    }

    private void RenderTiles()
    {
        Vector3Int[] positions = new Vector3Int[GameMap.MAP_WIDTH * GameMap.MAP_HEIGHT];
        TileBase[] tiles = new TileBase[GameMap.MAP_WIDTH * GameMap.MAP_HEIGHT];
        TileBase[] wallTiles = new TileBase[GameMap.MAP_WIDTH * GameMap.MAP_HEIGHT];

        int index = 0;
        for (int y = 0; y < GameMap.MAP_HEIGHT; y++)
        {
            for (int x = 0; x < GameMap.MAP_WIDTH; x++)
            {
                positions[index] = new Vector3Int(x, y, 0);
                tiles[index] = _resourceManager.GetTileAsset(_gameMap.TileGrid[x, y]);
                wallTiles[index] = _resourceManager.GetTileAsset(_gameMap.WallGrid[x, y]);
                index++;
            }
        }

        if (tilemap == null) Debug.LogError("[MapRenderer] tilemap이 연결되지 않았습니다!");
        else tilemap.SetTiles(positions, tiles);

        if (wallTilemap == null) Debug.LogError("[MapRenderer] wallTilemap이 연결되지 않았습니다!");
        else wallTilemap.SetTiles(positions, wallTiles);
    }

    private void RenderEntities()
    {
        if (entityParent == null) return;

        Debug.Log($"[MapRenderer] {_gameMap.Entities.Count}개의 개체 렌더링");

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
                Debug.LogWarning($"[MapRenderer] 개체 ID '{entity.id}'({entity.type})의 프리팹이 없습니다.");
            }
        }
    }

    #endregion

    #region 공개 API

    /// <summary>
    /// 특정 타일의 시각을 현재 GameMap 데이터로 갱신합니다.
    /// </summary>
    /// <param name="x">타일 X 좌표</param>
    /// <param name="y">타일 Y 좌표</param>
    public void UpdateTileVisual(int x, int y)
    {
        if (_gameMap == null || _resourceManager == null) return;

        int newTileId = _gameMap.TileGrid[x, y];
        TileBase tileAsset = _resourceManager.GetTileAsset(newTileId);
        tilemap.SetTile(new Vector3Int(x, y, 0), tileAsset);
    }

    /// <summary>
    /// 전체 타일맵을 현재 GameMap 데이터로 새로고침합니다.
    /// 세이브 파일 로드 시 사용됩니다.
    /// </summary>
    public void RefreshAllTiles()
    {
        if (_gameMap == null || _resourceManager == null)
        {
            Debug.LogWarning("[MapRenderer] RefreshAllTiles: GameMap 또는 ResourceManager가 없습니다.");
            return;
        }

        RenderTiles();
        Debug.Log("[MapRenderer] 전체 타일 새로고침 완료");
    }

    /// <summary>
    /// 리소스 매니저를 반환합니다.
    /// </summary>
    public ResourceManager GetResourceManager()
    {
        return _resourceManager;
    }

    #endregion
}
