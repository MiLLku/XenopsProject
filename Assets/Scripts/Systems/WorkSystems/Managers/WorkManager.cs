using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 직원의 작업 상태를 관리하는 매니저 (플레이어 수동 할당 전용)
/// </summary>
public class WorkManager : DestroySingleton<WorkManager>
{
    [Header("직원 관리")]
    [SerializeField] private List<Employee> allEmployees = new List<Employee>();
    
    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = true;
    
    private WorkOrderManager orderManager;
    
    // 직원별 현재 작업 중인 작업물 추적
    private Dictionary<Employee, WorkOrder> employeeToOrderMap = new Dictionary<Employee, WorkOrder>();
    
    protected override void Awake()
    {
        base.Awake();
    }
    
    void Start()
    {
        orderManager = WorkOrderManager.instance;
        if (orderManager == null)
        {
            Debug.LogError("[WorkManager] WorkOrderManager를 찾을 수 없습니다!");
        }
        
        // EmployeeManager 연동
        if (EmployeeManager.instance != null)
        {
            foreach (var employee in EmployeeManager.instance.AllEmployees)
            {
                RegisterEmployee(employee);
            }
            
            EmployeeManager.instance.OnEmployeeSpawned += RegisterEmployee;
            EmployeeManager.instance.OnEmployeeRemoved += UnregisterEmployee;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WorkManager] 직원 {EmployeeManager.instance.EmployeeCount}명 등록 완료");
            }
        }
    }
    
    void OnDestroy()
    {
        if (EmployeeManager.instance != null)
        {
            EmployeeManager.instance.OnEmployeeSpawned -= RegisterEmployee;
            EmployeeManager.instance.OnEmployeeRemoved -= UnregisterEmployee;
        }
    }
    
    #region 직원 관리
    
    public void RegisterEmployee(Employee employee)
    {
        if (employee == null || allEmployees.Contains(employee)) return;
        
        allEmployees.Add(employee);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] 직원 등록: {employee.Data.employeeName}");
        }
    }
    
    public void UnregisterEmployee(Employee employee)
    {
        if (employee == null || !allEmployees.Contains(employee)) return;
        
        allEmployees.Remove(employee);
        
        // 해당 직원이 작업 중이던 작업물에서 제거
        if (employeeToOrderMap.ContainsKey(employee))
        {
            WorkOrder order = employeeToOrderMap[employee];
            order.UnassignWorker(employee);
            employeeToOrderMap.Remove(employee);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] 직원 제거: {employee.Data.employeeName}");
        }
    }
    
    #endregion
    
    #region 수동 작업 할당
    
    /// <summary>
    /// 플레이어가 특정 직원을 특정 작업물에 수동으로 할당합니다.
    /// </summary>
    public bool AssignEmployeeToOrder(Employee employee, WorkOrder order)
    {
        if (employee == null || order == null)
        {
            Debug.LogWarning("[WorkManager] 직원 또는 작업물이 null입니다.");
            return false;
        }
        
        // 직원이 해당 작업을 수행할 수 있는지 확인
        if (!employee.CanPerformWork(order.workType))
        {
            Debug.LogWarning($"[WorkManager] {employee.Data.employeeName}은(는) {order.workType} 작업을 수행할 수 없습니다.");
            return false;
        }
        
        // 작업물에 더 이상 작업자를 할당할 수 없는 경우
        if (!order.CanAssignWorker())
        {
            Debug.LogWarning($"[WorkManager] 작업물 '{order.orderName}'에 더 이상 작업자를 할당할 수 없습니다.");
            return false;
        }
        
        // 직원이 이미 다른 작업 중이면 취소
        if (employee.State == EmployeeState.Working)
        {
            employee.CancelWork();
        }
        
        // 작업물에 직원 등록
        if (!order.AssignWorker(employee))
        {
            return false;
        }
        
        // 매핑 저장
        employeeToOrderMap[employee] = order;
        
        // 구체적인 작업 대상 할당
        if (AssignSpecificTarget(employee, order))
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkManager] {employee.Data.employeeName}을(를) '{order.orderName}'에 할당 완료");
            }
            return true;
        }
        else
        {
            // 할당 실패 시 롤백
            order.UnassignWorker(employee);
            employeeToOrderMap.Remove(employee);
            return false;
        }
    }
    
    /// <summary>
    /// 직원에게 구체적인 작업 대상을 할당합니다.
    /// </summary>
    private bool AssignSpecificTarget(Employee worker, WorkOrder order)
    {
        var availableTargets = order.GetAvailableTargets();
        
        if (availableTargets.Count == 0)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[WorkManager] 작업물 '{order.orderName}'에 가용 작업 대상이 없습니다.");
            }
            return false;
        }
        
        // 가장 가까운 작업 대상 찾기
        IWorkTarget closestTarget = availableTargets
            .OrderBy(t => Vector3.Distance(worker.transform.position, t.GetWorkPosition()))
            .First();
        
        // 작업물에 할당 기록
        order.AssignTargetToWorker(worker, closestTarget);
        
        // 직원에게 작업 할당
        worker.AssignWork(order, closestTarget);
        
        return true;
    }
    
    #endregion
    
    #region 작업 완료 처리
    
    /// <summary>
    /// 직원이 작업을 완료했을 때 호출됩니다.
    /// </summary>
    public void OnWorkerCompletedTarget(Employee worker, IWorkTarget target, WorkOrder order)
    {
        if (worker == null || order == null) return;
        
        // 작업물에서 해당 대상 완료 처리
        order.CompleteTarget(target, worker);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] {worker.Data.employeeName}이(가) 작업 완료. " +
                     $"진행률: {order.GetProgress() * 100:F0}%");
        }
        
        // 작업물이 완전히 완료되었는지 확인
        if (order.IsCompleted())
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkManager] 작업물 '{order.orderName}' 완전 완료!");
            }
            
            order.UnassignWorker(worker);
            employeeToOrderMap.Remove(worker);
            
            if (orderManager != null)
            {
                orderManager.RemoveWorkOrder(order);
            }
            
            return;
        }
        
        // 같은 작업물에서 다음 작업 대상 할당
        if (!AssignSpecificTarget(worker, order))
        {
            // 더 이상 할당할 작업이 없으면 작업자 해제
            order.UnassignWorker(worker);
            employeeToOrderMap.Remove(worker);
        }
    }
    
    /// <summary>
    /// 직원이 작업을 취소했을 때 호출됩니다.
    /// </summary>
    public void OnWorkerCancelledWork(Employee worker)
    {
        if (worker == null || !employeeToOrderMap.ContainsKey(worker))
            return;
        
        WorkOrder order = employeeToOrderMap[worker];
        order.UnassignWorker(worker);
        employeeToOrderMap.Remove(worker);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] {worker.Data.employeeName}의 작업 취소");
        }
    }
    
    #endregion
    
    // Public 프로퍼티
    public List<Employee> AllEmployees => allEmployees;
    public List<Employee> IdleEmployees => allEmployees.Where(e => e.State == EmployeeState.Idle).ToList();
}