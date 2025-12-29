using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 건설 시스템 매니저
/// 건설 모드 진입/퇴장, 청사진 배치, 건설 현장 관리를 담당합니다.
/// 
/// 저장 위치: Assets/Scripts/Systems/BuildingSystem/Construction/ConstructionManager.cs
/// </summary>
public class ConstructionManager : DestroySingleton<ConstructionManager>
{
    [Header("프리팹")]
    [Tooltip("건설 현장 프리팹 (ConstructionSite 컴포넌트 포함)")]
    [SerializeField] private GameObject constructionSitePrefab;
    
    [Tooltip("배치 미리보기용 고스트 프리팹")]
    [SerializeField] private GameObject placementGhostPrefab;
    
    [Header("건물 데이터베이스")]
    [Tooltip("모든 건물 데이터 목록")]
    [SerializeField] private List<BuildingData> allBuildingData = new List<BuildingData>();
    
    [Header("배치 설정")]
    [SerializeField] private Color validPlacementColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color invalidPlacementColor = new Color(1f, 0.3f, 0.3f, 0.5f);
    
    [Header("건설 현장 관리")]
    [SerializeField] private Transform constructionParent;
    [SerializeField] private List<ConstructionSite> activeConstructionSites = new List<ConstructionSite>();
    
    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = true;
    
    // 배치 모드 상태
    private bool isPlacementMode = false;
    private BuildingData selectedBuildingData;
    private GameObject ghostObject;
    private List<SpriteRenderer> ghostRenderers = new List<SpriteRenderer>();
    private bool isCurrentPlacementValid = false;
    
    // 캐시
    private GameMap gameMap;
    private Dictionary<BuildingCategory, List<BuildingData>> buildingsByCategory;
    
    // 이벤트
    public delegate void PlacementModeDelegate(bool isActive, BuildingData buildingData);
    public event PlacementModeDelegate OnPlacementModeChanged;
    
    public delegate void ConstructionSiteDelegate(ConstructionSite site);
    public event ConstructionSiteDelegate OnConstructionSiteCreated;
    public event ConstructionSiteDelegate OnConstructionSiteCompleted;
    public event ConstructionSiteDelegate OnConstructionSiteCancelled;
    
    protected override void Awake()
    {
        base.Awake();
        
        // 건설 현장 부모 생성
        if (constructionParent == null)
        {
            GameObject parent = new GameObject("ConstructionSites");
            constructionParent = parent.transform;
        }
        
        // 카테고리별 건물 분류
        OrganizeBuildingsByCategory();
    }
    
    void Start()
    {
        if (MapGenerator.instance != null)
        {
            gameMap = MapGenerator.instance.GameMapInstance;
        }
    }
    
    void Update()
    {
        if (isPlacementMode)
        {
            UpdatePlacementGhost();
            HandlePlacementInput();
        }
    }
    
    #region 건물 데이터베이스
    
