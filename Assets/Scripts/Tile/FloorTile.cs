using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 사다리, 가공된 바닥 등 건설 시스템으로 설치하고 철거 시스템으로 제거하는 바닥 타일
/// Building 컴포넌트와 함께 사용됩니다.
/// </summary>
[RequireComponent(typeof(Building))]
public class FloorTile : MonoBehaviour
{
    [Header("바닥 타일 속성")]
    [Tooltip("이 바닥 타일의 타입")]
    [SerializeField] private FloorTileType tileType;
    
    [Tooltip("이동 속도 배율 (1.0 = 기본 속도)")]
    [SerializeField] private float movementSpeedMultiplier = 1.0f;
    
    [Tooltip("통과 가능 여부 (사다리만 true)")]
    [SerializeField] private bool isPassable = false;
    
    [Tooltip("수직 이동 가능 여부 (사다리만 true)")]
    [SerializeField] private bool allowsVerticalMovement = false;
    
    private Building building;
    private Vector2Int gridPosition;
    
    // 정적 딕셔너리: 위치별 바닥 타일 추적
    private static Dictionary<Vector2Int, FloorTile> floorTileRegistry = new Dictionary<Vector2Int, FloorTile>();
    
    void Awake()
    {
        building = GetComponent<Building>();
    }
    
    void Start()
    {
        // 설치된 위치 계산
        gridPosition = new Vector2Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y)
        );
        
        // 레지스트리에 등록
        RegisterToRegistry();
    }
    
    void OnDestroy()
    {
        // 레지스트리에서 제거
        UnregisterFromRegistry();
    }
    
    private void RegisterToRegistry()
    {
        if (!floorTileRegistry.ContainsKey(gridPosition))
        {
            floorTileRegistry[gridPosition] = this;
            Debug.Log($"[FloorTile] 바닥 타일 등록: {tileType} at {gridPosition}");
        }
    }
    
    private void UnregisterFromRegistry()
    {
        if (floorTileRegistry.ContainsKey(gridPosition))
        {
            floorTileRegistry.Remove(gridPosition);
            Debug.Log($"[FloorTile] 바닥 타일 제거: {tileType} at {gridPosition}");
        }
    }
    
    /// <summary>
    /// 특정 위치에 바닥 타일이 있는지 확인
    /// </summary>
    public static FloorTile GetFloorTileAt(Vector2Int position)
    {
        floorTileRegistry.TryGetValue(position, out FloorTile tile);
        return tile;
    }
    
    /// <summary>
    /// 특정 위치에 바닥 타일이 있는지 확인
    /// </summary>
    public static bool HasFloorTileAt(Vector2Int position)
    {
        return floorTileRegistry.ContainsKey(position);
    }
    
    /// <summary>
    /// 이동 속도 배율을 반환합니다.
    /// </summary>
    public float GetMovementSpeedMultiplier()
    {
        // 건물이 비활성화 상태면 속도 감소
        if (building != null && !building.IsFunctional)
        {
            return movementSpeedMultiplier * 0.5f;
        }
        
        return movementSpeedMultiplier;
    }
    
    /// <summary>
    /// 통과 가능 여부를 반환합니다.
    /// </summary>
    public bool IsPassable()
    {
        // 건물이 비활성화 상태면 통과 불가
        if (building != null && !building.IsFunctional)
        {
            return false;
        }
        
        return isPassable;
    }
    
    /// <summary>
    /// 수직 이동 가능 여부를 반환합니다.
    /// </summary>
    public bool AllowsVerticalMovement()
    {
        if (building != null && !building.IsFunctional)
        {
            return false;
        }
        
        return allowsVerticalMovement;
    }
    
    // Public 프로퍼티
    public FloorTileType TileType => tileType;
    public Vector2Int GridPosition => gridPosition;
    public bool IsFunctional => building != null && building.IsFunctional;
    
    // 정적 유틸리티
    public static void ClearRegistry()
    {
        floorTileRegistry.Clear();
    }
}

/// <summary>
/// 바닥 타일 타입
/// </summary>
public enum FloorTileType
{
    WoodFloor,      // 나무 바닥 (속도: 1.0)
    StoneFloor,     // 돌 바닥 (속도: 1.0)
    MetalFloor,     // 금속 바닥 (속도: 1.2)
    WoodLadder,     // 나무 사다리 (속도: 0.8, 수직 이동 가능)
    MetalLadder,    // 금속 사다리 (속도: 0.9, 수직 이동 가능)
    Catwalk,        // 좁은 통로 (속도: 0.9)
    Bridge          // 다리 (속도: 1.0)
}