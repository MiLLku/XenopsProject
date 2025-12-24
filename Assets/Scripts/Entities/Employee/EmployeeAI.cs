using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 직원의 자율적인 행동을 관리하는 AI 컨트롤러
/// 작업 할당은 플레이어가 수동으로 하며, AI는 긴급 욕구만 처리합니다.
/// </summary>
public class EmployeeAI : MonoBehaviour
{
    [Header("AI 설정")]
    [SerializeField] private float decisionInterval = 2f;
    [SerializeField] private bool enableAutonomousBehavior = true;
    
    private float lastDecisionTime;
    private Employee employee;
    
    void Awake()
    {
        employee = GetComponent<Employee>();
    }
    
    void Update()
    {
        if (!enableAutonomousBehavior) return;
        if (employee == null) return;
        
        if (Time.time - lastDecisionTime < decisionInterval) return;
        
        lastDecisionTime = Time.time;
        
        CheckCriticalNeeds();
    }
    
    private void CheckCriticalNeeds()
    {
        if (employee.Needs.hunger < 20f && employee.State != EmployeeState.Eating)
        {
            HandleHunger();
            return;
        }
        
        if (employee.Needs.fatigue < 20f && employee.State != EmployeeState.Resting)
        {
            HandleFatigue();
            return;
        }
        
        if (employee.Stats.mental < employee.Stats.maxMental * 0.3f && 
            employee.State != EmployeeState.Resting && 
            employee.State != EmployeeState.MentalBreak)
        {
            HandleLowMental();
            return;
        }
    }
    
    private void HandleHunger()
    {
        Debug.Log($"[AI] {employee.Data.employeeName}이(가) 배고픕니다. (배고픔: {employee.Needs.hunger:F0}%)");
        
        if (employee.State == EmployeeState.Working)
        {
            employee.CancelWork();
        }
        
        GameObject[] foodStorages = GameObject.FindGameObjectsWithTag("FoodStorage");
        if (foodStorages.Length > 0)
        {
            GameObject nearest = foodStorages
                .OrderBy(f => Vector2.Distance(transform.position, f.transform.position))
                .First();
            
            EmployeeMovement movement = employee.GetComponent<EmployeeMovement>();
            if (movement != null)
            {
                movement.MoveTo(nearest.transform.position, () => {
                    employee.Eat(50f);
                    Debug.Log($"[AI] {employee.Data.employeeName}이(가) 식사를 완료했습니다.");
                });
            }
        }
        else
        {
            Debug.LogWarning($"[AI] 음식 저장소를 찾을 수 없습니다. {employee.Data.employeeName}이(가) 임시로 회복합니다.");
            employee.Eat(30f);
        }
    }
    
    private void HandleFatigue()
    {
        Debug.Log($"[AI] {employee.Data.employeeName}이(가) 피곤합니다. (피로: {employee.Needs.fatigue:F0}%)");
        
        if (employee.State == EmployeeState.Working)
        {
            employee.CancelWork();
        }
        
        GameObject[] beds = GameObject.FindGameObjectsWithTag("Bed");
        if (beds.Length > 0)
        {
            GameObject nearest = beds
                .OrderBy(b => Vector2.Distance(transform.position, b.transform.position))
                .First();
            
            EmployeeMovement movement = employee.GetComponent<EmployeeMovement>();
            if (movement != null)
            {
                movement.MoveTo(nearest.transform.position, () => {
                    Debug.Log($"[AI] {employee.Data.employeeName}이(가) 침대에 도착했습니다.");
                });
            }
        }
        else
        {
            Debug.LogWarning($"[AI] 침대를 찾을 수 없습니다. {employee.Data.employeeName}이(가) 제자리에서 휴식합니다.");
        }
    }
    
    private void HandleLowMental()
    {
        Debug.Log($"[AI] {employee.Data.employeeName}의 정신력이 낮습니다. (정신력: {employee.Stats.mental:F0}/{employee.Stats.maxMental})");
        
        if (employee.State == EmployeeState.Working)
        {
            employee.CancelWork();
        }
        
        GameObject[] restPlaces = GameObject.FindGameObjectsWithTag("Bed");
        if (restPlaces.Length > 0)
        {
            GameObject nearest = restPlaces
                .OrderBy(r => Vector2.Distance(transform.position, r.transform.position))
                .First();
            
            EmployeeMovement movement = employee.GetComponent<EmployeeMovement>();
            if (movement != null)
            {
                movement.MoveTo(nearest.transform.position, () => {
                    Debug.Log($"[AI] {employee.Data.employeeName}이(가) 휴식 중입니다.");
                });
            }
        }
        else
        {
            Debug.LogWarning($"[AI] 휴식 공간을 찾을 수 없습니다.");
        }
    }
    
    public void SetAutonomousBehavior(bool enabled)
    {
        enableAutonomousBehavior = enabled;
        
        if (!enabled)
        {
            Debug.Log($"[AI] {employee?.Data?.employeeName}의 자율 행동이 비활성화되었습니다.");
        }
    }
    
    [ContextMenu("Print AI Status")]
    public void PrintAIStatus()
    {
        if (employee == null)
        {
            Debug.Log("[AI] Employee 컴포넌트가 없습니다.");
            return;
        }
        
        Debug.Log($"=== {employee.Data.employeeName} AI 상태 ===");
        Debug.Log($"자율 행동: {(enableAutonomousBehavior ? "활성화" : "비활성화")}");
        Debug.Log($"배고픔: {employee.Needs.hunger:F0}%");
        Debug.Log($"피로: {employee.Needs.fatigue:F0}%");
        Debug.Log($"정신력: {employee.Stats.mental:F0}/{employee.Stats.maxMental}");
        Debug.Log($"현재 상태: {employee.State}");
    }
}