    private void OrganizeBuildingsByCategory()
    {
        buildingsByCategory = new Dictionary<BuildingCategory, List<BuildingData>>();
        
        foreach (BuildingCategory category in System.Enum.GetValues(typeof(BuildingCategory)))
        {
            buildingsByCategory[category] = new List<BuildingData>();
        }
        
        foreach (var data in allBuildingData)
        {
            if (data != null)
            {
                buildingsByCategory[data.category].Add(data);
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[ConstructionManager] 건물 데이터 분류 완료: 총 {allBuildingData.Count}개");
            foreach (var kvp in buildingsByCategory)
            {
                if (kvp.Value.Count > 0)
                {
                    Debug.Log($"  - {kvp.Key}: {kvp.Value.Count}개");
                }
            }
        }
    }
    
    /// <summary>
    /// 특정 카테고리의 건물 목록을 반환합니다.
    /// </summary>
    public List<BuildingData> GetBuildingsByCategory(BuildingCategory category)
    {
        if (buildingsByCategory.TryGetValue(category, out List<BuildingData> buildings))
        {
            return buildings;
        }
        return new List<BuildingData>();
    }
    
    /// <summary>
    /// 모든 카테고리 목록을 반환합니다.
    /// </summary>
    public List<BuildingCategory> GetAvailableCategories()
    {
        return buildingsByCategory
            .Where(kvp => kvp.Value.Count > 0)
            .Select(kvp => kvp.Key)
            .ToList();
    }
    
    /// <summary>
    /// 건물 ID로 BuildingData를 찾습니다.
    /// </summary>
    public BuildingData GetBuildingDataById(int buildingId)
    {
        return allBuildingData.FirstOrDefault(b => b.buildingID == buildingId);
    }
    
    /// <summary>
    /// 건물 데이터를 추가합니다 (런타임 등록용).
    /// </summary>
    public void RegisterBuildingData(BuildingData data)
    {
        if (data == null || allBuildingData.Contains(data)) return;
        
        allBuildingData.Add(data);
        buildingsByCategory[data.category].Add(data);
        
        if (showDebugInfo)
        {
            Debug.Log($"[ConstructionManager] 건물 등록: {data.buildingName}");
        }
    }
    
    #endregion
    
    #region 배치 모드
    
    /// <summary>
    /// 배치 모드를 시작합니다.
    /// </summary>
    public bool EnterPlacementMode(BuildingData buildingData)
    {
        if (buildingData == null)
        {
            Debug.LogWarning("[ConstructionManager] BuildingData가 null입니다.");
            return false;
        }
        
        // 자원 확인
        if (!HasRequiredResources(buildingData))
        {
            Debug.LogWarning($"[ConstructionManager] 자원 부족: {buildingData.buildingName}");
            LogMissingResources(buildingData);
            return false;
        }
        
        // 기존 배치 모드 종료
        if (isPlacementMode)
        {
            ExitPlacementMode();
        }
        
        selectedBuildingData = buildingData;
        isPlacementMode = true;
        
        // 고스트 생성
        CreatePlacementGhost();
        
        OnPlacementModeChanged?.Invoke(true, buildingData);
        
        if (showDebugInfo)
        {
            Debug.Log($"[ConstructionManager] 배치 모드 시작: {buildingData.buildingName}");
        }
        
        return true;
    }
    
    /// <summary>
    /// 배치 모드를 종료합니다.
    /// </summary>
    public void ExitPlacementMode()
    {
        if (!isPlacementMode) return;
        
        isPlacementMode = false;
        selectedBuildingData = null;
        
        // 고스트 제거
        DestroyPlacementGhost();
        
        OnPlacementModeChanged?.Invoke(false, null);
        
        if (showDebugInfo)
        {
            Debug.Log("[ConstructionManager] 배치 모드 종료");
        }
    }
    
    private void CreatePlacementGhost()
    {
        if (selectedBuildingData == null) return;
        
        ghostRenderers.Clear();
        
        // 고스트 부모 오브젝트 생성
        ghostObject = new GameObject("PlacementGhost");
        
        // 건물 크기만큼 타일 고스트 생성
        for (int x = 0; x < selectedBuildingData.size.x; x++)
        {
            for (int y = 0; y < selectedBuildingData.size.y; y++)
            {
                GameObject tile;
                
                if (placementGhostPrefab != null)
                {
                    tile = Instantiate(placementGhostPrefab, ghostObject.transform);
                }
                else
                {
                    // 기본 스프라이트 생성
                    tile = new GameObject($"GhostTile_{x}_{y}");
                    tile.transform.SetParent(ghostObject.transform);
                    SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
                    sr.sprite = CreateDefaultSprite();
                    sr.sortingOrder = 100;
                }
                
                tile.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0);
                
                SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    ghostRenderers.Add(renderer);
                }
            }
        }
        
