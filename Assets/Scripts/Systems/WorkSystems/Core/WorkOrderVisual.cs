using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 작업물의 시각적 표현 (외곽선 + 라벨)
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D))]
public class WorkOrderVisual : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private WorkOrder workOrder;
    [SerializeField] private List<Vector3Int> tilePositions = new List<Vector3Int>();

    [Header("외곽선 설정")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color pendingColor = new Color(0.2f, 0.8f, 1f, 0.8f);
    [SerializeField] private float lineWidth = 0.05f;

    [Header("UI 연결")]
    [SerializeField] private GameObject labelPrefab;

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private BoxCollider2D boxCollider;
    private GameObject labelInstance;
    private TMPro.TextMeshProUGUI labelText;
    
    private bool isSelected = false;
    private bool isPendingMode = false;

    public WorkOrder WorkOrder => workOrder;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;

        meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    /// <summary>
    /// 정식 작업물로 초기화할 때 호출
    /// </summary>
    public void Initialize(WorkOrder order, List<Vector3Int> tiles)
    {
        workOrder = order;
        tilePositions = new List<Vector3Int>(tiles);
        isPendingMode = false;

        RefreshVisuals();
        CreateLabel();
    }

    /// <summary>
    /// 예약 모드에서 타일 목록만 업데이트할 때 호출
    /// </summary>
    public void UpdateTiles(List<Vector3Int> newTiles)
    {
        isPendingMode = true;
        tilePositions = new List<Vector3Int>(newTiles);
        
        RefreshVisuals();
        
        if (labelInstance != null) Destroy(labelInstance);
    }

    private void RefreshVisuals()
    {
        GenerateOutlineMesh();
        UpdateCollider();
        UpdateColor();
    }

    void Update()
    {
        if (isPendingMode) return;

        if (workOrder == null || workOrder.IsCompleted() || !workOrder.isActive)
        {
            Destroy(gameObject);
            return;
        }

        UpdateLabel();
        
        // 완료된 타일 제거 (시각적 업데이트)
        UpdateTileVisuals();
    }
    
    /// <summary>
    /// 완료된 타일을 시각적으로 제거
    /// </summary>
    private void UpdateTileVisuals()
    {
        if (workOrder == null || workOrder.taskQueue == null) return;
        
        // 현재 남은 작업 타일만 필터링
        var remainingTiles = new List<Vector3Int>();
        
        foreach (var tile in tilePositions)
        {
            // 해당 타일에 대한 작업이 아직 완료되지 않았는지 확인
            bool isCompleted = false;
            
            foreach (var task in workOrder.taskQueue.CompletedTasks)
            {
                Vector3 taskPos = task.GetPosition();
                if (Mathf.FloorToInt(taskPos.x) == tile.x && Mathf.FloorToInt(taskPos.y) == tile.y)
                {
                    isCompleted = true;
                    break;
                }
            }
            
            if (!isCompleted)
            {
                remainingTiles.Add(tile);
            }
        }
        
        // 변경되었으면 비주얼 업데이트
        if (remainingTiles.Count != tilePositions.Count)
        {
            tilePositions = remainingTiles;
            RefreshVisuals();
        }
    }

    private void GenerateOutlineMesh()
    {
        if (tilePositions == null || tilePositions.Count == 0)
        {
            meshFilter.mesh = null;
            return;
        }

        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        HashSet<Vector3Int> tileSet = new HashSet<Vector3Int>(tilePositions);

        Vector3Int[] directions = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };
        
        foreach (var tile in tilePositions)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!tileSet.Contains(tile + directions[i]))
                {
                    AddEdgeMesh(tile, i, vertices, triangles);
                }
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateBounds();
        
        meshFilter.mesh = mesh;
        
        meshRenderer.sortingLayerName = "Default";
        meshRenderer.sortingOrder = 100;
    }

    private void AddEdgeMesh(Vector3Int tilePos, int dirIndex, List<Vector3> verts, List<int> tris)
    {
        Vector3 center = new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0) - transform.position;
        
        float d = 0.5f;
        float t = lineWidth;

        Vector3[] quad = new Vector3[4];

        switch (dirIndex)
        {
            case 0: // Up
                quad[0] = center + new Vector3(-d, d, 0);
                quad[1] = center + new Vector3(d, d, 0);
                quad[2] = center + new Vector3(-d, d - t, 0);
                quad[3] = center + new Vector3(d, d - t, 0);
                break;
            case 1: // Right
                quad[0] = center + new Vector3(d - t, d, 0);
                quad[1] = center + new Vector3(d, d, 0);
                quad[2] = center + new Vector3(d - t, -d, 0);
                quad[3] = center + new Vector3(d, -d, 0);
                break;
            case 2: // Down
                quad[0] = center + new Vector3(-d, -d + t, 0);
                quad[1] = center + new Vector3(d, -d + t, 0);
                quad[2] = center + new Vector3(-d, -d, 0);
                quad[3] = center + new Vector3(d, -d, 0);
                break;
            case 3: // Left
                quad[0] = center + new Vector3(-d, d, 0);
                quad[1] = center + new Vector3(-d + t, d, 0);
                quad[2] = center + new Vector3(-d, -d, 0);
                quad[3] = center + new Vector3(-d + t, -d, 0);
                break;
        }

        int startIndex = verts.Count;
        verts.AddRange(quad);
        
        tris.Add(startIndex + 0); tris.Add(startIndex + 1); tris.Add(startIndex + 2);
        tris.Add(startIndex + 2); tris.Add(startIndex + 1); tris.Add(startIndex + 3);
    }

    private void UpdateCollider()
    {
        if (tilePositions.Count == 0) return;

        int minX = tilePositions.Min(t => t.x);
        int maxX = tilePositions.Max(t => t.x);
        int minY = tilePositions.Min(t => t.y);
        int maxY = tilePositions.Max(t => t.y);

        Vector2 center = new Vector2((minX + maxX) / 2f + 0.5f, (minY + maxY) / 2f + 0.5f);
        Vector2 size = new Vector2(maxX - minX + 1, maxY - minY + 1);

        boxCollider.offset = center - (Vector2)transform.position;
        boxCollider.size = size;
    }

    private void CreateLabel()
    {
        if (labelPrefab != null && !isPendingMode)
        {
            labelInstance = Instantiate(labelPrefab, transform);
            labelText = labelInstance.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            UpdateLabelPos();
        }
    }
    
    private void UpdateLabelPos()
    {
        if (labelInstance != null && tilePositions.Count > 0)
        {
            int maxY = tilePositions.Max(t => t.y);
            int minX = tilePositions.Min(t => t.x);
            int maxX = tilePositions.Max(t => t.x);
            
            Vector3 centerTop = new Vector3((minX + maxX) / 2f + 0.5f, maxY + 1.0f, 0);
            labelInstance.transform.position = new Vector3(centerTop.x, centerTop.y, 0);
        }
    }

    private void UpdateLabel()
    {
        if (labelText != null && workOrder != null)
        {
            // 큐 정보 표시
            int pending = workOrder.taskQueue.PendingCount;
            int assigned = workOrder.taskQueue.AssignedCount;
            int completed = workOrder.taskQueue.CompletedCount;
            
            labelText.text = $"{workOrder.orderName}\n" +
                            $"({workOrder.assignedWorkers.Count}/{workOrder.maxAssignedWorkers})\n" +
                            $"[{completed}/{pending + assigned + completed}]";
        }
    }
    
    private void UpdateColor()
    {
        Color c = isPendingMode ? pendingColor : (isSelected ? selectedColor : normalColor);
        if (meshRenderer != null) meshRenderer.material.color = c;
    }

    /// <summary>
    /// 클릭 시 호출 (InteractionManager에서)
    /// </summary>
    public void OnClicked()
    {
        if (isPendingMode) return;

        // WorkSystemManager를 통해 UI 표시
        if (WorkSystemManager.instance != null)
        {
            isSelected = true;
            UpdateColor();
            WorkSystemManager.instance.ShowAssignmentUI(workOrder, this);
        }
    }
    
    public void Deselect()
    {
        isSelected = false;
        UpdateColor();
    }
}