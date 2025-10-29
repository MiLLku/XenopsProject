using System.Collections.Generic;
using UnityEngine;

namespace StampSystem
{
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

        // 맵의 모든 타일 ID를 저장 (원본의 Arr_MapSeed 역할)
        public int[,] TileGrid { get; private set; }
        
        // 맵의 모든 벽 ID를 저장
        public int[,] WallGrid { get; private set; }

        // 맵에 배치된 건물, 적, 식물 등의 개체 리스트
        public List<MapEntity> Entities { get; private set; }
        
        public GameMap()
        {
            TileGrid = new int[MAP_WIDTH, MAP_HEIGHT];
            WallGrid = new int[MAP_WIDTH, MAP_HEIGHT];
            Entities = new List<MapEntity>();
        }

        // 맵 경계를 벗어나는지 확인
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
        
        // (테스트용) 맵의 일부를 콘솔에 출력
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
}