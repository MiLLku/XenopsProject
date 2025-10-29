using UnityEngine;

namespace StampSystem
{
    public class MapStamper
    {
        private readonly GameMap _map;
        private readonly StampLibrary _library;

        public MapStamper(GameMap map, StampLibrary library)
        {
            _map = map;
            _library = library;
        }

        /// <summary>
        /// 지정된 위치에 스탬프를 찍습니다.
        /// </summary>
        /// <param name="key">라이브러리에서 찾을 스탬프 키</param>
        /// <param name="worldPosition">맵에 배치할 기준 좌표</param>
        /// <returns>성공 여부</returns>
        public bool PlaceStamp(string key, Vector2Int worldPosition)
        {
            // 1. 라이브러리에서 스탬프 청사진을 가져옵니다.
            StampData stamp = _library.GetStamp(key);
            if (stamp == null)
            {
                Debug.LogWarning($"[MapStamper] 스탬프를 찾지 못했습니다: {key}");
                return false;
            }

            // 2. 지형 타일(elements)을 배치합니다.
            foreach (var element in stamp.elements)
            {
                // ★★★ 피벗(Pivot)을 적용한 최종 맵 좌표 계산 ★★★
                // 월드 좌표 = 클릭 좌표 + 스탬프 상대 좌표 - 스탬프 피벗 좌표
                int targetX = worldPosition.x + element.position.x - stamp.pivot.x;
                int targetY = worldPosition.y + element.position.y - stamp.pivot.y;

                // 3. 요소의 타입에 따라 맵에 데이터를 기록합니다.
                switch (element.type)
                {
                    case TypeObjectTile.Tile:
                        _map.SetTile(targetX, targetY, element.id);
                        break;
                    
                    case TypeObjectTile.Building:
                    case TypeObjectTile.Enemy:
                    case TypeObjectTile.Plant:
                    case TypeObjectTile.MapObject:
                        _map.AddEntity(new MapEntity
                        {
                            position = new Vector2Int(targetX, targetY),
                            type = element.type,
                            id = element.id
                        });
                        break;
                }
            }
            
            // 4. 벽 타일(wallElements)을 배치합니다.
            foreach (var wallElement in stamp.wallElements)
            {
                int targetX = worldPosition.x + wallElement.position.x - stamp.pivot.x;
                int targetY = worldPosition.y + wallElement.position.y - stamp.pivot.y;
                _map.SetWall(targetX, targetY, wallElement.id);
            }

            return true;
        }
    }
}