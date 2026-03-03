using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타일 기반 길찾기와 이동을 지원하는 직원 이동 컨트롤러.
/// TilePathfinder를 사용하여 A* 경로 탐색 후 타일 단위로 Lerp 이동합니다.
///
/// 주요 기능:
///   - 타일 기반 경로 탐색 및 이동 (MoveTo)
///   - 바닥 감지 및 자연 낙하 (CheckGroundAndFall)
///   - 고체 타일 내부 진입 시 자동 보정 (AdjustPositionIfInsideSolid)
///   - 충돌 시 자동 경로 재탐색
///
/// 좌표 규칙:
///   - 직원 피벗: Bottom Center
///   - 타일 좌표 → 월드 좌표: (tileX + 0.5f, tileY, 0)
///   - 월드 좌표 → 타일 좌표: (FloorToInt(x), FloorToInt(y))
/// </summary>
public class EmployeeMovement : MonoBehaviour
{
    #region 상수

    /// <summary>직원 높이 (타일 단위)</summary>
    private const int EMPLOYEE_HEIGHT = 2;

    /// <summary>위치 보정 시 최대 검색 높이</summary>
    private const int MAX_POSITION_ADJUST_HEIGHT = 10;

    /// <summary>착지 시 최대 Y 보정 높이</summary>
    private const int MAX_LANDING_ADJUST_HEIGHT = 5;

    /// <summary>스프라이트 방향 전환 무시 임계값</summary>
    private const float DIRECTION_THRESHOLD = 0.01f;

    /// <summary>높이차에 따른 속도 감소 계수</summary>
    private const float HEIGHT_SPEED_PENALTY = 0.2f;

    #endregion

    #region 필드 및 설정

    [Header("이동 설정")]
    [SerializeField] private float baseSpeed = 3f;
    [SerializeField] private float stoppingDistance = 0.1f;
    [SerializeField] private float tileTransitionSpeed = 5f;

    [Header("낙하 설정")]
    [SerializeField] private float fallSpeed = 8f;

    [Header("디버그")]
    [SerializeField] private bool showPath = true;
    [SerializeField] private bool showDebugLogs = true;

    /// <summary>이동 목표 월드 좌표</summary>
    private Vector3 targetPosition;

    /// <summary>현재 낙하 중 여부</summary>
    private bool isFalling = false;

    /// <summary>현재 이동 중 여부</summary>
    private bool isMoving = false;

    /// <summary>이동 성공 콜백</summary>
    private Action onReachDestination;

    /// <summary>이동 실패 콜백</summary>
    private Action onMoveFailed;

    /// <summary>이동 코루틴 참조</summary>
    private Coroutine moveCoroutine;

    /// <summary>현재 경로 (타일 좌표 목록)</summary>
    private List<Vector2Int> currentPath;

    /// <summary>현재 경로 인덱스</summary>
    private int currentPathIndex = 0;

    // 컴포넌트 참조
    private Rigidbody2D rb;
    private Collider2D col;
    private Employee employee;
    private GameMap gameMap;
    private TilePathfinder pathfinder;

    /// <summary>원래 Rigidbody 타입 (이동 중 Kinematic으로 전환)</summary>
    private RigidbodyType2D originalBodyType;

    #endregion

    #region 이벤트

    /// <summary>착지 시 발생하는 이벤트 (발 위치 타일 좌표 전달)</summary>
    public event Action<Vector2Int> OnLanded;

    #endregion

    #region 프로퍼티

    /// <summary>현재 이동 중 여부</summary>
    public bool IsMoving => isMoving;

    /// <summary>현재 낙하 중 여부</summary>
    public bool IsFalling => isFalling;

    /// <summary>이동 목표 월드 좌표</summary>
    public Vector3 TargetPosition => targetPosition;

    /// <summary>목표까지의 거리</summary>
    public float DistanceToTarget => Vector3.Distance(transform.position, targetPosition);

    /// <summary>현재 경로</summary>
    public List<Vector2Int> CurrentPath => currentPath;

    #endregion

