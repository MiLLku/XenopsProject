// --- 파일 8: MapGenerator.cs (인스펙터 창에서 수정 가능 버전) ---

using UnityEngine;

namespace StampSystem
{
    [RequireComponent(typeof(MapRenderer))]
    public class MapGenerator : DestroySingleton<MapGenerator>
    {
        [Header("필수 연결")]
        [SerializeField]
        private StampLibrary stampLibrary;

        // --- 맵 생성 파라미터 (인스펙터에서 수정) ---
        
        [Header("맵 생성 시드")]
        [Tooltip("맵 지형을 고정하려면 0이 아닌 값을, 랜덤으로 하려면 0을 입력하세요.")]
        [SerializeField] private float noiseSeed = 0f;

        [Header("언덕 지형")]
        [Tooltip("기본 땅의 높이 (Y좌표)")]
        [SerializeField] private int baseGroundLevel = 140;
        
        [Tooltip("언덕의 최대 높낮이 (기본 높이 ±_칸)")]
        [SerializeField] [Range(0f, 50f)] private float hillAmplitude = 10f;
        
        [Tooltip("언덕의 완만함 (값이 작을수록 완만하고 거대해짐)")]
        [SerializeField] [Range(0.01f, 0.1f)] private float hillScale = 0.05f;
        
        [Tooltip("지표면의 흙 두께")]
        [SerializeField] [Range(1, 20)] private int surfaceDirtDepth = 5;

        [Header("흙 덩어리 (돌 속)")]
        [Tooltip("흙 덩어리 크기 (값이 작을수록 커짐)")]
        [SerializeField] [Range(0.01f, 0.2f)] private float dirtNoiseScale = 0.08f;
        
        [Tooltip("흙 덩어리 빈도 (값이 클수록 흙이 많아짐)")]
        [SerializeField] [Range(0f, 1f)] private float dirtThreshold = 0.5f;

        [Header("동굴")]
        [Tooltip("동굴의 크기 (값이 작을수록 커짐)")]
        [SerializeField] [Range(0.01f, 0.2f)] private float caveNoiseScale = 0.07f;
        
        [Tooltip("동굴의 빈도 (값이 클수록 동굴이 많아짐)")]
        [SerializeField] [Range(0f, 1f)] private float caveThreshold = 0.7f;

        // --- 내부 시스템 변수 ---
        private GameMap _gameMap;
        private MapStamper _stamper;
        private MapRenderer _mapRenderer;
        
        // 사용할 타일 ID (MyResourceManager의 ID와 일치해야 함)
        private const int DIRT_ID = 1;  // 흙
        private const int STONE_ID = 2; // 돌

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

            // 맵 시드를 랜덤으로 설정 (0이면)
            if (noiseSeed == 0f)
            {
                noiseSeed = Random.Range(0f, 1000f);
                Debug.Log($"[MapGenerator] 랜덤 시드 생성: {noiseSeed}");
            }

            GenerateWorld();
            
            Debug.Log("--- 맵 데이터 생성 완료 ---");
            
            _mapRenderer.RenderMap(_gameMap);
            
            Debug.Log("--- 스폰 지점 근처 맵 출력 ---");
            // Y좌표를 하드코딩(150)하지 않고, 실제 생성된 높이(baseGroundLevel) 근처를 보도록 수정
            _gameMap.PrintDebugMap(100, baseGroundLevel, 10);
            
            Debug.Log($"--- 배치된 개체: {_gameMap.Entities.Count}개 ---");
            foreach (var entity in _gameMap.Entities)
            {
                Debug.Log($"> {entity.type} (ID: {entity.id}) @ {entity.position}");
            }
        }
        
        /// <summary>
        /// (원본의 MakeRandomMap/SpaceOutMap 역할)
        /// Perlin Noise를 사용해 절차적으로 맵을 생성하고 스탬프를 찍습니다.
        /// </summary>
        private void GenerateWorld()
        {
            Debug.Log("맵 데이터 생성을 시작합니다...");

            // --- 1. ID 정의 ---
            const int AIR_ID = 0;   // 빈 공간
            
            // --- 2. 지형 생성 (언덕, 동굴, 흙 덩어리) ---
            int[] groundHeightMap = new int[GameMap.MAP_WIDTH];

            for (int x = 0; x < GameMap.MAP_WIDTH; x++)
            {
                // 1. 언덕 높이 계산 (1D Perlin Noise)
                float hillNoise = Mathf.PerlinNoise((x * hillScale) + noiseSeed, noiseSeed);
                int currentHeight = baseGroundLevel + (int)(hillNoise * hillAmplitude);
                groundHeightMap[x] = currentHeight; 

                for (int y = 0; y < GameMap.MAP_HEIGHT; y++)
                {
                    if (y > currentHeight)
                    {
                        // 땅 높이보다 위는 '하늘' (AIR_ID)
                        _gameMap.SetTile(x, y, AIR_ID);
                    }
                    else
                    {
                        // 땅 속일 경우, 동굴과 흙 덩어리를 계산 (2D Perlin Noise)
                        float noiseCoordX = (x * caveNoiseScale) + noiseSeed + 1000f; 
                        float noiseCoordY = (y * caveNoiseScale) + noiseSeed + 1000f;
                        float caveNoise = Mathf.PerlinNoise(noiseCoordX, noiseCoordY);

                        float dirtCoordX = (x * dirtNoiseScale) + noiseSeed - 1000f;
                        float dirtCoordY = (y * dirtNoiseScale) + noiseSeed - 1000f;
                        float dirtNoise = Mathf.PerlinNoise(dirtCoordX, dirtCoordY);

                        if (caveNoise > caveThreshold)
                        {
                            // 동굴 생성 (AIR_ID)
                            _gameMap.SetTile(x, y, AIR_ID);
                        }
                        else if (y >= currentHeight - surfaceDirtDepth || dirtNoise > dirtThreshold)
                        {
                            // 1. 지표면 근처(surfaceDirtDepth)이거나
                            // 2. 흙 덩어리 노이즈(dirtNoise)에 걸리면 흙(DIRT_ID)
                            _gameMap.SetTile(x, y, DIRT_ID);
                        }
                        else
                        {
                            // 그 외 땅 속은 모두 돌 (STONE_ID)
                            _gameMap.SetTile(x, y, STONE_ID);
                        }
                    }
                }
            }
            
            Debug.Log("절차적 지형 생성 완료.");

            // --- 3. 지형 위에 스탬프 배치 ---
            int spawnX = 100;
            int spawnY = groundHeightMap[spawnX]; // 1D 노이즈로 계산된 실제 땅 높이
            
            Vector2Int spawnPoint = new Vector2Int(spawnX, spawnY); 

            _stamper.PlaceStamp("FLOOR_PATCH", spawnPoint);
            _stamper.PlaceStamp("MY_2X1_BUILDING", spawnPoint);
        }
    }
}