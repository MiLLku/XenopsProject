using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 직원 작업 시스템 컴포넌트.
/// 작업 할당/실행/완료/취소, 우선순위 관리, 동적 비자격 리스트를 담당합니다.
///
/// 작업 흐름:
///   AssignWork → FindWorkablePosition → MoveTo → StartWork → PerformWork → CompleteWork
///
/// 비자격 시스템:
///   기본 능력(canXxx) + 우선순위(enabled) + 동적 비자격 리스트 + 정신이벤트(RefuseWork)
///   → 모두 통과해야 CanPerformWork() = true
/// </summary>
public class EmployeeWork : MonoBehaviour
{
    #region 상수

    /// <summary>직원 높이 (타일 단위)</summary>
    private const int EMPLOYEE_HEIGHT = 2;

    /// <summary>작업 우선순위 기본 최대값 (미할당)</summary>
    private const int DEFAULT_MAX_PRIORITY = 999;

    #endregion

    #region 필드

    [Header("작업 능력 (성장으로 변경 가능)")]
    [SerializeField] private WorkAbilities runtimeAbilities;

    [Header("작업 우선순위")]
    [SerializeField] private List<WorkPriority> workPriorities;

    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = true;

    /// <summary>현재 작업 대상</summary>
    private IWorkTarget currentWorkTarget;

    /// <summary>현재 작업 주문</summary>
    private WorkOrder currentWorkOrder;

    /// <summary>현재 작업 타입</summary>
    private WorkType currentWork = WorkType.None;

    /// <summary>현재 작업 진행도 (0~1)</summary>
    private float workProgress = 0f;

    /// <summary>현재 작업 코루틴</summary>
    private Coroutine currentWorkCoroutine;

    /// <summary>동적 비자격 리스트</summary>
    [SerializeField] private List<DisqualificationEntry> disqualifications = new List<DisqualificationEntry>();

    // 컴포넌트 참조
    private Employee employee;
    private EmployeeStatsController statsController;
    private EmployeeMovement movement;
    private EmployeeMental mental;
    private EmployeeGrowth growth;

    #endregion

    #region 프로퍼티

    /// <summary>현재 수행 중인 작업 타입</summary>
    public WorkType CurrentWork => currentWork;

    /// <summary>현재 작업 진행도 (0~1)</summary>
    public float WorkProgress => workProgress;

    /// <summary>작업 할당 가능 여부 (Idle 상태일 때)</summary>
    public bool IsAvailableForWork => employee != null && employee.State == EmployeeState.Idle;

    /// <summary>현재 작업 주문</summary>
    public WorkOrder CurrentWorkOrder => currentWorkOrder;

    /// <summary>현재 작업 대상</summary>
    public IWorkTarget CurrentWorkTarget => currentWorkTarget;

    /// <summary>작업 능력 (런타임 값 우선, 없으면 템플릿 복사)</summary>
    public WorkAbilities Abilities
    {
        get
        {
            if (runtimeAbilities == null && employee?.Data?.abilities != null)
            {
                runtimeAbilities = CopyAbilities(employee.Data.abilities);
            }
            return runtimeAbilities ?? new WorkAbilities();
        }
    }

    #endregion

    #region 초기화

    void Awake()
    {
        employee = GetComponent<Employee>();
        statsController = GetComponent<EmployeeStatsController>();
        movement = GetComponent<EmployeeMovement>();
        mental = GetComponent<EmployeeMental>();
        growth = GetComponent<EmployeeGrowth>();
    }

    /// <summary>
    /// 작업 시스템을 초기화합니다.
    /// </summary>
    public void Initialize(EmployeeData data)
    {
        runtimeAbilities = CopyAbilities(data?.abilities);
        InitializeWorkPriorities();
        disqualifications.Clear();
    }