    #region 초기화

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
    /// 직원이 고체 타일 안에 있으면 유효한 위치로 밀어냅니다.
    /// 피벗 변경 등으로 인해 잘못된 위치에 있을 때 보정합니다.
    /// 이 함수는 Start()에서만 호출됩니다 - 런타임 중에는 자연 낙하를 사용합니다.
    /// </summary>
    private void AdjustPositionIfInsideSolid()
    {
        if (gameMap == null) return;

        Vector2Int footTile = GetFootTile();

        if (footTile.x >= 0 && footTile.x < GameMap.MAP_WIDTH &&
            footTile.y >= 0 && footTile.y < GameMap.MAP_HEIGHT)
        {
            int tileId = gameMap.TileGrid[footTile.x, footTile.y];
            if (tileId != 0)
            {
                int[] xCandidates = { footTile.x, footTile.x - 1, footTile.x + 1 };

                foreach (int checkX in xCandidates)
                {
                    if (checkX < 0 || checkX >= GameMap.MAP_WIDTH) continue;

                    for (int dy = 0; dy <= MAX_POSITION_ADJUST_HEIGHT; dy++)
                    {
                        int checkY = footTile.y + dy;
                        if (checkY >= GameMap.MAP_HEIGHT) break;

                        int checkTileId = gameMap.TileGrid[checkX, checkY];
                        if (checkTileId == 0)
                        {
                            int groundY = checkY - 1;
                            bool hasGround = groundY >= 0 && (
                                gameMap.TileGrid[checkX, groundY] != 0 ||
                                FloorTile.HasFloorTileAt(new Vector2Int(checkX, groundY)) ||
                                (gameMap.IsTileOccupied(checkX, groundY) && !gameMap.DoesTileBlockMovement(checkX, groundY))
                            );

                            bool bodySpaceClear = checkY + 1 >= GameMap.MAP_HEIGHT ||
                                                  gameMap.TileGrid[checkX, checkY + 1] == 0;

                            if (hasGround && bodySpaceClear)
                            {
                                Vector3 newPos = new Vector3(checkX + 0.5f, checkY, transform.position.z);
                                Debug.Log($"[EmployeeMovement] 위치 보정: {transform.position} -> {newPos}");
                                transform.position = newPos;
                                return;
                            }
                        }
                    }
                }

                Debug.LogWarning($"[EmployeeMovement] 위치 보정 실패: 직원이 고체 안에 갇혀있음 {footTile}");
            }
        }
    }

    #endregion

    #region 업데이트

    void Update()
    {
        CheckGroundAndFall();
    }

    #endregion

    #region 낙하 처리

