using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 채굴 작업 선택기
/// 가중치 기반 점수 계산 + 계층적 채굴(Top-Down) + 대각선 우선 로직
/// 
/// Score = (Priority * W_p * HeightBonus) / (Distance * W_d + AccessCost * W_a)
/// </summary>
public class MiningTaskSelector
{
    #region 가중치 설정
    
    [System.Serializable]
    public class SelectorWeights
    {
        [Header("기본 가중치")]
        public float priorityWeight = 10f;      // W_p: 우선순위 가중치
        public float distanceWeight = 1f;       // W_d: 거리 가중치
        public float accessCostWeight = 2f;     // W_a: 접근 비용 가중치
        
        [Header("계층적 채굴 가중치")]
        public float heightBonusPerTile = 0.5f; // Y좌표가 높을수록 보너스
        public float diagonalPreference = 1.5f; // 대각선 아래 채굴 선호도
        
        [Header("페널티")]
        public float standingOnTaskPenalty = 100f;  // 서 있는 타일 페널티
        public float belowFootPenalty = 50f;        // 발 아래 타일 페널티
        public float unreachablePenalty = 1000f;    // 도달 불가 페널티
    }
    
    #endregion
    
    private SelectorWeights weights;
    private ReachabilityMap reachabilityMap;
    
    public MiningTaskSelector(SelectorWeights weights = null, ReachabilityMap reachabilityMap = null)
    {
        this.weights = weights ?? new SelectorWeights();
        this.reachabilityMap = reachabilityMap;
    }
    
    public void SetReachabilityMap(ReachabilityMap map)
    {
        this.reachabilityMap = map;
    }
    
    #region 작업 선택
    
    /// <summary>
    /// 직원에게 가장 적합한 작업을 선택합니다.
    /// </summary>
    public WorkTask SelectBestTask(Employee worker, IReadOnlyList<WorkTask> pendingTasks)
    {
        if (worker == null || pendingTasks == null || pendingTasks.Count == 0)
            return null;
        
        var validTasks = pendingTasks.Where(t => t != null && t.IsValid()).ToList();
        if (validTasks.Count == 0)
            return null;
        
        // 직원 정보 수집
        WorkerContext context = CreateWorkerContext(worker);
        
        // 모든 작업에 대해 점수 계산
        WorkTask bestTask = null;
        float bestScore = float.MinValue;
        
        foreach (var task in validTasks)
        {
            float score = CalculateTaskScore(task, context);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestTask = task;
            }
        }
        
        if (bestTask != null)
        {
            Debug.Log($"[MiningTaskSelector] 선택: {bestTask.GetPosition()} (점수: {bestScore:F2})");
        }
        
