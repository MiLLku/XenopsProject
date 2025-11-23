using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D))]
public class WorkOrderVisual : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private WorkOrder workOrder;
    [SerializeField] private List<Vector3Int> tilePositions = new List<Vector3Int>();

    [Header("외곽선 설정")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 1f); // 흰색
    [SerializeField] private Color selectedColor = new Color(1f, 0.8f, 0.2f, 1f); // 노란색
    [SerializeField] private Color pendingColor = new Color(0.2f, 0.8f, 1f, 0.8f); // 예약 중일 때 색상 (하늘색 등)
    [SerializeField] private float lineWidth = 0.05f; // 선 두께

    [Header("UI 연결")]
    [SerializeField] private GameObject labelPrefab;

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private BoxCollider2D boxCollider;
    private GameObject labelInstance;
    private TMPro.TextMeshProUGUI labelText;
    
    private bool isSelected = false;
    private bool isPendingMode = false; // 예약 모드 여부 (InteractionManager에서 사용)

    public WorkOrder WorkOrder => workOrder;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;

        // 기본 재질 설정
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
    /// [추가됨] 예약 모드에서 타일 목록만 업데이트할 때 호출
    /// </summary>
    public void UpdateTiles(List<Vector3Int> newTiles)
    {
        isPendingMode = true; // 예약 상태임
        tilePositions = new List<Vector3Int>(newTiles);
        
        RefreshVisuals();
        
        // 예약 모드일 때는 라벨을 끄거나 간단히 표시
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
        // 예약 모드일 때는 자동 파괴 로직을 건너뜀
        if (isPendingMode) return;

        // 정식 작업 모드일 때: 완료/취소 시 파괴
        if (workOrder == null || workOrder.IsCompleted() || !workOrder.isActive)
        {
            Destroy(gameObject);
            return;
        }

        UpdateLabel();
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
        // 월드 좌표를 로컬 좌표로 변환
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
        if(labelInstance != null && tilePositions.Count > 0)
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
            labelText.text = $"{workOrder.orderName}\n({workOrder.assignedWorkers.Count}/{workOrder.maxAssignedWorkers})";
        }
    }
    
    private void UpdateColor()
    {
        Color c = isPendingMode ? pendingColor : (isSelected ? selectedColor : normalColor);
        if(meshRenderer != null) meshRenderer.material.color = c;
    }

    public void OnClicked()
    {
        if (isPendingMode) return; // 예약 중인 더미는 클릭해도 UI 안 뜸

        if (WorkAssignmentManager.instance != null)
        {
            isSelected = true;
            UpdateColor();
            WorkAssignmentManager.instance.ShowAssignmentUI(workOrder, this, Input.mousePosition);
        }
    }
    
    public void Deselect()
    {
        isSelected = false;
        UpdateColor();
    }
}