    /// <summary>
    /// 매 프레임 바닥을 확인하고 낙하를 처리합니다.
    /// </summary>
    private void CheckGroundAndFall()
    {
        if (gameMap == null) return;

        Vector2Int footTile = GetFootTile();
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

    /// <summary>
    /// 낙하로 인해 이동을 중단합니다.
    /// 이동 실패 콜백을 호출합니다.
    /// </summary>
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

    /// <summary>
    /// 지정한 타일에 바닥이 있는지 확인합니다.
    /// 고체 타일, FloorTile, 건설된 바닥 타일을 모두 확인합니다.
    /// </summary>
    /// <param name="tilePos">확인할 타일 좌표</param>
    /// <returns>바닥 존재 여부</returns>
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

        // 건설된 바닥 타일 (OccupiedGrid=true, BlocksMovement=false)
        if (gameMap.IsTileOccupied(tilePos.x, tilePos.y) && !gameMap.DoesTileBlockMovement(tilePos.x, tilePos.y))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 바닥에 착지합니다.
    /// X/Y 좌표 보정, 고체 안 진입 보정을 수행한 뒤 OnLanded 이벤트를 발생시킵니다.
    /// </summary>
    /// <param name="groundTile">밟고 있는 바닥 타일 좌표</param>
    private void LandOnGround(Vector2Int groundTile)
    {
        isFalling = false;

        int landingY = groundTile.y + 1;

        float currentX = transform.position.x;
        int floorX = Mathf.FloorToInt(currentX);
        int ceilX = Mathf.CeilToInt(currentX);

        int landingTileX = floorX;

        // 착지 위치 유효성 검증 로컬 함수
        bool IsValidLandingX(int x, int y)
        {
            if (x < 0 || x >= GameMap.MAP_WIDTH) return false;
            if (y < 0 || y >= GameMap.MAP_HEIGHT) return false;

            if (gameMap.TileGrid[x, y] != 0) return false;

            if (y + 1 < GameMap.MAP_HEIGHT && gameMap.TileGrid[x, y + 1] != 0) return false;

            int groundY = y - 1;
            if (groundY < 0) return false;

            bool hasGround = gameMap.TileGrid[x, groundY] != 0 ||
                             FloorTile.HasFloorTileAt(new Vector2Int(x, groundY)) ||
                             (gameMap.IsTileOccupied(x, groundY) && !gameMap.DoesTileBlockMovement(x, groundY));

            return hasGround;
        }

        if (!IsValidLandingX(floorX, landingY))
        {
            if (IsValidLandingX(ceilX, landingY))
            {
                landingTileX = ceilX;
                if (showDebugLogs)
                    Debug.Log($"[EmployeeMovement] X 보정: {floorX} -> {ceilX} (Ceil)");
            }
            else
            {
                Debug.LogWarning($"[EmployeeMovement] 유효한 착지 X 좌표 없음! 현재 X={currentX}, 착지 Y={landingY}");
            }
        }

        // 착지 위치가 고체인 경우 위로 올려서 빈 공간 찾기
        if (landingY < GameMap.MAP_HEIGHT && gameMap.TileGrid[landingTileX, landingY] != 0)
        {
            for (int dy = 1; dy <= MAX_LANDING_ADJUST_HEIGHT; dy++)
            {
                int checkY = landingY + dy;
                if (checkY >= GameMap.MAP_HEIGHT) break;

                if (gameMap.TileGrid[landingTileX, checkY] == 0)
                {
                    landingY = checkY;
                    Debug.Log($"[EmployeeMovement] 착지 Y 보정: {groundTile.y + 1} -> {landingY}");
                    break;
                }
            }
        }

        Vector3 landingPos = new Vector3(landingTileX + 0.5f, landingY, 0);
        transform.position = landingPos;

        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeMovement] 착지 완료: 바닥타일={groundTile}, 최종위치={transform.position}");
        }

