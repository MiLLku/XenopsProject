using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 직원의 자율적인 행동을 관리하는 AI 컨트롤러
/// WorkManager와 연동하여 작업을 할당받습니다.
/// </summary>
public class EmployeeAI : MonoBehaviour
{
    [Header("AI 설정")]
    [SerializeField] private float decisionInterval = 2f; // 결정 간격
    [SerializeField] private float searchRadius = 20f;    // 작업 탐색 반경
    [SerializeField] private bool enableAIWork = false;   // AI 자율 작업 비활성화 (WorkManager가 관리)
    
    private float lastDecisionTime;
    private Employee employee;
    
    void Awake()
    {
        employee = GetComponent<Employee>();
    }
    
    public void UpdateAI(Employee emp)
    {
        if (Time.time - lastDecisionTime < decisionInterval) return;
        
        lastDecisionTime = Time.time;
        
        // 욕구 체크
        if (CheckCriticalNeeds(emp)) return;
        
        // WorkManager가 작업 할당을 관리하므로 AI는 긴급 욕구만 처리
        // 작업 할당은 WorkManager.ProcessWorkAssignment()에서 자동으로 처리됨
        
        // 디버그: AI 자율 작업이 활성화된 경우만 직접 작업 찾기
        if (enableAIWork)
        {
            FindAndAssignWorkViaWorkManager(emp);
        }
    }
    
    private bool CheckCriticalNeeds(Employee emp)
    {
        // 긴급 욕구 처리
        if (emp.Needs.hunger < 30f)
        {
            FindFood(emp);
            return true;
        }
        
        if (emp.Needs.fatigue < 30f)
        {
            FindRestingPlace(emp);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// WorkManager를 통해 작업을 찾습니다 (권장하지 않음, WorkManager가 자동으로 처리)
    /// </summary>
    private void FindAndAssignWorkViaWorkManager(Employee emp)
    {
        // WorkManager가 이미 주기적으로 작업을 할당하므로
        // 여기서는 아무것도 하지 않거나, 특수한 경우만 처리
        
        // 만약 WorkManager가 없거나 비활성화된 경우에만 직접 작업 찾기
        if (WorkManager.instance == null)
        {
            Debug.LogWarning("[EmployeeAI] WorkManager가 없어 AI가 직접 작업을 찾습니다.");
            LegacyFindAndAssignWork(emp);
        }
    }
    
    /// <summary>
    /// 레거시: WorkManager 없이 직접 작업을 찾는 방법 (사용 안 함)
    /// </summary>
    private void LegacyFindAndAssignWork(Employee emp)
    {
        // 활성화된 작업 타입 가져오기
        List<WorkType> enabledWorks = emp.GetEnabledWorkTypes();
        
        foreach (var workType in enabledWorks)
        {
            // WorkManager를 통해 가용 작업 찾기
            if (WorkManager.instance != null)
            {
                IWorkTarget target = WorkManager.instance.GetAvailableWork(
                    workType, 
                    transform.position, 
                    searchRadius
                );
                
                if (target != null && target.IsWorkAvailable())
                {
                    // WorkManager를 통해 정식으로 할당받아야 하지만
                    // 여기서는 임시로 로그만 출력
                    Debug.Log($"[AI] {emp.Data.employeeName}이(가) {workType} 작업을 찾았습니다. " +
                             "WorkManager가 자동으로 할당할 것입니다.");
                    return;
                }
            }
        }
        
        // 작업을 찾지 못했을 때
        Wander(emp);
    }
    
    private void FindFood(Employee emp)
    {
        // 음식 저장소 찾기
        Debug.Log($"[AI] {emp.Data.employeeName}이(가) 음식을 찾고 있습니다.");
        
        // 현재 작업 취소
        if (emp.State == EmployeeState.Working)
        {
            emp.CancelWork();
        }
        
        // 임시: 근처 음식 저장소 찾기
        GameObject[] foodStorages = GameObject.FindGameObjectsWithTag("FoodStorage");
        if (foodStorages.Length > 0)
        {
            GameObject nearest = foodStorages.OrderBy(f => 
                Vector2.Distance(transform.position, f.transform.position)).First();
            
            EmployeeMovement movement = emp.GetComponent<EmployeeMovement>();
            if (movement != null)
            {
                movement.MoveTo(nearest.transform.position, () => {
                    // 도착 후 식사
                    emp.Eat(50f); // 임시 값
                });
            }
        }
        else
        {
            // 음식 저장소가 없으면 임시로 즉시 회복
            Debug.LogWarning($"[AI] 음식 저장소를 찾을 수 없습니다. {emp.Data.employeeName}이(가) 임시로 회복합니다.");
            emp.Eat(30f);
        }
    }
    
    private void FindRestingPlace(Employee emp)
    {
        // 휴식 장소 찾기
        Debug.Log($"[AI] {emp.Data.employeeName}이(가) 휴식 장소를 찾고 있습니다.");
        
        // 현재 작업 취소
        if (emp.State == EmployeeState.Working)
        {
            emp.CancelWork();
        }
        
        // 임시: 근처 침대 찾기
        GameObject[] beds = GameObject.FindGameObjectsWithTag("B    ed");
        if (beds.Length > 0)
        {
            GameObject nearest = beds.OrderBy(b => 
                Vector2.Distance(transform.position, b.transform.position)).First();
            
            EmployeeMovement movement = emp.GetComponent<EmployeeMovement>();
            if (movement != null)
            {
                movement.MoveTo(nearest.transform.position, () => {
                    // 도착 후 휴식 (Employee 클래스에서 자동 처리)
                });
            }
        }
        else
        {
            // 침대가 없으면 제자리에서 휴식
            Debug.LogWarning($"[AI] 침대를 찾을 수 없습니다. {emp.Data.employeeName}이(가) 제자리에서 휴식합니다.");
        }
    }
    
    private void Wander(Employee emp)
    {
        // 할 일이 없을 때 배회 (선택사항)
        if (Random.Range(0f, 1f) < 0.1f) // 10% 확률로 이동
        {
            Vector2 randomDirection = Random.insideUnitCircle * 5f;
            Vector3 targetPos = transform.position + new Vector3(randomDirection.x, randomDirection.y, 0);
            
            EmployeeMovement movement = emp.GetComponent<EmployeeMovement>();
            if (movement != null)
            {
                movement.MoveTo(targetPos, null);
            }
        }
    }
}