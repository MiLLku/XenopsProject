// --- 파일 6: GameMap.cs (OccupiedGrid 추가 버전) ---

using System.Collections.Generic;
using UnityEngine;


// 맵에 배치될 개체(건물, 적 등)를 표현하는 구조체
public struct MapEntity
{
    public Vector2Int position;
    public TypeObjectTile type;
    public int id;
}

public class GameMap
{
    public const int MAP_WIDTH = 200;
    public const int MAP_HEIGHT = 200;

    public int[,] TileGrid { get; private set; }
    public int[,] WallGrid { get; private set; }
    public List<MapEntity> Entities { get; private set; }
    
    // ★★★ [새로 추가된 부분] ★★★
    // 타일이 점유되었는지(나무, 건물 등이 있는지) 확인하는 논리 그리드
    public bool[,] OccupiedGrid { get; private set; } 
    
    public GameMap()
    {
        TileGrid = new int[MAP_WIDTH, MAP_HEIGHT];
        WallGrid = new int[MAP_WIDTH, MAP_HEIGHT];
        Entities = new List<MapEntity>();
        OccupiedGrid = new bool[MAP_WIDTH, MAP_HEIGHT]; // ★ false로 자동 초기화
    }

    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < MAP_WIDTH && y >= 0 && y < MAP_HEIGHT;
    }

    public void SetTile(int x, int y, int tileId)
    {
        if (IsInBounds(x, y)) TileGrid[x, y] = tileId;
    }

    public void SetWall(int x, int y, int wallId)
    {
        if (IsInBounds(x, y)) WallGrid[x, y] = wallId;
    }

    public void AddEntity(MapEntity entity)
    {
        if (IsInBounds(entity.position.x, entity.position.y))
        {
            Entities.Add(entity);
        }
    }
    
    // ★★★ [새로 추가된 함수 1] ★★★
    /// <summary>
    /// (x, y) 타일을 점유 상태로 마킹합니다.
    /// </summary>
    public void MarkTileOccupied(int x, int y)
    {
        if (IsInBounds(x, y)) OccupiedGrid[x, y] = true;
    }

    // ★★★ [새로 추가된 함수 2] ★★★
    /// <summary>
    /// (x, y) 타일이 점유되었는지 확인합니다.
    /// </summary>
    public bool IsTileOccupied(int x, int y)
    {
        if (!IsInBounds(x, y)) return true; // 맵 밖은 점유된 것으로 간주
        return OccupiedGrid[x, y];
    }
    
    // ★★★ [수정된 함수] ★★★
    /// <summary>
    /// 해당 타일이 흙(ID 1) 또는 잔디(ID 6)인지,
    /// 그리고 '아직 점유되지 않았는지' 확인합니다.
    /// </summary>
    public bool IsTileSpawnable(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        
        // 1. 이미 점유되었으면 false
        if (IsTileOccupied(x, y)) return false; 
        
        int tileID = TileGrid[x, y];
        
        // 2. 흙(ID 1)이거나 잔디(ID 6)일 때만 true 반환
        return (tileID == 1 || tileID == 6); 
    }

    public void PrintDebugMap(int centerX, int centerY, int range)
    {
        for (int y = centerY + range; y >= centerY - range; y--)
        {
            string line = $"{y:D3} | ";
            for (int x = centerX - range * 2; x <= centerX + range * 2; x++)
            {
                if (!IsInBounds(x, y)) continue;
                int tileId = TileGrid[x, y];
                line += (tileId == 0 ? "." : tileId.ToString());
            }
            Debug.Log(line);
        }
    }
}
