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
    [SerializeField] private float tileTransitionSpeed = 5f; // 타일 간 이동 속도

    [Header("낙하 설정")]
    [SerializeField] private float fallSpeed = 8f;

    [Header("디버그")]
    [SerializeField] private bool showPath = true;
    [SerializeField] private bool showDebugLogs = false;

    private Vector3 targetPosition;
    private bool isFalling = false;
    private bool isMoving = false;
    private Action onReachDestination;
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
    private bool originalUseGravity;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        employee = GetComponent<Employee>();

        // Rigidbody2D 설정
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 원본 물리 상태 저장
            originalBodyType = rb.bodyType;
            originalUseGravity = rb.gravityScale > 0;
        }

        if (col == null)
        {
            Debug.LogWarning("[EmployeeMovement] Collider2D가 없습니다. 추가해주세요.");
        }
    }
    
    void Start()
    {
        // 게임 맵과 길찾기 시스템 초기화
        if (MapGenerator.instance != null)
        {
            gameMap = MapGenerator.instance.GameMapInstance;
            pathfinder = new TilePathfinder(gameMap);
        }
        else
        {
            Debug.LogError("[EmployeeMovement] MapGenerator를 찾을 수 없습니다!");
        }
    }

    void Update()
    {
        // 항상 바닥 체크 (어떤 상태에서도 바닥이 사라지면 낙하)
        CheckGroundAndFall();
    }

    /// <summary>
    /// 바닥이 있는지 확인하고 없으면 낙하 처리
    /// </summary>
    private void CheckGroundAndFall()
    {
        if (gameMap == null) return;

        Vector2Int currentTile = WorldToTile(transform.position);

        // 현재 발 아래 타일 확인
        if (!HasGroundAt(currentTile))
        {
            // 바닥이 없으면 낙하 시작
            if (!isFalling)
            {
                isFalling = true;

                // 이동 중이면 이동 중단
                if (isMoving)
                {
                    StopMovingForFall();
                }

                if (showDebugLogs)
                {
                    Debug.Log($"[EmployeeMovement] 바닥 없음! 낙하 시작: {currentTile}");
                }
            }

            // 아래로 이동
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;

            // 새로운 위치에서 바닥 확인
            Vector2Int newTile = WorldToTile(transform.position);
            if (HasGroundAt(newTile))
            {
                // 바닥에 착지
                LandOnGround(newTile);
            }
        }
        else if (isFalling)
        {
            // 이미 바닥이 있는데 falling 상태면 착지 처리
            LandOnGround(currentTile);
        }
    }

    /// <summary>
    /// 낙하를 위해 이동을 중단합니다 (콜백 없이).
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
        onReachDestination = null;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        Debug.Log("[EmployeeMovement] 낙하로 인해 이동 중단");
    }

    /// <summary>
    /// 해당 타일 위치에 바닥(서 있을 수 있는 곳)이 있는지 확인
    /// </summary>
    private bool HasGroundAt(Vector2Int tilePos)
    {
        // 맵 범위 체크
        if (tilePos.x < 0 || tilePos.x >= GameMap.MAP_WIDTH ||
            tilePos.y < 0 || tilePos.y >= GameMap.MAP_HEIGHT)
        {
            return false;
        }

        // 1. 고체 타일이 있는지 확인 (발 아래)
        int tileId = gameMap.TileGrid[tilePos.x, tilePos.y];
        if (tileId != 0)
        {
            return true;
        }

        // 2. 바닥 타일(FloorTile)이 있는지 확인
        if (FloorTile.HasFloorTileAt(tilePos))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 바닥에 착지
    /// </summary>
    private void LandOnGround(Vector2Int groundTile)
    {
        isFalling = false;

        // 정확한 위치로 스냅
        Vector3 landingPos = TileToWorld(groundTile);
        transform.position = landingPos;

        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeMovement] 착지 완료: {groundTile}");
        }
    }
    
    /// <summary>
    /// 목표 월드 좌표로 이동합니다 (자동 길찾기).
    /// </summary>
    public void MoveTo(Vector3 worldDestination, Action onComplete = null)
    {
        if (pathfinder == null)
        {
            Debug.LogError("[EmployeeMovement] Pathfinder가 초기화되지 않았습니다!");
            return;
        }

        StopMoving();

        // 월드 좌표를 타일 좌표로 변환
        Vector2Int currentTile = WorldToTile(transform.position);
        Vector2Int goalTile = WorldToTile(worldDestination);

        Debug.Log($"[EmployeeMovement] 이동 요청 - 현재 Transform: {transform.position}, 목표: {worldDestination}");
        Debug.Log($"[EmployeeMovement] 경로 탐색: {currentTile} -> {goalTile}");

        // 경로 찾기
        currentPath = pathfinder.FindPath(currentTile, goalTile);

        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogWarning($"[EmployeeMovement] 경로를 찾을 수 없습니다: {currentTile} -> {goalTile}");

            // 경로를 찾지 못했을 때도 콜백 호출
            onComplete?.Invoke();
            return;
        }

        targetPosition = worldDestination;
        onReachDestination = onComplete;
        currentPathIndex = 0;
        isMoving = true;

        Debug.Log($"[EmployeeMovement] 경로 발견: {currentPath.Count}개 타일");
        for (int i = 0; i < Mathf.Min(currentPath.Count, 5); i++)
        {
            Debug.Log($"  경로[{i}]: {currentPath[i]}");
        }

        moveCoroutine = StartCoroutine(FollowPathCoroutine());
    }
    
    /// <summary>
    /// 경로를 따라 이동하는 코루틴 (노드 기반 이동)
    /// </summary>
    private IEnumerator FollowPathCoroutine()
    {
        // 이동 시작: 물리 충돌 무시
        EnablePhysicsForMovement(false);

        while (isMoving && currentPathIndex < currentPath.Count)
        {
            Vector2Int currentTile = (currentPathIndex > 0) ? currentPath[currentPathIndex - 1] : WorldToTile(transform.position);
            Vector2Int nextTile = currentPath[currentPathIndex];

            // 높이 차이 계산
            int heightDiff = nextTile.y - currentTile.y;

            if (showDebugLogs)
            {
                Debug.Log($"[EmployeeMovement] 타일 이동: {currentTile} -> {nextTile} (높이차: {heightDiff})");
            }

            // 노드 간 Lerp 이동
            yield return MoveToTileCoroutine(nextTile, heightDiff);

            // 다음 타일로 이동
            currentPathIndex++;
        }

        // 최종 목표 지점으로 미세 조정
        if (Vector3.Distance(transform.position, targetPosition) > stoppingDistance)
        {
            yield return MoveToPositionCoroutine(targetPosition);
        }

        // 이동 완료: 물리 충돌 복구
        EnablePhysicsForMovement(true);

        // 목적지 도착
        ReachDestination();
    }

    /// <summary>
    /// 특정 타일로 Lerp 이동
    /// </summary>
    private IEnumerator MoveToTileCoroutine(Vector2Int targetTile, int heightDiff)
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = TileToWorld(targetTile);
        float journeyLength = Vector3.Distance(startPos, endPos);
        float startTime = Time.time;

        Debug.Log($"[EmployeeMovement] Lerp 이동 시작: {startPos} -> {endPos} (거리: {journeyLength:F2})");

        // 높이 차이가 있으면 이동 속도 조정
        float speedModifier = 1f + Mathf.Abs(heightDiff) * 0.2f; // 높이 차이만큼 느려짐
        float actualSpeed = tileTransitionSpeed / speedModifier;

        int frameCount = 0;
        while (Vector3.Distance(transform.position, endPos) > stoppingDistance)
        {
            float distCovered = (Time.time - startTime) * actualSpeed;
            float fractionOfJourney = distCovered / journeyLength;

            // Lerp로 부드럽게 이동
            transform.position = Vector3.Lerp(startPos, endPos, fractionOfJourney);

            // 스프라이트 방향 전환
            UpdateSpriteDirection(endPos.x - startPos.x);

            frameCount++;
            if (frameCount % 10 == 0)
            {
                Debug.Log($"[EmployeeMovement] 이동 중: {transform.position}, fraction: {fractionOfJourney:F2}");
            }

            yield return null;
        }

        // 정확히 목표 위치로 스냅
        transform.position = endPos;
        Debug.Log($"[EmployeeMovement] Lerp 이동 완료: {endPos}");
    }

    /// <summary>
    /// 특정 월드 좌표로 Lerp 이동
    /// </summary>
    private IEnumerator MoveToPositionCoroutine(Vector3 targetPos)
    {
        Vector3 startPos = transform.position;
        float journeyLength = Vector3.Distance(startPos, targetPos);
        float startTime = Time.time;

        while (Vector3.Distance(transform.position, targetPos) > stoppingDistance)
        {
            float distCovered = (Time.time - startTime) * tileTransitionSpeed;
            float fractionOfJourney = distCovered / journeyLength;

            transform.position = Vector3.Lerp(startPos, targetPos, fractionOfJourney);

            UpdateSpriteDirection(targetPos.x - startPos.x);

            yield return null;
        }

        transform.position = targetPos;
    }

    /// <summary>
    /// 이동 중 물리 상태 제어
    /// </summary>
    private void EnablePhysicsForMovement(bool enable)
    {
        if (rb != null)
        {
            if (enable)
            {
                // 물리 복구
                rb.bodyType = originalBodyType;
                if (showDebugLogs)
                {
                    Debug.Log("[EmployeeMovement] 물리 충돌 복구");
                }
            }
            else
            {
                // 물리 무시 (Kinematic으로 전환)
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                if (showDebugLogs)
                {
                    Debug.Log("[EmployeeMovement] 물리 충돌 무시 (Kinematic)");
                }
            }
        }
    }
    
    /// <summary>
    /// 현재 위치의 이동 속도 배율을 가져옵니다.
    /// </summary>
    private float GetCurrentSpeedMultiplier()
    {
        Vector2Int currentTile = WorldToTile(transform.position);
        
        // 바닥 타일이 있는지 확인
        FloorTile floorTile = FloorTile.GetFloorTileAt(currentTile);
        if (floorTile != null)
        {
            return floorTile.GetMovementSpeedMultiplier();
        }
        
        // 바닥 타일이 없으면 기본 속도
        return 1f;
    }
    
    /// <summary>
    /// 타일 좌표로 직접 이동합니다 (경로 없이).
    /// </summary>
    public void MoveToTile(Vector2Int tilePos, Action onComplete = null)
    {
        Vector3 worldPos = TileToWorld(tilePos);
        MoveTo(worldPos, onComplete);
    }
    
    private void UpdateSpriteDirection(float xDirection)
    {
        if (Mathf.Abs(xDirection) < 0.01f) return;
        
        // 스프라이트 좌우 반전
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
        
        if (onReachDestination != null)
        {
            var callback = onReachDestination;
            onReachDestination = null;
            callback.Invoke();
        }
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

        // 물리 복구
        EnablePhysicsForMovement(true);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
    
    /// <summary>
    /// 월드 좌표를 타일 좌표로 변환합니다.
    /// 직원의 발 위치(y)에서 1을 빼서 실제 타일 좌표를 얻습니다.
    /// </summary>
    private Vector2Int WorldToTile(Vector3 worldPos)
    {
        // 직원은 타일 위(y+1)에 있으므로, 타일 좌표는 y-1
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y - 1f)  // ★ 수정: y-1
        );
    }

    /// <summary>
    /// 타일 좌표를 월드 좌표로 변환합니다.
    /// 직원은 타일 위를 걸어다니므로 y+1 위치로 이동합니다.
    /// 직원의 스프라이트 피벗이 중심(0.5)이고 높이가 2이므로, y+2 위치가 직원 중심입니다.
    /// </summary>
    private Vector3 TileToWorld(Vector2Int tilePos)
    {
        // 타일 위 (y+1)에 직원 발이 오도록, 직원 중심은 y+2
        return new Vector3(tilePos.x + 0.5f, tilePos.y + 2f, 0);
    }
    
    // 장애물 회피
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isMoving)
        {
            // 충돌 시 경로 재탐색
            if (showDebugLogs)
            {
                Debug.Log($"[EmployeeMovement] 충돌 감지, 경로 재탐색");
            }
            
            Vector3 currentTarget = targetPosition;
            Action currentCallback = onReachDestination;
            
            StopMoving();
            MoveTo(currentTarget, currentCallback);
        }
    }
    
    // 디버그 시각화
    void OnDrawGizmos()
    {
        if (!showPath || currentPath == null || currentPath.Count == 0)
            return;
        
        Gizmos.color = Color.yellow;
        
        // 현재 위치에서 첫 경로 지점까지
        Vector3 currentPos = transform.position;
        if (currentPathIndex < currentPath.Count)
        {
            Vector3 firstPathPoint = TileToWorld(currentPath[currentPathIndex]);
            Gizmos.DrawLine(currentPos, firstPathPoint);
            currentPos = firstPathPoint;
        }
        
        // 경로 선 그리기
        for (int i = currentPathIndex; i < currentPath.Count - 1; i++)
        {
            Vector3 from = TileToWorld(currentPath[i]);
            Vector3 to = TileToWorld(currentPath[i + 1]);
            Gizmos.DrawLine(from, to);
        }
        
        // 경로 지점 표시
        Gizmos.color = Color.green;
        foreach (var tile in currentPath)
        {
            Vector3 pos = TileToWorld(tile);
            Gizmos.DrawWireCube(pos, Vector3.one * 0.3f);
        }
        
        // 목표 지점 표시
        if (currentPath.Count > 0)
        {
            Gizmos.color = Color.red;
            Vector3 goalPos = TileToWorld(currentPath[currentPath.Count - 1]);
            Gizmos.DrawWireSphere(goalPos, 0.5f);
        }
    }
    
    // Public 프로퍼티
    public bool IsMoving => isMoving;
    public bool IsFalling => isFalling;
    public Vector3 TargetPosition => targetPosition;
    public float DistanceToTarget => Vector3.Distance(transform.position, targetPosition);
    public List<Vector2Int> CurrentPath => currentPath;
}