    /// <summary>
    /// WorkAbilities 복사 (런타임 수정용)
    /// </summary>
    private WorkAbilities CopyAbilities(WorkAbilities source)
    {
        if (source == null) return new WorkAbilities();

        return new WorkAbilities
        {
            canMine = source.canMine,
            canChop = source.canChop,
            canResearch = source.canResearch,
            canCraft = source.canCraft,
            canGarden = source.canGarden,
            canBuild = source.canBuild,
            canHaul = source.canHaul,
            canDemolish = source.canDemolish,
            miningSpeed = source.miningSpeed,
            choppingSpeed = source.choppingSpeed,
            researchSpeed = source.researchSpeed,
            craftingSpeed = source.craftingSpeed,
            gardeningSpeed = source.gardeningSpeed,
            buildingSpeed = source.buildingSpeed,
            haulingSpeed = source.haulingSpeed,
            demolishSpeed = source.demolishSpeed
        };
    }

    /// <summary>
    /// 작업 우선순위를 기본값으로 초기화합니다.
    /// </summary>
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

    #endregion

    #region 비자격 시스템

    /// <summary>
    /// 동적 비자격을 추가합니다 (특성/이벤트/부상 등).
    /// </summary>
    /// <param name="workType">비자격 작업 타입</param>
    /// <param name="reason">비자격 사유 (UI 표시용)</param>
    public void AddDisqualification(WorkType workType, string reason = "")
    {
        if (IsDisqualified(workType)) return;

        disqualifications.Add(new DisqualificationEntry { workType = workType, reason = reason });
        Debug.Log($"[Work] {employee?.DisplayName}: {workType} 비자격 추가 ({reason})");

        // 현재 해당 타입 작업 중이면 취소
        if (currentWork == workType)
        {
            CancelWork();
        }
    }

    /// <summary>
    /// 동적 비자격을 제거합니다.
    /// </summary>
    public void RemoveDisqualification(WorkType workType)
    {
        int removed = disqualifications.RemoveAll(d => d.workType == workType);
        if (removed > 0)
        {
            Debug.Log($"[Work] {employee?.DisplayName}: {workType} 비자격 해제");
        }
    }

    /// <summary>
    /// 특정 작업 타입이 비자격 상태인지 확인합니다.
    /// </summary>
    public bool IsDisqualified(WorkType workType)
    {
        return disqualifications.Any(d => d.workType == workType);
    }

    /// <summary>
    /// 현재 비자격 목록을 반환합니다.
    /// </summary>
    public IReadOnlyList<DisqualificationEntry> GetDisqualifications()
    {
        return disqualifications.AsReadOnly();
    }

    #endregion

    #region 작업 능력 판단

    /// <summary>
    /// 특정 작업을 수행할 수 있는지 확인합니다.
    /// 기본 능력 + 우선순위 활성 + 비자격 아님 + 정신이벤트 거부 아님
    /// </summary>
    public bool CanPerformWork(WorkType type)
    {
        // 1. 기본 능력 체크
        WorkAbilities abilities = Abilities;
        if (abilities == null || !abilities.CanPerformWork(type)) return false;

        // 2. 우선순위에서 비활성
        var priority = workPriorities?.FirstOrDefault(w => w.workType == type);
        if (priority != null && !priority.enabled) return false;

        // 3. 동적 비자격
        if (IsDisqualified(type)) return false;

        // 4. 정신 이벤트로 작업 거부 중
        if (mental != null && mental.IsRefusingWork) return false;

        return true;
    }

    /// <summary>
    /// 특정 작업 타입의 작업 속도를 반환합니다.
    /// 기본속도 × 특성보정 × 피로보정 × 글로벌보정 × 정신이벤트보정
    /// </summary>
    public float GetWorkSpeed(WorkType type)
    {
        if (!CanPerformWork(type)) return 0f;

        WorkAbilities abilities = Abilities;
        float baseSpeed = abilities.GetWorkSpeed(type);
        float traitModifier = GetTraitWorkSpeedModifier(type);
        float fatigueModifier = statsController != null ? statsController.GetFatigueModifier() : 1f;
        float globalModifier = statsController != null ? statsController.CachedWorkSpeedModifier : 1f;
        float mentalModifier = mental != null ? mental.GetActiveSpeedModifier() : 1f;

        return baseSpeed * traitModifier * fatigueModifier * globalModifier * mentalModifier;
    }

