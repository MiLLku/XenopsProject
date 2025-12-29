using System;
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
    [SerializeField] private bool showDebugInfo = true;
    
    // 컴포넌트 참조
    private SpriteRenderer spriteRenderer;
    private EmployeeAI aiController;
    private EmployeeMovement movement;
    
    // 작업 관련
    private IWorkTarget currentWorkTarget;
    private WorkOrder currentWorkOrder;
    private float workProgress = 0f;
    private Coroutine currentWorkCoroutine;
    
    // 특성 효과 캐시
    private float cachedHealthModifier = 1f;
    private float cachedMentalModifier = 1f;
    private float cachedWorkSpeedModifier = 1f;
    private float cachedHungerRateModifier = 1f;
    private float cachedFatigueRateModifier = 1f;
    
    // 직원 높이 상수
    private const int EMPLOYEE_HEIGHT = 2;
    
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
        
        if (WorkSystemManager.instance != null)
        {
            WorkSystemManager.instance.RegisterEmployee(this);
        }
    }
    
    void OnDestroy()
    {
        if (WorkSystemManager.instance != null)
        {
            WorkSystemManager.instance.UnregisterEmployee(this);
        }
    }
    
    void Update()
    {
        if (currentState == EmployeeState.Dead) return;
        
        UpdateNeeds(Time.deltaTime);
        CheckCriticalNeeds();
        
        if (showDebugInfo)
        {
            ShowDebugStatus();
        }
    }
    
    public void Initialize(EmployeeData data)
    {
        employeeData = data;
        name = $"Employee_{data.employeeName}";
        
        CalculateTraitModifiers();
        
        currentStats = new EmployeeStats
        {
            health = Mathf.RoundToInt(data.maxHealth * cachedHealthModifier),
            maxHealth = Mathf.RoundToInt(data.maxHealth * cachedHealthModifier),
            mental = Mathf.RoundToInt(data.maxMental * cachedMentalModifier),
            maxMental = Mathf.RoundToInt(data.maxMental * cachedMentalModifier),
            attackPower = Mathf.RoundToInt(data.attackPower * (1f + GetTraitAttackModifier()))
        };
        
        currentNeeds = new EmployeeNeeds
        {
            hunger = 100f,
            fatigue = 100f
        };
        
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
        float hungerDecay = employeeData.hungerDecayRate * cachedHungerRateModifier;
        currentNeeds.hunger -= hungerDecay * deltaTime;
        currentNeeds.hunger = Mathf.Clamp(currentNeeds.hunger, 0f, 100f);
        
        if (currentState == EmployeeState.Working)
        {
            float fatigueIncrease = employeeData.fatigueIncreaseRate * cachedFatigueRateModifier;
            currentNeeds.fatigue -= fatigueIncrease * deltaTime;
            currentNeeds.fatigue = Mathf.Clamp(currentNeeds.fatigue, 0f, 100f);
        }
        else if (currentState == EmployeeState.Resting)
        {
            currentNeeds.fatigue += 10f * deltaTime;
            currentNeeds.fatigue = Mathf.Clamp(currentNeeds.fatigue, 0f, 100f);
        }
        
        if (currentNeeds.hunger <= 0f)
        {
            currentStats.health -= 1f * deltaTime;
            currentStats.mental -= 2f * deltaTime;
        }
        
        if (currentNeeds.fatigue <= 0f)
        {
            currentStats.mental -= 3f * deltaTime;
        }
        
        currentStats.health = Mathf.Clamp(currentStats.health, 0f, currentStats.maxHealth);
        currentStats.mental = Mathf.Clamp(currentStats.mental, 0f, currentStats.maxMental);
        
        OnNeedsChanged?.Invoke(currentNeeds);
        OnStatsChanged?.Invoke(currentStats);
    }
    
    private void CheckCriticalNeeds()
    {
        if (currentStats.health <= 0f)
        {
            SetState(EmployeeState.Dead);
            return;
        }
    
        if (currentStats.mental <= 0f)
        {
            SetState(EmployeeState.MentalBreak);
            return;
        }
    }
    
    #region 작업 시스템

    /// <summary>
    /// 직원의 현재 발 위치 타일 좌표를 반환합니다.
    /// 피벗이 Bottom Left이지만, 시각적 위치와 맞추기 위해 반올림 사용
    /// </summary>
    public Vector3Int GetFootTile()
    {
        return new Vector3Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y),
            0
        );
    }

    /// <summary>
    /// 직원의 현재 위치에서 작업 가능한 타일 범위를 반환합니다.
    /// 직원 발 기준으로: 좌우 1칸, 상위 3칸, 하단 1칸
    /// </summary>
    public List<Vector3Int> GetWorkableRange()
    {
        List<Vector3Int> workablePositions = new List<Vector3Int>();
        Vector3Int footPosition = GetFootTile();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 3; dy++)
            {
                Vector3Int targetPos = footPosition + new Vector3Int(dx, dy, 0);

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
    /// 중간에 고체 타일이 있으면 작업 불가합니다.
    /// </summary>
    public bool IsPositionInWorkRange(Vector3Int targetPosition)
    {
        Vector3Int standingTile = GetFootTile();

        int dx = Mathf.Abs(targetPosition.x - standingTile.x);
        int dy = targetPosition.y - standingTile.y;

        // ★ 디버그: 항상 로그 출력
        Debug.Log($"[Employee] {employeeData?.employeeName} 범위체크: " +
                  $"transform.pos=({transform.position.x:F1},{transform.position.y:F1}), " +
                  $"발위치={standingTile}, 타겟={targetPosition}, " +
                  $"dx={dx}, dy={dy} (양수=위, 음수=아래)");

        // 1. 기본 범위 체크: 좌우 1칸, 아래 1칸 ~ 위 3칸
        bool inRange = dx <= 1 && dy >= -1 && dy <= 3;
        
        if (!inRange)
        {
            Debug.LogWarning($"[Employee] {employeeData?.employeeName} - ★범위 밖! (허용: dx<=1, dy:-1~3, 실제: dx={dx}, dy={dy})");
            return false;
        }
        
        // 2. 시야 체크: 직원과 타겟 사이에 고체 타일이 있으면 작업 불가
        // (타겟 자체는 고체여도 됨 - 그걸 파는 거니까)
        if (!HasLineOfSight(standingTile, targetPosition))
        {
            Debug.LogWarning($"[Employee] {employeeData?.employeeName} - ★시야 차단됨: {standingTile} -> {targetPosition}");
            return false;
        }

        Debug.Log($"[Employee] {employeeData?.employeeName} - 범위 OK");

        return true;
    }
    
    /// <summary>
    /// 직원 위치에서 타겟까지 시야가 확보되는지 확인합니다.
    /// 중간에 고체 타일이 있으면 false를 반환합니다.
    /// 직원이 서 있는 타일 아래로는 시야가 확보되지 않습니다.
    /// </summary>
    private bool HasLineOfSight(Vector3Int from, Vector3Int to)
    {
        GameMap gameMap = MapGenerator.instance?.GameMapInstance;
        if (gameMap == null) return true;
        
        // 직원의 몸통 영역 (발 위치, 발 위치 + 1, 발 위치 + 2)
        int footY = from.y;
        int bodyY1 = from.y + 1;
        int bodyY2 = from.y + 2;
        
        // 같은 X 좌표인 경우: 수직 방향 체크
        if (from.x == to.x)
        {
            // 위쪽 타겟 (머리 위)
            if (to.y > bodyY2)
            {
                for (int y = bodyY2 + 1; y < to.y; y++)
                {
                    if (IsSolidTile(gameMap, from.x, y))
                        return false;
                }
            }
            // 아래쪽 타겟 (발 아래)
            else if (to.y < footY)
            {
                // 직원이 서 있는 발 위치 타일이 고체이면, 
                // 그 아래로는 시야가 확보되지 않음!
                // (자신이 밟고 있는 타일을 통과해서 작업할 수 없음)
                if (IsSolidTile(gameMap, from.x, footY))
                {
                    return false;
                }
                
                // 발 아래부터 타겟까지 중간 타일 체크
                for (int y = footY - 1; y > to.y; y--)
                {
                    if (IsSolidTile(gameMap, from.x, y))
                        return false;
                }
            }
            // 몸통 높이의 타겟 (발 ~ 머리 사이)은 항상 접근 가능
        }
        // 다른 X 좌표인 경우: 옆에서 접근
        else
        {
            int targetX = to.x;
            
            // 타겟이 몸통 높이 범위 내인 경우 (footY <= to.y <= bodyY2)
            if (to.y >= footY && to.y <= bodyY2)
            {
                // 바로 옆이면 OK
                return true;
            }
            
            // 타겟이 위쪽인 경우
            if (to.y > bodyY2)
            {
                for (int y = bodyY2 + 1; y < to.y; y++)
                {
                    if (IsSolidTile(gameMap, targetX, y))
                        return false;
                }
            }
            // 타겟이 아래쪽인 경우 (발 아래)
            else if (to.y < footY)
            {
                // 대각선 아래 작업: 옆 타일의 발 높이부터 체크
                for (int y = footY; y > to.y; y--)
                {
                    if (IsSolidTile(gameMap, targetX, y))
                        return false;
                }
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// 특정 위치가 고체 타일인지 확인합니다.
    /// </summary>
    private bool IsSolidTile(GameMap gameMap, int x, int y)
    {
        if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT)
            return false;
        
        return gameMap.TileGrid[x, y] != 0; // 0 = AIR
    }

    public bool CanPerformWork(WorkType type)
    {
        if (employeeData == null || employeeData.abilities == null) return false;

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

        Vector3Int currentFootTile = GetFootTile();

        if (showDebugInfo)
        {
            Debug.Log($"[Employee] {employeeData.employeeName} - 현재 발 위치: {currentFootTile}, 타겟 위치: {targetTilePos}");
        }

        bool inRange = IsPositionInWorkRange(targetTilePos);

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
                Vector3 workPosition = FindWorkablePositionForTarget(targetTilePos);

                if (workPosition == Vector3.zero)
                {
                    Debug.LogWarning($"[Employee] {employeeData.employeeName}: 작업 가능한 위치 없음, 작업 취소");
                    CancelWork();
                    return;
                }

                SetState(EmployeeState.Moving);
                Debug.Log($"[Employee] {employeeData.employeeName}: 작업 위치로 이동 {workPosition}");

                // 낙하 후 재시도를 위한 플래그
                bool isRetrying = false;
                
                // 착지 이벤트 핸들러 (낙하 후 재시도)
                Action<Vector2Int> onLandedHandler = null;
                onLandedHandler = (landedTile) => {
                    // 핸들러 제거
                    movement.OnLanded -= onLandedHandler;
                    
                    // 재시도 중이거나 이미 다른 상태면 무시
                    if (isRetrying || currentWorkTarget != target)
                        return;
                    
                    isRetrying = true;
                    
                    Debug.Log($"[Employee] {employeeData.employeeName}: 착지 완료, 작업 재시도");
                    
                    // 착지 후 작업 범위 내인지 확인
                    if (IsPositionInWorkRange(targetTilePos))
                    {
                        StartWork(target);
                    }
                    else
                    {
                        // 새 위치에서 다시 경로 탐색
                        Vector3 newWorkPosition = FindWorkablePositionForTarget(targetTilePos);
                        if (newWorkPosition != Vector3.zero)
                        {
                            isRetrying = false;
                            movement.OnLanded += onLandedHandler; // 다시 등록
                            
                            movement.MoveTo(newWorkPosition,
                                onComplete: () => {
                                    movement.OnLanded -= onLandedHandler;
                                    if (IsPositionInWorkRange(targetTilePos))
                                    {
                                        StartWork(target);
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"[Employee] {employeeData.employeeName}: 도착했지만 작업 범위 밖, 작업 취소");
                                        CancelWork();
                                    }
                                },
                                onFailed: () => {
                                    // 재시도 실패 - 대기 상태로
                                    Debug.LogWarning($"[Employee] {employeeData.employeeName}: 재시도 이동 실패");
                                    // 취소하지 않고 대기 (다음 프레임에 다시 시도될 수 있음)
                                    SetState(EmployeeState.Idle);
                                }
                            );
                        }
                        else
                        {
                            Debug.LogWarning($"[Employee] {employeeData.employeeName}: 착지 후 작업 가능 위치 없음, 작업 취소");
                            CancelWork();
                        }
                    }
                };
                
                // 착지 이벤트 등록
                movement.OnLanded += onLandedHandler;

                movement.MoveTo(workPosition, 
                    onComplete: () => {
                        // 이동 성공 - 착지 핸들러 제거
                        movement.OnLanded -= onLandedHandler;
                        
                        Debug.Log($"[Employee] {employeeData.employeeName}: 목적지 도착, 작업 시작");
                        
                        // 도착 후 다시 범위 확인
                        if (IsPositionInWorkRange(targetTilePos))
                        {
                            StartWork(target);
                        }
                        else
                        {
                            Debug.LogWarning($"[Employee] {employeeData.employeeName}: 도착했지만 작업 범위 밖, 작업 취소");
                            CancelWork();
                        }
                    },
                    onFailed: () => {
                        // 이동 실패 (경로 없음 등) - 착지 핸들러는 유지
                        // 낙하로 인한 실패인 경우 OnLanded에서 재시도됨
                        Debug.LogWarning($"[Employee] {employeeData.employeeName}: 이동 실패 (낙하 시 재시도 대기)");
                        // CancelWork() 호출하지 않음 - 착지 후 재시도
                    }
                );
            }
            else
            {
                Debug.LogWarning($"[Employee] {employeeData.employeeName}: Movement 컴포넌트 없음!");
                CancelWork();
            }
        }
    }

    /// <summary>
    /// 작업 대상 근처에서 실제로 작업할 수 있는 위치를 찾습니다.
    /// </summary>
    private Vector3 FindWorkablePositionForTarget(Vector3Int targetTilePos)
    {
        Vector3Int currentFootTile = GetFootTile();
        Vector2Int startPos = new Vector2Int(currentFootTile.x, currentFootTile.y);

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

        if (pathfinder == null)
        {
            Debug.LogError("[Employee] Pathfinder를 생성할 수 없습니다!");
            return Vector3.zero;
        }

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
                if (!pathfinder.IsValidPosition(candidatePos))
                    continue;

                // 현재 위치와 같으면 스킵
                if (candidatePos == startPos)
                    continue;

                // 경로가 존재하는지 확인
                List<Vector2Int> path = pathfinder.FindPath(startPos, candidatePos);
                if (path == null || path.Count == 0)
                    continue;

                float distance = Vector2Int.Distance(startPos, candidatePos);
                int heightDiff = Mathf.Abs(candidatePos.y - currentFootTile.y);

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
            return Vector3.zero;
        }

        // 휴리스틱 정렬
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

        // 타일 좌표를 월드 좌표로 변환
        // 피벗 = Bottom-Left이므로, 발 위치 타일 = transform.position
        return new Vector3(bestPos.x, bestPos.y, 0);
    }

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
            Debug.LogWarning($"[Employee] {employeeData.employeeName}: 작업 대상이 더 이상 유효하지 않음");
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
            
            // ★ 추가: 작업 중에도 범위 확인 (직원이 밀려났을 수 있음)
            Vector3 targetPos = target.GetWorkPosition();
            Vector3Int targetTilePos = new Vector3Int(
                Mathf.FloorToInt(targetPos.x),
                Mathf.FloorToInt(targetPos.y),
                0
            );
            
            if (!IsPositionInWorkRange(targetTilePos))
            {
                Debug.LogWarning($"[Employee] {employeeData.employeeName}: 작업 중 범위 이탈, 작업 취소");
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

        if (WorkSystemManager.instance != null && currentWorkTarget != null && currentWorkOrder != null)
        {
            IWorkTarget completedTarget = currentWorkTarget;
            WorkOrder completedOrder = currentWorkOrder;

            currentWorkTarget = null;
            currentWork = WorkType.None;
            workProgress = 0f;
            currentWorkCoroutine = null;

            WorkSystemManager.instance.OnWorkerCompletedTarget(this, completedTarget, completedOrder);

            if (currentWorkTarget == null && currentWorkOrder == completedOrder)
            {
                currentWorkOrder = null;
                SetState(EmployeeState.Idle);
            }
        }
        else
        {
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
            
            if (WorkSystemManager.instance != null)
            {
                WorkSystemManager.instance.OnWorkerCancelledWork(this);
            }
            
            currentWorkTarget = null;
            currentWorkOrder = null;
        }
        
        currentWork = WorkType.None;
        workProgress = 0f;
        
        if (movement != null)
        {
            movement.StopMoving();
        }
        
        SetState(EmployeeState.Idle);
    }
    
    #endregion
    
    #region 욕구 관리
    
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
        
        UpdateVisualState();
    }
    
    private void UpdateVisualState()
    {
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
        if (Time.frameCount % 60 != 0) return;
        
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