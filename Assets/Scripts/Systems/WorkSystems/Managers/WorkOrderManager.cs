using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 작업물 관리자 - 모든 작업물을 관리합니다.
/// </summary>
public class WorkOrderManager : DestroySingleton<WorkOrderManager>
{
    [Header("작업물 관리")]
    [SerializeField] private List<WorkOrder> allOrders = new List<WorkOrder>();
    [SerializeField] private int nextOrderId = 1;
    
    [Header("비주얼 설정")]
    [SerializeField] private GameObject workOrderVisualPrefab; // WorkOrderVisual 프리팹
    [SerializeField] private Transform visualParent; // 비주얼들의 부모 오브젝트
    
    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = true;
    
    // 작업물 ID -> 비주얼 매핑
    private Dictionary<int, WorkOrderVisual> orderVisuals = new Dictionary<int, WorkOrderVisual>();
    
    protected override void Awake()
    {
        base.Awake();
        
        // 비주얼 부모 오브젝트 생성
        if (visualParent == null)
        {
            GameObject parent = new GameObject("WorkOrderVisuals");
            visualParent = parent.transform;
        }
    }
    
    /// <summary>
    /// 새 작업물을 생성합니다.
    /// </summary>
    public WorkOrder CreateWorkOrder(string name, WorkType workType, int maxWorkers, int priority = 5)
    {
        WorkOrder order = new WorkOrder
        {
            orderId = nextOrderId++,
            orderName = name,
            workType = workType,
            maxAssignedWorkers = maxWorkers,
            priority = priority,
            createdTime = Time.time,
            isActive = true,
            isPaused = false
        };
        
        allOrders.Add(order);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkOrderManager] 작업물 생성: {name} (ID: {order.orderId})");
        }
        
        return order;
    }
    
    /// <summary>
    /// 작업물에 비주얼을 생성합니다 (타일 위치 포함).
    /// </summary>
    public WorkOrderVisual CreateWorkOrderWithVisual(string name, WorkType workType, int maxWorkers, 
        List<Vector3Int> tiles, int priority = 5)
    {
        WorkOrder order = CreateWorkOrder(name, workType, maxWorkers, priority);
        
        if (workOrderVisualPrefab == null)
        {
            Debug.LogWarning("[WorkOrderManager] WorkOrderVisual 프리팹이 설정되지 않았습니다!");
            return null;
        }
        
        // 비주얼 오브젝트 생성
        GameObject visualObj = Instantiate(workOrderVisualPrefab, visualParent);
        visualObj.name = $"WorkOrder_{order.orderId}_{name}";
        
        WorkOrderVisual visual = visualObj.GetComponent<WorkOrderVisual>();
        if (visual != null)
        {
            visual.Initialize(order, tiles);
            orderVisuals[order.orderId] = visual;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WorkOrderManager] 작업물 비주얼 생성: {name} (타일 {tiles.Count}개)");
            }
        }
        
        return visual;
    }
    
    /// <summary>
    /// 작업물을 삭제합니다.
    /// </summary>
    public void RemoveWorkOrder(WorkOrder order)
    {
        if (order == null) return;
        
        order.Cancel();
        allOrders.Remove(order);
        
        // 비주얼도 삭제
        if (orderVisuals.ContainsKey(order.orderId))
        {
            WorkOrderVisual visual = orderVisuals[order.orderId];
            if (visual != null)
            {
                Destroy(visual.gameObject);
            }
            orderVisuals.Remove(order.orderId);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkOrderManager] 작업물 삭제: {order.orderName}");
        }
    }
    
    /// <summary>
    /// 완료된 작업물을 자동으로 제거합니다.
    /// </summary>
    public void CleanupCompletedOrders()
    {
        var completedOrders = allOrders.Where(o => o.IsCompleted()).ToList();
        
        foreach (var order in completedOrders)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkOrderManager] 작업물 완료: {order.orderName}");
            }
            
            RemoveWorkOrder(order);
        }
    }
    
    /// <summary>
    /// 우선순위별로 정렬된 활성 작업물 목록을 반환합니다.
    /// </summary>
    public List<WorkOrder> GetActiveOrders()
    {
        return allOrders
            .Where(o => o.isActive && !o.isPaused)
            .OrderBy(o => o.priority)
            .ThenBy(o => o.createdTime)
            .ToList();
    }
    
    /// <summary>
    /// 특정 작업 타입의 작업물을 반환합니다.
    /// </summary>
    public List<WorkOrder> GetOrdersByType(WorkType type)
    {
        return allOrders.Where(o => o.workType == type && o.isActive && !o.isPaused).ToList();
    }
    
    /// <summary>
    /// ID로 작업물을 찾습니다.
    /// </summary>
    public WorkOrder GetOrderById(int id)
    {
        return allOrders.FirstOrDefault(o => o.orderId == id);
    }
    
    /// <summary>
    /// ID로 작업물 비주얼을 찾습니다.
    /// </summary>
    public WorkOrderVisual GetVisualById(int id)
    {
        orderVisuals.TryGetValue(id, out WorkOrderVisual visual);
        return visual;
    }
    
    void Update()
    {
        // 주기적으로 완료된 작업물 정리
        if (Time.frameCount % 300 == 0) // 5초마다
        {
            CleanupCompletedOrders();
        }
    }
    
    // Public 프로퍼티
    public List<WorkOrder> AllOrders => allOrders;
    public int ActiveOrderCount => allOrders.Count(o => o.isActive && !o.isPaused);
}