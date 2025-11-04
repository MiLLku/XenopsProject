// --- 파일 8: MapGenerator.cs (지하 잔디 버그 수정 버전) ---

using UnityEngine;

namespace StampSystem
{
    [RequireComponent(typeof(MapRenderer))]
    public class MapGenerator : DestroySingleton<MapGenerator>
    {
        [Header("필수 연결")]
        [SerializeField]
        private StampLibrary stampLibrary;

        // ... (인스펙터의 모든 파라미터는 동일하게 유지) ...
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
        [Header("구리 광맥 (지표면 -10칸 아래)")]
        [SerializeField] [Range(0.01f, 0.3f)] private float copperNoiseScale = 0.15f; 
        [SerializeField] [Range(0f, 1f)] private float copperThreshold = 0.7f; 
        [Header("철 광맥 (지표면 -20칸 아래)")]
        [SerializeField] [Range(0.01f, 0.3f)] private float ironNoiseScale = 0.18f; 
        [SerializeField] [Range(0f, 1f)] private float ironThreshold = 0.75f; 
        [Header("금 광맥 (지표면 -40칸 아래)")]
        [SerializeField] [Range(0.01f, 0.3f)] private float goldNoiseScale = 0.2f; 
        [SerializeField] [Range(0f, 1f)] private float goldThreshold = 0.8f; 
        [Header("나무 배치")]
        [SerializeField] private bool placeTrees = true;
        [SerializeField] private string treeStampKey = "TREE_2X3";
        [SerializeField] [Range(2, 20)] private int minTreeDistance = 5;
        [SerializeField] [Range(5, 50)] private int spawnAreaPadding = 15;
        [SerializeField] [Range(0f, 1f)] private float treePlacementChance = 0.5f;
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

        // --- Unity 생명주기 ---
        void Start()
        {
            // ... (Start 함수 내용은 동일) ...
            if (stampLibrary == null)
            {
                Debug.LogError("StampLibrary가 설정되지 않았습니다! MapGenerator를 실행할 수 없습니다.");
                return;
            }
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
            Debug.Log("맵 데이터 생성을 시작합니다...");
            int[] groundHeightMap = GenerateBaseTerrainAndOres();
            ConvertSurfaceDirtToGrass(); // ★ (groundHeightMap 인자 제거)
            PlaceSpawnPackage(groundHeightMap);
            PlaceTrees(groundHeightMap);
        }

