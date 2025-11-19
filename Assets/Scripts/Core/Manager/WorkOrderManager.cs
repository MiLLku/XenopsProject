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
    
    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = true;
    
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
            Debug.Log($"[WorkOrderManager] 작업물 생성: {name} (최대 작업자: {maxWorkers}명)");
        }
        
        return order;
    }
    
    /// <summary>
    /// 작업물을 삭제합니다.
    /// </summary>
    public void RemoveWorkOrder(WorkOrder order)
    {
        if (order == null) return;
        
        order.Cancel();
        allOrders.Remove(order);
        
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
            
            allOrders.Remove(order);
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
