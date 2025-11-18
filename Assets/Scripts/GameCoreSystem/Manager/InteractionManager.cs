using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;

public class InteractionManager : DestroySingleton<InteractionManager> 
{
    // 상호작용 모드 정의
    public enum InteractMode
    {
        Mine,      // 채광 모드
        Harvest,   // 벌목/수확 모드
        Build,     // 건설 모드  
        Demolish   // 철거 모드
    }

    [Header("필수 연결 (씬)")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Transform itemDropParent;
    
    [Header("드래그 선택")]
    [SerializeField] private GameObject selectionBoxPrefab; // 선택 영역 표시용
    [SerializeField] private Color miningSelectionColor = new Color(1, 1, 0, 0.3f);
    [SerializeField] private Color harvestSelectionColor = new Color(0, 1, 0, 0.3f);
    [SerializeField] private Color demolishSelectionColor = new Color(1, 0, 0, 0.3f);
    
    [Header("건설 설정")]
    [SerializeField] private GameObject placementGhostPrefab;
    [SerializeField] private Color validColor = new Color(0, 1, 0, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1, 0, 0, 0.5f);
    
    [Header("테스트용")]
    [SerializeField] private BuildingData testBuildingToBuild;
    [SerializeField] private bool enableCheatKey = true; // 1번 키 치트 활성화

    // 시스템 참조
    private GameMap _gameMap;
    private MapRenderer _mapRenderer;
    private ResourceManager _resourceManager;
    private CameraController _cameraController;
    
    // 모드 관리
    private InteractMode _currentMode = InteractMode.Mine;
    private BuildingData _buildingToBuild;
    
    // 건설 모드 변수
    private GameObject _ghostParent;
    private List<SpriteRenderer> _ghostSprites = new List<SpriteRenderer>();
    private bool _isPlacementValid = false;
    
    // 드래그 선택 변수
    private bool _isDragging = false;
    private Vector3 _dragStartPos;
    private Vector3 _dragEndPos;
    private GameObject _selectionBox;
    private List<GameObject> _selectedObjects = new List<GameObject>();
    private List<Vector3Int> _selectedTiles = new List<Vector3Int>();
    
    // 작업 명령 추적
    private List<MiningOrder> _miningOrders = new List<MiningOrder>();
    private List<HarvestOrder> _harvestOrders = new List<HarvestOrder>();
    private List<DemolishOrder> _demolishOrders = new List<DemolishOrder>();
    
    // 이벤트
    public delegate void ModeChangedDelegate(InteractMode newMode);
    public event ModeChangedDelegate OnModeChanged;
    
    protected override void Awake()
    {
        base.Awake();
        CreateSelectionBox();
    }

    void Start()
    {
        if (MapGenerator.instance == null)
        {
            Debug.LogError("MapGenerator.instance가 null입니다!");
            return;
        }

        _gameMap = MapGenerator.instance.GameMapInstance;
        _mapRenderer = MapGenerator.instance.MapRendererInstance;
        _resourceManager = MapGenerator.instance.ResourceManagerInstance;
        _cameraController = Camera.main.GetComponent<CameraController>();
    }

    void Update()
    {
        if (_gameMap == null) return;
        
        // 모드 전환 핫키
        HandleModeHotkeys();
        
        // 치트키 처리
        if (enableCheatKey && Input.GetKeyDown(KeyCode.Alpha1))
        {
            ExecuteAllOrdersInstantly();
        }
        
        // 현재 모드에 따른 업데이트
        switch (_currentMode)
        {
            case InteractMode.Mine:
                HandleMineMode();
                break;
            case InteractMode.Harvest:
                HandleHarvestMode();
                break;
            case InteractMode.Build:
                HandleBuildMode();
                break;
            case InteractMode.Demolish:
                HandleDemolishMode();
                break;
        }
        
        // 드래그 선택 업데이트
        if (_isDragging && _selectionBox != null)
        {
            UpdateSelectionBox();
        }
    }
    
    private void CreateSelectionBox()
    {
        if (selectionBoxPrefab != null)
        {
            _selectionBox = Instantiate(selectionBoxPrefab);
        }
        else
        {
            // 기본 선택 박스 생성
            _selectionBox = new GameObject("SelectionBox");
            SpriteRenderer sr = _selectionBox.AddComponent<SpriteRenderer>();
            sr.sprite = CreateWhiteSprite();
            sr.sortingOrder = 100;
        }
        _selectionBox.SetActive(false);
    }
    
    private Sprite CreateWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }
    
