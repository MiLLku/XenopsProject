// --- 파일 8: MapGenerator.cs (RESERVED_ID 제거 및 OccupiedGrid 사용) ---
using UnityEngine;

namespace StampSystem
{
    [RequireComponent(typeof(MapRenderer))]
    public class MapGenerator : DestroySingleton<MapGenerator>
    {
        // ... (모든 인스펙터 변수는 변경 없음) ...
        [Header("필수 연결")]
        [SerializeField] private StampLibrary stampLibrary;
        [SerializeField] private ResourceManager resourceManager;
        [Header("맵 생성 시드")]
        [SerializeField] private float noiseSeed = 0f;
        [Header("언덕 지형")]
        [SerializeField] private int baseGroundLevel = 140;
        [SerializeField] [Range(0f, 50f)] private float hillAmplitude = 10f;
        [SerializeField] [Range(0.01f, 0.1f)] private float hillScale = 0.05f;
        [SerializeField] [Range(1, 20)] private int surfaceDirtDepth = 5;
        [Header("흙 덩어리 (돌 속)")]
        [SerializeField] [Range(0.01f, 0.2f)] private float dirtNoiseScale = 0.08f;
        [SerializeField] [Range(0f, 1f)] private float dirtThreshold = 0.5f;
        [Header("동굴")]
        [SerializeField] [Range(0.01f, 0.2f)] private float caveNoiseScale = 0.07f;
        [SerializeField] [Range(0f, 1f)] private float caveThreshold = 0.7f;
        [Header("광물 지층 (지표면 기준)")]
        [SerializeField] [Range(0.01f, 0.3f)] private float copperNoiseScale = 0.15f; 
        [SerializeField] [Range(0f, 1f)] private float copperThreshold = 0.7f; 
        [SerializeField] [Range(0.01f, 0.3f)] private float ironNoiseScale = 0.18f; 
        [SerializeField] [Range(0f, 1f)] private float ironThreshold = 0.75f; 
        [SerializeField] [Range(0.01f, 0.3f)] private float goldNoiseScale = 0.2f; 
        [SerializeField] [Range(0f, 1f)] private float goldThreshold = 0.8f; 
        [Header("나무 배치 (지표면 잔디)")]
        [SerializeField] private string treeStampKey = "TREE_2X3";
        [SerializeField] [Range(2, 20)] private int minTreeDistance = 5;
        [SerializeField] [Range(5, 50)] private int spawnAreaPadding = 15;
        [SerializeField] [Range(0f, 1f)] private float treePlacementChance = 0.5f;
        [Header("열매 나무 (지하/지상 흙/잔디)")]
        [SerializeField] private string berryBushStampKey = "BERRY_BUSH";
        [SerializeField] [Range(0f, 0.1f)] private float berryBushSpawnChance = 0.05f;
        [Header("스폰 지점")]
        [SerializeField] private string spawnChestKey = "SPAWN_CHEST_3X2"; 

        // --- 내부 시스템 변수 ---
        private GameMap _gameMap;
        private MapStamper _stamper;
        private MapRenderer _mapRenderer;
        
        // 사용할 타일 ID
        private const int AIR_ID = 0;
        private const int DIRT_ID = 1;  // 흙
        private const int STONE_ID = 2; // 돌
        private const int COPPER_ID = 3;
        private const int IRON_ID = 4;
        private const int GOLD_ID = 5;
        private const int GRASS_ID = 6; // 잔디
        private const int PROCESSED_DIRT_ID = 7; // 가공된 흙
        
        public GameMap GameMapInstance { get; private set; }
        public MapStamper StamperInstance { get; private set; }
        public MapRenderer MapRendererInstance { get; private set; }
        public ResourceManager ResourceManagerInstance { get; private set; }
        

