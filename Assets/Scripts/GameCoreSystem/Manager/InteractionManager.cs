// --- 파일 12: TileMiningManager.cs (아이템 들기 로직 제거 버전) ---

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class InteractionManager : DestroySingleton<InteractionManager> 
    {
        // 상호작용 모드 정의
        public enum InteractMode
        {
            Mine,  // 채광 모드
            Build  // 건설 모드
        }

        [Header("필수 연결 (씬)")]
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Transform itemDropParent;
        
        [Header("건설 설정")]
        [Tooltip("1x1 크기의 흰색 테두리 스프라이트 프리팹 (피벗: 0,0)")]
        [SerializeField] private GameObject placementGhostPrefab;
        
        [Tooltip("건설 가능할 때의 고스트 색상")]
        [SerializeField] private Color validColor = new Color(0, 1, 0, 0.5f); // Green
        
        [Tooltip("건설 불가능할 때의 고스트 색상")]
        [SerializeField] private Color invalidColor = new Color(1, 0, 0, 0.5f); // Red
        
        [Header("테스트용 건물")]
        [Tooltip("(테스트용) 'B' 키를 눌렀을 때 건설할 건물 데이터")]
        [SerializeField] private BuildingData testBuildingToBuild; 

        // --- 시스템 참조 ---
        private GameMap _gameMap;
        private MapRenderer _mapRenderer;
        private ResourceManager _resourceManager;
        private CameraController _cameraController;
        
        // --- 모드 관리 변수 ---
        private InteractMode _currentMode = InteractMode.Mine; // 기본 모드는 채광
        private BuildingData _buildingToBuild; // 현재 건설하려는 건물
        private GameObject _ghostParent; // 고스트 타일들을 담을 부모
        private List<SpriteRenderer> _ghostSprites = new List<SpriteRenderer>();
        private bool _isPlacementValid = false;

        
        protected override void Awake()
        {
            base.Awake(); // 'Instance = this' 실행
        }

        void Start()
        {
            // MapGenerator의 Awake()가 끝난 후이므로 안전하게 참조
            if (MapGenerator.instance == null) { /* ... */ return; }

            _gameMap = MapGenerator.instance.GameMapInstance;
            _mapRenderer = MapGenerator.instance.MapRendererInstance;
            _resourceManager = MapGenerator.instance.ResourceManagerInstance;
            _cameraController = Camera.main.GetComponent<CameraController>();

            if (_gameMap == null || _mapRenderer == null || _resourceManager == null || _cameraController == null)
            {
                Debug.LogError("InteractionManager: 핵심 시스템 참조에 실패했습니다!");
            }
        }

        void Update()
        {
            if (_gameMap == null) return;
            
            // 'B' 키로 채광/건설 모드 토글 (테스트용)
            if (Input.GetKeyDown(KeyCode.B))
            {
                if (_currentMode == InteractMode.Mine)
                {
                    EnterBuildMode(testBuildingToBuild);
                }
                else
                {
                    ExitBuildMode();
                }
            }

            // 현재 모드에 따라 다른 Update 로직 실행
            if (_currentMode == InteractMode.Build)
            {
                HandleBuildMode();
            }
            else // InteractMode.Mine
            {
                HandleMineMode();
            }
        }

        #region 건설 모드 (Build Mode)
        
        /// <summary>
        /// 건설 모드로 진입합니다.
        /// </summary>
        public void EnterBuildMode(BuildingData buildingData)
        {
            if (buildingData == null || placementGhostPrefab == null)
            {
                Debug.LogError("건설할 건물 데이터(BuildingData) 또는 고스트 프리팹이 없습니다.");
                return;
            }

            _currentMode = InteractMode.Build;
            _buildingToBuild = buildingData;
            _ghostSprites.Clear();
            
            Debug.Log($"[Interaction] 건설 모드 진입: {_buildingToBuild.buildingName}");

            // 고스트 타일들을 담을 부모 오브젝트 생성
            _ghostParent = new GameObject("PlacementGhost");

            // (요청사항 1: 흰색 테두리 스프라이트)
            // 건물 크기(예: 3x2)만큼 고스트 프리팹(1x1)을 생성하여 격자로 배치
            // 피벗이 (0,0)이므로 타일 중앙에 오도록 0.5f씩 더해줍니다.
            for (int y = 0; y < _buildingToBuild.size.y; y++)
            {
                for (int x = 0; x < _buildingToBuild.size.x; x++)
                {
                    Vector3 localPos = new Vector3(x + 0.5f, y + 0.5f, 0); 
                    GameObject ghostTile = Instantiate(placementGhostPrefab, localPos, Quaternion.identity, _ghostParent.transform);
                    ghostTile.transform.localPosition = localPos;
                    _ghostSprites.Add(ghostTile.GetComponent<SpriteRenderer>());
                }
            }
        }

        /// <summary>
        /// 건설 모드를 종료하고 채광 모드로 복귀합니다.
        /// </summary>
        public void ExitBuildMode()
        {
            if (_ghostParent != null)
            {
                Destroy(_ghostParent);
            }
            _ghostSprites.Clear();
            _buildingToBuild = null;
            _currentMode = InteractMode.Mine;
            Debug.Log("[Interaction] 채광 모드 진입.");
        }

        /// <summary>
        /// 건설 모드일 때 매 프레임 실행 (고스트 이동, 유효성 검사)
        /// </summary>
        private void HandleBuildMode()
        {
            // 1. 마우스 위치를 타일맵 그리드 좌표로 변환
            Vector3 worldPos = _cameraController.GetMouseWorldPosition();
            Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);
            
            // 2. 고스트 오브젝트를 마우스의 그리드 위치로 이동 (피벗 적용)
            // 건물의 피벗(0,0)이 마우스 셀(cellPos) 위치에 오도록 함
            Vector3 targetWorldPos = groundTilemap.CellToWorld(cellPos);
            _ghostParent.transform.position = targetWorldPos;

            // 3. 건설 유효성 검사
            _isPlacementValid = CheckPlacementValidity(cellPos);

            // 4. 고스트 색상 변경
            Color color = _isPlacementValid ? validColor : invalidColor;
            foreach (var renderer in _ghostSprites)
            {
                renderer.color = color;
            }
            
            // 5. 건설 시도 (좌클릭)
            if (_isPlacementValid && Input.GetMouseButtonDown(0))
            {
                PlaceBuilding(cellPos);
            }
        }

        /// <summary>
        /// (요청사항 2: 건설 조건) 현재 위치에 건물을 지을 수 있는지 확인합니다.
        /// </summary>
        private bool CheckPlacementValidity(Vector3Int gridBasePos)
        {
            // gridBasePos는 건물의 (0,0) 피벗이 위치할 좌표입니다.
            int startX = gridBasePos.x;
            int startY = gridBasePos.y;

            for (int y = 0; y < _buildingToBuild.size.y; y++)
            {
                for (int x = 0; x < _buildingToBuild.size.x; x++)
                {
                    int checkX = startX + x;
                    int checkY = startY + y;
                    
                    // 조건 A: 이 공간이 비어있는가? (공기(0)이고 점유(false)되지 않음)
                    if (!_gameMap.IsSpaceAvailable(checkX, checkY))
                    {
                        return false; 
                    }
                    
                    // 조건 B: "바닥 타일이 무엇이라도 무조건 있어야" (건물 바닥(y=0)일 때만 그 아랫칸을 검사)
                    if (y == 0)
                    {
                        if (!_gameMap.IsSolidGround(checkX, checkY - 1)) // 아랫칸(y-1) 검사
                        {
                            return false;
                        }
                    }
                }
            }
            return true; // 모든 검사 통과
        }

        /// <summary>
        /// 실제 맵에 건물을 배치합니다.
        /// </summary>
        private void PlaceBuilding(Vector3Int gridBasePos)
        {
            // ★★★ [수정된 부분 1: 재료 확인] ★★★
            // 1. 글로벌 인벤토리에 이 건물의 건설 비용만큼 재료가 있는지 확인합니다.
            if (InventoryManager.instance == null)
            {
                Debug.LogError("[Interaction] InventoryManager를 찾을 수 없습니다!");
                return;
            }
            
            // 건설에 필요한 자원 목록 (예: 돌 2, 흙 1)
            var requiredRes = _buildingToBuild.requiredResources;

            if (!InventoryManager.instance.HasItems(requiredRes))
            {
                Debug.LogWarning($"[Interaction] 재료가 부족하여 '{_buildingToBuild.buildingName}'을(를) 건설할 수 없습니다.");
                // (나중에 여기에 "재료 부족" UI 알림 띄우기)
                return;
            }
            // ★★★ [수정 끝] ★★★


            // --- 재료가 있으므로 건설 진행 ---

            Debug.Log($"[Interaction] '{_buildingToBuild.buildingName}' 건설!");

            // 2. 인벤토리에서 재료 소모
            InventoryManager.instance.RemoveItems(requiredRes);

            // 3. 실제 프리팹 생성 (MapRenderer의 entityParent는 public이어야 함)
            Vector3 worldPos = groundTilemap.CellToWorld(gridBasePos);
            GameObject buildingObj = Instantiate(_buildingToBuild.buildingPrefab, worldPos, Quaternion.identity, _mapRenderer.entityParent); 
            
            // 4. 건물 스크립트 초기화
            Building buildingScript = buildingObj.GetComponent<Building>();
            if(buildingScript != null)
            {
                buildingScript.Initialize(_buildingToBuild);
            }

            // 5. 맵 데이터에 점유 상태(OccupiedGrid) 마킹
            int startX = gridBasePos.x;
            int startY = gridBasePos.y;
            
            for (int y = 0; y < _buildingToBuild.size.y; y++)
            {
                for (int x = 0; x < _buildingToBuild.size.x; x++)
                {
                    _gameMap.MarkTileOccupied(startX + x, startY + y);
                }
            }
            
            // 6. 건설 모드 종료
            ExitBuildMode();
        }
        
        #endregion

        #region 채광 모드 (Mine Mode)

        private void HandleMineMode()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 worldPos = _cameraController.GetMouseWorldPosition();
                Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);
                
                RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
                if (hit.collider != null && hit.collider.GetComponent<ClickableItem>() != null)
                {
                    return; 
                }
                
                MineTile(cellPos.x, cellPos.y);
            }
        }
        
        public void MineTile(int x, int y)
        {
            if (_gameMap == null || _resourceManager == null || _mapRenderer == null) return;
            if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT) return;
            
            int tileID = _gameMap.TileGrid[x, y];
            if (tileID == AIR_ID || tileID == PROCESSED_DIRT_ID) return;
            
            GameObject dropPrefab = _resourceManager.GetDropPrefab(tileID);
            if (dropPrefab != null)
            {
                Vector3 dropPos = groundTilemap.CellToWorld(new Vector3Int(x, y, 0)) + new Vector3(0.5f, 0.5f, 0);
                Instantiate(dropPrefab, dropPos, Quaternion.identity, itemDropParent);
            }

            // 1. 타일 데이터 삭제
            _gameMap.SetTile(x, y, AIR_ID);
            _gameMap.UnmarkTileOccupied(x, y); // 점유 해제
            
            // 2. 타일 시각적 업데이트
            _mapRenderer.UpdateTileVisual(x, y);
            
            // 3. ★★★ [새로 추가된 부분] ★★★
            // 방금 채광한 타일 '위'에 건물이 있었는지 확인
            CheckFoundationSupport(x, y);
        }
        
        /// <summary>
        /// (x, y) 타일이 파괴되었을 때, 그 '위' (x, y+1)에 있던 건물을 찾아 비활성화합니다.
        /// </summary>
        private void CheckFoundationSupport(int x, int y)
        {
            // 1. 방금 채광한 타일(x,y)의 '바로 위' 좌표(x, y+1)를 월드 좌표로 계산
            Vector3 worldPosAbove = groundTilemap.CellToWorld(new Vector3Int(x, y + 1, 0)) + new Vector3(0.5f, 0.5f, 0);

            // 2. 해당 위치에 있는 모든 콜라이더를 감지
            Collider2D[] colliders = Physics2D.OverlapPointAll(worldPosAbove);

            if (colliders.Length == 0) return; // 위에 아무것도 없었음

            foreach (Collider2D col in colliders)
            {
                // 3. 콜라이더에서 Building.cs 스크립트를 찾음
                Building building = col.GetComponent<Building>();
                if (building != null)
                {
                    // 4. 건물을 찾았으면, 기반이 파괴되었다고 알림
                    Debug.Log($"[Interaction] 기반 타일 ({x},{y}) 파괴! -> 위에 있던 '{building.name}' 비활성화.");
                    building.OnFoundationDestroyed();
                }
            }
        }
        
        #endregion

        private const int AIR_ID = 0;
        private const int PROCESSED_DIRT_ID = 7;
    }