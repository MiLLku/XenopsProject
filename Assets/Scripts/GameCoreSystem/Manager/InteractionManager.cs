using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;

public class InteractionManager : DestroySingleton<InteractionManager> 
{
    // 상호작용 모드 정의
    public enum InteractMode
    {
        Mine,
        Harvest,
        Build,
        Demolish
    }

    [Header("필수 연결 (씬)")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Transform itemDropParent;
    
    [Header("드래그 선택")]
    [SerializeField] private GameObject selectionBoxPrefab;
    [SerializeField] private Color miningSelectionColor = new Color(1, 1, 0, 0.3f);
    [SerializeField] private Color harvestSelectionColor = new Color(0, 1, 0, 0.3f);
    [SerializeField] private Color demolishSelectionColor = new Color(1, 0, 0, 0.3f);
    
    [Header("건설 설정")]
    [SerializeField] private GameObject placementGhostPrefab;
    [SerializeField] private Color validColor = new Color(0, 1, 0, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1, 0, 0, 0.5f);
    
    [Header("작업 설정")]
    [SerializeField] private int defaultMiningWorkers = 3;
    [SerializeField] private int defaultHarvestWorkers = 2;
    [SerializeField] private int defaultDemolishWorkers = 1;
    
    [Header("테스트용")]
    [SerializeField] private BuildingData testBuildingToBuild;
    [SerializeField] private bool enableCheatKey = true;

    // 시스템 참조
    private GameMap _gameMap;
    private MapRenderer _mapRenderer;
    private ResourceManager _resourceManager;
    private CameraController _cameraController;
    private WorkOrderManager _workOrderManager;
    
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
        _workOrderManager = WorkOrderManager.instance;
        
        if (_workOrderManager == null)
        {
            Debug.LogError("WorkOrderManager를 찾을 수 없습니다!");
        }
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

    #region 채광 모드
    
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
            CreateMiningWorkOrder(tilesToMine);
        }
    }
    
    private bool CanMineTile(int x, int y)
    {
        if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT) return false;
        
        int tileID = _gameMap.TileGrid[x, y];
        
        // 공기(0)와 가공된 흙(7)은 채광 불가
        return tileID != 0 && tileID != 7;
    }
    
    private void CreateMiningWorkOrder(List<Vector3Int> tiles)
    {
        if (_workOrderManager == null)
        {
            Debug.LogError("[Interaction] WorkOrderManager가 없습니다!");
            return;
        }
        
        // WorkOrder 생성
        WorkOrder workOrder = _workOrderManager.CreateWorkOrder(
            $"채광 작업 ({tiles.Count}개 타일)",
            WorkType.Mining,
            maxWorkers: defaultMiningWorkers,
            priority: 3
        );
        
        // 각 타일을 MiningOrder로 변환하여 추가
        List<IWorkTarget> targets = new List<IWorkTarget>();
        foreach (var tile in tiles)
        {
            MiningOrder miningOrder = new MiningOrder
            {
                position = tile,
                tileID = _gameMap.TileGrid[tile.x, tile.y],
                priority = 3,
                completed = false
            };
            targets.Add(miningOrder);
        }
        
        workOrder.AddTargets(targets);
        
        Debug.Log($"[Interaction] 채광 작업물 생성: {tiles.Count}개 타일, 최대 작업자 {defaultMiningWorkers}명");
    }
    
    #endregion

    #region 수확 모드
    
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
        if (_selectedObjects.Count == 0) return;
        
        if (_workOrderManager == null)
        {
            Debug.LogError("[Interaction] WorkOrderManager가 없습니다!");
            return;
        }
        
        // 수확 대상의 작업 타입 결정 (나무면 Chopping, 식물이면 Gardening)
        WorkType workType = DetermineHarvestWorkType(_selectedObjects[0]);
        
        // WorkOrder 생성
        WorkOrder workOrder = _workOrderManager.CreateWorkOrder(
            $"{GetWorkTypeName(workType)} 작업 ({_selectedObjects.Count}개)",
            workType,
            maxWorkers: defaultHarvestWorkers,
            priority: 4
        );
        
        // 수확 대상 추가
        List<IWorkTarget> targets = new List<IWorkTarget>();
        foreach (var obj in _selectedObjects)
        {
            IHarvestable harvestable = obj.GetComponent<IHarvestable>();
            if (harvestable != null && harvestable.CanHarvest())
            {
                HarvestOrder harvestOrder = new HarvestOrder
                {
                    target = harvestable,
                    position = obj.transform.position,
                    priority = 4,
                    completed = false
                };
                targets.Add(harvestOrder);
            }
            SetObjectHighlight(obj, false);
        }
        
        workOrder.AddTargets(targets);
        
        Debug.Log($"[Interaction] {GetWorkTypeName(workType)} 작업물 생성: {_selectedObjects.Count}개, 최대 작업자 {defaultHarvestWorkers}명");
        
        _selectedObjects.Clear();
    }
    
    private WorkType DetermineHarvestWorkType(GameObject obj)
    {
        // 나무인지 확인
        if (obj.GetComponent<ChoppableTree>() != null)
        {
            return WorkType.Chopping;
        }
        
        // 식물인지 확인
        if (obj.GetComponent<HarvestablePlant>() != null)
        {
            return WorkType.Gardening;
        }
        
        // 기본값
        return WorkType.Gardening;
    }
    
    private string GetWorkTypeName(WorkType type)
    {
        switch (type)
        {
            case WorkType.Chopping: return "벌목";
            case WorkType.Gardening: return "수확";
            case WorkType.Mining: return "채광";
            case WorkType.Demolish: return "철거";
            default: return "작업";
        }
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

    #region 건설 모드
    
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

    #region 철거 모드
    
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
        if (_selectedObjects.Count == 0) return;
        
        if (_workOrderManager == null)
        {
            Debug.LogError("[Interaction] WorkOrderManager가 없습니다!");
            return;
        }
        
        // 각 건물마다 개별 WorkOrder 생성 (철거는 1명만 작업)
        foreach (var obj in _selectedObjects)
        {
            Building building = obj.GetComponent<Building>();
            if (building != null)
            {
                // 개별 철거 작업물 생성
                WorkOrder workOrder = _workOrderManager.CreateWorkOrder(
                    $"철거: {building.buildingData.buildingName}",
                    WorkType.Demolish,
                    maxWorkers: 1,  // 철거는 1명만
                    priority: 6
                );
                
                DemolishOrder demolishOrder = new DemolishOrder
                {
                    building = building,
                    position = obj.transform.position,
                    priority = 6,
                    completed = false
                };
                
                workOrder.AddTarget(demolishOrder);
            }
            
            SetObjectHighlight(obj, false);
        }
        
        Debug.Log($"[Interaction] {_selectedObjects.Count}개 건물 철거 명령 생성");
        
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
        Debug.Log("[CHEAT] 모든 작업물 즉시 완료!");
        
        if (_workOrderManager == null)
        {
            Debug.LogWarning("[CHEAT] WorkOrderManager가 없습니다!");
            return;
        }
        
        var allOrders = _workOrderManager.AllOrders.ToList();
        
        foreach (var workOrder in allOrders)
        {
            foreach (var target in workOrder.targets.ToList())
            {
                // 타입별 즉시 실행
                if (target is MiningOrder miningOrder)
                {
                    ExecuteMiningInstantly(miningOrder);
                }
                else if (target is HarvestOrder harvestOrder)
                {
                    if (harvestOrder.target != null)
                    {
                        harvestOrder.target.Harvest();
                    }
                }
                else if (target is DemolishOrder demolishOrder)
                {
                    if (demolishOrder.building != null)
                    {
                        DemolishBuildingInstantly(demolishOrder.building);
                    }
                }
                
                workOrder.CompleteTarget(target, null);
            }
            
            _workOrderManager.RemoveWorkOrder(workOrder);
        }
        
        Debug.Log($"[CHEAT] {allOrders.Count}개 작업물 완료!");
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
        _gameMap.SetTile(x, y, 0);
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
}

// MiningOrder, HarvestOrder, DemolishOrder는 이미 WorkOrder.cs에 정의되어 있으므로
// 여기서는 제거하고 InteractionManager에서만 사용