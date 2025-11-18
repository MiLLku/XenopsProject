using System;
using System.Collections;
using UnityEngine;

public class EmployeeMovement : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stoppingDistance = 0.5f;
    [SerializeField] private bool usePathfinding = false; // 나중에 A* 추가
    
    private Vector3 targetPosition;
    private bool isMoving = false;
    private Action onReachDestination;
    private Coroutine moveCoroutine;
    
    // 컴포넌트 참조
    private Rigidbody2D rb;
    private Employee employee;
    
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
        }
    }
    
    public void MoveTo(Vector3 destination, Action onComplete = null)
    {
        StopMoving();
        
        targetPosition = destination;
        onReachDestination = onComplete;
        isMoving = true;
        
        moveCoroutine = StartCoroutine(MoveCoroutine());
    }
    
    private IEnumerator MoveCoroutine()
    {
        while (isMoving)
        {
            float distance = Vector3.Distance(transform.position, targetPosition);
            
            if (distance <= stoppingDistance)
            {
                // 목적지 도착
                ReachDestination();
                yield break;
            }
            
            // 이동
            Vector3 direction = (targetPosition - transform.position).normalized;
            Vector3 movement = direction * moveSpeed * Time.fixedDeltaTime;
            
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
        onReachDestination = null;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
    
    public bool IsMoving => isMoving;
    public Vector3 TargetPosition => targetPosition;
    public float DistanceToTarget => Vector3.Distance(transform.position, targetPosition);
    
    // 장애물 회피 (간단한 버전)
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isMoving)
        {
            // 장애물을 만났을 때 우회 경로 계산
            Vector2 avoidDirection = Vector2.Perpendicular(targetPosition - transform.position).normalized;
            Vector3 newTarget = transform.position + new Vector3(avoidDirection.x, avoidDirection.y, 0) * 2f;
            
            // 임시 우회점으로 이동
            StartCoroutine(AvoidObstacle(newTarget));
        }
    }
    
    private IEnumerator AvoidObstacle(Vector3 avoidPoint)
    {
        Vector3 originalTarget = targetPosition;
        targetPosition = avoidPoint;
        
        yield return new WaitForSeconds(1f);
        
        targetPosition = originalTarget;
    }
}