        Vector2Int footTile = GetFootTile();
        OnLanded?.Invoke(footTile);
    }

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

        if (currentTile == goalTile)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[EmployeeMovement] 이미 목표 위치에 있음");
            }
            onComplete?.Invoke();
            return;
        }

        currentPath = pathfinder.FindPath(currentTile, goalTile);

        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogWarning($"[EmployeeMovement] 경로를 찾을 수 없습니다: {currentTile} -> {goalTile}");
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
    /// 기존 호환성을 위한 오버로드 (실패 콜백 없이).
    /// </summary>
    /// <param name="worldDestination">목표 월드 좌표</param>
    /// <param name="onComplete">이동 성공 시 콜백</param>
    public void MoveTo(Vector3 worldDestination, Action onComplete)
    {
        MoveTo(worldDestination, onComplete, null);
    }

    /// <summary>
    /// 경로를 따라 이동하는 코루틴.
    /// </summary>
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

    /// <summary>
    /// 단일 타일로 Lerp 이동하는 코루틴.
    /// 높이차가 클수록 이동 속도가 감소합니다.
    /// </summary>
    /// <param name="targetTile">목표 타일</param>
    /// <param name="heightDiff">높이차</param>
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

        float speedModifier = 1f + Mathf.Abs(heightDiff) * HEIGHT_SPEED_PENALTY;
        float tileSpeedMult = GetTileSpeedMultiplier(targetTile);
        float actualSpeed = tileTransitionSpeed * tileSpeedMult / speedModifier;

        while (Vector3.Distance(transform.position, endPos) > stoppingDistance)
        {
            if (!isMoving) yield break;

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

    /// <summary>
    /// 특정 월드 좌표로 직접 Lerp 이동하는 코루틴.
    /// 경로의 마지막 지점에서 정확한 목표 위치까지 미세 조정할 때 사용합니다.
    /// </summary>
    /// <param name="targetPos">목표 월드 좌표</param>
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

    /// <summary>
    /// 이동 중 물리 시뮬레이션을 활성화/비활성화합니다.
    /// 이동 중에는 Kinematic으로 전환하여 물리 간섭을 방지합니다.
    /// </summary>
    /// <param name="enable">활성화 여부</param>
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

    /// <summary>
    /// 이동 방향에 따라 스프라이트를 좌우 반전합니다.
    /// </summary>
    /// <param name="xDirection">X축 이동 방향</param>
    private void UpdateSpriteDirection(float xDirection)
    {
        if (Mathf.Abs(xDirection) < DIRECTION_THRESHOLD) return;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(xDirection);
        transform.localScale = scale;
    }

    /// <summary>
    /// 목적지 도착 처리.
    /// 이동 상태를 초기화하고 성공 콜백을 호출합니다.
    /// </summary>
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

    /// <summary>
    /// 이동을 즉시 중지합니다.
    /// 코루틴 중지, 상태 초기화, 물리 복원을 수행합니다.
    /// </summary>
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

    #region 좌표 변환

    /// <summary>
    /// 직원의 현재 발 위치 타일 좌표를 반환합니다.
    /// 피벗이 Bottom Center이므로 FloorToInt를 사용합니다.
    /// </summary>
    /// <returns>발 위치 타일 좌표</returns>
    public Vector2Int GetFootTile()
    {
        return new Vector2Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y)
        );
    }

    /// <summary>
    /// 월드 좌표를 발 위치 타일 좌표로 변환합니다.
    /// </summary>
    /// <param name="worldPos">변환할 월드 좌표</param>
    /// <returns>타일 좌표</returns>
    private Vector2Int WorldToFootTile(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y)
        );
    }

    /// <summary>
    /// 타일 좌표를 직원 월드 좌표로 변환합니다.
    /// 피벗이 Bottom Center이므로 타일 중앙(+0.5f)으로 변환합니다.
    /// </summary>
    /// <param name="tilePos">변환할 타일 좌표 (발 위치)</param>
    /// <returns>월드 좌표</returns>
    private Vector3 TileToWorld(Vector2Int tilePos)
    {
        return new Vector3(tilePos.x + 0.5f, tilePos.y, 0);
    }

    /// <summary>
    /// 타일 위치의 이동 속도 배율을 반환합니다.
    /// FloorTile이 있으면 해당 속도 배율, 없으면 기본 1.0을 반환합니다.
    /// </summary>
    /// <param name="tilePos">조회할 타일 좌표</param>
    /// <returns>이동 속도 배율 (기본 1.0)</returns>
    private float GetTileSpeedMultiplier(Vector2Int tilePos)
    {
        // 발 아래 타일(groundY)에 FloorTile이 있는지 확인
        Vector2Int groundPos = new Vector2Int(tilePos.x, tilePos.y - 1);
        FloorTile floorTile = FloorTile.GetFloorTileAt(groundPos);

        if (floorTile != null)
        {
            return floorTile.GetMovementSpeedMultiplier();
        }

        // 현재 타일 위치에도 확인 (사다리 등)
        FloorTile currentTile = FloorTile.GetFloorTileAt(tilePos);
        if (currentTile != null)
        {
            return currentTile.GetMovementSpeedMultiplier();
        }

        return 1.0f;
    }

    #endregion

    #region 충돌 처리

    /// <summary>
    /// 이동 중 충돌 시 경로를 재탐색합니다.
    /// </summary>
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

    #region 공개 API

    /// <summary>
    /// 현재 위치가 유효하지 않으면 (고체 안에 있으면) 위치를 보정합니다.
    /// 주의: 일반적인 채광 후에는 자연 낙하(CheckGroundAndFall)를 사용하세요.
    /// 이 함수는 텔레포트 등 특수한 상황에서만 사용합니다.
    /// </summary>
    public void ValidateAndFixPosition()
    {
        if (gameMap == null) return;

        Vector2Int footTile = GetFootTile();

        if (footTile.x >= 0 && footTile.x < GameMap.MAP_WIDTH &&
            footTile.y >= 0 && footTile.y < GameMap.MAP_HEIGHT)
        {
            int tileId = gameMap.TileGrid[footTile.x, footTile.y];
            if (tileId != 0)
            {
                Debug.LogWarning($"[EmployeeMovement] 직원이 고체 안에 있음! 위치 보정 시도: {footTile}");
                AdjustPositionIfInsideSolid();
            }
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
}
