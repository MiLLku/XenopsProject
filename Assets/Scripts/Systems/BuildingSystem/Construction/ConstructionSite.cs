using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 건설 현장 (청사진)
/// 배치된 후 직원이 작업을 완료하면 실제 건물로 변환됩니다.
/// 
/// 저장 위치: Assets/Scripts/Systems/BuildingSystem/Construction/ConstructionSite.cs
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class ConstructionSite : MonoBehaviour
{
    [Header("건설 정보")]
    [SerializeField] private BuildingData buildingData;
    [SerializeField] private Vector3Int gridPosition; // 왼쪽 아래 기준
    
    [Header("상태")]
    [SerializeField] private ConstructionState state = ConstructionState.Blueprint;
    [SerializeField] private float constructionProgress = 0f;
    
    [Header("시각 설정")]
    [SerializeField] private Color blueprintColor = new Color(0.5f, 0.8f, 1f, 0.5f);
    [SerializeField] private Color inProgressColor = new Color(1f, 0.9f, 0.5f, 0.7f);
    
    // 컴포넌트
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;
    
    // 작업 관련
    private WorkOrder workOrder;
    private BuildOrder buildOrder;
    
    // 상태
    public bool IsCompleted => state == ConstructionState.Completed;
    public BuildingData BuildingData => buildingData;
    public Vector3Int GridPosition => gridPosition;
    public ConstructionState State => state;
    public float Progress => constructionProgress;
    public WorkOrder WorkOrder => workOrder;
    
    public enum ConstructionState
    {
        Blueprint,      // 청사진 (배치됨, 작업 대기)
        InProgress,     // 건설 중
        Completed       // 완료됨
    }
    
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();
    }
    
    /// <summary>
    /// 건설 현장을 초기화합니다.
    /// </summary>
    public void Initialize(BuildingData data, Vector3Int gridPos)
    {
        buildingData = data;
        gridPosition = gridPos;
        state = ConstructionState.Blueprint;
        constructionProgress = 0f;
        
        // 이름 설정
        gameObject.name = $"ConstructionSite_{data.buildingName}_{gridPos.x}_{gridPos.y}";
        
        // 스프라이트 설정 (완성 건물의 스프라이트를 반투명하게 사용)
        SetupVisuals();
        
        // 콜라이더 설정
        SetupCollider();
        
        // 타일 점유
        OccupyTiles();
        
        // 작업 생성
        CreateWorkOrder();
        
        Debug.Log($"[ConstructionSite] 건설 현장 생성: {data.buildingName} at {gridPos}");
    }
    
    private void SetupVisuals()
    {
        if (buildingData.buildingPrefab != null)
        {
            // 프리팹에서 스프라이트 가져오기
            SpriteRenderer prefabRenderer = buildingData.buildingPrefab.GetComponent<SpriteRenderer>();
            if (prefabRenderer != null)
            {
                spriteRenderer.sprite = prefabRenderer.sprite;
            }
        }
        
        // 청사진 색상 적용
        spriteRenderer.color = blueprintColor;
        spriteRenderer.sortingOrder = 5; // 다른 오브젝트 위에 표시
    }
    
    private void SetupCollider()
    {
        // 건물 크기에 맞게 콜라이더 설정
        Vector2 size = new Vector2(buildingData.size.x, buildingData.size.y);
        boxCollider.size = size;
        boxCollider.offset = new Vector2(size.x / 2f, size.y / 2f); // 피벗이 왼쪽 아래이므로
        boxCollider.isTrigger = true; // 클릭 감지용
    }
    
    private void OccupyTiles()
    {
        if (MapGenerator.instance == null) return;
        
        GameMap gameMap = MapGenerator.instance.GameMapInstance;
        
        for (int x = 0; x < buildingData.size.x; x++)
        {
            for (int y = 0; y < buildingData.size.y; y++)
            {
                int tileX = gridPosition.x + x;
                int tileY = gridPosition.y + y;
                gameMap.MarkTileOccupied(tileX, tileY);
            }
        }
    }
    
    private void ReleaseTiles()
    {
        if (MapGenerator.instance == null) return;
        
        GameMap gameMap = MapGenerator.instance.GameMapInstance;
        
        for (int x = 0; x < buildingData.size.x; x++)
        {
            for (int y = 0; y < buildingData.size.y; y++)
            {
                int tileX = gridPosition.x + x;
                int tileY = gridPosition.y + y;
                gameMap.UnmarkTileOccupied(tileX, tileY);
            }
        }
    }
    
    private void CreateWorkOrder()
    {
        if (WorkSystemManager.instance == null)
        {
            Debug.LogError("[ConstructionSite] WorkSystemManager가 없습니다!");
            return;
        }
        
        // BuildOrder 생성
        buildOrder = new BuildOrder
        {
            constructionSite = this,
            buildingData = buildingData,
            position = GetWorkPosition(),
            priority = 5,
            completed = false
        };
        
        // WorkOrder 생성
        workOrder = WorkSystemManager.instance.CreateWorkOrder(
            $"건설: {buildingData.buildingName}",
            WorkType.Building,
            maxWorkers: 1, // 건설은 한 명만
            priority: 5
        );
        
        // BuildOrder를 WorkOrder에 추가
        workOrder.AddTarget(buildOrder);
        
        Debug.Log($"[ConstructionSite] 작업물 생성 완료: {workOrder.orderName}");
    }
    
    /// <summary>
    /// 작업 위치를 반환합니다 (건물 앞쪽).
    /// </summary>
    public Vector3 GetWorkPosition()
    {
        // 건물 왼쪽 아래 앞에서 작업
        return new Vector3(gridPosition.x + 0.5f, gridPosition.y, 0);
    }
    
    /// <summary>
    /// 건설 작업이 시작될 때 호출됩니다.
    /// </summary>
    public void StartConstruction()
    {
        if (state != ConstructionState.Blueprint) return;
        
        state = ConstructionState.InProgress;
        spriteRenderer.color = inProgressColor;
        
        Debug.Log($"[ConstructionSite] 건설 시작: {buildingData.buildingName}");
    }
    
    /// <summary>
    /// 건설이 완료될 때 호출됩니다.
    /// </summary>
    public void CompleteConstruction()
    {
        if (state == ConstructionState.Completed) return;
        
        state = ConstructionState.Completed;
        constructionProgress = 1f;
        
        Debug.Log($"[ConstructionSite] 건설 완료, 실제 건물 생성: {buildingData.buildingName}");
        
        // 실제 건물 생성
        SpawnBuilding();
        
        // ConstructionManager에 완료 알림
        if (ConstructionManager.instance != null)
        {
            ConstructionManager.instance.OnConstructionCompleted(this);
        }
        
        // 건설 현장 제거
        Destroy(gameObject);
    }
    
    private void SpawnBuilding()
    {
        if (buildingData.buildingPrefab == null)
        {
            Debug.LogError($"[ConstructionSite] buildingPrefab이 없습니다: {buildingData.buildingName}");
            return;
        }
        
        // 실제 건물 생성
        Vector3 worldPos = new Vector3(gridPosition.x, gridPosition.y, 0);
        
        Transform parent = null;
        if (MapGenerator.instance != null && MapGenerator.instance.MapRendererInstance != null)
        {
            parent = MapGenerator.instance.MapRendererInstance.entityParent;
        }
        
        GameObject buildingObj = Instantiate(buildingData.buildingPrefab, worldPos, Quaternion.identity, parent);
        
        // Building 컴포넌트 초기화
        Building building = buildingObj.GetComponent<Building>();
        if (building != null)
        {
            building.Initialize(buildingData);
        }
        
        Debug.Log($"[ConstructionSite] 건물 생성 완료: {buildingData.buildingName} at {worldPos}");
    }
    
    /// <summary>
    /// 건설을 취소합니다 (자원 100% 환불).
    /// </summary>
    public void CancelConstruction()
    {
        Debug.Log($"[ConstructionSite] 건설 취소: {buildingData.buildingName}");
        
        // 자원 환불
        if (InventoryManager.instance != null && buildingData.requiredResources != null)
        {
            foreach (var cost in buildingData.requiredResources)
            {
                InventoryManager.instance.AddItem(cost.item, cost.amount);
                Debug.Log($"[ConstructionSite] 자원 환불: {cost.item.itemName} x{cost.amount}");
            }
        }
        
        // 작업물 제거
        if (workOrder != null && WorkSystemManager.instance != null)
        {
            WorkSystemManager.instance.RemoveWorkOrder(workOrder);
        }
        
        // 타일 점유 해제
        ReleaseTiles();
        
        // 오브젝트 제거
        Destroy(gameObject);
    }
    
    /// <summary>
    /// 클릭 시 작업 할당 UI를 엽니다.
    /// </summary>
    void OnMouseDown()
    {
        if (state == ConstructionState.Completed) return;
        
        if (workOrder != null && WorkSystemManager.instance != null)
        {
            WorkSystemManager.instance.ShowAssignmentUI(workOrder, null, Input.mousePosition);
        }
    }
    
    void OnMouseEnter()
    {
        if (state == ConstructionState.Completed) return;
        
        // 호버 효과
        Color hoverColor = spriteRenderer.color;
        hoverColor.a = Mathf.Min(1f, hoverColor.a + 0.2f);
        spriteRenderer.color = hoverColor;
    }
    
    void OnMouseExit()
    {
        if (state == ConstructionState.Completed) return;
        
        // 원래 색상으로 복구
        spriteRenderer.color = (state == ConstructionState.InProgress) ? inProgressColor : blueprintColor;
    }
}