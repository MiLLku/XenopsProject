// --- 파일 10: MapRenderer.cs (강화된 디버그 버전) ---

using UnityEngine;
using UnityEngine.Tilemaps;

namespace StampSystem
{
    public class MapRenderer : MonoBehaviour
    {
        [Header("필수 연결")] [SerializeField] private ResourceManager resourceManager;
        [SerializeField] private Tilemap tilemap;
        [SerializeField] private Tilemap wallTilemap;
        [SerializeField] private Transform entityParent;

        private GameMap _gameMap;

        public void RenderMap(GameMap map)
        {
            if (map == null)
            {
                Debug.LogError("[MapRenderer] 렌더링할 GameMap이 없습니다! MapGenerator가 GameMap을 생성하지 못했습니다.");
                return;
            }

            if (resourceManager == null)
            {
                Debug.LogError("[MapRenderer] ResourceManager가 연결되지 않았습니다! 인스펙터 창을 확인하세요.");
                return;
            }

            _gameMap = map;

            Debug.Log("--- 맵 렌더링 시작 ---");

            RenderTiles();
            RenderEntities();

            Debug.Log("--- 맵 렌더링 완료 ---");
        }

        // --- 이 코드로 MapRenderer.cs의 RenderTiles 함수를 교체하세요 ---

        private void RenderTiles()
        {
            // Vector3Int와 TileBase 배열을 맵 크기만큼 준비합니다.
            Vector3Int[] positions = new Vector3Int[GameMap.MAP_WIDTH * GameMap.MAP_HEIGHT];
            TileBase[] tiles = new TileBase[GameMap.MAP_WIDTH * GameMap.MAP_HEIGHT];
            TileBase[] wallTiles = new TileBase[GameMap.MAP_WIDTH * GameMap.MAP_HEIGHT];

            int index = 0;
            int validTileCount = 0; // 실제로 찾은 타일 에셋 수
            int nonEmptyTileIdCount = 0; // 0이 아닌 타일 ID 수

            // GameMap의 모든 좌표를 순회합니다.
            for (int y = 0; y < GameMap.MAP_HEIGHT; y++)
            {
                for (int x = 0; x < GameMap.MAP_WIDTH; x++)
                {
                    positions[index] = new Vector3Int(x, y, 0);

                    // 1. 맵 데이터에서 타일 ID를 가져옵니다.
                    int tileId = _gameMap.TileGrid[x, y];
                    
                    if (tileId != 0)
                    {
                        nonEmptyTileIdCount++; // ID가 0이 아닌 타일을 맵 데이터에서 찾음
                        
                        // 2. 리소스 매니저에서 ID에 해당하는 실제 타일 에셋을 찾습니다.
                        TileBase tileAsset = resourceManager.GetTileAsset(tileId);

                        if (tileAsset != null)
                        {
                            // 3. 타일 에셋을 배열에 추가합니다.
                            tiles[index] = tileAsset;
                            validTileCount++; // 리소스 매니저에서도 에셋을 찾음
                        }
                        else
                        {
                            // ★ 만약 이 로그가 뜬다면, MyResourceManager 설정이 누락된 것입니다.
                            Debug.LogWarning(
                                $"[RenderTiles] 맵 데이터에서 타일 ID '{tileId}'를 ({x},{y})에 찾았으나, MyResourceManager에 이 ID가 등록되지 않았거나 에셋이 비어있습니다.");
                        }
                    }

                    // (벽 타일 로직)
                    int wallId = _gameMap.WallGrid[x, y];
                    if (wallId != 0)
                    {
                        // ID에 해당하는 벽 타일 에셋을 찾습니다. (GetTileAsset을 공유해서 씀)
                        TileBase wallAsset = resourceManager.GetTileAsset(wallId);
                        if(wallAsset != null)
                        {
                            wallTiles[index] = wallAsset;
                        }
                    }

                    index++;
                }
            }

            // 디버그 로그
            Debug.Log($"[RenderTiles] 맵 데이터에서 {nonEmptyTileIdCount}개의 타일 ID를 찾았습니다. (Y=150까지 흙/돌)");
            Debug.Log($"[RenderTiles] ResourceManager에서 {validTileCount}개의 유효한 타일 에셋을 매칭했습니다.");
            
            // ★ 4. 타일맵에 타일 배열을 '한 번에' 찍습니다. (이것이 매우 빠릅니다)
            if (tilemap != null)
                tilemap.SetTiles(positions, tiles);
            else
                Debug.LogError("[MapRenderer] 'tilemap' 슬롯이 비어있습니다!");

            if (wallTilemap != null)
                wallTilemap.SetTiles(positions, wallTiles);
            else
                Debug.LogError("[MapRenderer] 'wallTilemap' 슬롯이 비어있습니다!");
        }
        
        private void RenderEntities()
        {
            if (entityParent == null)
            {
                Debug.LogError("[MapRenderer] 'entityParent' 슬롯이 비어있습니다!");
                return;
            }

            Debug.Log($"[RenderEntities] {_gameMap.Entities.Count}개의 개체 렌더링을 시도합니다...");
            
            foreach (var entity in _gameMap.Entities)
            {
                GameObject prefab = resourceManager.GetEntityPrefab(entity.id);
                if (prefab != null)
                {
                    Vector3 worldPos = new Vector3(entity.position.x, entity.position.y, 0);
                    
                    // ★ 핵심 디버그: 프리팹 생성 직전 로그
                    Debug.Log($"[RenderEntities] ✅ 성공: ID '{entity.id}' ({prefab.name}) 프리팹을 {worldPos} 좌표에 생성합니다.");
                    
                    Instantiate(prefab, worldPos, Quaternion.identity, entityParent);
                }
                else
                {
                    // ★ 핵심 디버그: ID는 있으나 리소스 매니저에 프리팹이 없는 경우
                    Debug.LogWarning($"[RenderEntities] 맵 데이터에서 개체 ID '{entity.id}'({entity.type})를 찾았으나, MyResourceManager에 이 ID가 등록되지 않았거나 프리팹이 비어있습니다.");
                }
            }
        }
        
        public void UpdateTileVisual(int x, int y)
        {
            // 1. 맵 데이터에서 현재 타일 ID를 가져옵니다.
            int newTileId = _gameMap.TileGrid[x, y];
        
            // 2. 리소스 매니저에서 ID에 맞는 타일 에셋을 찾습니다.
            TileBase tileAsset = resourceManager.GetTileAsset(newTileId);
        
            // 3. 타일맵에 해당 타일을 덮어씁니다. (ID가 0이면 null이 반환되어 타일이 지워짐)
            tilemap.SetTile(new Vector3Int(x, y, 0), tileAsset);
        }
        public ResourceManager GetResourceManager()
        {
            return resourceManager;
        }
    }
}