        // 건물 프리팹의 스프라이트도 표시 (있다면)
        if (selectedBuildingData.buildingPrefab != null)
        {
            SpriteRenderer prefabRenderer = selectedBuildingData.buildingPrefab.GetComponent<SpriteRenderer>();
            if (prefabRenderer != null && prefabRenderer.sprite != null)
            {
                GameObject buildingGhost = new GameObject("BuildingGhost");
                buildingGhost.transform.SetParent(ghostObject.transform);
                SpriteRenderer sr = buildingGhost.AddComponent<SpriteRenderer>();
                sr.sprite = prefabRenderer.sprite;
                sr.sortingOrder = 101;
                sr.color = new Color(1f, 1f, 1f, 0.5f);
                
                // 스프라이트 위치 조정 (피벗에 따라)
                buildingGhost.transform.localPosition = Vector3.zero;
            }
        }
    }
    
    private Sprite CreateDefaultSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }
    
    private void DestroyPlacementGhost()
    {
        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }
        ghostRenderers.Clear();
    }
    
    private void UpdatePlacementGhost()
    {
        if (ghostObject == null || selectedBuildingData == null) return;
        
        // 마우스 위치를 그리드 좌표로 변환
        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector3Int gridPos = new Vector3Int(
            Mathf.FloorToInt(mouseWorld.x),
            Mathf.FloorToInt(mouseWorld.y),
            0
        );
        
        // 고스트 위치 업데이트
        ghostObject.transform.position = new Vector3(gridPos.x, gridPos.y, 0);
        
        // 배치 가능 여부 확인
        isCurrentPlacementValid = CanPlaceAt(gridPos);
        
        // 색상 업데이트
        Color color = isCurrentPlacementValid ? validPlacementColor : invalidPlacementColor;
        foreach (var renderer in ghostRenderers)
        {
            if (renderer != null)
            {
                renderer.color = color;
            }
        }
        
        // 건물 고스트 색상도 업데이트
        if (ghostObject.transform.childCount > 0)
        {
            Transform buildingGhost = ghostObject.transform.Find("BuildingGhost");
            if (buildingGhost != null)
            {
                SpriteRenderer sr = buildingGhost.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(color.r, color.g, color.b, 0.5f);
                }
            }
        }
    }
    
    private void HandlePlacementInput()
    {
        // 좌클릭: 배치
        if (Input.GetMouseButtonDown(0))
        {
            if (isCurrentPlacementValid)
            {
                Vector3 mouseWorld = GetMouseWorldPosition();
                Vector3Int gridPos = new Vector3Int(
                    Mathf.FloorToInt(mouseWorld.x),
                    Mathf.FloorToInt(mouseWorld.y),
                    0
                );
                
                PlaceConstruction(gridPos);
            }
            else
            {
                Debug.LogWarning("[ConstructionManager] 이 위치에 건설할 수 없습니다.");
            }
        }
        
        // 우클릭: 취소
        if (Input.GetMouseButtonDown(1))
        {
            ExitPlacementMode();
        }
        
        // ESC: 취소
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitPlacementMode();
        }
    }
    
    #endregion
    
    #region 배치 검증
    
    /// <summary>
    /// 해당 위치에 건물을 배치할 수 있는지 확인합니다.
    /// </summary>
    public bool CanPlaceAt(Vector3Int gridPos)
    {
        if (selectedBuildingData == null || gameMap == null) return false;
        
        // 모든 타일 검사
        for (int x = 0; x < selectedBuildingData.size.x; x++)
        {
            for (int y = 0; y < selectedBuildingData.size.y; y++)
            {
                int checkX = gridPos.x + x;
                int checkY = gridPos.y + y;
                
                // 맵 범위 확인
                if (checkX < 0 || checkX >= GameMap.MAP_WIDTH ||
                    checkY < 0 || checkY >= GameMap.MAP_HEIGHT)
                {
                    return false;
                }
                
                // 공간이 비어있는지 확인 (공기 타일 + 점유되지 않음)
                if (!gameMap.IsSpaceAvailable(checkX, checkY))
                {
                    return false;
                }
            }
        }
        
        // 바닥 확인 (y-1 위치에 고체 타일이 있어야 함)
        for (int x = 0; x < selectedBuildingData.size.x; x++)
        {
            int checkX = gridPos.x + x;
            int groundY = gridPos.y - 1;
            
            if (groundY < 0 || !gameMap.IsSolidGround(checkX, groundY))
            {
                return false;
            }
        }
        
        // TODO: 추후 특수 조건 추가 (물 위, 특정 지형 등)
        
        return true;
    }
    
    /// <summary>
    /// 필요한 자원이 있는지 확인합니다.
    /// </summary>
    public bool HasRequiredResources(BuildingData buildingData)
    {
        if (buildingData.requiredResources == null || buildingData.requiredResources.Count == 0)
        {
            return true;
        }
        
        if (InventoryManager.instance == null)
        {
            Debug.LogError("[ConstructionManager] InventoryManager가 없습니다.");
            return false;
        }
        
        return InventoryManager.instance.HasItems(buildingData.requiredResources);
    }
    
    private void LogMissingResources(BuildingData buildingData)
    {
        if (InventoryManager.instance == null || buildingData.requiredResources == null) return;
        
        foreach (var cost in buildingData.requiredResources)
        {
            int current = InventoryManager.instance.GetItemCount(cost.item);
            if (current < cost.amount)
            {
                Debug.LogWarning($"  - {cost.item.itemName}: {current}/{cost.amount}");
            }
        }
    }
    
    #endregion
    
    #region 건설 현장 생성
    
    /// <summary>
    /// 건설 현장을 배치합니다.
    /// </summary>
    private void PlaceConstruction(Vector3Int gridPos)
    {
        if (selectedBuildingData == null) return;
        
        // 자원 소모
        if (!ConsumeResources(selectedBuildingData))
        {
            Debug.LogWarning("[ConstructionManager] 자원 소모 실패");
            return;
        }
        
        // 건설 현장 생성
        ConstructionSite site = CreateConstructionSite(selectedBuildingData, gridPos);
        
        if (site != null)
        {
            activeConstructionSites.Add(site);
            OnConstructionSiteCreated?.Invoke(site);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ConstructionManager] 건설 현장 배치: {selectedBuildingData.buildingName} at {gridPos}");
            }
        }
        
        // 배치 모드 종료 (또는 연속 배치를 원하면 유지)
        ExitPlacementMode();
    }
    
    private bool ConsumeResources(BuildingData buildingData)
    {
        if (InventoryManager.instance == null) return false;
        
        if (buildingData.requiredResources == null || buildingData.requiredResources.Count == 0)
        {
            return true;
        }
        
        return InventoryManager.instance.RemoveItems(buildingData.requiredResources);
    }
    
    private ConstructionSite CreateConstructionSite(BuildingData buildingData, Vector3Int gridPos)
    {
        GameObject siteObj;
        
        if (constructionSitePrefab != null)
        {
            siteObj = Instantiate(constructionSitePrefab, constructionParent);
        }
        else
        {
            // 기본 오브젝트 생성
            siteObj = new GameObject("ConstructionSite");
            siteObj.transform.SetParent(constructionParent);
            siteObj.AddComponent<SpriteRenderer>();
            siteObj.AddComponent<BoxCollider2D>();
            siteObj.AddComponent<ConstructionSite>();
        }
        
        siteObj.transform.position = new Vector3(gridPos.x, gridPos.y, 0);
        
        ConstructionSite site = siteObj.GetComponent<ConstructionSite>();
        if (site != null)
        {
            site.Initialize(buildingData, gridPos);
        }
        
        return site;
    }
    
    #endregion
    
    #region 건설 현장 관리
    
    /// <summary>
    /// 건설 현장을 취소합니다.
    /// </summary>
    public void CancelConstruction(ConstructionSite site)
    {
        if (site == null) return;
        
        activeConstructionSites.Remove(site);
        OnConstructionSiteCancelled?.Invoke(site);
        
        site.CancelConstruction();
    }
    
    /// <summary>
    /// 건설이 완료되었을 때 호출됩니다.
    /// </summary>
    public void OnConstructionCompleted(ConstructionSite site)
    {
        if (site == null) return;
        
        activeConstructionSites.Remove(site);
        OnConstructionSiteCompleted?.Invoke(site);
    }
    
    /// <summary>
    /// 특정 위치의 건설 현장을 찾습니다.
    /// </summary>
    public ConstructionSite GetConstructionSiteAt(Vector3Int gridPos)
    {
        return activeConstructionSites.FirstOrDefault(s => s.GridPosition == gridPos);
    }
    
    #endregion
    
    #region 유틸리티
    
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -Camera.main.transform.position.z;
        return Camera.main.ScreenToWorldPoint(mousePos);
    }
    
    #endregion
    
    #region Public 프로퍼티
    
    public bool IsPlacementMode => isPlacementMode;
    public BuildingData SelectedBuildingData => selectedBuildingData;
    public List<ConstructionSite> ActiveConstructionSites => activeConstructionSites;
    public int ActiveConstructionCount => activeConstructionSites.Count;
    
    #endregion
}