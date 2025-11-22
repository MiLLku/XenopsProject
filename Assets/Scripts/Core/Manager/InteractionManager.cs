using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;

public class InteractionManager : DestroySingleton<InteractionManager> 
{
    // 상호작용 모드 정의
    public enum InteractMode
    {
        Normal,     // 일반 모드 (직원, 건물, 작업물 클릭)
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
    
    [Header("최적화 설정")]
    [SerializeField] private int maxTilesPerOrder = 200;
    
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
    private InteractMode _currentMode = InteractMode.Normal; // 기본값을 Normal로 변경
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
    
    // Normal 모드용 변수
    private GameObject _hoveredObject;
    private Color _originalColor;
    private SpriteRenderer _hoveredRenderer;
    
    private TileHighlighter _tileHighlighter;
    
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
        
        _tileHighlighter = TileHighlighter.instance;
        if (_tileHighlighter == null)
        {
            Debug.LogWarning("[InteractionManager] TileHighlighter를 찾을 수 없습니다!");
        }
    }

    void Update()
    {
        if (_gameMap == null) return;
        
        HandleModeHotkeys();
        
        if (enableCheatKey && Input.GetKeyDown(KeyCode.Alpha1))
        {
            ExecuteAllOrdersInstantly();
        }
        
        switch (_currentMode)
        {
            case InteractMode.Normal:
                HandleNormalMode();
                break;
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
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
        {
            SetMode(InteractMode.Normal);
        }
        else if (Input.GetKeyDown(KeyCode.Q))
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
                SetMode(InteractMode.Normal);
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

    #region 일반 모드 (Normal Mode)
    
    private void HandleNormalMode()
    {
        // 마우스 위치에서 레이캐스트
        Vector3 mousePos = _cameraController.GetMouseWorldPosition();
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
        
        // 호버 처리
        if (hit.collider != null)
        {
            GameObject hitObject = hit.collider.gameObject;
            
            // 새로운 오브젝트에 호버
            if (_hoveredObject != hitObject)
            {
                ClearHover();
                SetHover(hitObject);
            }
        }
        else
        {
            ClearHover();
        }
        
        // 클릭 처리
        if (Input.GetMouseButtonDown(0) && hit.collider != null)
        {
            HandleNormalModeClick(hit.collider.gameObject);
        }
    }
    
    private void SetHover(GameObject obj)
    {
        _hoveredObject = obj;
        _hoveredRenderer = obj.GetComponent<SpriteRenderer>();
        
        if (_hoveredRenderer != null)
        {
            _originalColor = _hoveredRenderer.color;
            _hoveredRenderer.color = new Color(
                _originalColor.r * 1.2f,
                _originalColor.g * 1.2f,
                _originalColor.b * 1.2f,
                _originalColor.a
            );
        }
    }
    
    private void ClearHover()
    {
        if (_hoveredObject != null && _hoveredRenderer != null)
        {
            _hoveredRenderer.color = _originalColor;
        }
        
        _hoveredObject = null;
        _hoveredRenderer = null;
    }
    
    private void HandleNormalModeClick(GameObject clickedObject)
    {
        // 1. 직원 클릭
        Employee employee = clickedObject.GetComponent<Employee>();
        if (employee != null)
        {
            OnEmployeeClicked(employee);
            return;
        }
        
        // 2. 건물 클릭
        Building building = clickedObject.GetComponent<Building>();
        if (building != null)
        {
            OnBuildingClicked(building);
            return;
        }
        
        // 3. 작업대 클릭
        CraftingTable craftingTable = clickedObject.GetComponent<CraftingTable>();
        if (craftingTable != null)
        {
            OnCraftingTableClicked(craftingTable);
            return;
        }
        
        // 4. 수확 가능한 식물/나무 클릭
        IHarvestable harvestable = clickedObject.GetComponent<IHarvestable>();
        if (harvestable != null)
        {
            OnHarvestableClicked(harvestable, clickedObject);
            return;
        }
        
        // 5. 아이템 클릭
        ClickableItem item = clickedObject.GetComponent<ClickableItem>();
        if (item != null)
        {
            // ClickableItem은 자체적으로 클릭 처리
            return;
        }
        
        Debug.Log($"[Normal Mode] 클릭: {clickedObject.name} (상호작용 불가)");
    }
    
    private void OnEmployeeClicked(Employee employee)
    {
        Debug.Log($"[Normal Mode] 직원 클릭: {employee.Data.employeeName}");
        Debug.Log($"  - 상태: {employee.State}");
        Debug.Log($"  - 체력: {employee.Stats.health}/{employee.Stats.maxHealth}");
        Debug.Log($"  - 정신: {employee.Stats.mental}/{employee.Stats.maxMental}");
        Debug.Log($"  - 배고픔: {employee.Needs.hunger:F0}%");
        Debug.Log($"  - 피로: {employee.Needs.fatigue:F0}%");
        
        if (employee.CurrentWork != WorkType.None)
        {
            Debug.Log($"  - 현재 작업: {employee.CurrentWork} ({employee.WorkProgress * 100:F0}%)");
        }
        
        // TODO: 직원 정보 UI 패널 열기
    }
    
    private void OnBuildingClicked(Building building)
    {
        Debug.Log($"[Normal Mode] 건물 클릭: {building.buildingData.buildingName}");
        Debug.Log($"  - 체력: {building.CurrentHealth}/{building.buildingData.maxHealth}");
        Debug.Log($"  - 작동 중: {(building.IsFunctional ? "예" : "아니오")}");
        
        // TODO: 건물 정보 UI 패널 열기
    }
    
    private void OnCraftingTableClicked(CraftingTable craftingTable)
    {
        Debug.Log($"[Normal Mode] 작업대 클릭");
        
        if (craftingTable.IsCrafting)
        {
            Debug.Log($"  - 제작 중: {craftingTable.CurrentRecipe.outputItem.itemName}");
            Debug.Log($"  - 진행도: {craftingTable.CraftingProgress * 100:F0}%");
        }
        else
        {
            Debug.Log($"  - 상태: 대기 중");
            Debug.Log($"  - 사용 가능한 레시피: {craftingTable.AvailableRecipes.Count}개");
        }
        
        // 작업대 UI는 CraftingTable의 OnMouseDown에서 자동으로 처리됨
    }
    
    private void OnHarvestableClicked(IHarvestable harvestable, GameObject obj)
    {
        if (harvestable.CanHarvest())
        {
            Debug.Log($"[Normal Mode] 수확 가능한 오브젝트 클릭: {obj.name}");
            Debug.Log($"  - 작업 타입: {harvestable.GetHarvestType()}");
            Debug.Log($"  - 수확 시간: {harvestable.GetHarvestTime()}초");
            Debug.Log($"  힌트: [W] 키를 눌러 수확 모드로 전환하세요");
        }
        else
        {
            Debug.Log($"[Normal Mode] {obj.name} - 아직 수확할 수 없습니다");
        }
        
        // TODO: 수확물 정보 UI 표시
    }
    
    #endregion

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
        
            // ★ 드래그 중 호버 표시
            UpdateMiningHover();
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
    
    /// <summary>
    /// 채광 모드 드래그 중 호버 표시
    /// </summary>
    private void UpdateMiningHover()
    {
        if (_tileHighlighter == null) return;
    
        Vector3Int startCell = groundTilemap.WorldToCell(_dragStartPos);
        Vector3Int endCell = groundTilemap.WorldToCell(_dragEndPos);
    
        int minX = Mathf.Min(startCell.x, endCell.x);
        int maxX = Mathf.Max(startCell.x, endCell.x);
        int minY = Mathf.Min(startCell.y, endCell.y);
        int maxY = Mathf.Max(startCell.y, endCell.y);
    
        List<Vector3Int> tilesToHighlight = new List<Vector3Int>();
    
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (CanMineTile(x, y))
                {
                    tilesToHighlight.Add(new Vector3Int(x, y, 0));
                }
            }
        }
    
        _tileHighlighter.SetHoveredTiles(tilesToHighlight);
    }
    
