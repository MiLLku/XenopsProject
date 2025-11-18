using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EmployeeAI : MonoBehaviour
{
    [Header("AI 설정")]
    [SerializeField] private float decisionInterval = 2f; // 결정 간격
    [SerializeField] private float searchRadius = 20f;    // 작업 탐색 반경
    
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
        
        // 작업 찾기
        FindAndAssignWork(emp);
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
    
    private void FindAndAssignWork(Employee emp)
    {
        // 활성화된 작업 타입 가져오기
        List<WorkType> enabledWorks = emp.GetEnabledWorkTypes();
        
        foreach (var workType in enabledWorks)
        {
            IWorkTarget target = FindNearestWork(workType);
            if (target != null && target.IsWorkAvailable())
            {
                emp.AssignWork(target);
                Debug.Log($"[AI] {emp.Data.employeeName}에게 {workType} 작업 할당");
                return;
            }
        }
        
        // 작업을 찾지 못했을 때
        Wander(emp);
    }
    
    private IWorkTarget FindNearestWork(WorkType type)
    {
        // WorkManager에서 작업 찾기
        if (WorkManager.instance != null)
        {
            return WorkManager.instance.GetAvailableWork(type, transform.position, searchRadius);
        }
        
        // 대체 로직: 근처 작업 대상 직접 탐색
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, searchRadius);
        
        IWorkTarget nearestTarget = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var col in colliders)
        {
            IWorkTarget target = GetWorkTargetOfType(col.gameObject, type);
            if (target != null && target.IsWorkAvailable())
            {
                float distance = Vector2.Distance(transform.position, col.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestTarget = target;
                }
            }
        }
        
        return nearestTarget;
    }
    
    private IWorkTarget GetWorkTargetOfType(GameObject obj, WorkType type)
    {
        switch (type)
        {

        }
        
        return null;
    }
    
    private void FindFood(Employee emp)
    {
        // 음식 저장소 찾기
        Debug.Log($"[AI] {emp.Data.employeeName}이(가) 음식을 찾고 있습니다.");
        
        // 임시: 근처 음식 저장소 찾기
        GameObject[] foodStorages = GameObject.FindGameObjectsWithTag("FoodStorage");
        if (foodStorages.Length > 0)
        {
            GameObject nearest = foodStorages.OrderBy(f => 
                Vector2.Distance(transform.position, f.transform.position)).First();
            
            emp.GetComponent<EmployeeMovement>()?.MoveTo(nearest.transform.position, () => {
                // 도착 후 식사
                emp.Eat(50f); // 임시 값
            });
        }
    }
    
    private void FindRestingPlace(Employee emp)
    {
        // 휴식 장소 찾기
        Debug.Log($"[AI] {emp.Data.employeeName}이(가) 휴식 장소를 찾고 있습니다.");
        
        // 임시: 근처 침대 찾기
        GameObject[] beds = GameObject.FindGameObjectsWithTag("Bed");
        if (beds.Length > 0)
        {
            GameObject nearest = beds.OrderBy(b => 
                Vector2.Distance(transform.position, b.transform.position)).First();
            
            emp.GetComponent<EmployeeMovement>()?.MoveTo(nearest.transform.position, () => {
                // 도착 후 휴식 (Employee 클래스에서 자동 처리)
            });
        }
    }
    
    private void Wander(Employee emp)
    {
        // 할 일이 없을 때 배회
        if (Random.Range(0f, 1f) < 0.3f) // 30% 확률로 이동
        {
            Vector2 randomDirection = Random.insideUnitCircle * 5f;
            Vector3 targetPos = transform.position + new Vector3(randomDirection.x, randomDirection.y, 0);
            
            emp.GetComponent<EmployeeMovement>()?.MoveTo(targetPos, null);
        }
    }
}