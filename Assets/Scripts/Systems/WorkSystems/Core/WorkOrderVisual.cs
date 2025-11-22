using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(LineRenderer))]
public class WorkOrderVisual : MonoBehaviour
{
    [Header("작업 더미 정보")]
    [SerializeField] private WorkOrder workOrder;
    [SerializeField] private List<Vector3Int> tilePositions = new List<Vector3Int>(); // 오타 수정 tilePosisitons -> tilePositions
    
    [Header("시각적 표현")]
    [SerializeField] private Color normalColor = new Color(1f, 0.8f, 0.3f, 0.7f);
    [SerializeField] private Color selectedColor = new Color(0.3f, 1f, 0.3f, 0.9f);
    
    [Header("UI 표시")]
    [SerializeField] private GameObject labelPrefab;
    private GameObject labelInstance;
    private TMPro.TextMeshProUGUI labelText;
    
    private bool isSelected = false;
    private LineRenderer lineRenderer; // TileHighlighter 대신 LineRenderer 사용 권장 (이전 코드 기반)
    private BoxCollider2D boxCollider;

    public WorkOrder WorkOrder => workOrder;

    void Awake()
    {
        // 라인 렌더러가 없다면 추가
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
        
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null) boxCollider = gameObject.AddComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
    }

    public void Initialize(WorkOrder order, List<Vector3Int> tiles)
    {
        workOrder = order;
        tilePositions = new List<Vector3Int>(tiles);

        // 1. 외곽선 그리기
        DrawOutline();
        
        // 2. 클릭 영역(콜라이더) 만들기
        UpdateCollider();
        
        // 3. 라벨(텍스트) 달기
        CreateLabel();
    }

    void Update()
    {
        if (workOrder == null || workOrder.IsCompleted() || !workOrder.isActive)
        {
            Destroy(gameObject);
            return;
        }

        // 선택 상태에 따라 색상 변경
        Color targetColor = isSelected ? selectedColor : normalColor;
        lineRenderer.startColor = targetColor;
        lineRenderer.endColor = targetColor;

        UpdateLabel();
    }

    private void DrawOutline()
    {
        if (tilePositions == null || tilePositions.Count == 0) return;

        List<Vector3> linePoints = new List<Vector3>();
        HashSet<Vector3Int> tileSet = new HashSet<Vector3Int>(tilePositions);

        // 상하좌우 체크용 오프셋
        Vector3Int[] directions = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };
        
        // 각 방향별 선분 그리기 오프셋 (타일 중심 기준)
        Vector3[] lineOffsets = { 
            new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f),   // Top
            new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f),   // Right
            new Vector3(0.5f, -0.5f), new Vector3(-0.5f, -0.5f), // Bottom
            new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f)  // Left
        };

        // 모든 타일을 순회하며 외곽선(이웃이 없는 변)을 찾음
        foreach (var tile in tilePositions)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!tileSet.Contains(tile + directions[i]))
                {
                    Vector3 center = new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0);
                    // 월드 좌표를 로컬 좌표로 변환하여 추가
                    linePoints.Add(center + lineOffsets[i * 2] - transform.position);
                    linePoints.Add(center + lineOffsets[i * 2 + 1] - transform.position);
                }
            }
        }

        lineRenderer.positionCount = linePoints.Count;
        lineRenderer.SetPositions(linePoints.ToArray());
        lineRenderer.loop = false; // 세그먼트 단위로 끊어서 그리므로 loop는 false
    }

    private void UpdateCollider()
    {
        // 기존 콜라이더가 있으면 제거 (안전장치)
        if(boxCollider != null) Destroy(boxCollider);
        
        boxCollider = gameObject.AddComponent<BoxCollider2D>();
        boxCollider.isTrigger = true; // 트리거로 설정하여 물리 충돌은 무시하고 클릭만 감지

        if (tilePositions.Count == 0) return;

        int minX = tilePositions.Min(t => t.x);
        int maxX = tilePositions.Max(t => t.x);
        int minY = tilePositions.Min(t => t.y);
        int maxY = tilePositions.Max(t => t.y);

        // 타일 뭉치의 정중앙과 크기 계산
        Vector2 center = new Vector2((minX + maxX) / 2f + 0.5f, (minY + maxY) / 2f + 0.5f);
        Vector2 size = new Vector2(maxX - minX + 1, maxY - minY + 1);

        // 로컬 좌표로 변환하여 설정
        boxCollider.offset = center - (Vector2)transform.position;
        boxCollider.size = size;
    }

    private void CreateLabel()
    {
        if (labelPrefab != null)
        {
            labelInstance = Instantiate(labelPrefab, transform);
            labelText = labelInstance.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            
            if(tilePositions.Count > 0)
            {
                 // 라벨 위치를 작업 더미의 가장 위쪽 중앙에 배치
                 int maxY = tilePositions.Max(t => t.y);
                 int minX = tilePositions.Min(t => t.x);
                 int maxX = tilePositions.Max(t => t.x);
                 
                 // 부모(Visual)의 위치를 고려한 월드 좌표 계산
                 float midX = (minX + maxX) / 2f; 
                 
                 // Visual 오브젝트가 (0,0,0)이 아닌 경우를 대비해 로컬 좌표로 설정하는 것이 안전함
                 labelInstance.transform.position = new Vector3(midX + 0.5f, maxY + 1.2f, 0);
            }
        }
    }

    private void UpdateLabel()
    {
        if (labelText != null && workOrder != null)
        {
            int assigned = workOrder.assignedWorkers.Count;
            int max = workOrder.maxAssignedWorkers;
            labelText.text = $"{assigned}/{max}"; 
        }
    }

    void OnMouseDown()
    {
        // 일반 모드에서만 클릭 가능
        if (InteractionManager.instance.GetCurrentMode() == InteractionManager.InteractMode.Normal)
        {
            Debug.Log($"[Visual] 작업 더미 클릭됨: {workOrder.orderName}");

            // ★★★ [수정됨] WorkAssignmentManager 호출 ★★★
            if (WorkAssignmentManager.instance != null)
            {
                isSelected = true;
                WorkAssignmentManager.instance.ShowAssignmentUI(workOrder, this, Input.mousePosition);
            }
            else
            {
                Debug.LogError("WorkAssignmentManager가 씬에 없습니다!");
            }
        }
    }
    
    public void Deselect()
    {
        isSelected = false;
    }
}