        private int[] GenerateBaseTerrainAndOres()
        {
            // ... (이 함수는 변경 사항 없음) ...
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
            // ... (이 함수는 변경 사항 없음) ...
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

        // ★★★ [수정된 부분 1] ★★★
        /// <summary>
        /// 맵의 '하늘에 노출된' 흙(ID 1)만 잔디(ID 6)로 변환하는 후처리 함수
        /// </summary>
        private void ConvertSurfaceDirtToGrass()
        {
            Debug.Log("[MapGenerator] 흙 지표면을 잔디로 변환 중...");
            
            for (int x = 0; x < GameMap.MAP_WIDTH; x++)
            {
                for (int y = 0; y < GameMap.MAP_HEIGHT - 1; y++)
                {
                    // 1. 흙 타일이고, 바로 윗 칸이 공기인지 확인
                    if (_gameMap.TileGrid[x, y] == DIRT_ID && _gameMap.TileGrid[x, y + 1] == AIR_ID)
                    {
                        // 2. "공기"가 "하늘"인지 "동굴"인지 확인 (Raycast Up)
                        if (IsSky(x, y + 2)) // y+1은 공기인걸 아니까 y+2부터 검사
                        {
                            // 흙을 잔디(ID 6)로 변경
                            _gameMap.SetTile(x, y, GRASS_ID);
                        }
                        // else: 지하 동굴 천장/바닥이므로 잔디로 바꾸지 않음.
                    }
                }
            }
        }
        
        // ★★★ [새로 추가된 함수] ★★★
        /// <summary>
        /// (x, startY) 좌표부터 맵 꼭대기까지 수직으로 검사하여 
        /// 막힌 곳이 없는지(하늘인지) 확인합니다.
        /// </summary>
        private bool IsSky(int x, int startY)
        {
            for (int y = startY; y < GameMap.MAP_HEIGHT; y++)
            {
                // 한 칸이라도 공기(AIR_ID)가 아닌 것이 나오면
                if (_gameMap.TileGrid[x, y] != AIR_ID)
                {
                    return false; // 막혀있음 = 동굴
                }
            }
            return true; // 맵 꼭대기까지 뚫려있음 = 하늘
        }

        // ... (PlaceTrees 함수는 변경 사항 없음) ...
        private void PlaceTrees(int[] groundHeightMap)
        {
            if (!placeTrees) { /* ... */ return; }
            int spawnX = 100;
            int lastTreeX = -minTreeDistance; 
            const int treeWidth = 2; 
            int skippedByPadding = 0, skippedByDistance = 0, failedFlatGroundCheck = 0, failedChanceRoll = 0, treesPlaced = 0;
            for (int x = 0; x < GameMap.MAP_WIDTH - treeWidth; x++) 
            {
                if (x >= spawnX - spawnAreaPadding && x <= spawnX + spawnAreaPadding) { skippedByPadding++; continue; }
                if (x < lastTreeX + minTreeDistance) { skippedByDistance++; continue; }
                int y = groundHeightMap[x];
                bool isFlatGrassPatch = _gameMap.TileGrid[x, y] == GRASS_ID; 
                for (int i = 1; i < treeWidth; i++) 
                {
                    if (groundHeightMap[x + i] != y || _gameMap.TileGrid[x + i, y] != GRASS_ID) 
                    { isFlatGrassPatch = false; break; }
                }
                if (isFlatGrassPatch)
                {
                    if (Random.value < treePlacementChance) 
                    {
                        _stamper.PlaceStamp(treeStampKey, new Vector2Int(x, y + 1)); 
                        lastTreeX = x;
                        treesPlaced++;
                    } else { failedChanceRoll++; }
                } else { failedFlatGroundCheck++; }
            }
            Debug.Log($"[PlaceTrees] 배치 완료. (배치: {treesPlaced}그루, 평지실패: {failedFlatGroundCheck}칸, 확률실패: {failedChanceRoll}칸)");
        }
        
        // ... (PlaceSpawnPackage 함수는 변경 사항 없음) ...
        private void PlaceSpawnPackage(int[] groundHeightMap)
        {
            StampData chestStamp = stampLibrary.GetStamp(spawnChestKey);
            
            if (chestStamp == null)
            {
                Debug.LogWarning($"[PlaceSpawnPackage] 실패: StampLibrary에서 Key '{spawnChestKey}'를 찾을 수 없습니다.");
                return; 
            }
            int spawnX = 100; 
            int groundSurfaceY = groundHeightMap[spawnX];
            const int platformWidth = 5;
            const int platformPivotX = 2;
            int startX = spawnX - platformPivotX;
            int endX = startX + platformWidth; 
            Debug.Log($"[PlaceSpawnPackage] 스폰 지점 평탄화 작업 시작... (X:{startX}~{endX-1} @ Y:{groundSurfaceY})");
            for (int x = startX; x < endX; x++)
            {
                // ConvertSurfaceDirtToGrass가 잔디(ID 6)로 바꿔놓았더라도,
                // 이 코드가 가공된 흙(ID 7)으로 덮어씁니다. (의도된 동작)
                _gameMap.SetTile(x, groundSurfaceY, PROCESSED_DIRT_ID); 
                _gameMap.SetTile(x, groundSurfaceY + 1, AIR_ID);
                _gameMap.SetTile(x, groundSurfaceY + 2, AIR_ID);
                _gameMap.SetTile(x, groundSurfaceY + 3, AIR_ID);
            }
            Vector2Int chestSpawnPos = new Vector2Int(startX + 1, groundSurfaceY + 1);
            _stamper.PlaceStamp(spawnChestKey, chestSpawnPos);
            Debug.Log($"[PlaceSpawnPackage] '{spawnChestKey}' 스탬프 배치 완료.");
        }
    }
}