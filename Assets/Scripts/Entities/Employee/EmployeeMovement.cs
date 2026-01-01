using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타일 기반 길찾기와 이동을 지원하는 직원 이동 컨트롤러
/// </summary>
public class EmployeeMovement : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float baseSpeed = 3f;
    [SerializeField] private float stoppingDistance = 0.1f;
    [SerializeField] private float tileTransitionSpeed = 5f;

    [Header("낙하 설정")]
    [SerializeField] private float fallSpeed = 8f;

    [Header("디버그")]
    [SerializeField] private bool showPath = true;
    [SerializeField] private bool showDebugLogs = true;

    private Vector3 targetPosition;
    private bool isFalling = false;
    private bool isMoving = false;
    private Action onReachDestination;
    private Action onMoveFailed; // 이동 실패 콜백 추가
    private Coroutine moveCoroutine;

    // 타일 기반 경로
    private List<Vector2Int> currentPath;
    private int currentPathIndex = 0;

    // 컴포넌트 참조
    private Rigidbody2D rb;
    private Collider2D col;
    private Employee employee;
    private GameMap gameMap;
    private TilePathfinder pathfinder;

    // 물리 상태 저장
    private RigidbodyType2D originalBodyType;
    
    // 직원 높이 상수 (직원은 2칸 높이)
    private const int EMPLOYEE_HEIGHT = 2;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        employee = GetComponent<Employee>();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            originalBodyType = rb.bodyType;
        }

        if (col == null)
        {
            Debug.LogWarning("[EmployeeMovement] Collider2D가 없습니다.");
        }
    }
    
    void Start()
    {
        if (MapGenerator.instance != null)
        {
            gameMap = MapGenerator.instance.GameMapInstance;
            pathfinder = new TilePathfinder(gameMap);
            
            // 피벗 변경 후 위치 보정: 직원이 고체 안에 있으면 위로 밀어냄
            AdjustPositionIfInsideSolid();
        }
        else
        {
            Debug.LogError("[EmployeeMovement] MapGenerator를 찾을 수 없습니다!");
        }
    }
    
    /// <summary>
    /// 직원이 고체 타일 안에 있으면 위로 밀어냅니다.
    /// 피벗 변경 등으로 인해 잘못된 위치에 있을 때 보정합니다.
    /// </summary>
    private void AdjustPositionIfInsideSolid()
    {
        if (gameMap == null) return;
        
        Vector2Int footTile = GetFootTile();
        
        // 발 위치가 고체인지 확인
        if (footTile.x >= 0 && footTile.x < GameMap.MAP_WIDTH &&
            footTile.y >= 0 && footTile.y < GameMap.MAP_HEIGHT)
        {
            int tileId = gameMap.TileGrid[footTile.x, footTile.y];
            if (tileId != 0) // 고체 안에 있음
            {
                // 위로 올려서 빈 공간 찾기
                for (int dy = 1; dy <= 10; dy++)
                {
                    int checkY = footTile.y + dy;
                    if (checkY >= GameMap.MAP_HEIGHT) break;
                    
                    int checkTileId = gameMap.TileGrid[footTile.x, checkY];
                    if (checkTileId == 0) // 빈 공간 발견
                    {
                        // 그 아래가 고체인지 확인 (서 있을 수 있는지)
                        int groundY = checkY - 1;
                        if (groundY >= 0 && gameMap.TileGrid[footTile.x, groundY] != 0)
                        {
                            Vector3 newPos = new Vector3(transform.position.x, checkY, transform.position.z);
                            Debug.Log($"[EmployeeMovement] 위치 보정: {transform.position} -> {newPos}");
                            transform.position = newPos;
                            return;
                        }
                    }
                }
                
                Debug.LogWarning($"[EmployeeMovement] 위치 보정 실패: 직원이 고체 안에 갇혀있음 {footTile}");
            }
        }
    }

    void Update()
    {
        CheckGroundAndFall();
    }

    #region 낙하 처리

    private void CheckGroundAndFall()
    {
        if (gameMap == null) return;

        // 현재 발이 속한 타일 좌표
        Vector2Int footTile = GetFootTile();
        
        // 바닥 체크: 발 위치 아래 타일 (발 - 1)이 고체인지 확인
        // ★ 중요: footTile.x 기준으로 체크 (transform.x가 아닌 정수 타일 좌표)
        Vector2Int groundTile = new Vector2Int(footTile.x, footTile.y - 1);

        if (!HasGroundAt(groundTile))
        {
            if (!isFalling)
            {
                isFalling = true;

                if (isMoving)
                {
                    StopMovingForFall();
                }

                if (showDebugLogs)
                {
                    Debug.Log($"[EmployeeMovement] 바닥 없음! 낙하 시작: 발={footTile}, 바닥체크={groundTile}");
                }
            }

            transform.position += Vector3.down * fallSpeed * Time.deltaTime;

            // 새 위치에서 바닥 체크
            Vector2Int newFootTile = GetFootTile();
            Vector2Int newGroundTile = new Vector2Int(newFootTile.x, newFootTile.y - 1);
            
            if (HasGroundAt(newGroundTile))
            {
                LandOnGround(newGroundTile);
            }
        }
        else if (isFalling)
        {
            LandOnGround(groundTile);
        }
    }

    private void StopMovingForFall()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        isMoving = false;
        currentPath = null;
        currentPathIndex = 0;
        
        // 이동 실패 콜백 호출
        var failCallback = onMoveFailed;
        onReachDestination = null;
        onMoveFailed = null;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        Debug.Log("[EmployeeMovement] 낙하로 인해 이동 중단");
        
        failCallback?.Invoke();
    }

    private bool HasGroundAt(Vector2Int tilePos)
    {
        if (tilePos.x < 0 || tilePos.x >= GameMap.MAP_WIDTH ||
            tilePos.y < 0 || tilePos.y >= GameMap.MAP_HEIGHT)
        {
            return false;
        }

        int tileId = gameMap.TileGrid[tilePos.x, tilePos.y];
        if (tileId != 0)
        {
            return true;
        }

        if (FloorTile.HasFloorTileAt(tilePos))
        {
            return true;
        }
        
        // ★ 건설된 바닥 타일도 바닥으로 인식 (OccupiedGrid=true, BlocksMovement=false)
        if (gameMap.IsTileOccupied(tilePos.x, tilePos.y) && !gameMap.DoesTileBlockMovement(tilePos.x, tilePos.y))
        {
            return true;
        }

        return false;
    }

    private void LandOnGround(Vector2Int groundTile)
    {
        isFalling = false;

        // groundTile = 밟고 있는 고체 타일
        // 직원 발 위치 = groundTile.y + 1 (그 위 공간)
        int landingY = groundTile.y + 1;
        
        // 안전 체크: 착지 위치가 고체가 아닌지 확인
        if (landingY < GameMap.MAP_HEIGHT && gameMap.TileGrid[groundTile.x, landingY] != 0)
        {
            // 착지 위치가 고체! 위로 올려서 빈 공간 찾기
            for (int dy = 1; dy <= 5; dy++)
            {
                int checkY = landingY + dy;
                if (checkY >= GameMap.MAP_HEIGHT) break;
                
                if (gameMap.TileGrid[groundTile.x, checkY] == 0)
                {
                    landingY = checkY;
                    Debug.Log($"[EmployeeMovement] 착지 위치 보정: {groundTile.y + 1} -> {landingY}");
                    break;
                }
            }
        }
        
        // ★ 착지 시 반올림으로 스냅 (시각적 위치와 맞춤)
        int snappedX = Mathf.RoundToInt(transform.position.x);
        Vector3 landingPos = new Vector3(snappedX, landingY, 0);
        transform.position = landingPos;

        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeMovement] 착지 완료: 바닥타일={groundTile}, 최종위치={transform.position}");
        }
        
        // 착지 이벤트 발생 (발 위치의 타일 좌표)
        Vector2Int footTile = GetFootTile();
        OnLanded?.Invoke(footTile);
    }
    
    /// <summary>
    /// 착지 시 발생하는 이벤트
    /// </summary>
    public event Action<Vector2Int> OnLanded;

    #endregion

    #region 이동 시스템
    
    /// <summary>
    /// 목표 월드 좌표로 이동합니다 (자동 길찾기).
    /// </summary>
    /// <param name="worldDestination">목표 월드 좌표</param>
    /// <param name="onComplete">이동 성공 시 콜백</param>
    /// <param name="onFailed">이동 실패 시 콜백 (경로 없음 등)</param>
    public void MoveTo(Vector3 worldDestination, Action onComplete = null, Action onFailed = null)
    {
        if (pathfinder == null)
        {
            Debug.LogError("[EmployeeMovement] Pathfinder가 초기화되지 않았습니다!");
            onFailed?.Invoke();
            return;
        }

        StopMoving();

        Vector2Int currentTile = GetFootTile();
        Vector2Int goalTile = WorldToFootTile(worldDestination);

        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeMovement] 이동 요청 - 현재: {currentTile}, 목표: {goalTile}");
        }

        // 이미 목표 위치에 있는 경우
        if (currentTile == goalTile)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[EmployeeMovement] 이미 목표 위치에 있음");
            }
            onComplete?.Invoke();
            return;
        }

        // 경로 찾기
        currentPath = pathfinder.FindPath(currentTile, goalTile);

        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogWarning($"[EmployeeMovement] 경로를 찾을 수 없습니다: {currentTile} -> {goalTile}");
            
            // ★ 핵심 수정: 경로 실패 시 성공 콜백 호출 안 함!
            onFailed?.Invoke();
            return;
        }

        targetPosition = worldDestination;
        onReachDestination = onComplete;
        onMoveFailed = onFailed;
        currentPathIndex = 0;
        isMoving = true;

        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeMovement] 경로 발견: {currentPath.Count}개 타일");
            for (int i = 0; i < Mathf.Min(currentPath.Count, 5); i++)
            {
                Debug.Log($"  경로[{i}]: {currentPath[i]}");
            }
        }

        moveCoroutine = StartCoroutine(FollowPathCoroutine());
    }

    /// <summary>
    /// 기존 호환성을 위한 오버로드 (실패 콜백 없이)
    /// </summary>
    public void MoveTo(Vector3 worldDestination, Action onComplete)
    {
        MoveTo(worldDestination, onComplete, null);
    }
    
    private IEnumerator FollowPathCoroutine()
    {
        EnablePhysicsForMovement(false);

        while (isMoving && currentPathIndex < currentPath.Count)
        {
            Vector2Int currentTile = (currentPathIndex > 0) ? currentPath[currentPathIndex - 1] : GetFootTile();
            Vector2Int nextTile = currentPath[currentPathIndex];

            int heightDiff = nextTile.y - currentTile.y;

            if (showDebugLogs)
            {
                Debug.Log($"[EmployeeMovement] 타일 이동: {currentTile} -> {nextTile} (높이차: {heightDiff})");
            }

            yield return MoveToTileCoroutine(nextTile, heightDiff);

            currentPathIndex++;
        }

        if (isMoving && Vector3.Distance(transform.position, targetPosition) > stoppingDistance)
        {
            yield return MoveToPositionCoroutine(targetPosition);
        }

        EnablePhysicsForMovement(true);

        ReachDestination();
    }

    private IEnumerator MoveToTileCoroutine(Vector2Int targetTile, int heightDiff)
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = TileToWorld(targetTile);
        float journeyLength = Vector3.Distance(startPos, endPos);
        float startTime = Time.time;

        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeMovement] Lerp 이동 시작: {startPos} -> {endPos}, TileToWorld({targetTile}) = {endPos}");
        }

        float speedModifier = 1f + Mathf.Abs(heightDiff) * 0.2f;
        float actualSpeed = tileTransitionSpeed / speedModifier;

        while (Vector3.Distance(transform.position, endPos) > stoppingDistance)
        {
            if (!isMoving) yield break; // 이동 중단 체크
            
            float distCovered = (Time.time - startTime) * actualSpeed;
            float fractionOfJourney = distCovered / journeyLength;

            Vector3 newPos = Vector3.Lerp(startPos, endPos, fractionOfJourney);
            transform.position = newPos;
            UpdateSpriteDirection(endPos.x - startPos.x);

            yield return null;
        }

        transform.position = endPos;
        
        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeMovement] Lerp 이동 완료: 최종위치={transform.position}");
        }
    }

    private IEnumerator MoveToPositionCoroutine(Vector3 targetPos)
    {
        Vector3 startPos = transform.position;
        float journeyLength = Vector3.Distance(startPos, targetPos);
        float startTime = Time.time;

        while (Vector3.Distance(transform.position, targetPos) > stoppingDistance)
        {
            if (!isMoving) yield break;
            
            float distCovered = (Time.time - startTime) * tileTransitionSpeed;
            float fractionOfJourney = distCovered / journeyLength;

            transform.position = Vector3.Lerp(startPos, targetPos, fractionOfJourney);
            UpdateSpriteDirection(targetPos.x - startPos.x);

            yield return null;
        }

        transform.position = targetPos;
    }

    private void EnablePhysicsForMovement(bool enable)
    {
        if (rb != null)
        {
            if (enable)
            {
                rb.bodyType = originalBodyType;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
            }
        }
    }
    
    private void UpdateSpriteDirection(float xDirection)
    {
        if (Mathf.Abs(xDirection) < 0.01f) return;
        
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(xDirection);
        transform.localScale = scale;
    }
    
    private void ReachDestination()
    {
        isMoving = false;
        currentPath = null;
        currentPathIndex = 0;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        
        var callback = onReachDestination;
        onReachDestination = null;
        onMoveFailed = null;
        
        callback?.Invoke();
    }
    
    public void StopMoving()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        isMoving = false;
        currentPath = null;
        currentPathIndex = 0;
        onReachDestination = null;
        onMoveFailed = null;

        EnablePhysicsForMovement(true);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    #endregion

    #region 좌표 변환 (통일된 규칙)
    
    /// <summary>
    /// 직원의 현재 발 위치 타일 좌표를 반환합니다.
    /// 피벗이 Bottom Left이지만, 시각적 위치와 맞추기 위해 X는 반올림 사용
    /// </summary>
    public Vector2Int GetFootTile()
    {
        return new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y)
        );
    }

    /// <summary>
    /// 월드 좌표를 발 위치 타일 좌표로 변환합니다.
    /// </summary>
    private Vector2Int WorldToFootTile(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y)
        );
    }

    /// <summary>
    /// 타일 좌표를 직원 월드 좌표로 변환합니다.
    /// tilePos는 발 위치 타일 좌표입니다 (피벗이 Bottom Left이므로 transform.position과 동일).
    /// </summary>
    private Vector3 TileToWorld(Vector2Int tilePos)
    {
        // tilePos = 발 위치 타일
        // 피벗 = Bottom Left, transform.position = 발 위치
        // 따라서 그대로 반환
        return new Vector3(tilePos.x, tilePos.y, 0);
    }

    #endregion

    #region 충돌 처리
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isMoving)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[EmployeeMovement] 충돌 감지, 경로 재탐색");
            }
            
            Vector3 currentTarget = targetPosition;
            Action currentCallback = onReachDestination;
            Action currentFailCallback = onMoveFailed;
            
            StopMoving();
            MoveTo(currentTarget, currentCallback, currentFailCallback);
        }
    }

    #endregion

    #region 디버그
    
    void OnDrawGizmos()
    {
        if (!showPath || currentPath == null || currentPath.Count == 0)
            return;
        
        Gizmos.color = Color.yellow;
        
        Vector3 currentPos = transform.position;
        if (currentPathIndex < currentPath.Count)
        {
            Vector3 firstPathPoint = TileToWorld(currentPath[currentPathIndex]);
            Gizmos.DrawLine(currentPos, firstPathPoint);
            currentPos = firstPathPoint;
        }
        
        for (int i = currentPathIndex; i < currentPath.Count - 1; i++)
        {
            Vector3 from = TileToWorld(currentPath[i]);
            Vector3 to = TileToWorld(currentPath[i + 1]);
            Gizmos.DrawLine(from, to);
        }
        
        Gizmos.color = Color.green;
        foreach (var tile in currentPath)
        {
            Vector3 pos = TileToWorld(tile);
            Gizmos.DrawWireCube(pos, Vector3.one * 0.3f);
        }
        
        if (currentPath.Count > 0)
        {
            Gizmos.color = Color.red;
            Vector3 goalPos = TileToWorld(currentPath[currentPath.Count - 1]);
            Gizmos.DrawWireSphere(goalPos, 0.5f);
        }
    }

    #endregion
    
    // Public 프로퍼티
    public bool IsMoving => isMoving;
    public bool IsFalling => isFalling;
    public Vector3 TargetPosition => targetPosition;
    public float DistanceToTarget => Vector3.Distance(transform.position, targetPosition);
    public List<Vector2Int> CurrentPath => currentPath;
}