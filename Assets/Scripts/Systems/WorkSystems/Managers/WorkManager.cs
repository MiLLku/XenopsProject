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
    
    [Header("작업 할당 설정")]
    [SerializeField] private float assignmentInterval = 0.5f; // 작업 할당 체크 간격
    [SerializeField] private float maxAssignmentDistance = 100f; // 최대 작업 할당 거리
    
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
            // 이미 생성된 직원들 등록
            foreach (var employee in EmployeeManager.instance.AllEmployees)
            {
                RegisterEmployee(employee);
            }
            
            // 새로 생성되는 직원 자동 등록
            EmployeeManager.instance.OnEmployeeSpawned += RegisterEmployee;
            EmployeeManager.instance.OnEmployeeRemoved += UnregisterEmployee;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WorkManager] EmployeeManager와 연동 완료. 현재 직원: {EmployeeManager.instance.EmployeeCount}명");
            }
        }
        
        // 주기적으로 작업 할당 처리
        InvokeRepeating(nameof(ProcessWorkAssignment), 1f, assignmentInterval);
    }
    
    void OnDestroy()
    {
        CancelInvoke();
        
        // 이벤트 구독 해제
        if (EmployeeManager.instance != null)
        {
            EmployeeManager.instance.OnEmployeeSpawned -= RegisterEmployee;
            EmployeeManager.instance.OnEmployeeRemoved -= UnregisterEmployee;
        }
    }
    
    #region 직원 관리
    
    private void OnEmployeeStateChanged(EmployeeState state)
    {
        UpdateIdleEmployees();
        
        // 직원이 유휴 상태가 되면 즉시 작업 할당 시도
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
        if (employee == null || allEmployees.Contains(employee)) return;
        
        allEmployees.Add(employee);
        employee.OnStateChanged += OnEmployeeStateChanged;
        UpdateIdleEmployees();
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] 직원 등록: {employee.Data.employeeName}");
        }
    }
    
    public void UnregisterEmployee(Employee employee)
    {
        if (employee == null || !allEmployees.Contains(employee)) return;
        
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
        
        if (showDebugInfo)
        {
            Debug.Log($"[WorkManager] 직원 제거: {employee.Data.employeeName}");
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
        
        if (activeOrders.Count == 0)
            return;
        
        // 각 유휴 직원에 대해 작업 할당
        foreach (var employee in idleEmployees.ToList())
        {
            if (employee == null || employee.State != EmployeeState.Idle)
                continue;
            
            // 직원의 우선순위에 맞는 작업물 찾기
            WorkOrder bestOrder = FindBestOrderForEmployee(employee, activeOrders);
            
            if (bestOrder != null)
            {
                AssignWorkerToOrder(employee, bestOrder);
            }
        }
        
        UpdateIdleEmployees();
    }
    
    /// <summary>
    /// 직원에게 가장 적합한 작업물을 찾습니다.
    /// </summary>
    private WorkOrder FindBestOrderForEmployee(Employee employee, List<WorkOrder> orders)
    {
        WorkOrder bestOrder = null;
        float bestScore = float.MinValue;
        
        foreach (var order in orders)
        {
            // 직원이 할 수 없는 작업 타입이면 스킵
            if (!employee.CanPerformWork(order.workType))
                continue;
            
            // 더 이상 작업자를 할당할 수 없으면 스킵
            if (!order.CanAssignWorker())
                continue;
            
            // 가용 작업 대상이 있는지 확인
            var availableTargets = order.GetAvailableTargets();
            if (availableTargets.Count == 0)
                continue;
            
            // 점수 계산
            float score = CalculateOrderScore(employee, order, availableTargets);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestOrder = order;
            }
        }
        
        return bestOrder;
    }
    
    /// <summary>
    /// 작업물의 적합도 점수를 계산합니다.
    /// </summary>
    private float CalculateOrderScore(Employee employee, WorkOrder order, List<IWorkTarget> availableTargets)
    {
        float score = 0f;
        
        // 1. 작업 우선순위 (낮을수록 높은 점수)
        int employeePriority = employee.GetWorkPriority(order.workType);
        score += (100 - employeePriority) * 10f;
        
        // 2. 작업물 우선순위 (낮을수록 높은 점수)
        score += (100 - order.priority) * 5f;
        
        // 3. 거리 (가까울수록 높은 점수)
        float minDistance = availableTargets
            .Min(t => Vector3.Distance(employee.transform.position, t.GetWorkPosition()));
        
        if (minDistance > maxAssignmentDistance)
        {
            return float.MinValue; // 너무 멀면 할당 안 함
        }
        
        score += (maxAssignmentDistance - minDistance) * 2f;
        
        // 4. 작업 속도
        float workSpeed = employee.GetWorkSpeed(order.workType);
        score += workSpeed * 30f;
        
        // 5. 직원 상태
        float healthRatio = employee.Stats.health / employee.Stats.maxHealth;
        float mentalRatio = employee.Stats.mental / employee.Stats.maxMental;
        score += (healthRatio + mentalRatio) * 10f;
        
        // 6. 피로도
        float fatigueRatio = employee.Needs.fatigue / 100f;
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
            if (showDebugInfo)
            {
                Debug.LogWarning($"[WorkManager] {worker.Data.employeeName}을(를) 작업물 '{order.orderName}'에 할당 실패");
            }
            return;
        }
        
        // 매핑 저장
        employeeToOrderMap[worker] = order;
        
        // 구체적인 작업 대상 할당
        if (AssignSpecificTarget(worker, order))
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WorkManager] {worker.Data.employeeName}을(를) 작업물 '{order.orderName}'에 할당 " +
                         $"(현재 작업자: {order.assignedWorkers.Count}/{order.maxAssignedWorkers})");
            }
        }
        else
        {
            // 할당 실패 시 롤백
            order.UnassignWorker(worker);
            employeeToOrderMap.Remove(worker);
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
                     $"진행률: {order.GetProgress() * 100:F0}% | {order.GetDebugInfo()}");
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
            
            // 완료된 작업물 제거
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
        
        var orders = orderManager.GetOrdersByType(workType);
        
        IWorkTarget closestTarget = null;
        float closestDistance = searchRadius;
        
        foreach (var order in orders)
        {
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
    
    /// <summary>
    /// 디버그용: 모든 작업 할당 상태 출력
    /// </summary>
    [ContextMenu("Print Work Assignments")]
    public void PrintWorkAssignments()
    {
        Debug.Log($"=== 작업 할당 상태 ===");
        Debug.Log($"전체 직원: {allEmployees.Count}명, 유휴: {idleEmployees.Count}명, 작업 중: {employeeToOrderMap.Count}명");
        
        foreach (var kvp in employeeToOrderMap)
        {
            Debug.Log($"- {kvp.Key.Data.employeeName}: {kvp.Value.GetDebugInfo()}");
        }
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