    /// <summary>
    /// 특성에 의한 작업 타입별 속도 보정을 반환합니다.
    /// </summary>
    private float GetTraitWorkSpeedModifier(WorkType type)
    {
        float modifier = 1f;
        EmployeeData data = employee?.Data;
        if (data?.traits == null) return modifier;

        foreach (var trait in data.traits)
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

    #endregion

    #region 작업 할당 및 실행

    /// <summary>
    /// 직원의 현재 발 위치 타일 좌표를 반환합니다.
    /// </summary>
    public Vector3Int GetFootTile()
    {
        return new Vector3Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y),
            0
        );
    }

    /// <summary>
    /// WorkManager로부터 작업물과 구체적인 작업 대상을 할당받습니다.
    /// </summary>
    public void AssignWork(WorkOrder workOrder, IWorkTarget target)
    {
        if (target == null || workOrder == null ||
            employee.State == EmployeeState.Dead || employee.State == EmployeeState.MentalBreak)
            return;

        if (currentWorkTarget != null)
        {
            CancelWork();
        }

        currentWorkOrder = workOrder;
        currentWorkTarget = target;

        if (showDebugInfo)
        {
            Debug.Log($"[Work] {employee.DisplayName}에게 작업 할당: {target.GetWorkType()} at {target.GetWorkPosition()}");
        }

        Vector3 targetPos = target.GetWorkPosition();
        Vector3Int targetTilePos = new Vector3Int(
            Mathf.FloorToInt(targetPos.x),
            Mathf.FloorToInt(targetPos.y),
            0
        );

        Vector3Int currentFootTile = GetFootTile();

        bool inRange = IsPositionInWorkRange(targetTilePos);

        if (inRange)
        {
            StartWork(target);
        }
        else
        {
            if (movement != null)
            {
                Vector3 workPosition = FindWorkablePositionForTarget(targetTilePos);

                if (workPosition == Vector3.zero)
                {
                    Debug.LogWarning($"[Work] {employee.DisplayName}: 작업 가능한 위치 없음, 작업 취소");
                    CancelWork();
                    return;
                }

                employee.SetState(EmployeeState.Moving);

                bool isRetrying = false;

                Action<Vector2Int> onLandedHandler = null;
                onLandedHandler = (landedTile) =>
                {
                    movement.OnLanded -= onLandedHandler;

                    if (isRetrying || currentWorkTarget != target)
                        return;

                    isRetrying = true;

                    if (IsPositionInWorkRange(targetTilePos))
                    {
                        StartWork(target);
                    }
                    else
                    {
                        Vector3 newWorkPosition = FindWorkablePositionForTarget(targetTilePos);
                        if (newWorkPosition != Vector3.zero)
                        {
                            isRetrying = false;
                            movement.OnLanded += onLandedHandler;

                            movement.MoveTo(newWorkPosition,
                                onComplete: () =>
                                {
                                    movement.OnLanded -= onLandedHandler;
                                    if (IsPositionInWorkRange(targetTilePos))
                                        StartWork(target);
                                    else
                                        CancelWork();
                                },
                                onFailed: () =>
                                {
                                    employee.SetState(EmployeeState.Idle);
                                }
                            );
                        }
                        else
                        {
                            CancelWork();
                        }
                    }
                };

                movement.OnLanded += onLandedHandler;

                movement.MoveTo(workPosition,
                    onComplete: () =>
                    {
                        movement.OnLanded -= onLandedHandler;

                        if (IsPositionInWorkRange(targetTilePos))
                            StartWork(target);
                        else
                            CancelWork();
                    },
                    onFailed: () =>
                    {
                        Debug.LogWarning($"[Work] {employee.DisplayName}: 이동 실패 (낙하 시 재시도 대기)");
                    }
                );
            }
            else
            {
                CancelWork();
            }
        }
    }

    /// <summary>
    /// 제작 작업을 할당합니다 (생산 건물용).
    /// </summary>
    public void AssignCraftingWork(CraftingOrder craftingOrder, Vector3 workPosition)
    {
        if (craftingOrder == null) return;

        if (employee.State == EmployeeState.Dead || employee.State == EmployeeState.MentalBreak)
            return;

        if (currentWorkTarget != null)
        {
            CancelWork();
        }

        currentWorkOrder = craftingOrder;
        currentWorkTarget = craftingOrder;
        currentWork = WorkType.Crafting;

        if (movement != null)
        {
            employee.SetState(EmployeeState.Moving);

            movement.MoveTo(workPosition,
                onComplete: () =>
                {
                    if (currentWorkTarget == craftingOrder)
                        StartCraftingWork(craftingOrder);
                },
                onFailed: () =>
                {
                    CancelWork();
                }
            );
        }
        else
        {
            StartCraftingWork(craftingOrder);
        }
    }

    private void StartWork(IWorkTarget target)
    {
        if (target == null || !target.IsWorkAvailable())
        {
            CompleteWork();
            return;
        }

        employee.SetState(EmployeeState.Working);
        currentWork = target.GetWorkType();

        float speed = GetWorkSpeed(currentWork);
        if (speed <= 0f)
        {
            Debug.LogWarning($"[Work] {employee.DisplayName}: 작업 속도 0 이하, 작업 취소");
            CancelWork();
            return;
        }
        float workTime = target.GetWorkTime() / speed;

        currentWorkCoroutine = StartCoroutine(PerformWork(target, workTime));
    }

    private IEnumerator PerformWork(IWorkTarget target, float workTime)
    {
        workProgress = 0f;

        while (workProgress < 1f)
        {
            workProgress += Time.deltaTime / workTime;

            if (employee.State != EmployeeState.Working || !target.IsWorkAvailable())
            {
                CancelWork();
                yield break;
            }

            yield return null;
        }

        target.CompleteWork(employee);
        CompleteWork();
    }

    private void StartCraftingWork(CraftingOrder craftingOrder)
    {
        if (craftingOrder == null || !craftingOrder.IsWorkAvailable())
        {
            CancelWork();
            return;
        }

        employee.SetState(EmployeeState.Working);
        currentWork = WorkType.Crafting;
        workProgress = 0f;

        craftingOrder.StartWorking();
        currentWorkCoroutine = StartCoroutine(CraftingWorkCoroutine(craftingOrder));
    }

    private IEnumerator CraftingWorkCoroutine(CraftingOrder craftingOrder)
    {
        float workSpeed = GetWorkSpeed(WorkType.Crafting);
        if (workSpeed <= 0f)
        {
            CancelWork();
            yield break;
        }

        while (craftingOrder != null && craftingOrder.IsWorkAvailable())
        {
            float deltaTime = Time.deltaTime * workSpeed;
            craftingOrder.UpdateProgress(deltaTime);
            workProgress = craftingOrder.CraftingProgress;

            if (craftingOrder.CraftingProgress >= 1f)
            {
                int expGain = Mathf.CeilToInt(craftingOrder.GetWorkTime() * 2f);
                if (growth != null) growth.GainExperience(expGain);

                currentWorkTarget = null;
                currentWorkOrder = null;
                currentWork = WorkType.None;
                workProgress = 0f;
                currentWorkCoroutine = null;

                employee.SetState(EmployeeState.Idle);
                yield break;
            }

            yield return null;
        }

        CancelWork();
    }

    private void CompleteWork()
    {
        if (WorkSystemManager.instance != null && currentWorkTarget != null && currentWorkOrder != null)
        {
            IWorkTarget completedTarget = currentWorkTarget;
            WorkOrder completedOrder = currentWorkOrder;

            currentWorkTarget = null;
            currentWork = WorkType.None;
            workProgress = 0f;
            currentWorkCoroutine = null;

            WorkSystemManager.instance.OnWorkerCompletedTarget(employee, completedTarget, completedOrder);

            if (currentWorkTarget == null)
            {
                currentWorkOrder = null;
                employee.SetState(EmployeeState.Idle);
            }
        }
        else
        {
            currentWorkTarget = null;
            currentWorkOrder = null;
            currentWork = WorkType.None;
            workProgress = 0f;
            currentWorkCoroutine = null;
            employee.SetState(EmployeeState.Idle);
        }
    }

    /// <summary>
    /// 현재 작업을 취소합니다.
    /// </summary>
    public void CancelWork()
    {
        if (currentWorkCoroutine != null)
        {
            StopCoroutine(currentWorkCoroutine);
            currentWorkCoroutine = null;
        }

        if (currentWorkTarget != null)
        {
            currentWorkTarget.CancelWork(employee);

            if (WorkSystemManager.instance != null)
            {
                WorkSystemManager.instance.OnWorkerCancelledWork(employee);
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

        employee.SetState(EmployeeState.Idle);
    }

    #endregion

    #region 작업 우선순위

    public void SetWorkPriority(WorkType type, int priority, bool enabled)
    {
        var work = workPriorities?.FirstOrDefault(w => w.workType == type);
        if (work != null)
        {
            work.priority = priority;
            work.enabled = enabled;
        }
    }

    public List<WorkType> GetEnabledWorkTypes()
    {
        if (workPriorities == null) return new List<WorkType>();

        return workPriorities
            .Where(w => w.enabled && CanPerformWork(w.workType))
            .OrderBy(w => w.priority)
            .Select(w => w.workType)
            .ToList();
    }

    public int GetWorkPriority(WorkType type)
    {
        var work = workPriorities?.FirstOrDefault(w => w.workType == type);
        return work != null ? work.priority : DEFAULT_MAX_PRIORITY;
    }

    #endregion

    #region 작업 범위 / 시야

    /// <summary>
    /// 특정 위치가 현재 직원의 작업 범위 내에 있는지 확인합니다.
    /// </summary>
    public bool IsPositionInWorkRange(Vector3Int targetPosition)
    {
        Vector3Int standingTile = GetFootTile();

        int dx = Mathf.Abs(targetPosition.x - standingTile.x);
        int dy = targetPosition.y - standingTile.y;

        bool inRange = dx <= 1 && dy >= -1 && dy <= 3;

        if (!inRange) return false;

        if (!HasLineOfSight(standingTile, targetPosition)) return false;

        return true;
    }

    /// <summary>
    /// 직원의 작업 가능 범위 타일 목록을 반환합니다.
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

    private bool HasLineOfSight(Vector3Int from, Vector3Int to)
    {
        GameMap gameMap = MapGenerator.instance?.GameMapInstance;
        if (gameMap == null) return true;

        int footY = from.y;
        int bodyY2 = from.y + 2;

        if (from.x == to.x)
        {
            if (to.y > bodyY2)
            {
                for (int y = bodyY2 + 1; y < to.y; y++)
                {
                    if (IsSolidTile(gameMap, from.x, y)) return false;
                }
            }
            else if (to.y < footY)
            {
                if (IsSolidTile(gameMap, from.x, footY)) return false;

                for (int y = footY - 1; y > to.y; y--)
                {
                    if (IsSolidTile(gameMap, from.x, y)) return false;
                }
            }
        }
        else
        {
            int targetX = to.x;

            if (to.y >= footY && to.y <= bodyY2) return true;

            if (to.y > bodyY2)
            {
                for (int y = bodyY2 + 1; y < to.y; y++)
                {
                    if (IsSolidTile(gameMap, targetX, y)) return false;
                }
            }
            else if (to.y < footY)
            {
                for (int y = footY; y > to.y; y--)
                {
                    if (IsSolidTile(gameMap, targetX, y)) return false;
                }
            }
        }

        return true;
    }

    private bool IsSolidTile(GameMap gameMap, int x, int y)
    {
        if (x < 0 || x >= GameMap.MAP_WIDTH || y < 0 || y >= GameMap.MAP_HEIGHT)
            return false;
        return gameMap.TileGrid[x, y] != 0;
    }

    #endregion

    #region 작업 위치 탐색

    private struct WorkPositionCandidate
    {
        public Vector2Int position;
        public float distance;
        public int heightDiff;
        public int pathLength;
        public int fallSafety;
        public int verticalPriority;
    }

    /// <summary>
    /// 작업 대상 근처에서 실제로 작업할 수 있는 위치를 찾습니다.
    /// </summary>
    public Vector3 FindWorkablePositionForTarget(Vector3Int targetTilePos)
    {
        Vector3Int currentFootTile = GetFootTile();
        Vector2Int startPos = new Vector2Int(currentFootTile.x, currentFootTile.y);

        TilePathfinder pathfinder = null;
        GameMap gameMap = null;
        if (MapGenerator.instance != null)
        {
            gameMap = MapGenerator.instance.GameMapInstance;
            if (gameMap != null)
            {
                pathfinder = new TilePathfinder(gameMap);
            }
        }

        if (pathfinder == null || gameMap == null) return Vector3.zero;

        List<WorkPositionCandidate> candidates = new List<WorkPositionCandidate>();

        int[] dyOrder = { 1, 2, 3, 0, -1, -2, -3 };
        int[] dxOrder = { 1, -1, 0 };

        foreach (int dy in dyOrder)
        {
            foreach (int dx in dxOrder)
            {
                Vector2Int candidatePos = new Vector2Int(
                    targetTilePos.x + dx,
                    targetTilePos.y + dy
                );

                if (candidatePos.x < 0 || candidatePos.x >= GameMap.MAP_WIDTH ||
                    candidatePos.y < 0 || candidatePos.y >= GameMap.MAP_HEIGHT)
                    continue;

                int workDx = Mathf.Abs(targetTilePos.x - candidatePos.x);
                int workDy = targetTilePos.y - candidatePos.y;
                if (workDx > 1 || workDy < -1 || workDy > 3)
                    continue;

                int footTileId = gameMap.TileGrid[candidatePos.x, candidatePos.y];
                if (footTileId != 0) continue;

                if (candidatePos.y + 1 < GameMap.MAP_HEIGHT)
                {
                    int bodyTileId = gameMap.TileGrid[candidatePos.x, candidatePos.y + 1];
                    if (bodyTileId != 0) continue;
                }

                int groundY = candidatePos.y - 1;
                if (groundY < 0) continue;

                int groundTileId = gameMap.TileGrid[candidatePos.x, groundY];
                bool hasFloorTile = FloorTile.HasFloorTileAt(new Vector2Int(candidatePos.x, groundY));
                bool hasConstructedFloor = gameMap.IsTileOccupied(candidatePos.x, groundY) &&
                                           !gameMap.DoesTileBlockMovement(candidatePos.x, groundY);
                bool hasGround = groundTileId != 0 || hasFloorTile || hasConstructedFloor;

                if (!hasGround)
                {
                    FloorTile ladder = FloorTile.GetFloorTileAt(new Vector2Int(candidatePos.x, groundY));
                    FloorTile currentLadder = FloorTile.GetFloorTileAt(candidatePos);
                    bool hasLadder = (ladder != null && ladder.AllowsVerticalMovement()) ||
                                     (currentLadder != null && currentLadder.AllowsVerticalMovement());
                    if (!hasLadder) continue;
                }

                if (candidatePos == startPos) continue;

                List<Vector2Int> path = pathfinder.FindPath(startPos, candidatePos);
                if (path == null || path.Count == 0) continue;

                float distance = Vector2Int.Distance(startPos, candidatePos);
                int heightDiff = Mathf.Abs(candidatePos.y - currentFootTile.y);

                bool willFallAfterMining = (candidatePos.x == targetTilePos.x) &&
                                           (candidatePos.y > targetTilePos.y) &&
                                           (candidatePos.y == targetTilePos.y + 1);
                int fallSafety = willFallAfterMining ? 1 : 0;
                int verticalPriority = (candidatePos.y > targetTilePos.y) ? 0 : 1;

                candidates.Add(new WorkPositionCandidate
                {
                    position = candidatePos,
                    distance = distance,
                    heightDiff = heightDiff,
                    pathLength = path.Count,
                    fallSafety = fallSafety,
                    verticalPriority = verticalPriority
                });
            }
        }

        if (candidates.Count == 0) return Vector3.zero;

        var sortedCandidates = candidates
            .OrderBy(c => c.fallSafety)
            .ThenBy(c => c.verticalPriority)
            .ThenBy(c => c.pathLength)
            .ThenBy(c => c.heightDiff)
            .ThenBy(c => c.distance)
            .ToList();

        Vector2Int bestPos = sortedCandidates[0].position;
        return new Vector3(bestPos.x + 0.5f, bestPos.y, 0);
    }

    #endregion

    #region 저장/복원

    /// <summary>
    /// 저장 데이터에 작업 정보를 기록합니다.
    /// </summary>
    public void PopulateSaveData(EmployeeSaveData data)
    {
        data.abilities = WorkAbilitiesSaveData.FromWorkAbilities(runtimeAbilities);
        data.assignedWorkOrderId = currentWorkOrder != null ? currentWorkOrder.orderId : -1;

        // 작업 우선순위
        data.workPriorities = new List<WorkPrioritySaveData>();
        if (workPriorities != null)
        {
            foreach (var wp in workPriorities)
            {
                data.workPriorities.Add(new WorkPrioritySaveData
                {
                    workType = (int)wp.workType,
                    priority = wp.priority,
                    enabled = wp.enabled
                });
            }
        }

        // 비자격 목록
        data.disqualifiedWorkTypes = new List<int>();
        foreach (var dq in disqualifications)
        {
            data.disqualifiedWorkTypes.Add((int)dq.workType);
        }
    }

    /// <summary>
    /// 저장 데이터에서 작업 정보를 복원합니다.
    /// </summary>
    public void RestoreFromSaveData(EmployeeSaveData data)
    {
        if (data.abilities != null)
        {
            runtimeAbilities = data.abilities.ToWorkAbilities();
        }

        if (data.workPriorities != null && data.workPriorities.Count > 0)
        {
            workPriorities = new List<WorkPriority>();
            foreach (var wp in data.workPriorities)
            {
                workPriorities.Add(new WorkPriority
                {
                    workType = (WorkType)wp.workType,
                    priority = wp.priority,
                    enabled = wp.enabled
                });
            }
        }

        // 비자격 복원
        disqualifications.Clear();
        if (data.disqualifiedWorkTypes != null)
        {
            foreach (var wt in data.disqualifiedWorkTypes)
            {
                disqualifications.Add(new DisqualificationEntry
                {
                    workType = (WorkType)wt,
                    reason = "저장 데이터 복원"
                });
            }
        }
    }

    #endregion
}

/// <summary>
/// 작업 비자격 항목.
/// </summary>
[System.Serializable]
public struct DisqualificationEntry
{
    /// <summary>비자격 작업 타입</summary>
    public WorkType workType;

    /// <summary>비자격 사유 (UI 표시용)</summary>
    public string reason;
}
