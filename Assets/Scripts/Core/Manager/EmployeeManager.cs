using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 모든 직원을 중앙에서 관리하는 매니저
/// 직원 생성, 스폰, 관리를 담당합니다.
/// </summary>
public class EmployeeManager : DestroySingleton<EmployeeManager>
{
    [Header("직원 프리팹")]
    [Tooltip("사용할 직원 프리팹")]
    [SerializeField] private GameObject employeePrefab;
    
    [Header("초기 직원 설정")]
    [Tooltip("게임 시작 시 자동으로 생성할 직원들")]
    [SerializeField] private List<EmployeeSpawnData> initialEmployees = new List<EmployeeSpawnData>();
    
    [Header("직원 관리")]
    [Tooltip("현재 게임에 존재하는 모든 직원")]
    [SerializeField] private List<Employee> allEmployees = new List<Employee>();
    
    [Header("스폰 설정")]
    [Tooltip("직원들이 스폰될 위치 (자동 설정됨)")]
    [SerializeField] private Vector3 spawnPoint = new Vector3(105, 142, 0);
    
    [Tooltip("여러 직원 스폰 시 간격")]
    [SerializeField] private float spawnSpacing = 2f;
    
    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = true;
    
    // 이벤트
    public delegate void EmployeeDelegate(Employee employee);
    public event EmployeeDelegate OnEmployeeSpawned;
    public event EmployeeDelegate OnEmployeeRemoved;
    
    protected override void Awake()
    {
        base.Awake();
    }
    
    /// <summary>
    /// 스폰 지점을 설정합니다 (MapGenerator에서 호출)
    /// </summary>
    public void SetSpawnPoint(Vector3 point)
    {
        spawnPoint = point;
        if (showDebugInfo)
        {
            Debug.Log($"[EmployeeManager] 스폰 지점 설정: {point}");
        }
    }
    
    /// <summary>
    /// 초기 직원들을 스폰합니다
    /// </summary>
    public void SpawnInitialEmployees()
    {
        if (employeePrefab == null)
        {
            Debug.LogError("[EmployeeManager] Employee 프리팹이 설정되지 않았습니다!");
            return;
        }
        
        if (initialEmployees.Count == 0)
        {
            Debug.LogWarning("[EmployeeManager] 초기 직원 목록이 비어있습니다.");
            return;
        }
        
        for (int i = 0; i < initialEmployees.Count; i++)
        {
            var spawnData = initialEmployees[i];
            if (spawnData.employeeData == null)
            {
                Debug.LogWarning($"[EmployeeManager] 초기 직원 {i}의 EmployeeData가 null입니다.");
                continue;
            }
            
            // 스폰 위치 계산 (여러 직원이 겹치지 않도록)
            Vector3 offset = new Vector3(i * spawnSpacing, 0, 0);
            Vector3 finalSpawnPos = spawnPoint + offset;
            
            SpawnEmployee(spawnData.employeeData, finalSpawnPos);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[EmployeeManager] 초기 직원 {allEmployees.Count}명 스폰 완료");
        }
    }
    
    /// <summary>
    /// 특정 위치에 직원을 스폰합니다
    /// </summary>
    public Employee SpawnEmployee(EmployeeData employeeData, Vector3 position)
    {
        if (employeePrefab == null)
        {
            Debug.LogError("[EmployeeManager] Employee 프리팹이 설정되지 않았습니다!");
            return null;
        }
        
        if (employeeData == null)
        {
            Debug.LogError("[EmployeeManager] EmployeeData가 null입니다!");
            return null;
        }
        
        // 프리팹 인스턴스 생성
        GameObject employeeObj = Instantiate(employeePrefab, position, Quaternion.identity);
        employeeObj.name = $"Employee_{employeeData.employeeName}_{allEmployees.Count}";
        
        // Employee 컴포넌트 가져오기
        Employee employee = employeeObj.GetComponent<Employee>();
        if (employee == null)
        {
            Debug.LogError("[EmployeeManager] 프리팹에 Employee 컴포넌트가 없습니다!");
            Destroy(employeeObj);
            return null;
        }
        
        // 직원 데이터 초기화
        employee.Initialize(employeeData);
        
        // 목록에 추가
        allEmployees.Add(employee);
        
        // 이벤트 발생
        OnEmployeeSpawned?.Invoke(employee);
        
        if (showDebugInfo)
        {
            Debug.Log($"[EmployeeManager] '{employeeData.employeeName}' 스폰 완료: {position}");
        }
        
        return employee;
    }
    
