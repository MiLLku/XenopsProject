using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using System.Linq;

public class InteractionManager : DestroySingleton<InteractionManager> 
{
    // 상호작용 모드 정의
    public enum InteractMode
    {
        Normal,     // 일반 모드 (직원, 건물, 작업물 클릭)
        Mine,       // 채광 모드 (드래그로 영역 지정 -> Q로 확정)
        Harvest,
        Build,
        Demolish
    }

    [Header("필수 연결 (씬)")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Transform itemDropParent;
    
    [Header("프리팹 연결")]
    [SerializeField] private GameObject selectionBoxPrefab;
    [Tooltip("작업 외곽선을 표시할 프리팹 (WorkOrderVisual 스크립트 포함)")]
    [SerializeField] private GameObject workOrderVisualPrefab; 
    
    [Header("드래그 선택 색상")]
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
    private TileHighlighter _tileHighlighter;
    
    // 모드 관리
    private InteractMode _currentMode = InteractMode.Normal;
    
    // ★ 예약(Pending) 시스템 변수 (채광용)
    private HashSet<Vector3Int> _pendingMiningTiles = new HashSet<Vector3Int>();
    private WorkOrderVisual _pendingVisual; 
    private GameObject _pendingVisualObject; 

    // 건설 모드 변수
    private BuildingData _buildingToBuild;
    private GameObject _ghostParent;
    private List<SpriteRenderer> _ghostSprites = new List<SpriteRenderer>();
    private bool _isPlacementValid = false;
    
    // 드래그 선택 변수
    private bool _isDragging = false;
    private Vector3 _dragStartPos;
    private Vector3 _dragEndPos;
    private GameObject _selectionBox;
    private List<GameObject> _selectedObjects = new List<GameObject>();
    
    // Normal 모드 호버링
    private GameObject _hoveredObject;
    private Color _originalColor;
    private SpriteRenderer _hoveredRenderer;
    
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
        _tileHighlighter = TileHighlighter.instance; // (이제 사용 비중이 줄었으나 호환성을 위해 유지)
        
        if (_workOrderManager == null) Debug.LogError("WorkOrderManager를 찾을 수 없습니다!");
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
        
        if (_isDragging && _selectionBox != null && _currentMode != InteractMode.Mine)
        {
            // Mine 모드에서는 자체 Visual을 쓰므로 기본 박스는 숨기거나 다른 용도로 사용
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
    
    // ★★★ 키 입력 처리 로직 수정 (Q 토글 적용) ★★★
    private void HandleModeHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
        {
            SetMode(InteractMode.Normal);
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            // Mine 모드이면 확정하고 나감, 아니면 진입
            if (_currentMode == InteractMode.Mine)
            {
                ConfirmMiningSelection();
            }
            else
            {
                SetMode(InteractMode.Mine);
            }
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            SetMode(InteractMode.Harvest);
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            if (_currentMode == InteractMode.Build)
                SetMode(InteractMode.Normal);
            else
                EnterBuildMode(testBuildingToBuild);
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
    
        // 모드 진입 시 초기화
        if (mode == InteractMode.Mine)
        {
            PrepareMiningPendingVisual();
        }
    
        Debug.Log($"[Interaction] 모드 변경: {mode}");
        OnModeChanged?.Invoke(mode);
    
        UpdateSelectionBoxColor();
    }
    
    private void ExitCurrentMode()
    {
        CancelDrag();
        
        // 채광 모드를 나갈 때, 확정되지 않은 예약 내역은 취소(초기화)
        // 단, ConfirmMiningSelection을 통해 나가는 경우는 이미 작업이 생성된 후임
        if (_currentMode == InteractMode.Mine)
        {
            ClearMiningPending();
        }
        else if (_currentMode == InteractMode.Build)
        {
            ExitBuildMode();
        }
    }
    
    private void UpdateSelectionBoxColor()
    {
        if (_selectionBox == null) return;
        SpriteRenderer sr = _selectionBox.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        
        switch (_currentMode)
        {
            case InteractMode.Mine: sr.color = miningSelectionColor; break;
            case InteractMode.Harvest: sr.color = harvestSelectionColor; break;
            case InteractMode.Demolish: sr.color = demolishSelectionColor; break;
            default: sr.color = new Color(1, 1, 1, 0.3f); break;
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

        // 클릭 처리 - UI 위에 있는지 확인
        if (Input.GetMouseButtonDown(0))
        {
            // UI 버튼이나 인터랙티브한 UI 위에 있으면 무시
            if (IsPointerOverInteractiveUI())
            {
                return;
            }

            if (hit.collider != null)
            {
                HandleNormalModeClick(hit.collider.gameObject);
            }
        }
    }

    /// <summary>
    /// 마우스가 클릭 가능한 UI 위에 있는지 확인
    /// </summary>
    private bool IsPointerOverInteractiveUI()
    {
        if (EventSystem.current == null) return false;

        var eventData = new UnityEngine.EventSystems.PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        // Button, Toggle, Slider 등 실제로 클릭 가능한 UI 컴포넌트만 확인
        foreach (var result in results)
        {
            // Selectable 컴포넌트(Button, Toggle, Slider 등)가 있으면 차단
            if (result.gameObject.GetComponent<UnityEngine.UI.Selectable>() != null)
            {
                return true;
            }
        }

        return false; // 일반 Panel, Text, Image는 통과 (작업 더미 클릭 가능)
    }
    
    private void HandleNormalModeClick(GameObject clickedObject)
    {
        // 0. 작업 더미(Visual) 클릭 처리 (UI 오픈용)
        WorkOrderVisual workVisual = clickedObject.GetComponent<WorkOrderVisual>();
        if (workVisual != null)
        {
            workVisual.OnClicked();
            return;
        }

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
        
        // 3. 작업대 등 기타 상호작용...
        CraftingTable craftingTable = clickedObject.GetComponent<CraftingTable>();
        if (craftingTable != null) return; // 작업대는 자체 클릭 처리
        
        IHarvestable harvestable = clickedObject.GetComponent<IHarvestable>();
        if (harvestable != null)
        {
            OnHarvestableClicked(harvestable, clickedObject);
            return;
        }
    }
    
    private void SetHover(GameObject obj)
    {
        _hoveredObject = obj;
        _hoveredRenderer = obj.GetComponent<SpriteRenderer>();
        if (_hoveredRenderer != null)
        {
            _originalColor = _hoveredRenderer.color;
            _hoveredRenderer.color = new Color(_originalColor.r * 1.2f, _originalColor.g * 1.2f, _originalColor.b * 1.2f, _originalColor.a);
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
    
    private void OnEmployeeClicked(Employee employee) { /* UI 표시 로직 */ }
    private void OnBuildingClicked(Building building) { /* UI 표시 로직 */ }
    private void OnHarvestableClicked(IHarvestable harvestable, GameObject obj) { /* 정보 표시 */ }
    
    #endregion

    #region 채광 모드 (예약 시스템 적용)
    
    private void PrepareMiningPendingVisual()
    {
        // 예약용 비주얼 생성 (기존 프리팹 활용)
        if (_pendingVisualObject == null && workOrderVisualPrefab != null)
        {
            _pendingVisualObject = Instantiate(workOrderVisualPrefab);
            _pendingVisualObject.name = "Pending Mining Selection";
            _pendingVisual = _pendingVisualObject.GetComponent<WorkOrderVisual>();
        }
        
        if (_pendingVisualObject != null) _pendingVisualObject.SetActive(true);
        _pendingMiningTiles.Clear();
    }

    private void ClearMiningPending()
    {
        _pendingMiningTiles.Clear();
        if (_pendingVisualObject != null)
        {
            Destroy(_pendingVisualObject); // 비주얼 삭제
            _pendingVisualObject = null;
            _pendingVisual = null;
        }
    }

    private void HandleMineMode()
    {
        // 드래그 시작 (좌클릭: 추가, 우클릭: 제거)
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            _dragStartPos = _cameraController.GetMouseWorldPosition();
            _isDragging = true;
        }
        
        // 드래그 중 위치 업데이트
        if (_isDragging)
        {
            _dragEndPos = _cameraController.GetMouseWorldPosition();
            // 선택 박스 표시 (선택사항)
            UpdateSelectionBox();
        }
        
        // 드래그 끝 (마우스 뗌) -> ★작업 생성이 아니라 '예약 목록'에만 추가/제거★
        if ((Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) && _isDragging)
        {
            bool isAdding = Input.GetMouseButtonUp(0); 
            ModifyPendingTiles(isAdding);
            _isDragging = false;
            if (_selectionBox != null) _selectionBox.SetActive(false);
        }
    }

    private void ModifyPendingTiles(bool isAdding)
    {
        Vector3Int startCell = groundTilemap.WorldToCell(_dragStartPos);
        Vector3Int endCell = groundTilemap.WorldToCell(_dragEndPos);
    
        int minX = Mathf.Min(startCell.x, endCell.x);
        int maxX = Mathf.Max(startCell.x, endCell.x);
        int minY = Mathf.Min(startCell.y, endCell.y);
        int maxY = Mathf.Max(startCell.y, endCell.y);
        
        List<Vector3Int> currentSelection = new List<Vector3Int>();

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                // 추가할 때: 채광 가능한 타일인지 확인
                if (isAdding)
                {
                    if (CanMineTile(x, y)) 
                        currentSelection.Add(new Vector3Int(x, y, 0));
                }
                else
                {
                    // 제거할 때: 범위 내 타일이면 리스트에 넣음
                    currentSelection.Add(new Vector3Int(x, y, 0));
                }
            }
        }

        // HashSet 연산
        if (isAdding)
        {
            _pendingMiningTiles.UnionWith(currentSelection); // 합집합
        }
        else
        {
            _pendingMiningTiles.ExceptWith(currentSelection); // 차집합
        }

        // 비주얼 실시간 업데이트
        if (_pendingVisual != null)
        {
            _pendingVisual.UpdateTiles(_pendingMiningTiles.ToList());
        }
    }

    /// <summary>
    /// [Q] 키를 눌렀을 때 호출: 예약된 모든 타일을 '하나의 작업'으로 확정
    /// </summary>
    private void ConfirmMiningSelection()
    {
        if (_pendingMiningTiles.Count > 0)
        {
            // ★ 여기서 실제 작업 생성 (한 번만 호출됨)
            CreateMiningWorkOrder(_pendingMiningTiles.ToList());
            Debug.Log($"[Interaction] 채광 작업 확정: {_pendingMiningTiles.Count}개 타일");
        }
        
        // 일반 모드로 복귀 (이 과정에서 ClearMiningPending이 호출되어 임시 비주얼은 삭제됨)
        SetMode(InteractMode.Normal);
    }

    private bool CanMineTile(int x, int y)
    {
        if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT) return false;
        
        int tileID = _gameMap.TileGrid[x, y];
        // 0: 공기, 7: 이미 가공된 바닥(또는 다른 식별자)은 채광 불가
        if (tileID == 0 || tileID == 7) return false;

        // 이미 다른 작업에 포함된 타일인지 확인 (중복 생성 방지)
        if (_workOrderManager != null && _workOrderManager.IsTileUnderWork(new Vector3Int(x, y, 0)))
        {
            return false; 
        }
        
        return true;
    }
    
    private void CreateMiningWorkOrder(List<Vector3Int> tiles)
    {
        if (_workOrderManager == null) return;
    
        // 비주얼 생성 및 작업 등록
        WorkOrderVisual visual = _workOrderManager.CreateWorkOrderWithVisual(
            $"채광 작업 ({tiles.Count}개)",
            WorkType.Mining,
            maxWorkers: defaultMiningWorkers,
            tiles: tiles,
            priority: 3
        );
    
        // 데이터 연결
        if (visual != null)
        {
            WorkOrder workOrder = visual.WorkOrder;
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
        }
    }
    
    #endregion

    #region 수확 모드
    
    private void HandleHarvestMode()
    {
        // ... (기존 코드와 동일하게 드래그로 선택) ...
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
            FinishHarvestSelection(); // 수확은 아직 즉시 생성 방식 (필요시 변경 가능)
            CancelDrag();
        }
        
        if (Input.GetMouseButtonDown(1)) CancelDrag();
    }
    