    private void HandleModeHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            SetMode(InteractMode.Mine);
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            SetMode(InteractMode.Harvest);
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            if (_currentMode == InteractMode.Build)
            {
                SetMode(InteractMode.Mine);
            }
            else
            {
                EnterBuildMode(testBuildingToBuild);
            }
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            SetMode(InteractMode.Demolish);
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetMode(InteractMode.Mine);
        }
    }
    
    public void SetMode(InteractMode mode)
    {
        if (_currentMode == mode) return;
        
        ExitCurrentMode();
        _currentMode = mode;
        
        Debug.Log($"[Interaction] 모드 변경: {mode}");
        OnModeChanged?.Invoke(mode);
        
        // 선택 박스 색상 변경
        UpdateSelectionBoxColor();
    }
    
    private void ExitCurrentMode()
    {
        CancelDrag();
        
        switch (_currentMode)
        {
            case InteractMode.Build:
                ExitBuildMode();
                break;
        }
    }
    
    private void UpdateSelectionBoxColor()
    {
        if (_selectionBox == null) return;
        
        SpriteRenderer sr = _selectionBox.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        
        switch (_currentMode)
        {
            case InteractMode.Mine:
                sr.color = miningSelectionColor;
                break;
            case InteractMode.Harvest:
                sr.color = harvestSelectionColor;
                break;
            case InteractMode.Demolish:
                sr.color = demolishSelectionColor;
                break;
            default:
                sr.color = new Color(1, 1, 1, 0.3f);
                break;
        }
    }

    #region 채광 모드 (Mine Mode)
    
    private void HandleMineMode()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _dragStartPos = _cameraController.GetMouseWorldPosition();
            _isDragging = true;
        }
        else if (Input.GetMouseButton(0) && _isDragging)
        {
            _dragEndPos = _cameraController.GetMouseWorldPosition();
        }
        else if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            FinishMiningSelection();
            CancelDrag();
        }
        
        // 우클릭으로 취소
        if (Input.GetMouseButtonDown(1))
        {
            CancelDrag();
        }
    }
    
    private void FinishMiningSelection()
    {
        Vector3Int startCell = groundTilemap.WorldToCell(_dragStartPos);
        Vector3Int endCell = groundTilemap.WorldToCell(_dragEndPos);
        
        int minX = Mathf.Min(startCell.x, endCell.x);
        int maxX = Mathf.Max(startCell.x, endCell.x);
        int minY = Mathf.Min(startCell.y, endCell.y);
        int maxY = Mathf.Max(startCell.y, endCell.y);
        
        List<Vector3Int> tilesToMine = new List<Vector3Int>();
        
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (CanMineTile(x, y))
                {
                    tilesToMine.Add(new Vector3Int(x, y, 0));
                }
            }
        }
        
        if (tilesToMine.Count > 0)
        {
            CreateMiningOrders(tilesToMine);
        }
    }
    
    private bool CanMineTile(int x, int y)
    {
        if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT) return false;
        
        int tileID = _gameMap.TileGrid[x, y];
        
        // 공기(0)와 가공된 흙(7)은 채광 불가
        return tileID != 0 && tileID != 7;
    }
    
    private void CreateMiningOrders(List<Vector3Int> tiles)
    {
        foreach (var tile in tiles)
        {
            // 이미 명령이 있는지 확인
            if (_miningOrders.Any(o => o.position == tile && !o.completed)) continue;
            
            MiningOrder order = new MiningOrder
            {
                position = tile,
                tileID = _gameMap.TileGrid[tile.x, tile.y],
                priority = CalculatePriority(tile),
                completed = false
            };
            
            _miningOrders.Add(order);
            
            // WorkManager에 작업 등록
            if (WorkManager.instance != null)
            {
                WorkManager.instance.RegisterWork(order);
            }
            
            // 시각적 마커 표시 (선택사항)
            ShowMiningMarker(tile);
        }
        
        Debug.Log($"[Interaction] {tiles.Count}개 타일에 채광 명령 추가");
    }
    
    private void ShowMiningMarker(Vector3Int position)
    {
        // 채광 예정 타일에 마커 표시 (나중에 구현)
        // 반투명 X 표시나 테두리 등
    }
    
    #endregion

    #region 수확 모드 (Harvest Mode)
    
    private void HandleHarvestMode()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _dragStartPos = _cameraController.GetMouseWorldPosition();
            _isDragging = true;
        }
        else if (Input.GetMouseButton(0) && _isDragging)
        {
            _dragEndPos = _cameraController.GetMouseWorldPosition();
            UpdateHarvestSelection();
        }
        else if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            FinishHarvestSelection();
            CancelDrag();
        }
        
        if (Input.GetMouseButtonDown(1))
        {
            CancelDrag();
        }
    }
    
    private void UpdateHarvestSelection()
    {
        // 범위 내 수확 가능한 오브젝트 하이라이트
        Bounds selectionBounds = GetSelectionBounds();
        
        // 이전 선택 초기화
        foreach (var obj in _selectedObjects)
        {
            if (obj != null)
            {
                SetObjectHighlight(obj, false);
            }
        }
        _selectedObjects.Clear();
        
        // 새로운 선택
        Collider2D[] colliders = Physics2D.OverlapBoxAll(
            selectionBounds.center,
            selectionBounds.size,
            0f
        );
        
        foreach (var collider in colliders)
        {
            IHarvestable harvestable = collider.GetComponent<IHarvestable>();
            if (harvestable != null && harvestable.CanHarvest())
            {
                _selectedObjects.Add(collider.gameObject);
                SetObjectHighlight(collider.gameObject, true);
            }
        }
    }
    
    private void FinishHarvestSelection()
    {
        foreach (var obj in _selectedObjects)
        {
            IHarvestable harvestable = obj.GetComponent<IHarvestable>();
            if (harvestable != null && harvestable.CanHarvest())
            {
                HarvestOrder order = new HarvestOrder
                {
                    target = harvestable,
                    position = obj.transform.position,
                    priority = CalculatePriority(obj.transform.position),
                    completed = false
                };
                
                _harvestOrders.Add(order);
                
                if (WorkManager.instance != null)
                {
                    WorkManager.instance.RegisterWork(order);
                }
            }
            
            SetObjectHighlight(obj, false);
        }
        
        if (_selectedObjects.Count > 0)
        {
            Debug.Log($"[Interaction] {_selectedObjects.Count}개 오브젝트에 수확 명령 추가");
        }
        
        _selectedObjects.Clear();
    }
    
    private void SetObjectHighlight(GameObject obj, bool highlight)
    {
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = highlight ? new Color(1.5f, 1.5f, 1.5f) : Color.white;
        }
    }
    
    #endregion

    #region 건설 모드 (Build Mode)
    
    public void EnterBuildMode(BuildingData buildingData)
    {
        if (buildingData == null || placementGhostPrefab == null)
        {
            Debug.LogError("건설할 건물 데이터 또는 고스트 프리팹이 없습니다.");
            return;
        }

        ExitCurrentMode();
        _currentMode = InteractMode.Build;
        _buildingToBuild = buildingData;
        _ghostSprites.Clear();
        
        Debug.Log($"[Interaction] 건설 모드 진입: {_buildingToBuild.buildingName}");

        _ghostParent = new GameObject("PlacementGhost");

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
        
        OnModeChanged?.Invoke(InteractMode.Build);
    }

    public void ExitBuildMode()
    {
        if (_ghostParent != null)
        {
            Destroy(_ghostParent);
        }
        _ghostSprites.Clear();
        _buildingToBuild = null;
    }

    private void HandleBuildMode()
    {
        Vector3 worldPos = _cameraController.GetMouseWorldPosition();
        Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);
        
        Vector3 targetWorldPos = groundTilemap.CellToWorld(cellPos);
        _ghostParent.transform.position = targetWorldPos;

        _isPlacementValid = CheckPlacementValidity(cellPos);

        Color color = _isPlacementValid ? validColor : invalidColor;
        foreach (var renderer in _ghostSprites)
        {
            renderer.color = color;
        }
        
        if (_isPlacementValid && Input.GetMouseButtonDown(0))
        {
            PlaceBuilding(cellPos);
        }
        else if (Input.GetMouseButtonDown(1))
        {
            SetMode(InteractMode.Mine);
        }
    }

    private bool CheckPlacementValidity(Vector3Int gridBasePos)
    {
        int startX = gridBasePos.x;
        int startY = gridBasePos.y;

        for (int y = 0; y < _buildingToBuild.size.y; y++)
        {
            for (int x = 0; x < _buildingToBuild.size.x; x++)
            {
                int checkX = startX + x;
                int checkY = startY + y;
                
                if (!_gameMap.IsSpaceAvailable(checkX, checkY))
                {
                    return false;
                }
                
                if (y == 0)
                {
                    if (!_gameMap.IsSolidGround(checkX, checkY - 1))
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private void PlaceBuilding(Vector3Int gridBasePos)
    {
        if (InventoryManager.instance == null)
        {
            Debug.LogError("[Interaction] InventoryManager를 찾을 수 없습니다!");
            return;
        }
        
        var requiredRes = _buildingToBuild.requiredResources;

        if (!InventoryManager.instance.HasItems(requiredRes))
        {
            Debug.LogWarning($"[Interaction] 재료가 부족하여 '{_buildingToBuild.buildingName}'을(를) 건설할 수 없습니다.");
            return;
        }

        InventoryManager.instance.RemoveItems(requiredRes);

        Vector3 worldPos = groundTilemap.CellToWorld(gridBasePos);
        GameObject buildingObj = Instantiate(_buildingToBuild.buildingPrefab, worldPos, Quaternion.identity, _mapRenderer.entityParent);
        
        Building buildingScript = buildingObj.GetComponent<Building>();
        if(buildingScript != null)
        {
            buildingScript.Initialize(_buildingToBuild);
        }

        int startX = gridBasePos.x;
        int startY = gridBasePos.y;
        
        for (int y = 0; y < _buildingToBuild.size.y; y++)
        {
            for (int x = 0; x < _buildingToBuild.size.x; x++)
            {
                _gameMap.MarkTileOccupied(startX + x, startY + y);
            }
        }
        
        Debug.Log($"[Interaction] '{_buildingToBuild.buildingName}' 건설 완료!");
    }
    
    #endregion

    #region 철거 모드 (Demolish Mode)
    
    private void HandleDemolishMode()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _dragStartPos = _cameraController.GetMouseWorldPosition();
            _isDragging = true;
        }
        else if (Input.GetMouseButton(0) && _isDragging)
        {
            _dragEndPos = _cameraController.GetMouseWorldPosition();
            UpdateDemolishSelection();
        }
        else if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            FinishDemolishSelection();
            CancelDrag();
        }
        
        if (Input.GetMouseButtonDown(1))
        {
            CancelDrag();
        }
    }
    
    private void UpdateDemolishSelection()
    {
        Bounds selectionBounds = GetSelectionBounds();
        
        foreach (var obj in _selectedObjects)
        {
            if (obj != null)
            {
                SetObjectHighlight(obj, false);
            }
        }
        _selectedObjects.Clear();
        
        Collider2D[] colliders = Physics2D.OverlapBoxAll(
            selectionBounds.center,
            selectionBounds.size,
            0f
        );
        
        foreach (var collider in colliders)
        {
            Building building = collider.GetComponent<Building>();
            if (building != null)
            {
                _selectedObjects.Add(collider.gameObject);
                SetObjectHighlight(collider.gameObject, true);
            }
        }
    }
    
    private void FinishDemolishSelection()
    {
        foreach (var obj in _selectedObjects)
        {
            Building building = obj.GetComponent<Building>();
            if (building != null)
            {
                DemolishOrder order = new DemolishOrder
                {
                    building = building,
                    position = obj.transform.position,
                    priority = CalculatePriority(obj.transform.position),
                    completed = false
                };
                
                _demolishOrders.Add(order);
                
                if (WorkManager.instance != null)
                {
                    WorkManager.instance.RegisterWork(order);
                }
            }
            
            SetObjectHighlight(obj, false);
        }
        
        if (_selectedObjects.Count > 0)
        {
            Debug.Log($"[Interaction] {_selectedObjects.Count}개 건물에 철거 명령 추가");
        }
        
        _selectedObjects.Clear();
    }
    
    #endregion

    #region 드래그 선택 시스템
    
    private void UpdateSelectionBox()
    {
        if (_selectionBox == null) return;
        
        _selectionBox.SetActive(true);
        
        Vector3 currentMousePos = _cameraController.GetMouseWorldPosition();
        Vector3 center = (_dragStartPos + currentMousePos) / 2f;
        Vector3 size = new Vector3(
            Mathf.Abs(currentMousePos.x - _dragStartPos.x),
            Mathf.Abs(currentMousePos.y - _dragStartPos.y),
            1f
        );
        
        _selectionBox.transform.position = center;
        _selectionBox.transform.localScale = size;
    }
    
    private Bounds GetSelectionBounds()
    {
        Vector3 center = (_dragStartPos + _dragEndPos) / 2f;
        Vector3 size = new Vector3(
            Mathf.Abs(_dragEndPos.x - _dragStartPos.x),
            Mathf.Abs(_dragEndPos.y - _dragStartPos.y),
            1f
        );
        
        return new Bounds(center, size);
    }
    
    private void CancelDrag()
    {
        _isDragging = false;
        if (_selectionBox != null)
        {
            _selectionBox.SetActive(false);
        }
        
        foreach (var obj in _selectedObjects)
        {
            if (obj != null)
            {
                SetObjectHighlight(obj, false);
            }
        }
        _selectedObjects.Clear();
    }
    
    #endregion

    #region 치트 기능
    
    private void ExecuteAllOrdersInstantly()
    {
        Debug.Log("[CHEAT] 모든 작업 즉시 완료!");
        
        // 채광 명령 즉시 실행
        foreach (var order in _miningOrders.ToList())
        {
            if (!order.completed)
            {
                ExecuteMiningInstantly(order);
            }
        }
        _miningOrders.Clear();
        
        // 수확 명령 즉시 실행
        foreach (var order in _harvestOrders.ToList())
        {
            if (!order.completed)
            {
                order.target.Harvest();
                order.completed = true;
            }
        }
        _harvestOrders.Clear();
        
        // 철거 명령 즉시 실행
        foreach (var order in _demolishOrders.ToList())
        {
            if (!order.completed && order.building != null)
            {
                DemolishBuildingInstantly(order.building);
                order.completed = true;
            }
        }
        _demolishOrders.Clear();
    }
    
    private void ExecuteMiningInstantly(MiningOrder order)
    {
        int x = order.position.x;
        int y = order.position.y;
        
        // 드롭 아이템 생성
        GameObject dropPrefab = _resourceManager.GetDropPrefab(order.tileID);
        if (dropPrefab != null)
        {
            Vector3 dropPos = groundTilemap.CellToWorld(order.position) + new Vector3(0.5f, 0.5f, 0);
            Instantiate(dropPrefab, dropPos, Quaternion.identity, itemDropParent);
        }
        
        // 타일 제거
        _gameMap.SetTile(x, y, 0); // AIR_ID
        _gameMap.UnmarkTileOccupied(x, y);
        _mapRenderer.UpdateTileVisual(x, y);
        
        // 기반 체크
        CheckFoundationSupport(x, y);
        
        order.completed = true;
    }
    
    private void DemolishBuildingInstantly(Building building)
    {
        Vector3Int cellPos = groundTilemap.WorldToCell(building.transform.position);
        
        // 점유 해제
        for (int y = 0; y < building.buildingData.size.y; y++)
        {
            for (int x = 0; x < building.buildingData.size.x; x++)
            {
                _gameMap.UnmarkTileOccupied(cellPos.x + x, cellPos.y + y);
            }
        }
        
        // 자원 일부 반환
        if (InventoryManager.instance != null && building.buildingData != null)
        {
            foreach (var cost in building.buildingData.requiredResources)
            {
                int returnAmount = Mathf.Max(1, cost.amount / 2);
                InventoryManager.instance.AddItem(cost.item, returnAmount);
            }
        }
        
        Destroy(building.gameObject);
    }
    
    private void CheckFoundationSupport(int x, int y)
    {
        Vector3 worldPosAbove = groundTilemap.CellToWorld(new Vector3Int(x, y + 1, 0)) + new Vector3(0.5f, 0.5f, 0);
        Collider2D[] colliders = Physics2D.OverlapPointAll(worldPosAbove);

        foreach (Collider2D col in colliders)
        {
            Building building = col.GetComponent<Building>();
            if (building != null)
            {
                building.OnFoundationDestroyed();
            }
        }
    }
    
    #endregion
    
    private int CalculatePriority(Vector3Int position)
    {
        // 플레이어 또는 중요 지점과의 거리로 우선순위 계산
        return 1; // 임시
    }
    
    private int CalculatePriority(Vector3 position)
    {
        return 1; // 임시
    }
}

