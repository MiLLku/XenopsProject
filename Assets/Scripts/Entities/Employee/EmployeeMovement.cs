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
    
    [Header("디버그")]
    [SerializeField] private bool showPath = true;
    [SerializeField] private bool showDebugLogs = false;
    
    private Vector3 targetPosition;
    private bool isMoving = false;
    private Action onReachDestination;
    private Coroutine moveCoroutine;
    
    // 타일 기반 경로
    private List<Vector2Int> currentPath;
    private int currentPathIndex = 0;
    
    // 컴포넌트 참조
    private Rigidbody2D rb;
    private Employee employee;
    private GameMap gameMap;
    private TilePathfinder pathfinder;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        employee = GetComponent<Employee>();
        
        // Rigidbody2D 설정
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
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
        
        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeMovement] 경로 탐색: {currentTile} -> {goalTile}");
        }
        
        // 경로 찾기
        currentPath = pathfinder.FindPath(currentTile, goalTile);
        
        if (currentPath == null || currentPath.Count == 0)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"[EmployeeMovement] 경로를 찾을 수 없습니다: {currentTile} -> {goalTile}");
            }
            
            // 경로를 찾지 못했을 때도 콜백 호출
            onComplete?.Invoke();
            return;
        }
        
        targetPosition = worldDestination;
        onReachDestination = onComplete;
        currentPathIndex = 0;
        isMoving = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"[EmployeeMovement] 경로 발견: {currentPath.Count}개 타일");
        }
        
        moveCoroutine = StartCoroutine(FollowPathCoroutine());
    }
    
    /// <summary>
    /// 경로를 따라 이동하는 코루틴
    /// </summary>
    private IEnumerator FollowPathCoroutine()
    {
        while (isMoving && currentPathIndex < currentPath.Count)
        {
            Vector2Int nextTile = currentPath[currentPathIndex];
            Vector3 nextWorldPos = TileToWorld(nextTile);
            
            // 다음 타일까지 이동
            while (Vector3.Distance(transform.position, nextWorldPos) > stoppingDistance)
            {
                // 현재 위치의 이동 속도 배율 가져오기
                float speedMultiplier = GetCurrentSpeedMultiplier();
                
                // 이동
                Vector3 direction = (nextWorldPos - transform.position).normalized;
                float currentSpeed = baseSpeed * speedMultiplier;
                Vector3 movement = direction * currentSpeed * Time.fixedDeltaTime;
                
                if (rb != null)
                {
                    rb.MovePosition(transform.position + movement);
                }
                else
                {
                    transform.position += movement;
                }
                
                // 스프라이트 방향 전환
                UpdateSpriteDirection(direction.x);
                
                yield return new WaitForFixedUpdate();
            }
            
            // 다음 타일로 이동
            currentPathIndex++;
        }
        
        // 최종 목표 지점까지 이동
        while (Vector3.Distance(transform.position, targetPosition) > stoppingDistance)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            Vector3 movement = direction * baseSpeed * Time.fixedDeltaTime;
            
            if (rb != null)
            {
                rb.MovePosition(transform.position + movement);
            }
            else
            {
                transform.position += movement;
            }
            
            UpdateSpriteDirection(direction.x);
            
            yield return new WaitForFixedUpdate();
        }
        
        // 목적지 도착
        ReachDestination();
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
        
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
    
    /// <summary>
    /// 월드 좌표를 타일 좌표로 변환합니다.
    /// </summary>
    private Vector2Int WorldToTile(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y)
        );
    }
    
    /// <summary>
    /// 타일 좌표를 월드 좌표(타일 중심)로 변환합니다.
    /// </summary>
    private Vector3 TileToWorld(Vector2Int tilePos)
    {
        return new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0);
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
    public Vector3 TargetPosition => targetPosition;
    public float DistanceToTarget => Vector3.Distance(transform.position, targetPosition);
    public List<Vector2Int> CurrentPath => currentPath;
}