    private void UpdateHarvestSelection()
    {
        Bounds selectionBounds = GetSelectionBounds();
        foreach (var obj in _selectedObjects) if (obj != null) SetObjectHighlight(obj, false);
        _selectedObjects.Clear();
        
        Collider2D[] colliders = Physics2D.OverlapBoxAll(selectionBounds.center, selectionBounds.size, 0f);
        int maxSelection = 50;
        int count = 0;
        
        foreach (var collider in colliders)
        {
            if (count >= maxSelection) break;
            IHarvestable harvestable = collider.GetComponent<IHarvestable>();
            if (harvestable != null && harvestable.CanHarvest())
            {
                _selectedObjects.Add(collider.gameObject);
                SetObjectHighlight(collider.gameObject, true);
                count++;
            }
        }
    }
    
    private void FinishHarvestSelection()
    {
        if (_selectedObjects.Count == 0) return;
        // ... (기존 수확 작업 생성 로직 유지) ...
        WorkType workType = DetermineHarvestWorkType(_selectedObjects[0]);
        
        List<Vector3Int> objectTiles = _selectedObjects.Select(obj => new Vector3Int(Mathf.FloorToInt(obj.transform.position.x), Mathf.FloorToInt(obj.transform.position.y), 0)).ToList();
        
        WorkOrderVisual visual = _workOrderManager.CreateWorkOrderWithVisual(
            $"{GetWorkTypeName(workType)} 작업 ({_selectedObjects.Count}개)", workType, defaultHarvestWorkers, objectTiles, 4);
            
        if (visual != null)
        {
            WorkOrder workOrder = visual.WorkOrder;
            List<IWorkTarget> targets = new List<IWorkTarget>();
            foreach (var obj in _selectedObjects)
            {
                IHarvestable harvestable = obj.GetComponent<IHarvestable>();
                if (harvestable != null)
                {
                    targets.Add(new HarvestOrder { target = harvestable, position = obj.transform.position, priority = 4 });
                }
                SetObjectHighlight(obj, false);
            }
            workOrder.AddTargets(targets);
        }
        _selectedObjects.Clear();
    }
    
