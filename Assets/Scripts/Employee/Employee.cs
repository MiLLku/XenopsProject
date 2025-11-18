using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(Collider2D))]
public class Employee : MonoBehaviour
{
    [Header("직원 데이터")]
    [SerializeField] private EmployeeData employeeData;
    
    [Header("현재 상태")]
    [SerializeField] private EmployeeStats currentStats;
    [SerializeField] private EmployeeNeeds currentNeeds;
    [SerializeField] private WorkType currentWork = WorkType.None;
    [SerializeField] private EmployeeState currentState = EmployeeState.Idle;
    
    [Header("작업 우선순위")]
    [SerializeField] private List<WorkPriority> workPriorities;
    
    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = false;
    
    // 컴포넌트 참조
    private SpriteRenderer spriteRenderer;
    private EmployeeAI aiController;
    private EmployeeMovement movement;
    
    // 작업 관련
    private IWorkTarget currentWorkTarget;
    private float workProgress = 0f;
    private Coroutine currentWorkCoroutine;
    
    // 특성 효과 캐시
    private float cachedHealthModifier = 1f;
    private float cachedMentalModifier = 1f;
    private float cachedWorkSpeedModifier = 1f;
    private float cachedHungerRateModifier = 1f;
    private float cachedFatigueRateModifier = 1f;
    
    // 이벤트
    public delegate void StatsChangedDelegate(EmployeeStats stats);
    public event StatsChangedDelegate OnStatsChanged;
    
    public delegate void NeedsChangedDelegate(EmployeeNeeds needs);
    public event NeedsChangedDelegate OnNeedsChanged;
    
    public delegate void StateChangedDelegate(EmployeeState state);
    public event StateChangedDelegate OnStateChanged;
    
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        aiController = GetComponent<EmployeeAI>() ?? gameObject.AddComponent<EmployeeAI>();
        movement = GetComponent<EmployeeMovement>() ?? gameObject.AddComponent<EmployeeMovement>();
        
