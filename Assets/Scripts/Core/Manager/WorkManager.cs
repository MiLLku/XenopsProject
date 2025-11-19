using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// WorkOrder 시스템과 연동하여 직원을 작업에 할당하는 매니저
/// </summary>
public class WorkManager : DestroySingleton<WorkManager>
{
    [Header("직원 관리")]
    [SerializeField] private List<Employee> allEmployees = new List<Employee>();
    [SerializeField] private List<Employee> idleEmployees = new List<Employee>();
    
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
        
        RefreshEmployeeList();
        InvokeRepeating(nameof(ProcessWorkAssignment), 1f, 0.5f);
    }
    
    void OnDestroy()
    {
        CancelInvoke();
    }
    
    #region 직원 관리
    
    public void RefreshEmployeeList()
    {
        foreach (var emp in allEmployees)
        {
            if (emp != null)
            {
                emp.OnStateChanged -= OnEmployeeStateChanged;
            }
        }
        
        allEmployees.Clear();
        allEmployees.AddRange(FindObjectsOfType<Employee>());
        
        foreach (var emp in allEmployees)
        {
            emp.OnStateChanged += OnEmployeeStateChanged;
        }
        
        UpdateIdleEmployees();
        
        Debug.Log($"[WorkManager] {allEmployees.Count}명의 직원 등록 완료");
    }
    
    private void OnEmployeeStateChanged(EmployeeState state)
    {
        UpdateIdleEmployees();
        
        if (state == EmployeeState.Idle)
        {
            ProcessWorkAssignment();
        }
    }
    
    private void UpdateIdleEmployees()
    {
        idleEmployees = allEmployees
            .Where(e => e != null && e.State == EmployeeState.Idle)
            .ToList();
    }
    
    public void RegisterEmployee(Employee employee)
    {
        if (!allEmployees.Contains(employee))
        {
            allEmployees.Add(employee);
            employee.OnStateChanged += OnEmployeeStateChanged;
            UpdateIdleEmployees();
        }
    }
    
    public void UnregisterEmployee(Employee employee)
    {
        if (allEmployees.Contains(employee))
        {
            allEmployees.Remove(employee);
            employee.OnStateChanged -= OnEmployeeStateChanged;
            
            // 해당 직원이 작업 중이던 작업물에서 제거
            if (employeeToOrderMap.ContainsKey(employee))
            {
                WorkOrder order = employeeToOrderMap[employee];
                order.UnassignWorker(employee);
                employeeToOrderMap.Remove(employee);
            }
            
            UpdateIdleEmployees();
        }
    }
    
    #endregion
    
    #region 작업 할당
    
    /// <summary>
    /// 유휴 직원에게 작업을 할당합니다.
    /// </summary>
    private void ProcessWorkAssignment()
    {
        if (orderManager == null || idleEmployees.Count == 0)
            return;
        
        // 우선순위별로 정렬된 작업물 가져오기
        var activeOrders = orderManager.GetActiveOrders();
        
        foreach (var order in activeOrders)
        {
            // 이 작업물에 더 할당할 수 있는지 확인
            while (order.CanAssignWorker() && idleEmployees.Count > 0)
            {
                // 이 작업에 적합한 직원 찾기
                Employee bestWorker = FindBestWorkerForOrder(order);
                
                if (bestWorker != null)
                {
                    AssignWorkerToOrder(bestWorker, order);
                    UpdateIdleEmployees();
                }
                else
                {
                    break; // 더 이상 할당 가능한 직원이 없음
                }
            }
        }
    }
    
    /// <summary>
    /// 작업물에 가장 적합한 직원을 찾습니다.
    /// </summary>
    private Employee FindBestWorkerForOrder(WorkOrder order)
    {
        // 해당 작업을 수행할 수 있는 직원 필터링
        var capableWorkers = idleEmployees
            .Where(e => e.CanPerformWork(order.workType))
            .ToList();
        
        if (capableWorkers.Count == 0)
            return null;
        
        // 가용 작업 대상 중 가장 가까운 곳 찾기
        var availableTargets = order.GetAvailableTargets();
        if (availableTargets.Count == 0)
            return null;
        
        Employee bestWorker = null;
        float bestScore = float.MinValue;
        
        foreach (var worker in capableWorkers)
        {
            // 가장 가까운 작업 대상까지의 거리 계산
            float minDistance = availableTargets
                .Min(t => Vector3.Distance(worker.transform.position, t.GetWorkPosition()));
            
            float score = CalculateWorkerScore(worker, order, minDistance);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestWorker = worker;
            }
        }
        
        return bestWorker;
    }
    
    /// <summary>
    /// 직원의 작업 적합도 점수를 계산합니다.
    /// </summary>
    private float CalculateWorkerScore(Employee worker, WorkOrder order, float distanceToWork)
    {
        float score = 0f;
        
        // 거리 (가까울수록 높은 점수)
        score += (50f - distanceToWork) * 2f;
        
        // 작업 속도
        float workSpeed = worker.GetWorkSpeed(order.workType);
        score += workSpeed * 30f;
        
        // 직원 상태
        float healthRatio = worker.Stats.health / worker.Stats.maxHealth;
        float mentalRatio = worker.Stats.mental / worker.Stats.maxMental;
        score += (healthRatio + mentalRatio) * 10f;
        
        // 피로도
        float fatigueRatio = worker.Needs.fatigue / 100f;
        score += fatigueRatio * 20f;
        
        return score;
    }
    
    /// <summary>
    /// 직원을 작업물에 할당합니다.
    /// </summary>
    private void AssignWorkerToOrder(Employee worker, WorkOrder order)
    {
        // 작업물에 직원 등록
        if (!order.AssignWorker(worker))
        {
            Debug.LogWarning($"[WorkManager] {worker.Data.employeeName}을(를) 작업물 '{order.orderName}'에 할당 실패");
            return;
        }
        
        // 매핑 저장
        employeeToOrderMap[worker] = order;
        idleEmployees.Remove(worker);
        
        // 구체적인 작업 대상 할당
        AssignSpecificTarget(worker, order);
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] {worker.Data.employeeName}을(를) 작업물 '{order.orderName}'에 할당 " +
                     $"(현재 작업자: {order.assignedWorkers.Count}/{order.maxAssignedWorkers})");
        }
    }
    
    /// <summary>
    /// 직원에게 구체적인 작업 대상을 할당합니다.
    /// </summary>
    private void AssignSpecificTarget(Employee worker, WorkOrder order)
    {
        var availableTargets = order.GetAvailableTargets();
        
        if (availableTargets.Count == 0)
        {
            Debug.LogWarning($"[WorkManager] 작업물 '{order.orderName}'에 가용 작업 대상이 없습니다.");
            order.UnassignWorker(worker);
            employeeToOrderMap.Remove(worker);
            return;
        }
        
        // 가장 가까운 작업 대상 찾기
        IWorkTarget closestTarget = availableTargets
            .OrderBy(t => Vector3.Distance(worker.transform.position, t.GetWorkPosition()))
            .First();
        
        // 작업물에 할당 기록
        order.AssignTargetToWorker(worker, closestTarget);
        
        // 직원에게 작업 할당
        worker.AssignWork(closestTarget);
    }
    
    #endregion
    
    #region 작업 완료 처리
    
    /// <summary>
    /// 직원이 작업을 완료했을 때 호출됩니다.
    /// </summary>
    public void OnWorkerCompletedTarget(Employee worker, IWorkTarget target)
    {
        if (!employeeToOrderMap.ContainsKey(worker))
            return;
        
        WorkOrder order = employeeToOrderMap[worker];
        
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
            
            employeeToOrderMap.Remove(worker);
            return;
        }
        
        // 다음 작업 대상 할당
        AssignSpecificTarget(worker, order);
    }
    
    /// <summary>
    /// 직원이 작업을 취소했을 때 호출됩니다.
    /// </summary>
    public void OnWorkerCancelledWork(Employee worker)
    {
        if (!employeeToOrderMap.ContainsKey(worker))
            return;
        
        WorkOrder order = employeeToOrderMap[worker];
        order.UnassignWorker(worker);
        employeeToOrderMap.Remove(worker);
        
        UpdateIdleEmployees();
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] {worker.Data.employeeName}의 작업 취소");
        }
    }
    
    #endregion
    
    #region 유틸리티
    
    /// <summary>
    /// 특정 작업 타입의 가용 작업을 찾습니다 (EmployeeAI 호환용).
    /// </summary>
    public IWorkTarget GetAvailableWork(WorkType workType, Vector3 position, float searchRadius)
    {
        if (orderManager == null) return null;
        
        // WorkOrderManager에서 해당 타입의 활성 작업물 가져오기
        var orders = orderManager.GetOrdersByType(workType);
        
        IWorkTarget closestTarget = null;
        float closestDistance = searchRadius;
        
        foreach (var order in orders)
        {
            // 가용 작업 대상 가져오기
            var availableTargets = order.GetAvailableTargets();
            
            foreach (var target in availableTargets)
            {
                float distance = Vector3.Distance(position, target.GetWorkPosition());
                
                if (distance < closestDistance && target.IsWorkAvailable())
                {
                    closestDistance = distance;
                    closestTarget = target;
                }
            }
        }
        
        return closestTarget;
    }
    
    /// <summary>
    /// 특정 작업물의 모든 작업자를 해제합니다.
    /// </summary>
    public void UnassignAllWorkersFromOrder(WorkOrder order)
    {
        var workersToRemove = employeeToOrderMap
            .Where(kvp => kvp.Value == order)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var worker in workersToRemove)
        {
            worker.CancelWork();
            employeeToOrderMap.Remove(worker);
        }
        
        order.Cancel();
        UpdateIdleEmployees();
    }
    
    /// <summary>
    /// 작업 통계를 반환합니다.
    /// </summary>
    public WorkStatistics GetStatistics()
    {
        return new WorkStatistics
        {
            totalEmployees = allEmployees.Count,
            idleEmployees = idleEmployees.Count,
            workingEmployees = allEmployees.Count - idleEmployees.Count,
            activeOrders = orderManager?.ActiveOrderCount ?? 0
        };
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugInfo || orderManager == null)
            return;
        
        var activeOrders = orderManager.GetActiveOrders();
        
        foreach (var order in activeOrders)
        {
            // 작업 대상 표시
            Gizmos.color = Color.yellow;
            foreach (var target in order.targets)
            {
                if (target != null)
                {
                    Gizmos.DrawWireCube(target.GetWorkPosition(), Vector3.one * 0.3f);
                }
            }
            
            // 작업자와 작업 대상 연결선
            Gizmos.color = Color.green;
            foreach (var assignment in order.workerAssignments)
            {
                if (assignment.Key != null && assignment.Value != null)
                {
                    Gizmos.DrawLine(
                        assignment.Key.transform.position,
                        assignment.Value.GetWorkPosition()
                    );
                }
            }
        }
    }
    
    #endregion
    
    // Public 프로퍼티
    public List<Employee> AllEmployees => allEmployees;
    public List<Employee> IdleEmployees => idleEmployees;
}

public class WorkStatistics
{
    public int totalEmployees;
    public int idleEmployees;
    public int workingEmployees;
    public int activeOrders;
}