    private WorkType DetermineHarvestWorkType(GameObject obj)
    {
        if (obj.GetComponent<ChoppableTree>() != null) return WorkType.Chopping;
        return WorkType.Gardening;
    }
    
    private string GetWorkTypeName(WorkType type)
    {
        switch (type) { case WorkType.Chopping: return "벌목"; case WorkType.Mining: return "채광"; default: return "작업"; }
    }
    
    private void SetObjectHighlight(GameObject obj, bool highlight)
    {
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = highlight ? new Color(1.5f, 1.5f, 1.5f) : Color.white;
    }
    
    #endregion

    #region 건설 모드
    
    public void EnterBuildMode(BuildingData buildingData)
    {
        if (buildingData == null || placementGhostPrefab == null) return;

        ExitCurrentMode();
        _currentMode = InteractMode.Build;
        _buildingToBuild = buildingData;
        _ghostSprites.Clear();
        
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
        if (_ghostParent != null) Destroy(_ghostParent);
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
        foreach (var renderer in _ghostSprites) renderer.color = color;
        
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
                if (!_gameMap.IsSpaceAvailable(checkX, checkY)) return false;
                if (y == 0 && !_gameMap.IsSolidGround(checkX, checkY - 1)) return false;
            }
        }
        return true;
    }

    private void PlaceBuilding(Vector3Int gridBasePos)
    {
        if (InventoryManager.instance == null) return;
        var requiredRes = _buildingToBuild.requiredResources;
        if (!InventoryManager.instance.HasItems(requiredRes)) return;

        InventoryManager.instance.RemoveItems(requiredRes);
        Vector3 worldPos = groundTilemap.CellToWorld(gridBasePos);
        GameObject buildingObj = Instantiate(_buildingToBuild.buildingPrefab, worldPos, Quaternion.identity, _mapRenderer.entityParent);
        Building buildingScript = buildingObj.GetComponent<Building>();
        if(buildingScript != null) buildingScript.Initialize(_buildingToBuild);

        for (int y = 0; y < _buildingToBuild.size.y; y++)
        {
            for (int x = 0; x < _buildingToBuild.size.x; x++)
            {
                _gameMap.MarkTileOccupied(gridBasePos.x + x, gridBasePos.y + y);
            }
        }
    }
    
    #endregion

    #region 철거 모드
    private void HandleDemolishMode()
    {
        // (생략 - 기존 코드와 동일한 드래그 로직 사용)
        // 필요시 채광과 비슷하게 변경 가능하지만, 현재는 즉시 철거 명령 생성 유지
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
        if (Input.GetMouseButtonDown(1)) CancelDrag();
    }
    
    private void UpdateDemolishSelection()
    {
        Bounds selectionBounds = GetSelectionBounds();
        foreach (var obj in _selectedObjects) if (obj != null) SetObjectHighlight(obj, false);
        _selectedObjects.Clear();
        
        Collider2D[] colliders = Physics2D.OverlapBoxAll(selectionBounds.center, selectionBounds.size, 0f);
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
        if (_workOrderManager == null) return;
        
        foreach (var obj in _selectedObjects)
        {
            Building building = obj.GetComponent<Building>();
            if (building != null)
            {
                WorkOrder workOrder = _workOrderManager.CreateWorkOrder($"철거: {building.buildingData.buildingName}", WorkType.Demolish, defaultDemolishWorkers, 6);
                workOrder.AddTarget(new DemolishOrder { building = building, position = obj.transform.position, priority = 6 });
            }
            SetObjectHighlight(obj, false);
        }
        _selectedObjects.Clear();
    }
    #endregion

    #region 공통 유틸
    private void UpdateSelectionBox()
    {
        if (_selectionBox == null) return;
        _selectionBox.SetActive(true);
        Vector3 currentMousePos = _cameraController.GetMouseWorldPosition();
        Vector3 center = (_dragStartPos + currentMousePos) / 2f;
        Vector3 size = new Vector3(Mathf.Abs(currentMousePos.x - _dragStartPos.x), Mathf.Abs(currentMousePos.y - _dragStartPos.y), 1f);
        _selectionBox.transform.position = center;
        _selectionBox.transform.localScale = size;
    }
    
    private Bounds GetSelectionBounds()
    {
        Vector3 center = (_dragStartPos + _dragEndPos) / 2f;
        Vector3 size = new Vector3(Mathf.Abs(_dragEndPos.x - _dragStartPos.x), Mathf.Abs(_dragEndPos.y - _dragStartPos.y), 1f);
        return new Bounds(center, size);
    }
    
    private void CancelDrag()
    {
        _isDragging = false;
        if (_selectionBox != null) _selectionBox.SetActive(false);
        foreach (var obj in _selectedObjects) if (obj != null) SetObjectHighlight(obj, false);
        _selectedObjects.Clear();
    }
    
    private void ExecuteAllOrdersInstantly()
    {
        // 치트 기능 (기존 유지)
        if (_workOrderManager == null) return;
        var allOrders = _workOrderManager.AllOrders.ToList();
        foreach (var workOrder in allOrders)
        {
            foreach (var target in workOrder.targets.ToList())
            {
                if (target is MiningOrder miningOrder)
                {
                    int x = miningOrder.position.x;
                    int y = miningOrder.position.y;
                    GameObject dropPrefab = _resourceManager.GetDropPrefab(miningOrder.tileID);
                    if (dropPrefab != null) Instantiate(dropPrefab, groundTilemap.CellToWorld(miningOrder.position) + new Vector3(0.5f, 0.5f, 0), Quaternion.identity, itemDropParent);
                    _gameMap.SetTile(x, y, 0);
                    _gameMap.UnmarkTileOccupied(x, y);
                    _mapRenderer.UpdateTileVisual(x, y);
                }
                // 다른 작업 타입 즉시 완료 처리...
                workOrder.CompleteTarget(target, null);
            }
            _workOrderManager.RemoveWorkOrder(workOrder);
        }
    }
    #endregion
    
    public InteractMode GetCurrentMode() => _currentMode;
}