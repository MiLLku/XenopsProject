using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 작업 더미(WorkOrder)를 시각적으로 표현하고 플레이어가 클릭할 수 있게 만드는 컴포넌트
/// </summary>
public class WorkOrderVisual : MonoBehaviour
{
    [Header("작업 더미 정보")]
    [SerializeField] private WorkOrder workOrder;
    [SerializeField] private List<Vector3Int> tilePosisitons = new List<Vector3Int>();
    
    [Header("시각적 표현")]
    [SerializeField] private Color normalColor = new Color(1f, 0.8f, 0.3f, 0.7f);
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 0.5f, 0.9f);
    [SerializeField] private Color selectedColor = new Color(0.3f, 1f, 0.3f, 0.9f);
    
    [Header("UI 표시")]
    [SerializeField] private GameObject labelPrefab;
    private GameObject labelInstance;
    private TMPro.TextMeshProUGUI labelText;
    
    private bool isHovered = false;
    private bool isSelected = false;
    private TileHighlighter tileHighlighter;
    private BoxCollider2D boxCollider;
    
    void Awake()
    {
        boxCollider = gameObject.AddComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
    }
    
    void Start()
    {
        tileHighlighter = TileHighlighter.instance;
        
        if (tileHighlighter != null && tilePosisitons.Count > 0)
        {
            UpdateVisual();
            UpdateCollider();
        }
        
        CreateLabel();
    }
    
    void Update()
    {
        // 작업 완료 확인
        if (workOrder != null && workOrder.IsCompleted())
        {
            Destroy(gameObject);
        }
        
        // 라벨 업데이트
        UpdateLabel();
    }
    
    public void Initialize(WorkOrder order, List<Vector3Int> tiles)
    {
        workOrder = order;
        tilePosisitons = new List<Vector3Int>(tiles);
        
        if (tileHighlighter != null)
        {
            UpdateVisual();
            UpdateCollider();
        }
    }
    
    private void UpdateVisual()
    {
        Color color = isSelected ? selectedColor : (isHovered ? hoverColor : normalColor);
        
        if (tileHighlighter != null)
        {
            tileHighlighter.AddAssignedTiles(tilePosisitons, color);
        }
    }
    
    private void UpdateCollider()
    {
        if (tilePosisitons.Count == 0) return;
        
        // 타일들의 범위 계산
        int minX = tilePosisitons.Min(t => t.x);
        int maxX = tilePosisitons.Max(t => t.x);
        int minY = tilePosisitons.Min(t => t.y);
        int maxY = tilePosisitons.Max(t => t.y);
        
        // 콜라이더 중심과 크기 설정
        Vector2 center = new Vector2(
            (minX + maxX) / 2f + 0.5f,
            (minY + maxY) / 2f + 0.5f
        );
        
        Vector2 size = new Vector2(
            maxX - minX + 1f,
            maxY - minY + 1f
        );
        
        boxCollider.offset = center - (Vector2)transform.position;
        boxCollider.size = size;
    }
    
    private void CreateLabel()
    {
        if (labelPrefab == null || workOrder == null) return;
        
        // 라벨 생성
        labelInstance = Instantiate(labelPrefab, transform);
        labelText = labelInstance.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        
        // 라벨 위치 설정 (작업 더미 중앙 위쪽)
        if (tilePosisitons.Count > 0)
        {
            int minX = tilePosisitons.Min(t => t.x);
            int maxX = tilePosisitons.Max(t => t.x);
            int maxY = tilePosisitons.Max(t => t.y);
            
            Vector3 labelPos = new Vector3(
                (minX + maxX) / 2f + 0.5f,
                maxY + 1.5f,
                0
            );
            
            labelInstance.transform.position = labelPos;
        }
    }
    
    private void UpdateLabel()
    {
        if (labelText == null || workOrder == null) return;
        
        int completed = workOrder.completedTargets.Count;
        int total = workOrder.targets.Count + completed;
        int workers = workOrder.assignedWorkers.Count;
        
        labelText.text = $"{workOrder.orderName}\n{completed}/{total} (작업자: {workers})";
    }
    
    void OnMouseEnter()
    {
        if (InteractionManager.instance != null && 
            InteractionManager.instance.GetCurrentMode() == InteractionManager.InteractMode.Normal)
        {
            isHovered = true;
            UpdateVisual();
        }
    }
    
    void OnMouseExit()
    {
        isHovered = false;
        UpdateVisual();
    }
    
    void OnMouseDown()
    {
        if (InteractionManager.instance != null && 
            InteractionManager.instance.GetCurrentMode() == InteractionManager.InteractMode.Normal)
        {
            OnWorkOrderClicked();
        }
    }
    
    private void OnWorkOrderClicked()
    {
        Debug.Log($"[WorkOrderVisual] 작업 더미 클릭: {workOrder.orderName}");
        
        // 작업 할당 UI 열기 (나중에 구현)
        if (WorkAssignmentUI.instance != null)
        {
            WorkAssignmentUI.instance.OpenAssignmentUI(workOrder);
        }
        else
        {
            Debug.Log($"작업: {workOrder.GetDebugInfo()}");
        }
        
        isSelected = !isSelected;
        UpdateVisual();
    }
    
    void OnDestroy()
    {
        // 하이라이트 제거
        if (tileHighlighter != null && tilePosisitons != null)
        {
            foreach (var tile in tilePosisitons)
            {
                tileHighlighter.RemoveAssignedTile(tile);
            }
        }
        
        if (labelInstance != null)
        {
            Destroy(labelInstance);
        }
    }
    
    // Public 프로퍼티
    public WorkOrder WorkOrder => workOrder;
    public List<Vector3Int> TilePositions => tilePosisitons;
}