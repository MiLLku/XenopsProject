using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 타일 기반 A* 길찾기 시스템
/// 직원은 2칸 높이를 차지하므로, 이동 시 수직 2칸 공간이 필요합니다.
/// </summary>
public class TilePathfinder
{
    private GameMap gameMap;
    private const int EMPLOYEE_HEIGHT = 2;
    
    private class PathNode
    {
        public Vector2Int position;
        public PathNode parent;
        public float gCost;
        public float hCost;
        public float fCost => gCost + hCost;
        
        public PathNode(Vector2Int pos)
        {
            position = pos;
        }
    }
    
    public TilePathfinder(GameMap map)
    {
        gameMap = map;
    }
    
    /// <summary>
    /// 시작점에서 목표점까지의 경로를 찾습니다.
    /// start와 goal은 모두 직원의 발 위치 타일 좌표입니다.
    /// </summary>
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        // 목표가 유효한지 확인
        if (!IsValidPosition(goal))
        {
            Debug.LogWarning($"[Pathfinder] 목표 위치가 유효하지 않음: {goal}");
            
            // 목표 근처의 유효한 위치 찾기 시도
            Vector2Int? nearestValid = FindNearestValidPosition(goal);
            if (nearestValid.HasValue)
            {
                Debug.Log($"[Pathfinder] 대체 목표 위치 사용: {nearestValid.Value}");
                goal = nearestValid.Value;
            }
            else
            {
                return null;
            }
        }
        
        // 시작 위치도 확인
        if (!IsValidPosition(start))
        {
            Debug.LogWarning($"[Pathfinder] 시작 위치가 유효하지 않음: {start}");
            return null;
        }
        
        List<PathNode> openList = new List<PathNode>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        
        PathNode startNode = new PathNode(start);
        startNode.gCost = 0;
        startNode.hCost = GetHeuristic(start, goal);
        openList.Add(startNode);
        
        Dictionary<Vector2Int, PathNode> allNodes = new Dictionary<Vector2Int, PathNode>();
        allNodes[start] = startNode;
        
        int iterations = 0;
        int maxIterations = 10000;
        
        while (openList.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            
            PathNode currentNode = openList.OrderBy(n => n.fCost).ThenBy(n => n.hCost).First();
            
            if (currentNode.position == goal)
            {
                Debug.Log($"[Pathfinder] 경로 발견! 반복: {iterations}회");
                return ReconstructPath(currentNode);
            }
            
            openList.Remove(currentNode);
            closedSet.Add(currentNode.position);
            
            foreach (Vector2Int neighbor in GetNeighbors(currentNode.position))
            {
                if (closedSet.Contains(neighbor))
                    continue;
                
                float moveCost = GetMovementCost(currentNode.position, neighbor);
                float tentativeGCost = currentNode.gCost + moveCost;
                
                PathNode neighborNode;
                if (!allNodes.TryGetValue(neighbor, out neighborNode))
                {
                    neighborNode = new PathNode(neighbor);
                    allNodes[neighbor] = neighborNode;
                }
                
                if (!openList.Contains(neighborNode))
                {
                    openList.Add(neighborNode);
                }
                else if (tentativeGCost >= neighborNode.gCost)
                {
                    continue;
                }
                
                neighborNode.parent = currentNode;
                neighborNode.gCost = tentativeGCost;
                neighborNode.hCost = GetHeuristic(neighbor, goal);
            }
        }
        
        if (iterations >= maxIterations)
        {
            Debug.LogWarning($"[Pathfinder] 최대 반복 횟수 초과");
        }
        else
        {
            Debug.LogWarning($"[Pathfinder] 경로 없음: {start} -> {goal}");
        }
        