        // --- Unity 생명주기 ---
        void Start()
        {
            // ... (Start 함수 내용은 동일) ...
            if (stampLibrary == null) { /* ... */ return; }
            _mapRenderer = GetComponent<MapRenderer>();
            _gameMap = new GameMap();
            _stamper = new MapStamper(_gameMap, stampLibrary);
            if (noiseSeed == 0f) { noiseSeed = Random.Range(0f, 1000f); }
            GenerateWorld();
            Debug.Log("--- 맵 데이터 생성 완료 ---");
            _mapRenderer.RenderMap(_gameMap);
            Debug.Log("--- 스폰 지점 근처 맵 출력 ---");
            _gameMap.PrintDebugMap(100, baseGroundLevel, 10);
            Debug.Log($"--- 배치된 개체: {_gameMap.Entities.Count}개 ---");
            foreach (var entity in _gameMap.Entities)
            {
                Debug.Log($"> {entity.type} (ID: {entity.id}) @ {entity.position}");
            }
        }
        
        // --- 맵 생성 메인 함수 ---
        private void GenerateWorld()
        {
            // ... (GenerateWorld 함수 내용은 동일) ...
            Debug.Log("맵 데이터 생성을 시작합니다...");
            int[] groundHeightMap = GenerateBaseTerrainAndOres();
            ConvertSurfaceDirtToGrass(groundHeightMap);
            PlaceSpawnPackage(groundHeightMap); // 스폰 지점이 먼저 점유 마킹
            PlaceBerryBushes();
            PlaceTrees(groundHeightMap);
        }

        #region 지형 및 광물 (Terrain & Ores)
        
        private int[] GenerateBaseTerrainAndOres()
        {
            // ... (변경 사항 없음) ...
            int[] groundHeightMap = new int[GameMap.MAP_WIDTH];
            for (int x = 0; x < GameMap.MAP_WIDTH; x++)
            {
                float hillNoise = Mathf.PerlinNoise((x * hillScale) + noiseSeed, noiseSeed);
                int currentHeight = baseGroundLevel + (int)(hillNoise * hillAmplitude);
                groundHeightMap[x] = currentHeight; 
                for (int y = 0; y < GameMap.MAP_HEIGHT; y++)
                {
                    int tileID = GetTileIDForCoordinate(x, y, currentHeight);
                    _gameMap.SetTile(x, y, tileID);
                }
            }
            return groundHeightMap;
        }
        
        private int GetTileIDForCoordinate(int x, int y, int currentHeight)
        {
            // ... (변경 사항 없음) ...
            if (y > currentHeight) { return AIR_ID; }
            float caveNoise = Mathf.PerlinNoise((x * caveNoiseScale) + noiseSeed + 1000f, (y * caveNoiseScale) + noiseSeed + 1000f);
            float dirtNoise = Mathf.PerlinNoise((x * dirtNoiseScale) + noiseSeed - 1000f, (y * dirtNoiseScale) + noiseSeed - 1000f);
            float copperNoise = Mathf.PerlinNoise((x * copperNoiseScale) + noiseSeed + 2000f, (y * copperNoiseScale) + noiseSeed + 2000f);
            float ironNoise = Mathf.PerlinNoise((x * ironNoiseScale) + noiseSeed + 3000f, (y * ironNoiseScale) + noiseSeed + 3000f);
            float goldNoise = Mathf.PerlinNoise((x * goldNoiseScale) + noiseSeed + 4000f, (y * goldNoiseScale) + noiseSeed + 4000f);
            if (caveNoise > caveThreshold) { return AIR_ID; }
            if (y >= currentHeight - surfaceDirtDepth || dirtNoise > dirtThreshold) { return DIRT_ID; }
            if (y < currentHeight - 40 && goldNoise > goldThreshold) { return GOLD_ID; }
            if (y < currentHeight - 20 && ironNoise > ironThreshold) { return IRON_ID; }
            if (y < currentHeight - 10 && copperNoise > copperThreshold) { return COPPER_ID; }
            return STONE_ID;
        }
        