    /// <summary>
    /// 스폰 지점에 직원을 스폰합니다 (위치 자동)
    /// </summary>
    public Employee SpawnEmployee(EmployeeData employeeData)
    {
        Vector3 offset = new Vector3(allEmployees.Count * spawnSpacing, 0, 0);
        return SpawnEmployee(employeeData, spawnPoint + offset);
    }
    
    /// <summary>
    /// 직원을 제거합니다
    /// </summary>
    public void RemoveEmployee(Employee employee)
    {
        if (employee == null) return;
        
        if (allEmployees.Contains(employee))
        {
            allEmployees.Remove(employee);
            OnEmployeeRemoved?.Invoke(employee);
            
            if (showDebugInfo)
            {
                Debug.Log($"[EmployeeManager] '{employee.Data.employeeName}' 제거됨");
            }
        }
        
        Destroy(employee.gameObject);
    }
    
    /// <summary>
    /// 모든 직원을 제거합니다
    /// </summary>
    public void RemoveAllEmployees()
    {
        foreach (var employee in allEmployees.ToList())
        {
            RemoveEmployee(employee);
        }
    }
    
    /// <summary>
    /// ID로 직원을 찾습니다
    /// </summary>
    public Employee GetEmployeeById(int employeeId)
    {
        return allEmployees.FirstOrDefault(e => e.Data != null && e.Data.employeeID == employeeId);
    }
    
    /// <summary>
    /// 이름으로 직원을 찾습니다
    /// </summary>
    public Employee GetEmployeeByName(string name)
    {
        return allEmployees.FirstOrDefault(e => e.Data != null && e.Data.employeeName == name);
    }
    
    /// <summary>
    /// 특정 작업을 수행할 수 있는 직원 목록을 반환합니다
    /// </summary>
    public List<Employee> GetEmployeesCapableOf(WorkType workType)
    {
        return allEmployees.Where(e => e != null && e.CanPerformWork(workType)).ToList();
    }
    
    /// <summary>
    /// 유휴 상태인 직원 목록을 반환합니다
    /// </summary>
    public List<Employee> GetIdleEmployees()
    {
        return allEmployees.Where(e => e != null && e.State == EmployeeState.Idle).ToList();
    }
    
    /// <summary>
    /// 작업 중인 직원 목록을 반환합니다
    /// </summary>
    public List<Employee> GetWorkingEmployees()
    {
        return allEmployees.Where(e => e != null && e.State == EmployeeState.Working).ToList();
    }
    
    /// <summary>
    /// 직원 통계를 반환합니다
    /// </summary>
    public EmployeeStatistics GetStatistics()
    {
        return new EmployeeStatistics
        {
            totalEmployees = allEmployees.Count,
            idleEmployees = GetIdleEmployees().Count,
            workingEmployees = GetWorkingEmployees().Count,
            averageHealth = allEmployees.Average(e => e.Stats.health),
            averageMental = allEmployees.Average(e => e.Stats.mental),
            averageHunger = allEmployees.Average(e => e.Needs.hunger),
            averageFatigue = allEmployees.Average(e => e.Needs.fatigue)
        };
    }
    
    /// <summary>
    /// 디버그용: 모든 직원의 상태를 출력합니다
    /// </summary>
    [ContextMenu("Print All Employees")]
    public void PrintAllEmployees()
    {
        Debug.Log($"=== 직원 목록 ({allEmployees.Count}명) ===");
        foreach (var employee in allEmployees)
        {
            if (employee == null || employee.Data == null) continue;
            
            Debug.Log($"- {employee.Data.employeeName}: " +
                     $"상태={employee.State}, " +
                     $"체력={employee.Stats.health}/{employee.Stats.maxHealth}, " +
                     $"정신={employee.Stats.mental}/{employee.Stats.maxMental}");
        }
    }
    
    // Public 프로퍼티
    public List<Employee> AllEmployees => allEmployees;
    public int EmployeeCount => allEmployees.Count;
    public Vector3 SpawnPoint => spawnPoint;
}


/// <summary>
/// 초기 직원 스폰 데이터
/// </summary>
[System.Serializable]
public class EmployeeSpawnData
{
    [Tooltip("스폰할 직원의 데이터")]
    public EmployeeData employeeData;
    
    [Tooltip("이 직원이 활성화되어 있는지 (체크 해제 시 스폰 안 됨)")]
    public bool isEnabled = true;
}

/// <summary>
/// 직원 통계 데이터
/// </summary>
public class EmployeeStatistics
{
    public int totalEmployees;
    public int idleEmployees;
    public int workingEmployees;
    public float averageHealth;
    public float averageMental;
    public float averageHunger;
    public float averageFatigue;
}
