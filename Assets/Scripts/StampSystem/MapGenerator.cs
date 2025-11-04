// --- 파일 8: MapGenerator.cs (나무 배치 디버그 추가 버전) ---

using UnityEngine;

namespace StampSystem
{
    [RequireComponent(typeof(MapRenderer))]
    public class MapGenerator : DestroySingleton<MapGenerator>
    {
        [Header("필수 연결")]
        [SerializeField]
        private StampLibrary stampLibrary;

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
        [Tooltip("나무 배치를 활성화합니다.")]
        [SerializeField] private bool placeTrees = true;
        
        [Tooltip("MyStampLibrary에 등록된 나무 스탬프의 Key")]
        [SerializeField] private string treeStampKey = "TREE_2x3";
        
        [Tooltip("나무와 나무 사이의 최소 X좌표 거리")]
        [SerializeField] [Range(2, 20)] private int minTreeDistance = 5;
        
        [Tooltip("나무가 심어질 수 없는 스폰 지점(X=100) 좌우 여유 공간")]
        [SerializeField] [Range(5, 50)] private int spawnAreaPadding = 15;
        
        [Tooltip("평평한 흙 타일에서 나무가 심어질 확률 (0.0 ~ 1.0)")]
        [SerializeField] [Range(0f, 1f)] private float treePlacementChance = 0.5f;

        // --- 내부 시스템 변수 ---
        private GameMap _gameMap;
        private MapStamper _stamper;
        private MapRenderer _mapRenderer;
        
        // 사용할 타일 ID
        private const int AIR_ID = 0;
        private const int DIRT_ID = 1;
        private const int STONE_ID = 2;
        private const int COPPER_ID = 3;
        private const int IRON_ID = 4;
        private const int GOLD_ID = 5;

        // --- Unity 생명주기 ---
        void Start()
        {
            if (stampLibrary == null)
            {
                Debug.LogError("StampLibrary가 설정되지 않았습니다! MapGenerator를 실행할 수 없습니다.");
                return;
            }

            _mapRenderer = GetComponent<MapRenderer>();
            _gameMap = new GameMap();
            _stamper = new MapStamper(_gameMap, stampLibrary);

            if (noiseSeed == 0f)
            {
                noiseSeed = Random.Range(0f, 1000f);
                Debug.Log($"[MapGenerator] 랜덤 시드 생성: {noiseSeed}");
            }

            GenerateWorld(); // ★★★ 메인 함수 호출 ★★★
            
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

            // 1. 기본 지형(언덕, 돌, 흙)과 광물, 동굴을 생성하고, Y좌표 높이맵을 반환받습니다.
            int[] groundHeightMap = GenerateBaseTerrainAndOres();

            // 2. 생성된 지형 위에 나무를 배치합니다.
            PlaceTrees(groundHeightMap);

            // 3. 마지막으로 스폰 지점 스탬프를 찍습니다.
            PlaceSpawnStamps(groundHeightMap);
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

        // ★★★ [디버그 로그가 추가된 함수] ★★★
        private void PlaceTrees(int[] groundHeightMap)
        {
            Debug.Log($"[PlaceTrees] 나무 배치 시작. (Place Trees 체크: {placeTrees})");
            
            // 1. 인스펙터에서 비활성화했으면 바로 종료
            if (!placeTrees)
            {
                Debug.LogWarning("[PlaceTrees] 실패: 인스펙터에서 'Place Trees'가 체크 해제되었습니다.");
                return;
            }

            int spawnX = 100;
            int lastTreeX = -minTreeDistance; 
            
            // 디버그용 카운터
            int skippedByPadding = 0;
            int skippedByDistance = 0;
            int failedFlatDirtCheck = 0;
            int failedChanceRoll = 0;
            int treesPlaced = 0;

            // 나무 스탬프의 너비 (2x3이므로 너비 2)
            const int treeWidth = 2; 

            for (int x = 0; x < GameMap.MAP_WIDTH - treeWidth; x++) 
            {
                // 조건 1: 스폰 지점 보호
                if (x >= spawnX - spawnAreaPadding && x <= spawnX + spawnAreaPadding)
                {
                    skippedByPadding++;
                    continue;
                }

                // 조건 2: 나무 최소 거리
                if (x < lastTreeX + minTreeDistance)
                {
                    skippedByDistance++;
                    continue;
                }

                // 조건 3: 평평한 '흙' 바닥 확인 (나무 너비만큼)
                int y = groundHeightMap[x]; // 현재 (x) 위치의 땅 높이
                bool isFlatDirtPatch = _gameMap.TileGrid[x, y] == DIRT_ID; // (x)가 흙인가?

                for (int i = 1; i < treeWidth; i++) // 나무 너비(2)만큼 옆칸도 검사
                {
                    if (groundHeightMap[x + i] != y || _gameMap.TileGrid[x + i, y] != DIRT_ID)
                    {
                        isFlatDirtPatch = false; // 높이가 다르거나 흙이 아니면 실패
                        break;
                    }
                }
                
                if (isFlatDirtPatch)
                {
                    // 조건 4: 배치 확률
                    if (Random.value < treePlacementChance) 
                    {
                        _stamper.PlaceStamp(treeStampKey, new Vector2Int(x, y + 1));
                        lastTreeX = x; // 마지막 나무 위치 갱신
                        treesPlaced++;
                    }
                    else
                    {
                        failedChanceRoll++; // 확률에서 탈락
                    }
                }
                else
                {
                    failedFlatDirtCheck++; // 평평한 흙바닥이 아님
                }
            }
            
            // ★★★ [최종 요약 디버그 로그] ★★★
            Debug.Log($"[PlaceTrees] 배치 완료. 결과 요약:\n" +
                      $"- 스폰 지점이라 건너뜀: {skippedByPadding}칸\n" +
                      $"- 나무가 너무 가까워 건너뜀: {skippedByDistance}칸\n" +
                      $"- <b>평평한 흙바닥이 아니라 실패: {failedFlatDirtCheck}칸</b>\n" +
                      $"- <b>확률(Chance)에서 탈락: {failedChanceRoll}칸</b>\n" +
                      $"- <b><color=green>최종 배치된 나무: {treesPlaced}그루</color></b>");
        }

        private void PlaceSpawnStamps(int[] groundHeightMap)
        {
            int spawnX = 100;
            int spawnY = groundHeightMap[spawnX]; // 실제 지형 높이
            Vector2Int spawnPoint = new Vector2Int(spawnX, spawnY + 1); 

            _stamper.PlaceStamp("FLOOR_PATCH", spawnPoint);
            _stamper.PlaceStamp("MY_2X1_BUILDING", spawnPoint);
        }
    }
}