        InitializeWorkPriorities();
    }
    
    void Start()
    {
        if (employeeData != null)
        {
            Initialize(employeeData);
        }
        
        // WorkManager에 등록
        if (WorkManager.instance != null)
        {
            WorkManager.instance.RegisterEmployee(this);
        }
    }
    
    void OnDestroy()
    {
        // WorkManager에서 제거
        if (WorkManager.instance != null)
        {
            WorkManager.instance.UnregisterEmployee(this);
        }
    }
    
    void Update()
    {
        if (currentState == EmployeeState.Dead) return;
        
        // 욕구 업데이트
        UpdateNeeds(Time.deltaTime);
        
        // 상태 체크
        CheckCriticalNeeds();
        
        // AI 업데이트 (긴급 욕구만 처리)
        if (aiController != null && currentState == EmployeeState.Idle)
        {
            aiController.UpdateAI(this);
        }
        
        // 디버그 표시
        if (showDebugInfo)
        {
            ShowDebugStatus();
        }
    }
    
    public void Initialize(EmployeeData data)
    {
        employeeData = data;
        name = $"Employee_{data.employeeName}";
        
        // 특성 효과 계산
        CalculateTraitModifiers();
        
        // 스탯 초기화
        currentStats = new EmployeeStats
        {
            health = Mathf.RoundToInt(data.maxHealth * cachedHealthModifier),
            maxHealth = Mathf.RoundToInt(data.maxHealth * cachedHealthModifier),
            mental = Mathf.RoundToInt(data.maxMental * cachedMentalModifier),
            maxMental = Mathf.RoundToInt(data.maxMental * cachedMentalModifier),
            attackPower = Mathf.RoundToInt(data.attackPower * (1f + GetTraitAttackModifier()))
        };
        
        // 욕구 초기화
        currentNeeds = new EmployeeNeeds
        {
            hunger = 100f,
            fatigue = 100f
        };
        
        // 작업 우선순위 초기화
        if (workPriorities == null || workPriorities.Count == 0)
        {
            InitializeWorkPriorities();
        }
    }
    
    private void InitializeWorkPriorities()
    {
        workPriorities = new List<WorkPriority>
        {
            new WorkPriority { workType = WorkType.Mining, priority = 1, enabled = true },
            new WorkPriority { workType = WorkType.Chopping, priority = 2, enabled = true },
            new WorkPriority { workType = WorkType.Crafting, priority = 3, enabled = true },
            new WorkPriority { workType = WorkType.Research, priority = 4, enabled = true },
            new WorkPriority { workType = WorkType.Gardening, priority = 5, enabled = true },
            new WorkPriority { workType = WorkType.Hauling, priority = 6, enabled = true }
        };
    }
    
    private void CalculateTraitModifiers()
    {
        cachedHealthModifier = 1f;
        cachedMentalModifier = 1f;
        cachedWorkSpeedModifier = 1f;
        cachedHungerRateModifier = 1f;
        cachedFatigueRateModifier = 1f;
        
        if (employeeData.traits == null) return;
        
        foreach (var trait in employeeData.traits)
        {
            if (trait == null) continue;
            
            cachedHealthModifier += trait.effects.healthModifier / 100f;
            cachedMentalModifier += trait.effects.mentalModifier / 100f;
            cachedWorkSpeedModifier += trait.effects.globalWorkSpeedModifier / 100f;
            cachedHungerRateModifier += trait.effects.hungerRateModifier / 100f;
            cachedFatigueRateModifier += trait.effects.fatigueRateModifier / 100f;
        }
    }
    
    private float GetTraitAttackModifier()
    {
        float modifier = 0f;
        if (employeeData.traits == null) return modifier;
        
        foreach (var trait in employeeData.traits)
        {
            if (trait != null)
            {
                modifier += trait.effects.attackModifier / 100f;
            }
        }
        return modifier;
    }
    
    private void UpdateNeeds(float deltaTime)
    {
        // 배고픔 감소
        float hungerDecay = employeeData.hungerDecayRate * cachedHungerRateModifier;
        currentNeeds.hunger -= hungerDecay * deltaTime;
        currentNeeds.hunger = Mathf.Clamp(currentNeeds.hunger, 0f, 100f);
        
        // 피로 증가 (작업 중일 때만)
        if (currentState == EmployeeState.Working)
        {
            float fatigueIncrease = employeeData.fatigueIncreaseRate * cachedFatigueRateModifier;
            currentNeeds.fatigue -= fatigueIncrease * deltaTime;
            currentNeeds.fatigue = Mathf.Clamp(currentNeeds.fatigue, 0f, 100f);
        }
        else if (currentState == EmployeeState.Resting)
        {
            // 휴식 중일 때 피로 회복
            currentNeeds.fatigue += 10f * deltaTime;
            currentNeeds.fatigue = Mathf.Clamp(currentNeeds.fatigue, 0f, 100f);
        }
        
        // 배고픔이 0이면 체력과 정신력 감소
        if (currentNeeds.hunger <= 0f)
        {
            currentStats.health -= 1f * deltaTime;
            currentStats.mental -= 2f * deltaTime;
        }
        
        // 피로가 0이면 정신력 감소
        if (currentNeeds.fatigue <= 0f)
        {
            currentStats.mental -= 3f * deltaTime;
        }
        
        // 스탯 범위 제한
        currentStats.health = Mathf.Clamp(currentStats.health, 0f, currentStats.maxHealth);
        currentStats.mental = Mathf.Clamp(currentStats.mental, 0f, currentStats.maxMental);
        
        // 이벤트 발생
        OnNeedsChanged?.Invoke(currentNeeds);
        OnStatsChanged?.Invoke(currentStats);
    }
    
    private void CheckCriticalNeeds()
    {
        // 체력이 0 이하면 사망
        if (currentStats.health <= 0f)
        {
            SetState(EmployeeState.Dead);
            return;
        }
        
        // 정신력이 20% 이하면 정신 붕괴
        if (currentStats.mental < currentStats.maxMental * 0.2f)
        {
            SetState(EmployeeState.MentalBreak);
            return;
        }
        
        // 배고픔이 20% 이하면 식사 필요
        if (currentNeeds.hunger < 20f && currentState != EmployeeState.Eating)
        {
            RequestFood();
        }
        
        // 피로가 20% 이하면 휴식 필요
        if (currentNeeds.fatigue < 20f && currentState != EmployeeState.Resting)
        {
            RequestRest();
        }
    }
    
    #region 작업 시스템
    
    public bool CanPerformWork(WorkType type)
    {
        if (employeeData == null || employeeData.abilities == null) return false;
        return employeeData.abilities.CanPerformWork(type);
    }
    
    public float GetWorkSpeed(WorkType type)
    {
        if (!CanPerformWork(type)) return 0f;
        
        float baseSpeed = employeeData.abilities.GetWorkSpeed(type);
        float traitModifier = GetTraitWorkSpeedModifier(type);
        float fatigueModifier = GetFatigueModifier();
        
        return baseSpeed * traitModifier * fatigueModifier * cachedWorkSpeedModifier;
    }
    
    private float GetTraitWorkSpeedModifier(WorkType type)
    {
        float modifier = 1f;
        if (employeeData.traits == null) return modifier;
        
        foreach (var trait in employeeData.traits)
        {
            if (trait == null || trait.effects.specificWorkModifiers == null) continue;
            
            var specific = trait.effects.specificWorkModifiers.FirstOrDefault(m => m.workType == type);
            if (specific.workType == type)
            {
                modifier += specific.speedModifier / 100f;
            }
        }
        
        return modifier;
    }
    
    private float GetFatigueModifier()
    {
        // 피로도에 따른 작업 속도 감소
        if (currentNeeds.fatigue < 20f) return 0.5f;
        if (currentNeeds.fatigue < 50f) return 0.75f;
        return 1f;
    }
    
    public void AssignWork(IWorkTarget target)
    {
        if (target == null || currentState == EmployeeState.Dead || currentState == EmployeeState.MentalBreak) 
            return;
        
        // 현재 작업 취소
        if (currentWorkTarget != null)
        {
            CancelWork();
        }
        
        currentWorkTarget = target;
        SetState(EmployeeState.Moving);
        
        // 작업 위치로 이동
        if (movement != null)
        {
            movement.MoveTo(target.GetWorkPosition(), () => {
                StartWork(target);
            });
        }
    }
    
    private void StartWork(IWorkTarget target)
    {
        if (target == null || !target.IsWorkAvailable()) 
        {
            CompleteWork();
            return;
        }
        
        SetState(EmployeeState.Working);
        currentWork = target.GetWorkType();
        
        float workTime = target.GetWorkTime() / GetWorkSpeed(currentWork);
        currentWorkCoroutine = StartCoroutine(PerformWork(target, workTime));
    }
    
    private IEnumerator PerformWork(IWorkTarget target, float workTime)
    {
        workProgress = 0f;
        
        while (workProgress < 1f)
        {
            workProgress += Time.deltaTime / workTime;
            
            // 작업 중단 체크
            if (currentState != EmployeeState.Working || !target.IsWorkAvailable())
            {
                CancelWork();
                yield break;
            }
            
            yield return null;
        }
        
        // 작업 완료
        target.CompleteWork(this);
        CompleteWork();
    }
    
    private void CompleteWork()
    {
        Debug.Log($"[Employee] {employeeData.employeeName}이(가) {currentWork} 작업을 완료했습니다.");
        
        // WorkManager에 알림
        if (WorkManager.instance != null && currentWorkTarget != null)
        {
            WorkManager.instance.OnWorkerCompletedTarget(this, currentWorkTarget);
        }
        
        currentWorkTarget = null;
        currentWork = WorkType.None;
        workProgress = 0f;
        currentWorkCoroutine = null;
        
        SetState(EmployeeState.Idle);
    }
    
    public void CancelWork()
    {
        if (currentWorkCoroutine != null)
        {
            StopCoroutine(currentWorkCoroutine);
            currentWorkCoroutine = null;
        }
        
        if (currentWorkTarget != null)
        {
            currentWorkTarget.CancelWork(this);
            
            // WorkManager에 알림
            if (WorkManager.instance != null)
            {
                WorkManager.instance.OnWorkerCancelledWork(this);
            }
            
            currentWorkTarget = null;
        }
        
        currentWork = WorkType.None;
        workProgress = 0f;
        
        // 이동 중단
        if (movement != null)
        {
            movement.StopMoving();
        }
        
        SetState(EmployeeState.Idle);
    }
    
    #endregion
    
    #region 욕구 관리
    
    private void RequestFood()
    {
        // 현재 작업 취소하고 식사
        if (currentState == EmployeeState.Working)
        {
            CancelWork();
        }
        
        Debug.Log($"[Employee] {employeeData.employeeName}이(가) 배고픕니다!");
    }
    
    private void RequestRest()
    {
        if (currentState == EmployeeState.Working)
        {
            CancelWork();
        }
        
        SetState(EmployeeState.Resting);
        Debug.Log($"[Employee] {employeeData.employeeName}이(가) 휴식을 취합니다.");
    }
    
    public void Eat(float nutritionValue)
    {
        currentNeeds.hunger += nutritionValue;
        currentNeeds.hunger = Mathf.Clamp(currentNeeds.hunger, 0f, 100f);
        Debug.Log($"[Employee] {employeeData.employeeName}이(가) 식사했습니다. (배고픔: {currentNeeds.hunger:F0}%)");
    }
    
    #endregion
    
    #region 상태 관리
    
    private void SetState(EmployeeState newState)
    {
        if (currentState == newState) return;
        
        currentState = newState;
        OnStateChanged?.Invoke(newState);
        
        // 상태별 시각 효과
        UpdateVisualState();
    }
    
    private void UpdateVisualState()
    {
        // 상태에 따른 색상 변경
        Color color = Color.white;
        
        switch (currentState)
        {
            case EmployeeState.Working:
                color = Color.yellow;
                break;
            case EmployeeState.Resting:
                color = Color.cyan;
                break;
            case EmployeeState.MentalBreak:
                color = Color.magenta;
                break;
            case EmployeeState.Dead:
                color = Color.gray;
                break;
        }
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }
    
    #endregion
    
    #region 작업 우선순위
    
    public void SetWorkPriority(WorkType type, int priority, bool enabled)
    {
        var work = workPriorities.FirstOrDefault(w => w.workType == type);
        if (work != null)
        {
            work.priority = priority;
            work.enabled = enabled;
        }
    }
    
    public List<WorkType> GetEnabledWorkTypes()
    {
        return workPriorities
            .Where(w => w.enabled && CanPerformWork(w.workType))
            .OrderBy(w => w.priority)
            .Select(w => w.workType)
            .ToList();
    }
    
    #endregion
    
    private void ShowDebugStatus()
    {
        string status = $"{employeeData.employeeName}\n";
        status += $"HP: {currentStats.health:F0}/{currentStats.maxHealth} ";
        status += $"Mental: {currentStats.mental:F0}/{currentStats.maxMental}\n";
        status += $"Hunger: {currentNeeds.hunger:F0}% ";
        status += $"Fatigue: {currentNeeds.fatigue:F0}%\n";
        status += $"State: {currentState} Work: {currentWork}";
        
        Debug.Log(status);
    }
    
    // Public 프로퍼티
    public EmployeeData Data => employeeData;
    public EmployeeStats Stats => currentStats;
    public EmployeeNeeds Needs => currentNeeds;
    public EmployeeState State => currentState;
    public WorkType CurrentWork => currentWork;
    public float WorkProgress => workProgress;
}

// 데이터 구조체들
[System.Serializable]
public struct EmployeeStats
{
    public float health;
    public float maxHealth;
    public float mental;
    public float maxMental;
    public int attackPower;
}

[System.Serializable]
public struct EmployeeNeeds
{
    [Range(0, 100)]
    public float hunger;
    [Range(0, 100)]
    public float fatigue;
}

[System.Serializable]
public class WorkPriority
{
    public WorkType workType;
    public int priority;
    public bool enabled;
}

public enum EmployeeState
{
    Idle,
    Moving,
    Working,
    Eating,
    Resting,
    MentalBreak,
    Dead
}

// 작업 대상 인터페이스
public interface IWorkTarget
{
    Vector3 GetWorkPosition();
    WorkType GetWorkType();
    float GetWorkTime();
    bool IsWorkAvailable();
    void CompleteWork(Employee worker);
    void CancelWork(Employee worker);
}