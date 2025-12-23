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
    private WorkOrder currentWorkOrder; // 현재 할당된 작업물
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

        Debug.Log($"[Employee] {name} Awake - Movement 컴포넌트: {(movement != null ? "OK" : "NULL")}");

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
            new WorkPriority { workType = WorkType.Hauling, priority = 6, enabled = true },
            new WorkPriority { workType = WorkType.Building, priority = 7, enabled = true },
            new WorkPriority { workType = WorkType.Demolish, priority = 8, enabled = true }
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
    
        // 정신력이 0 이하면 정신 붕괴
        if (currentStats.mental <= 0f)
        {
            SetState(EmployeeState.MentalBreak);
            return;
        }
    }
    
    #region 작업 시스템

    /// <summary>
    /// 직원의 현재 위치에서 작업 가능한 타일 범위를 반환합니다.
    /// 직원은 세로 2칸 크기이며, 현재 바닥 기준으로:
    /// - 좌우 1칸
    /// - 상위 3칸 (머리 위 1칸 포함)
    /// - 하단 1칸
    /// </summary>
    public List<Vector3Int> GetWorkableRange()
    {
        List<Vector3Int> workablePositions = new List<Vector3Int>();

        // 직원의 발 위치 (바닥 타일) - transform.position은 중심이므로 y-2
        Vector3Int footPosition = new Vector3Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y - 2f),
            0
        );

        // 좌우 1칸, 상위 3칸, 하위 1칸 범위
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 3; dy++)
            {
                Vector3Int targetPos = footPosition + new Vector3Int(dx, dy, 0);

                // 맵 범위 내인지 확인
                if (targetPos.x >= 0 && targetPos.x < GameMap.MAP_WIDTH &&
                    targetPos.y >= 0 && targetPos.y < GameMap.MAP_HEIGHT)
                {
                    workablePositions.Add(targetPos);
                }
            }
        }

        return workablePositions;
    }

    /// <summary>
    /// 특정 위치가 현재 직원의 작업 범위 내에 있는지 확인합니다.
    /// 직원이 서 있는 타일 기준으로 좌우 1칸, 상 3칸, 하 1칸
    /// </summary>
    public bool IsPositionInWorkRange(Vector3Int position)
    {
        // 직원이 서 있는 타일 계산
        // transform.position.y는 직원 중심 (높이 2칸의 중심)
        // 직원 중심 = 서 있는 타일 y + 2, 따라서 서 있는 타일 = 중심 - 2
        Vector3Int standingTile = new Vector3Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y) - 2,
            0
        );

        int dx = Mathf.Abs(position.x - standingTile.x);
        int dy = position.y - standingTile.y;

        // 좌우 1칸 이내, 상위 3칸 ~ 하위 1칸 범위
        bool inRange = dx <= 1 && dy >= -1 && dy <= 3;

        // 항상 로그 출력 (디버그용)
        Debug.Log($"[Employee] {employeeData.employeeName} - 범위 체크: 서있는 타일 {standingTile}, 타겟 {position}, dx={dx}, dy={dy}, inRange={inRange}");

        return inRange;
    }

    public bool CanPerformWork(WorkType type)
    {
        if (employeeData == null || employeeData.abilities == null) return false;

        // 우선순위에서 비활성화된 작업은 불가
        var priority = workPriorities.FirstOrDefault(w => w.workType == type);
        if (priority != null && !priority.enabled) return false;

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
    
    /// <summary>
    /// WorkManager로부터 작업물과 구체적인 작업 대상을 할당받습니다.
    /// </summary>
    public void AssignWork(WorkOrder workOrder, IWorkTarget target)
    {
        if (target == null || workOrder == null ||
            currentState == EmployeeState.Dead || currentState == EmployeeState.MentalBreak)
            return;

        // 현재 작업 취소
        if (currentWorkTarget != null)
        {
            CancelWork();
        }

        currentWorkOrder = workOrder;
        currentWorkTarget = target;

        if (showDebugInfo)
        {
            Debug.Log($"[Employee] {employeeData.employeeName}에게 작업 할당: {target.GetWorkType()} at {target.GetWorkPosition()}");
        }

        // 작업 대상의 타일 위치
        Vector3 targetPos = target.GetWorkPosition();
        Vector3Int targetTilePos = new Vector3Int(
            Mathf.FloorToInt(targetPos.x),
            Mathf.FloorToInt(targetPos.y),
            0
        );

        // 직원의 발 위치 계산 (transform.position은 중심이므로 y-1)
        Vector3Int currentPos = new Vector3Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y - 2f), // 중심에서 발 위치로 변환
            0
        );

        Debug.Log($"[Employee] {employeeData.employeeName} - 현재 발 위치: {currentPos}, 타겟 위치: {targetTilePos}, Transform: {transform.position}");

        bool inRange = IsPositionInWorkRange(targetTilePos);
        Debug.Log($"[Employee] {employeeData.employeeName} - 작업 범위 내: {inRange}");

        if (inRange)
        {
            // 작업 범위 내에 있으면 즉시 작업 시작
            Debug.Log($"[Employee] {employeeData.employeeName}: 작업 범위 내 타겟, 즉시 작업 시작");
            StartWork(target);
        }
        else
        {
            // 작업 범위 밖이면 작업 가능한 위치로 이동
            if (movement != null)
            {
                // 작업 대상 근처의 작업 가능한 위치 찾기
                Vector3 workPosition = FindWorkablePositionForTarget(targetTilePos);

                // 작업 가능한 위치를 찾지 못한 경우
                if (workPosition == Vector3.zero)
                {
                    Debug.LogWarning($"[Employee] {employeeData.employeeName}: 작업 가능한 위치 없음, 작업 취소");
                    CancelWork();
                    return;
                }

                SetState(EmployeeState.Moving);
                Debug.Log($"[Employee] {employeeData.employeeName}: 작업 위치로 이동 {workPosition}");

                movement.MoveTo(workPosition, () => {
                    Debug.Log($"[Employee] {employeeData.employeeName}: 목적지 도착, 작업 시작");
                    StartWork(target);
                });
            }
            else
            {
                // movement가 없으면 작업 불가
                Debug.LogWarning($"[Employee] {employeeData.employeeName}: Movement 컴포넌트 없음!");
                CancelWork();
            }
        }
    }

    /// <summary>
    /// 작업 대상 근처에서 실제로 작업할 수 있는 위치를 찾습니다.
    /// 휴리스틱 정렬: 거리, 도달 가능성, 높이 차이를 고려합니다.
    /// </summary>
    private Vector3 FindWorkablePositionForTarget(Vector3Int targetTilePos)
    {
        // 직원의 발 위치 계산
        Vector3Int currentPos = new Vector3Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y - 2f),
            0
        );

        // TilePathfinder 가져오기
        TilePathfinder pathfinder = null;
        if (MapGenerator.instance != null)
        {
            GameMap gameMap = MapGenerator.instance.GameMapInstance;
            if (gameMap != null)
            {
                pathfinder = new TilePathfinder(gameMap);
            }
        }

        // 작업 가능한 후보 위치들 (타겟 주변에서 타겟을 작업 범위에 넣을 수 있는 위치)
        List<WorkPositionCandidate> candidates = new List<WorkPositionCandidate>();

        // 타겟 기준으로 작업 범위를 만족하는 위치 찾기
        // 직원이 (cx, cy)에 서 있을 때, 타겟 (tx, ty)가 작업 범위 내에 있으려면:
        // |tx - cx| <= 1, -1 <= ty - cy <= 3
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -3; dy <= 1; dy++)
            {
                Vector2Int candidatePos = new Vector2Int(
                    targetTilePos.x + dx,
                    targetTilePos.y + dy
                );

                // 맵 범위 확인
                if (candidatePos.x < 0 || candidatePos.x >= GameMap.MAP_WIDTH ||
                    candidatePos.y < 0 || candidatePos.y >= GameMap.MAP_HEIGHT)
                    continue;

                // 해당 위치에서 타겟이 작업 범위 내인지 확인
                int workDx = Mathf.Abs(targetTilePos.x - candidatePos.x);
                int workDy = targetTilePos.y - candidatePos.y;
                if (workDx > 1 || workDy < -1 || workDy > 3)
                    continue;

                // 해당 위치가 직원이 서 있을 수 있는 유효한 위치인지 확인
                if (pathfinder != null && !pathfinder.IsValidPosition(candidatePos))
                    continue;

                // 현재 위치와 같으면 스킵 (이미 작업 범위 체크에서 걸러짐)
                Vector2Int startPos = new Vector2Int(currentPos.x, currentPos.y);
                if (candidatePos == startPos)
                    continue;

                // 경로가 존재하는지 확인 - 경로가 있어야만 후보로 추가
                List<Vector2Int> path = pathfinder?.FindPath(startPos, candidatePos);
                if (path == null || path.Count == 0)
                    continue;

                // 후보에 추가 (경로가 있는 것만)
                float distance = Vector2Int.Distance(startPos, candidatePos);
                int heightDiff = Mathf.Abs(candidatePos.y - currentPos.y);

                candidates.Add(new WorkPositionCandidate
                {
                    position = candidatePos,
                    distance = distance,
                    heightDiff = heightDiff,
                    pathLength = path.Count
                });
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[Employee] 작업 가능한 위치를 찾을 수 없음: {targetTilePos}");
            return Vector3.zero; // 작업 불가능 표시
        }

        // 휴리스틱 정렬:
        // 1. 경로 길이가 짧은 것 우선
        // 2. 높이 차이가 적은 것 우선
        // 3. 거리가 가까운 것 우선
        var sortedCandidates = candidates
            .OrderBy(c => c.pathLength)
            .ThenBy(c => c.heightDiff)
            .ThenBy(c => c.distance)
            .ToList();

        Vector2Int bestPos = sortedCandidates[0].position;

        if (showDebugInfo)
        {
            Debug.Log($"[Employee] 작업 위치 선택: {bestPos} (타겟: {targetTilePos}, 후보: {candidates.Count}개)");
        }

        // 타일 위로 변환 (직원 중심이 y + 2가 되도록)
        return new Vector3(bestPos.x + 0.5f, bestPos.y + 2f, 0);
    }

    /// <summary>
    /// 작업 위치 후보 정보
    /// </summary>
    private struct WorkPositionCandidate
    {
        public Vector2Int position;
        public float distance;
        public int heightDiff;
        public int pathLength;
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
        
        if (showDebugInfo)
        {
            Debug.Log($"[Employee] {employeeData.employeeName} 작업 시작: {currentWork}, 예상 시간: {workTime:F1}초");
        }
        
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
                if (showDebugInfo)
                {
                    Debug.Log($"[Employee] {employeeData.employeeName} 작업 중단됨");
                }
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
        if (showDebugInfo)
        {
            Debug.Log($"[Employee] {employeeData.employeeName}이(가) {currentWork} 작업을 완료했습니다.");
        }

        // WorkManager에 알림 (다음 작업 할당을 위해 currentWorkOrder 유지)
        if (WorkManager.instance != null && currentWorkTarget != null && currentWorkOrder != null)
        {
            // 임시로 저장
            IWorkTarget completedTarget = currentWorkTarget;
            WorkOrder completedOrder = currentWorkOrder;

            // 현재 작업 대상만 초기화 (WorkOrder는 유지)
            currentWorkTarget = null;
            currentWork = WorkType.None;
            workProgress = 0f;
            currentWorkCoroutine = null;

            // WorkManager에 알림 (다음 작업 할당 시도)
            WorkManager.instance.OnWorkerCompletedTarget(this, completedTarget, completedOrder);

            // WorkManager가 다음 작업을 할당하지 않았으면 완전히 초기화
            if (currentWorkTarget == null && currentWorkOrder == completedOrder)
            {
                currentWorkOrder = null;
                SetState(EmployeeState.Idle);
            }
        }
        else
        {
            // WorkManager가 없으면 즉시 초기화
            currentWorkTarget = null;
            currentWorkOrder = null;
            currentWork = WorkType.None;
            workProgress = 0f;
            currentWorkCoroutine = null;
            SetState(EmployeeState.Idle);
        }
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
            currentWorkOrder = null;
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
        
        if (showDebugInfo)
        {
            Debug.Log($"[Employee] {employeeData.employeeName}이(가) 배고픕니다!");
        }
    }
    
    private void RequestRest()
    {
        if (currentState == EmployeeState.Working)
        {
            CancelWork();
        }
        
        SetState(EmployeeState.Resting);
        
        if (showDebugInfo)
        {
            Debug.Log($"[Employee] {employeeData.employeeName}이(가) 휴식을 취합니다.");
        }
    }
    
    public void Eat(float nutritionValue)
    {
        currentNeeds.hunger += nutritionValue;
        currentNeeds.hunger = Mathf.Clamp(currentNeeds.hunger, 0f, 100f);
        
        if (showDebugInfo)
        {
            Debug.Log($"[Employee] {employeeData.employeeName}이(가) 식사했습니다. (배고픔: {currentNeeds.hunger:F0}%)");
        }
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
            case EmployeeState.Moving:
                color = Color.cyan;
                break;
            case EmployeeState.Resting:
                color = new Color(0.5f, 0.5f, 1f);
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
    
    public int GetWorkPriority(WorkType type)
    {
        var work = workPriorities.FirstOrDefault(w => w.workType == type);
        return work != null ? work.priority : 999;
    }
    
    #endregion
    
    private void ShowDebugStatus()
    {
        if (Time.frameCount % 60 != 0) return; // 1초마다만 출력
        
        string status = $"[{employeeData.employeeName}] ";
        status += $"State:{currentState} Work:{currentWork} ";
        status += $"HP:{currentStats.health:F0}/{currentStats.maxHealth} ";
        status += $"Mental:{currentStats.mental:F0}/{currentStats.maxMental} ";
        status += $"Hunger:{currentNeeds.hunger:F0}% Fatigue:{currentNeeds.fatigue:F0}%";
        
        if (currentWorkTarget != null)
        {
            status += $" | Target:{currentWorkTarget.GetWorkPosition()}";
        }
        
        Debug.Log(status);
    }
    
    // Public 프로퍼티
    public EmployeeData Data => employeeData;
    public EmployeeStats Stats => currentStats;
    public EmployeeNeeds Needs => currentNeeds;
    public EmployeeState State => currentState;
    public WorkType CurrentWork => currentWork;
    public float WorkProgress => workProgress;
    public bool IsAvailableForWork => currentState == EmployeeState.Idle;
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