// 작업 명령 클래스들
[System.Serializable]
public class MiningOrder : IWorkTarget
{
    public Vector3Int position;
    public int tileID;
    public int priority;
    public bool completed;
    public Employee assignedWorker;
    
    // IWorkTarget 구현
    public Vector3 GetWorkPosition() => new Vector3(position.x + 0.5f, position.y + 0.5f, 0);
    public WorkType GetWorkType() => WorkType.Mining;
    public float GetWorkTime() => 3f; // 기본 채광 시간
    public bool IsWorkAvailable() => !completed && assignedWorker == null;
    
    public void CompleteWork(Employee worker)
    {
        if (InteractionManager.instance != null)
        {
            // 실제 채광 실행은 InteractionManager가 처리
            completed = true;
            assignedWorker = null;
        }
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}

[System.Serializable]
public class HarvestOrder : IWorkTarget
{
    public IHarvestable target;
    public Vector3 position;
    public int priority;
    public bool completed;
    public Employee assignedWorker;
    
    // IWorkTarget 구현
    public Vector3 GetWorkPosition() => position;
    public WorkType GetWorkType() => target.GetHarvestType();
    public float GetWorkTime() => target.GetHarvestTime();
    public bool IsWorkAvailable() => !completed && target.CanHarvest() && assignedWorker == null;
    
    public void CompleteWork(Employee worker)
    {
        target.Harvest();
        completed = true;
        assignedWorker = null;
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}

[System.Serializable]
public class DemolishOrder : IWorkTarget
{
    public Building building;
    public Vector3 position;
    public int priority;
    public bool completed;
    public Employee assignedWorker;
    
    // IWorkTarget 구현
    public Vector3 GetWorkPosition() => position;
    public WorkType GetWorkType() => WorkType.Demolish;
    public float GetWorkTime() => 5f; // 기본 철거 시간
    public bool IsWorkAvailable() => !completed && building != null && assignedWorker == null;
    
    public void CompleteWork(Employee worker)
    {
        // 실제 철거는 InteractionManager가 처리
        completed = true;
        assignedWorker = null;
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}

// 수확 가능한 오브젝트 인터페이스
public interface IHarvestable
{
    bool CanHarvest();
    void Harvest();
    float GetHarvestTime();
    WorkType GetHarvestType();
}