        private void ConvertSurfaceDirtToGrass(int[] groundHeightMap)
        {
            // ... (IsSky 헬퍼 함수를 사용하는 버전으로 되돌림)
            Debug.Log("[MapGenerator] 흙 지표면을 잔디로 변환 중...");
            for (int x = 0; x < GameMap.MAP_WIDTH; x++)
            {
                for (int y = 0; y < GameMap.MAP_HEIGHT - 1; y++)
                {
                    if (_gameMap.TileGrid[x, y] == DIRT_ID && _gameMap.TileGrid[x, y + 1] == AIR_ID)
                    {
                        if (IsSky(x, y + 2)) 
                        {
                            _gameMap.SetTile(x, y, GRASS_ID);
                        }
                    }
                }
            }
        }
        
        private bool IsSky(int x, int startY)
        {
            // ... (변경 사항 없음) ...
            for (int y = startY; y < GameMap.MAP_HEIGHT; y++)
            {
                if (_gameMap.TileGrid[x, y] != AIR_ID) { return false; }
            }
            return true;
        }

        #endregion

        #region 구조물 (Structures)
        
        // ★★★ [수정된 부분 1: 스폰 지점] ★★★
        private void PlaceSpawnPackage(int[] groundHeightMap)
        {
            StampData chestStamp = stampLibrary.GetStamp(spawnChestKey);
            if (chestStamp == null) { /* ... */ return; }

            int spawnX = 100; 
            int groundSurfaceY = groundHeightMap[spawnX];
            const int platformWidth = 5;
            const int platformPivotX = 2;
            int startX = spawnX - platformPivotX;
            int endX = startX + platformWidth; 

            Debug.Log($"[PlaceSpawnPackage] 스폰 지점 평탄화 작업 시작... (X:{startX}~{endX-1} @ Y:{groundSurfaceY})");

            for (int x = startX; x < endX; x++)
            {
                // 1. 타일 ID를 PROCESSED_DIRT_ID (7)로 변경
                _gameMap.SetTile(x, groundSurfaceY, PROCESSED_DIRT_ID); 
                // 2. ★논리 그리드도 점유 상태로 마킹★
                _gameMap.MarkTileOccupied(x, groundSurfaceY); 

                _gameMap.SetTile(x, groundSurfaceY + 1, AIR_ID);
                _gameMap.SetTile(x, groundSurfaceY + 2, AIR_ID);
                _gameMap.SetTile(x, groundSurfaceY + 3, AIR_ID);
            }
            Vector2Int chestSpawnPos = new Vector2Int(startX + 1, groundSurfaceY + 1);
            _stamper.PlaceStamp(spawnChestKey, chestSpawnPos);
            
            // 3. ★상자가 차지하는 공간도 점유 마킹★ (3x2 크기)
            Vector2Int chestPivot = chestStamp.pivot;
            foreach(var element in chestStamp.elements)
            {
                // 상자 프리팹이 차지하는 3x2 공간의 '바닥 타일'을 점유 마킹
                int occupyX = chestSpawnPos.x + element.position.x - chestPivot.x;
                int occupyY = chestSpawnPos.y + element.position.y - chestPivot.y;
                
                // (이 예제는 1x1 프리팹 기준이지만, 3x2 프리팹이라면 그 바닥 3칸을 모두 마킹해야 합니다)
                // (일단 상자 스탬프의 (0,0) 위치만 마킹)
                // (상자 피벗이 (0,0)이고 스폰위치가 (99, 141)이면, (99, 141)을 점유)
                // (이것은 타일이 아닌 '공간'을 점유하는 것이므로 별도 로직이 필요할 수 있음)
                // (지금은 프리팹이 서 있는 '바닥'만 점유 마킹합니다)
                _gameMap.MarkTileOccupied(startX + 1, groundSurfaceY);
                _gameMap.MarkTileOccupied(startX + 2, groundSurfaceY);
                _gameMap.MarkTileOccupied(startX + 3, groundSurfaceY);
            }

            Debug.Log($"[PlaceSpawnPackage] '{spawnChestKey}' 스탬프 배치 완료.");
        }