        return null;
    }
    
    /// <summary>
    /// 목표 근처에서 유효한 위치를 찾습니다.
    /// </summary>
    private Vector2Int? FindNearestValidPosition(Vector2Int target)
    {
        // 주변 탐색 (반경 3칸)
        for (int radius = 1; radius <= 3; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                        continue; // 테두리만 탐색
                    
                    Vector2Int candidate = target + new Vector2Int(dx, dy);
                    if (IsValidPosition(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }
        
        return null;
    }
    
    private List<Vector2Int> ReconstructPath(PathNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        PathNode current = endNode;
        
        while (current != null)
        {
            path.Add(current.position);
            current = current.parent;
        }
        
        path.Reverse();
        
        if (path.Count > 0)
        {
            path.RemoveAt(0); // 시작점 제거
        }
        
        return path;
    }
    
    private float GetHeuristic(Vector2Int from, Vector2Int to)
    {
        return Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);
    }
    
    /// <summary>
    /// 현재 위치에서 이동 가능한 이웃 타일들을 반환합니다.
    /// </summary>
    private List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        // 수평 이동 (좌우)
        Vector2Int[] horizontalDirs = new Vector2Int[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
        };

        foreach (var dir in horizontalDirs)
        {
            // 같은 높이로 이동
            Vector2Int sameLevel = pos + dir;
            if (CanMoveTo(pos, sameLevel))
            {
                neighbors.Add(sameLevel);
            }

            // 1칸 위로 이동
            Vector2Int upOne = pos + dir + new Vector2Int(0, 1);
            if (CanMoveTo(pos, upOne))
            {
                neighbors.Add(upOne);
            }

            // 2칸 위로 이동
            Vector2Int upTwo = pos + dir + new Vector2Int(0, 2);
            if (CanMoveTo(pos, upTwo))
            {
                neighbors.Add(upTwo);
            }

            // 1칸 아래로 이동
            Vector2Int downOne = pos + dir + new Vector2Int(0, -1);
            if (CanMoveTo(pos, downOne))
            {
                neighbors.Add(downOne);
            }

            // 2칸 아래로 이동
            Vector2Int downTwo = pos + dir + new Vector2Int(0, -2);
            if (CanMoveTo(pos, downTwo))
            {
                neighbors.Add(downTwo);
            }
        }

        // 수직 이동 (사다리)
        Vector2Int up = pos + new Vector2Int(0, 1);
        if (CanMoveTo(pos, up))
        {
            neighbors.Add(up);
        }

        Vector2Int down = pos + new Vector2Int(0, -1);
        if (CanMoveTo(pos, down))
        {
            neighbors.Add(down);
        }

        return neighbors;
    }
    
    /// <summary>
    /// 한 타일에서 다른 타일로 이동할 수 있는지 확인합니다.
    /// </summary>
    private bool CanMoveTo(Vector2Int from, Vector2Int to)
    {
        if (!IsInBounds(to))
            return false;

        if (!IsValidPosition(to))
            return false;

        int heightDiff = to.y - from.y;
        int horizontalDiff = Mathf.Abs(to.x - from.x);

        // 순수 수평 이동
        if (heightDiff == 0 && horizontalDiff == 1)
        {
            return CanWalkHorizontally(from, to);
        }

        // 대각선 이동 (수평 1칸 + 높이 차이)
        if (horizontalDiff == 1 && Mathf.Abs(heightDiff) <= 2)
        {
            return CanMoveDiagonal(from, to, heightDiff);
        }

        // 순수 수직 이동 (사다리)
        if (horizontalDiff == 0 && Mathf.Abs(heightDiff) == 1)
        {
            if (heightDiff > 0)
                return CanClimbUp(from, to);
            else
                return CanClimbDown(from, to);
        }

        return false;
    }

    private bool CanMoveDiagonal(Vector2Int from, Vector2Int to, int heightDiff)
    {
        if (!IsValidPosition(to))
            return false;

        if (Mathf.Abs(heightDiff) > 2)
            return false;

        // 위로 올라가는 경우
        if (heightDiff > 0)
        {
            for (int y = from.y + 1; y <= to.y; y++)
            {
                if (!IsSpaceClear(to.x, y + EMPLOYEE_HEIGHT - 1))
                    return false;
            }
        }

        // 아래로 내려가는 경우
        if (heightDiff < 0)
        {
            if (Mathf.Abs(heightDiff) > 2)
                return false;
        }

        return true;
    }
    
    private bool CanWalkHorizontally(Vector2Int from, Vector2Int to)
    {
        // 발을 디딜 곳이 있어야 함
        FloorTile toFloorTile = FloorTile.GetFloorTileAt(to);
        
        // 바닥 타일이 있거나, 발 아래에 고체 타일이어야 함
        bool hasFloor = toFloorTile != null || gameMap.TileGrid[to.x, to.y] != 0;
        if (!hasFloor)
            return false;
        
        // 직원 몸통이 들어갈 2칸이 비어있어야 함
        for (int i = 1; i <= EMPLOYEE_HEIGHT; i++)
        {
            if (!IsSpaceClear(to.x, to.y + i))
                return false;
        }
        
        return true;
    }
    
    private bool CanClimbUp(Vector2Int from, Vector2Int to)
    {
        // 현재 위치나 목표 위치에 사다리가 있어야 함
        FloorTile fromLadder = FloorTile.GetFloorTileAt(new Vector2Int(from.x, from.y + 1));
        FloorTile toLadder = FloorTile.GetFloorTileAt(to);
        
        bool hasLadder = (fromLadder != null && fromLadder.AllowsVerticalMovement()) ||
                        (toLadder != null && toLadder.AllowsVerticalMovement());
        
        if (!hasLadder)
            return false;
        
        // 목표 위치에서 직원 몸통 공간 확인
        for (int i = 1; i <= EMPLOYEE_HEIGHT; i++)
        {
            if (!IsSpaceClear(to.x, to.y + i))
                return false;
        }
        
        return true;
    }
    
    private bool CanClimbDown(Vector2Int from, Vector2Int to)
    {
        // 사다리 확인
        FloorTile currentLadder = FloorTile.GetFloorTileAt(new Vector2Int(from.x, from.y + 1));
        FloorTile toLadder = FloorTile.GetFloorTileAt(to);
        
        bool hasLadder = (currentLadder != null && currentLadder.AllowsVerticalMovement()) ||
                        (toLadder != null && toLadder.AllowsVerticalMovement());
        
        if (hasLadder)
        {
            return IsValidPosition(to);
        }
        
        // 떨어지기 (최대 1칸)
        if (from.y - to.y == 1)
        {
            return IsValidPosition(to);
        }
        
        return false;
    }
    
    /// <summary>
    /// 특정 공간이 비어있는지 (직원이 통과할 수 있는지) 확인합니다.
    /// </summary>
    private bool IsSpaceClear(int x, int y)
    {
        if (!IsInBounds(new Vector2Int(x, y)))
            return false;
        
        int tileId = gameMap.TileGrid[x, y];
        
        // 공기 타일은 통과 가능
        if (tileId == 0)
            return true;
        
        // 바닥 타일(사다리 등)이 통과 가능한 경우
        FloorTile floorTile = FloorTile.GetFloorTileAt(new Vector2Int(x, y));
        if (floorTile != null && floorTile.IsPassable())
            return true;
        
        // 고체 타일은 통과 불가
        return false;
    }
    
    /// <summary>
    /// 직원이 특정 위치에 서 있을 수 있는지 확인합니다.
    /// 발 아래에 바닥이 있고, 몸통 공간이 비어있어야 합니다.
    /// </summary>
    private bool CanStandAt(int x, int y)
    {
        if (!IsInBounds(new Vector2Int(x, y)))
            return false;
        
        // 발 위치 타일 확인 (서 있는 타일)
        int footTileId = gameMap.TileGrid[x, y];
        
        // 발 아래가 고체이거나 바닥 타일이어야 함
        bool hasFooting = footTileId != 0 || FloorTile.HasFloorTileAt(new Vector2Int(x, y));
        
        // 사다리 위에 서 있는 경우도 허용
        if (!hasFooting)
        {
            FloorTile ladder = FloorTile.GetFloorTileAt(new Vector2Int(x, y));
            if (ladder == null || !ladder.AllowsVerticalMovement())
                return false;
        }
        
        // 직원 몸통 공간 (발 위로 2칸)이 비어있어야 함
        for (int i = 1; i <= EMPLOYEE_HEIGHT; i++)
        {
            int checkY = y + i;
            if (!IsInBounds(new Vector2Int(x, checkY)))
                return false;
            
            // 해당 위치가 비어있어야 함 (고체 타일 통과 불가!)
            if (!IsSpaceClear(x, checkY))
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 위치가 유효한지 확인합니다 (직원이 서 있을 수 있는지).
    /// 발 위치 타일 좌표를 입력받습니다.
    /// </summary>
    public bool IsValidPosition(Vector2Int pos)
    {
        return CanStandAt(pos.x, pos.y);
    }
    
    private float GetMovementCost(Vector2Int from, Vector2Int to)
    {
        float baseCost = 1f;

        FloorTile floorTile = FloorTile.GetFloorTileAt(to);
        if (floorTile != null)
        {
            float speedMultiplier = floorTile.GetMovementSpeedMultiplier();
            if (speedMultiplier > 0)
            {
                baseCost = baseCost / speedMultiplier;
            }
        }

        int heightDifference = Mathf.Abs(to.y - from.y);
        if (heightDifference > 0)
        {
            baseCost += heightDifference * 1f;
        }

        return baseCost;
    }
    
    private bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < GameMap.MAP_WIDTH && 
               pos.y >= 0 && pos.y < GameMap.MAP_HEIGHT;
    }
}