        return bestTask;
    }
    
    /// <summary>
    /// 작업 점수 계산
    /// Score = (Priority * W_p * HeightBonus) / (Distance * W_d + AccessCost * W_a + Penalties)
    /// </summary>
    public float CalculateTaskScore(WorkTask task, WorkerContext context)
    {
        Vector2Int taskTile = GetTaskTile(task);
        
        // 1. 현재 위치에서 작업 가능한지 체크 (범위 + 시야)
        bool isInWorkRange = IsInWorkRange(context.footTile, taskTile);
        bool hasLineOfSight = HasLineOfSight(context, taskTile);
        bool canWorkFromHere = isInWorkRange && hasLineOfSight;
        
        // 2. 직원이 차지하고 있는 타일인지 체크
        float penalty = 0f;
        
        if (context.occupiedTiles.Contains(taskTile))
        {
            penalty += weights.standingOnTaskPenalty;
            canWorkFromHere = false; // 서 있는 타일은 현재 위치에서 작업 불가
        }
        
        // 3. 발 아래 타일인지 체크 (파면 떨어짐)
        if (IsDirectlyBelowFoot(taskTile, context))
        {
            penalty += weights.belowFootPenalty;
        }
        
        // 4. 현재 위치에서 작업 불가능하면 이동해야 함 - 도달 가능성 체크
        if (!canWorkFromHere)
        {
            if (!IsTaskReachable(taskTile, context))
            {
                return float.MinValue; // 도달 불가능
            }
        }
        
        // 5. 기본 점수 요소 계산
        float priority = Mathf.Max(1, 10 - task.priority); // priority가 낮을수록 높은 점수
        float distance = canWorkFromHere ? 0f : CalculateDistance(context.footTile, taskTile);
        float accessCost = canWorkFromHere ? 0f : CalculateAccessCost(context, taskTile);
        float heightBonus = CalculateHeightBonus(taskTile, context);
        float diagonalBonus = CalculateDiagonalBonus(taskTile, context);
        
        // 6. 현재 위치에서 작업 가능하면 큰 보너스
        float inRangeBonus = canWorkFromHere ? 10f : 1f;
        
        // 7. 최종 점수 계산
        float numerator = priority * weights.priorityWeight * heightBonus * diagonalBonus * inRangeBonus;
        float denominator = distance * weights.distanceWeight + accessCost * weights.accessCostWeight + penalty + 0.1f;
        
        float score = numerator / denominator;
        
        return score;
    }
    
    /// <summary>
    /// 작업자 발 위치에서 타겟이 작업 범위 내인지 확인
    /// 범위: 좌우 1칸, 위로 3칸, 아래로 1칸
    /// </summary>
    private bool IsInWorkRange(Vector2Int footTile, Vector2Int targetTile)
    {
        int dx = Mathf.Abs(targetTile.x - footTile.x);
        int dy = targetTile.y - footTile.y;  // 양수면 위, 음수면 아래
        
        // 범위: dx <= 1, dy: -1 ~ +3
        return dx <= 1 && dy >= -1 && dy <= 3;
    }
    
    #endregion
    
    #region 컨텍스트 생성
    
    public struct WorkerContext
    {
        public Vector2Int footTile;           // 발 위치
        public HashSet<Vector2Int> occupiedTiles;  // 직원이 차지하는 모든 타일
        public int employeeHeight;            // 직원 높이
        public GameMap gameMap;
        public TilePathfinder pathfinder;
    }
    
    private WorkerContext CreateWorkerContext(Employee worker)
    {
        Vector3 pos = worker.transform.position;
        // X는 반올림으로 시각적 위치와 맞춤
        Vector2Int footTile = new Vector2Int(
            Mathf.RoundToInt(pos.x),
            Mathf.FloorToInt(pos.y)
        );
        
        var context = new WorkerContext
        {
            footTile = footTile,
            // 직원이 차지하는 타일: 발 위치와 그 위 (2칸)
            occupiedTiles = new HashSet<Vector2Int>
            {
                footTile,
                new Vector2Int(footTile.x, footTile.y + 1)
            },
            employeeHeight = 2,
            gameMap = MapGenerator.instance?.GameMapInstance,
            pathfinder = null
        };
        
        if (context.gameMap != null)
        {
            context.pathfinder = new TilePathfinder(context.gameMap);
        }
        
        return context;
    }
    
    #endregion
    
    #region 도달 가능성
    
    private bool IsTaskReachable(Vector2Int taskTile, WorkerContext context)
    {
        // ReachabilityMap이 있으면 사용
        if (reachabilityMap != null)
        {
            // 작업 가능한 위치가 도달 가능한지 확인
            var workPositions = GetPotentialWorkPositions(taskTile, context);
            return workPositions.Any(pos => reachabilityMap.IsReachable(context.footTile, pos));
        }
        
        // 없으면 직접 경로 탐색
        if (context.pathfinder == null)
            return true;
        
        // 작업 가능한 위치 중 하나라도 도달 가능하면 OK
        var positions = GetPotentialWorkPositions(taskTile, context);
        foreach (var pos in positions)
        {
            if (pos == context.footTile)
                return true;
            
            var path = context.pathfinder.FindPath(context.footTile, pos);
            if (path != null && path.Count > 0)
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// 작업 대상 타일에 대해 작업 가능한 작업자 발 위치들을 반환
    /// 타겟 기준이 아닌, "이 위치에 서면 타겟을 작업할 수 있다"는 위치들
    /// </summary>
    private List<Vector2Int> GetPotentialWorkPositions(Vector2Int taskTile, WorkerContext context)
    {
        List<Vector2Int> positions = new List<Vector2Int>();

        Debug.Log($"[MiningTaskSelector] GetPotentialWorkPositions: 타겟={taskTile}, 현재발위치={context.footTile}");

        // 작업 범위: 작업자 기준 좌우 1칸, 위로 3칸, 아래로 1칸
        // 역으로 계산: 타겟 기준으로 작업자가 서 있을 수 있는 위치
        // 타겟이 작업자의 (dx, dy) 위치에 있으려면, 작업자는 타겟의 (-dx, -dy) 위치에 있어야 함

        for (int dx = -1; dx <= 1; dx++)
        {
            // 작업자 발 기준 dy: -1 ~ +3이면, 
            // 타겟 기준으로 작업자는 dy: -3 ~ +1 위치
            for (int workerDy = -3; workerDy <= 1; workerDy++)
            {
                Vector2Int candidateFootPos = new Vector2Int(taskTile.x + dx, taskTile.y + workerDy);

                // 맵 범위 체크
                if (!IsInBounds(candidateFootPos))
                    continue;

                // 타겟 자체 위치는 제외 (타겟은 고체니까 서 있을 수 없음)
                if (candidateFootPos == taskTile)
                    continue;

                // 해당 위치에서 타겟이 작업 범위 내인지 확인
                if (!IsInWorkRange(candidateFootPos, taskTile))
                    continue;

                // 유효한 위치인지 확인 (서 있을 수 있는지)
                if (context.pathfinder != null)
                {
                    bool isValid = context.pathfinder.IsValidPosition(candidateFootPos);
                    Debug.Log($"[MiningTaskSelector] 후보 {candidateFootPos}: IsValidPosition={isValid}");

                    if (isValid)
                    {
                        positions.Add(candidateFootPos);
                    }
                }
            }


        }
        return positions;
    }

    #endregion
    
    #region 시야 체크
    
    private bool HasLineOfSight(WorkerContext context, Vector2Int targetTile)
    {
        if (context.gameMap == null)
            return true;
        
        Vector2Int from = context.footTile;
        int footY = from.y;
        int bodyY2 = from.y + 2;
        
        // 같은 X 좌표
        if (from.x == targetTile.x)
        {
            // 위쪽 타겟
            if (targetTile.y > bodyY2)
            {
                for (int y = bodyY2 + 1; y < targetTile.y; y++)
                {
                    if (IsSolidTile(context.gameMap, from.x, y))
                        return false;
                }
            }
            // 아래쪽 타겟
            else if (targetTile.y < footY)
            {
                // 발 밑이 고체면 그 아래로 시야 차단
                if (IsSolidTile(context.gameMap, from.x, footY))
                    return false;
                
                for (int y = footY - 1; y > targetTile.y; y--)
                {
                    if (IsSolidTile(context.gameMap, from.x, y))
                        return false;
                }
            }
        }
        // 다른 X 좌표 (옆)
        else
        {
            // 몸통 높이는 OK
            if (targetTile.y >= footY && targetTile.y <= bodyY2)
                return true;
            
            // 위쪽
            if (targetTile.y > bodyY2)
            {
                for (int y = bodyY2 + 1; y < targetTile.y; y++)
                {
                    if (IsSolidTile(context.gameMap, targetTile.x, y))
                        return false;
                }
            }
            // 아래쪽
            else if (targetTile.y < footY)
            {
                for (int y = footY; y > targetTile.y; y--)
                {
                    if (IsSolidTile(context.gameMap, targetTile.x, y))
                        return false;
                }
            }
        }
        
        return true;
    }
    
    #endregion
    
    #region 점수 계산 헬퍼
    
    private float CalculateDistance(Vector2Int from, Vector2Int to)
    {
        // 맨해튼 거리 + 약간의 유클리드 보정
        float manhattan = Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);
        float euclidean = Vector2Int.Distance(from, to);
        return manhattan * 0.7f + euclidean * 0.3f;
    }
    
    private float CalculateAccessCost(WorkerContext context, Vector2Int taskTile)
    {
        float cost = 0f;
        
        // 사다리 필요 여부
        if (Mathf.Abs(taskTile.y - context.footTile.y) > 2)
        {
            cost += 5f; // 사다리 비용
        }
        
        // 경로 복잡도 (실제 경로 길이 vs 직선 거리)
        if (context.pathfinder != null)
        {
            var workPositions = GetPotentialWorkPositions(taskTile, context);
            float minPathRatio = float.MaxValue;
            
            foreach (var pos in workPositions)
            {
                if (pos == context.footTile)
                {
                    minPathRatio = 1f;
                    break;
                }
                
                var path = context.pathfinder.FindPath(context.footTile, pos);
                if (path != null && path.Count > 0)
                {
                    float directDist = Vector2Int.Distance(context.footTile, pos);
                    float pathRatio = path.Count / Mathf.Max(1f, directDist);
                    minPathRatio = Mathf.Min(minPathRatio, pathRatio);
                }
            }
            
            if (minPathRatio < float.MaxValue)
            {
                cost += (minPathRatio - 1f) * 2f; // 우회 비용
            }
        }
        
        return cost;
    }
    
    /// <summary>
    /// 높이 보너스 계산 (Top-Down 채굴)
    /// 높은 타일일수록 높은 점수
    /// </summary>
    private float CalculateHeightBonus(Vector2Int taskTile, WorkerContext context)
    {
        // 직원 머리 높이 기준으로 상대 높이 계산
        int relativeHeight = taskTile.y - (context.footTile.y + 2);
        
        // 위쪽 타일에 보너스, 아래쪽에 페널티
        float bonus = 1f + (relativeHeight * weights.heightBonusPerTile);
        
        return Mathf.Max(0.1f, bonus);
    }
    
    /// <summary>
    /// 대각선 보너스 계산
    /// 발 바로 아래보다 대각선 아래를 선호
    /// </summary>
    private float CalculateDiagonalBonus(Vector2Int taskTile, WorkerContext context)
    {
        // 발 바로 아래인 경우
        if (taskTile.x == context.footTile.x && taskTile.y < context.footTile.y)
        {
            return 1f; // 보너스 없음
        }
        
        // 대각선 아래인 경우 (다른 X, 아래 Y)
        if (taskTile.x != context.footTile.x && taskTile.y < context.footTile.y)
        {
            return weights.diagonalPreference;
        }
        
        return 1f;
    }
    
    /// <summary>
    /// 발 바로 아래 타일인지 확인 (파면 떨어짐)
    /// </summary>
    private bool IsDirectlyBelowFoot(Vector2Int taskTile, WorkerContext context)
    {
        return taskTile.x == context.footTile.x && 
               taskTile.y == context.footTile.y - 1;
    }
    
    #endregion
    
    #region 유틸리티
    
    private Vector2Int GetTaskTile(WorkTask task)
    {
        Vector3 pos = task.GetPosition();
        return new Vector2Int(
            Mathf.FloorToInt(pos.x),
            Mathf.FloorToInt(pos.y)
        );
    }
    
    private bool IsSolidTile(GameMap gameMap, int x, int y)
    {
        if (!IsInBounds(x, y))
            return false;
        return gameMap.TileGrid[x, y] != 0;
    }
    
    private bool IsInBounds(Vector2Int pos)
    {
        return IsInBounds(pos.x, pos.y);
    }
    
    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < GameMap.MAP_WIDTH && y >= 0 && y < GameMap.MAP_HEIGHT;
    }
    
    #endregion
}