        #endregion
        
        #region 식물 (Vegetation)

        // ★★★ [수정된 부분 2: 열매 나무 배치] ★★★
        private void PlaceBerryBushes()
        {
            if (berryBushSpawnChance <= 0f) return;

            int bushesPlaced = 0;
            for (int x = 0; x < GameMap.MAP_WIDTH; x++)
            {
                for (int y = 0; y < GameMap.MAP_HEIGHT - 1; y++)
                {
                    // 1. '스폰 가능'한 흙/잔디 타일이고 (점유되지 않았음)
                    // 2. 윗 칸이 공기인지 확인
                    if (_gameMap.IsTileSpawnable(x, y) && _gameMap.TileGrid[x, y + 1] == AIR_ID)
                    {
                        if (Random.value < berryBushSpawnChance)
                        {
                            _stamper.PlaceStamp(berryBushStampKey, new Vector2Int(x, y + 1));
                            
                            // 2. ★타일 ID를 바꾸는 대신 '점유됨'으로 마킹★
                            _gameMap.MarkTileOccupied(x, y); 
                            bushesPlaced++;
                        }
                    }
                }
            }
            Debug.Log($"[PlaceBerryBushes] 배치 완료. (배치: {bushesPlaced}그루)");
        }
        
        // ★★★ [수정된 부분 3: 큰 나무 배치] ★★★
        private void PlaceTrees(int[] groundHeightMap)
        {
            if (treePlacementChance <= 0f) return;

            int spawnX = 100;
            int lastTreeX = -minTreeDistance; 
            const int treeWidth = 2; 
            int skippedByPadding = 0, skippedByDistance = 0, failedFlatGroundCheck = 0, failedChanceRoll = 0, treesPlaced = 0;

            for (int x = 0; x < GameMap.MAP_WIDTH - treeWidth; x++) 
            {
                if (x >= spawnX - spawnAreaPadding && x <= spawnX + spawnAreaPadding) { skippedByPadding++; continue; }
                if (x < lastTreeX + minTreeDistance) { skippedByDistance++; continue; }

                int y = groundHeightMap[x];
                
                // 1. (x,y) 타일이 잔디(GRASS_ID)이고 '스폰 가능'한지 확인
                // (IsTileSpawnable이 OccupiedGrid를 검사하므로 열매 나무와 겹치지 않음)
                bool isFlatGrassPatch = _gameMap.IsTileSpawnable(x, y) && 
                                        _gameMap.TileGrid[x, y] == GRASS_ID; 

                for (int i = 1; i < treeWidth; i++) 
                {
                    // 2. 옆 타일(x+i, y)도 잔디이고, 높이가 같고, '스폰 가능'한지 확인
                    if (groundHeightMap[x + i] != y || 
                        !_gameMap.IsTileSpawnable(x + i, y) || // ★점유 상태 확인
                        _gameMap.TileGrid[x + i, y] != GRASS_ID) 
                    { 
                        isFlatGrassPatch = false; 
                        break; 
                    }
                }
                
                if (isFlatGrassPatch)
                {
                    if (Random.value < treePlacementChance) 
                    {
                        _stamper.PlaceStamp(treeStampKey, new Vector2Int(x, y + 1)); 
                        lastTreeX = x;
                        
                        // 2. ★타일 2칸을 '점유됨'으로 마킹 (ID를 바꾸지 않음)★
                        _gameMap.MarkTileOccupied(x, y);
                        _gameMap.MarkTileOccupied(x + 1, y);
                        treesPlaced++;
                    } else { failedChanceRoll++; }
                } else { failedFlatGroundCheck++; }
            }
            Debug.Log($"[PlaceTrees] 배치 완료. (배치: {treesPlaced}그루, 평지실패: {failedFlatGroundCheck}칸, 확률실패: {failedChanceRoll}칸)");
        }

        #endregion
    }
}