    private void FinishMiningSelection()
    {
        Vector3Int startCell = groundTilemap.WorldToCell(_dragStartPos);
        Vector3Int endCell = groundTilemap.WorldToCell(_dragEndPos);
    
        int minX = Mathf.Min(startCell.x, endCell.x);
        int maxX = Mathf.Max(startCell.x, endCell.x);
        int minY = Mathf.Min(startCell.y, endCell.y);
        int maxY = Mathf.Max(startCell.y, endCell.y);
    
        int areaSize = (maxX - minX + 1) * (maxY - minY + 1);
        if (areaSize > maxTilesPerOrder)
        {
            Debug.LogWarning($"[Interaction] 선택 영역이 너무 큽니다! (최대: {maxTilesPerOrder}, 현재: {areaSize})");
            return;
        }
    
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
            // ★ 더 이상 즉시 하이라이트하지 않음 (WorkOrderVisual이 처리)
            CreateMiningWorkOrder(tilesToMine);
        }
    }
    
    private bool CanMineTile(int x, int y)
    {
        // 1. 맵 범위 체크
        if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT) return false;
        
        // 2. 타일 존재 여부 (공기나 이미 가공된 타일 제외)
        int tileID = _gameMap.TileGrid[x, y];
        if (tileID == 0 || tileID == 7) return false;

        // 3. ★★★ [추가됨] 이미 작업 예약된 타일인지 확인 ★★★
        if (_workOrderManager != null && _workOrderManager.IsTileUnderWork(new Vector3Int(x, y, 0)))
        {
            return false; 
        }
        
        return true;
    }
    
    private void CreateMiningWorkOrder(List<Vector3Int> tiles)
    {
        if (_workOrderManager == null)
        {
            Debug.LogError("[Interaction] WorkOrderManager가 없습니다!");
            return;
        }
    
        // ★ 비주얼과 함께 작업물 생성
        WorkOrderVisual visual = _workOrderManager.CreateWorkOrderWithVisual(
            $"채광 작업 ({tiles.Count}개 타일)",
            WorkType.Mining,
            maxWorkers: defaultMiningWorkers,
            tiles: tiles,
            priority: 3
        );
    
        if (visual != null)
        {
            WorkOrder workOrder = visual.WorkOrder;
        
            // 작업 대상 추가
            List<IWorkTarget> targets = new List<IWorkTarget>();
            foreach (var tile in tiles)
            {
                MiningOrder miningOrder = new MiningOrder
                {
                    position = tile,
                    tileID = _gameMap.TileGrid[tile.x, tile.y],
                    priority = 3,
                    completed = false,
                    assignedWorker = null
                };
                targets.Add(miningOrder);
            }
        
            workOrder.AddTargets(targets);
        
            Debug.Log($"[Interaction] 채광 작업물 생성: {tiles.Count}개 타일");
        }
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
        
        int maxSelection = 50;
        int selectedCount = 0;
        
        foreach (var collider in colliders)
        {
            if (selectedCount >= maxSelection) break;
            
            IHarvestable harvestable = collider.GetComponent<IHarvestable>();
            if (harvestable != null && harvestable.CanHarvest())
            {
                _selectedObjects.Add(collider.gameObject);
                SetObjectHighlight(collider.gameObject, true);
                selectedCount++;
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
    
        WorkType workType = DetermineHarvestWorkType(_selectedObjects[0]);
    
        // ★ 비주얼과 함께 작업물 생성
        // (수확은 타일 위치가 아니라 오브젝트 위치를 사용)
        List<Vector3Int> objectTiles = _selectedObjects
            .Select(obj => new Vector3Int(
                Mathf.FloorToInt(obj.transform.position.x),
                Mathf.FloorToInt(obj.transform.position.y),
                0))
            .ToList();
    
        WorkOrderVisual visual = _workOrderManager.CreateWorkOrderWithVisual(
            $"{GetWorkTypeName(workType)} 작업 ({_selectedObjects.Count}개)",
            workType,
            maxWorkers: defaultHarvestWorkers,
            tiles: objectTiles,
            priority: 4
        );
    
        if (visual != null)
        {
            WorkOrder workOrder = visual.WorkOrder;
        
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
                        completed = false,
                        assignedWorker = null
                    };
                    targets.Add(harvestOrder);
                }
                SetObjectHighlight(obj, false);
            }
        
            workOrder.AddTargets(targets);
        
            Debug.Log($"[Interaction] {GetWorkTypeName(workType)} 작업물 생성: {_selectedObjects.Count}개");
        }
    
        _selectedObjects.Clear();
    }
    
    private WorkType DetermineHarvestWorkType(GameObject obj)
    {
        if (obj.GetComponent<ChoppableTree>() != null)
        {
            return WorkType.Chopping;
        }
        
        if (obj.GetComponent<HarvestablePlant>() != null)
        {
            return WorkType.Gardening;
        }
        
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
            case WorkType.Building: return "건설";
            case WorkType.Crafting: return "제작";
            case WorkType.Research: return "연구";
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
            SetMode(InteractMode.Normal);
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
        
        foreach (var obj in _selectedObjects)
        {
            Building building = obj.GetComponent<Building>();
            if (building != null)
            {
                WorkOrder workOrder = _workOrderManager.CreateWorkOrder(
                    $"철거: {building.buildingData.buildingName}",
                    WorkType.Demolish,
                    maxWorkers: defaultDemolishWorkers,
                    priority: 6
                );
                
                DemolishOrder demolishOrder = new DemolishOrder
                {
                    building = building,
                    position = obj.transform.position,
                    priority = 6,
                    completed = false,
                    assignedWorker = null
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
        
        GameObject dropPrefab = _resourceManager.GetDropPrefab(order.tileID);
        if (dropPrefab != null)
        {
            Vector3 dropPos = groundTilemap.CellToWorld(order.position) + new Vector3(0.5f, 0.5f, 0);
            Instantiate(dropPrefab, dropPos, Quaternion.identity, itemDropParent);
        }
        
        _gameMap.SetTile(x, y, 0);
        _gameMap.UnmarkTileOccupied(x, y);
        _mapRenderer.UpdateTileVisual(x, y);
        
        CheckFoundationSupport(x, y);
        
        order.completed = true;
    }
    
    private void DemolishBuildingInstantly(Building building)
    {
        Vector3Int cellPos = groundTilemap.WorldToCell(building.transform.position);
        
        for (int y = 0; y < building.buildingData.size.y; y++)
        {
            for (int x = 0; x < building.buildingData.size.x; x++)
            {
                _gameMap.UnmarkTileOccupied(cellPos.x + x, cellPos.y + y);
            }
        }
        
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
    
    #region Public API
    
    /// <summary>
    /// 현재 상호작용 모드를 반환합니다.
    /// </summary>
    public InteractMode GetCurrentMode()
    {
        return _currentMode;
    }
    
    #endregion
}