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
    private const int EMPLOYEE_HEIGHT = 2; // 직원이 차지하는 높이
    
    // 길찾기용 노드
    private class PathNode
    {
        public Vector2Int position; // 직원의 발 위치 (아래쪽 타일)
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
    /// </summary>
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        // 목표가 유효한지 확인
        if (!IsValidPosition(goal))
        {
            Debug.LogWarning($"[Pathfinder] 목표 위치가 유효하지 않음: {goal}");
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
    /// 직원은 높이 차이 2칸까지 이동 가능합니다.
    /// </summary>
    private List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        // 수평 이동 (좌우)
        Vector2Int[] horizontalDirs = new Vector2Int[]
        {
            new Vector2Int(1, 0),   // 오른쪽
            new Vector2Int(-1, 0),  // 왼쪽
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
    /// 직원은 높이 차이 2칸까지 올라가거나 내려갈 수 있습니다.
    /// </summary>
    private bool CanMoveTo(Vector2Int from, Vector2Int to)
    {
        if (!IsInBounds(to))
            return false;

        // 목표 위치가 유효한지 확인 (2칸 높이 필요)
        if (!IsValidPosition(to))
            return false;

        int heightDiff = to.y - from.y;
        int horizontalDiff = Mathf.Abs(to.x - from.x);

        // 순수 수평 이동 (같은 높이)
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

    /// <summary>
    /// 대각선 이동 (높이 차이 포함)이 가능한지 확인합니다.
    /// </summary>
    private bool CanMoveDiagonal(Vector2Int from, Vector2Int to, int heightDiff)
    {
        // 목표 위치가 유효한지 먼저 확인
        if (!IsValidPosition(to))
            return false;

        // 높이 차이가 2칸 이하인지 확인
        if (Mathf.Abs(heightDiff) > 2)
            return false;

        // 위로 올라가는 경우
        if (heightDiff > 0)
        {
            // 중간 경로에 블록이 없어야 함
            for (int y = from.y + 1; y <= to.y; y++)
            {
                // 머리 부분 (2칸 높이) 확인
                if (!IsSpaceClear(to.x, y + EMPLOYEE_HEIGHT - 1))
                    return false;
            }
        }

        // 아래로 내려가는 경우 - 자유 낙하 가능
        // 단, 목표 위치에 발판이 있어야 함
        if (heightDiff < 0)
        {
            // 2칸 이상 떨어지는 것은 불가
            if (Mathf.Abs(heightDiff) > 2)
                return false;
        }

        return true;
    }
    
    /// <summary>
    /// 수평으로 이동할 수 있는지 확인합니다.
    /// </summary>
    private bool CanWalkHorizontally(Vector2Int from, Vector2Int to)
    {
        // 발을 디딜 곳이 있어야 함
        FloorTile toFloorTile = FloorTile.GetFloorTileAt(to);
        
        // 바닥 타일이 있거나, 고체 타일이어야 함
        bool hasFloor = toFloorTile != null || gameMap.TileGrid[to.x, to.y] != 0;
        if (!hasFloor)
            return false;
        
        // 머리 위 2칸이 비어있어야 함 (또는 통과 가능해야 함)
        for (int i = 1; i <= EMPLOYEE_HEIGHT; i++)
        {
            if (!IsSpaceClear(to.x, to.y + i))
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 위로 올라갈 수 있는지 확인합니다 (사다리 필요).
    /// </summary>
    private bool CanClimbUp(Vector2Int from, Vector2Int to)
    {
        // 현재 위치나 목표 위치에 사다리가 있어야 함
        FloorTile fromLadder = FloorTile.GetFloorTileAt(new Vector2Int(from.x, from.y + 1));
        FloorTile toLadder = FloorTile.GetFloorTileAt(to);
        
        bool hasLadder = (fromLadder != null && fromLadder.AllowsVerticalMovement()) ||
                        (toLadder != null && toLadder.AllowsVerticalMovement());
        
        if (!hasLadder)
            return false;
        
        // 목표 위치에서 2칸이 비어있어야 함
        for (int i = 1; i <= EMPLOYEE_HEIGHT; i++)
        {
            if (!IsSpaceClear(to.x, to.y + i))
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 아래로 내려갈 수 있는지 확인합니다.
    /// </summary>
    private bool CanClimbDown(Vector2Int from, Vector2Int to)
    {
        // 사다리를 타고 내려가기
        FloorTile currentLadder = FloorTile.GetFloorTileAt(new Vector2Int(from.x, from.y + 1));
        FloorTile toLadder = FloorTile.GetFloorTileAt(to);
        
        bool hasLadder = (currentLadder != null && currentLadder.AllowsVerticalMovement()) ||
                        (toLadder != null && toLadder.AllowsVerticalMovement());
        
        if (hasLadder)
        {
            // 목표 위치가 유효한지만 확인
            return IsValidPosition(to);
        }
        
        // 떨어지기 (최대 1칸까지만)
        if (from.y - to.y == 1)
        {
            return IsValidPosition(to);
        }
        
        return false;
    }
    
    /// <summary>
    /// 특정 공간이 비어있는지 확인합니다 (또는 통과 가능한지).
    /// </summary>
    private bool IsSpaceClear(int x, int y)
    {
        if (!IsInBounds(new Vector2Int(x, y)))
            return false;
        
        // 타일이 공기이거나
        int tileId = gameMap.TileGrid[x, y];
        if (tileId == 0) // AIR
            return true;
        
        // 통과 가능한 바닥 타일이거나 (사다리)
        FloorTile floorTile = FloorTile.GetFloorTileAt(new Vector2Int(x, y));
        if (floorTile != null && floorTile.IsPassable())
            return true;
        
        return false;
    }
    
    /// <summary>
    /// 위치가 유효한지 확인합니다 (직원이 서 있을 수 있는지).
    /// </summary>
    private bool IsValidPosition(Vector2Int pos)
    {
        if (!IsInBounds(pos))
            return false;
        
        // 발 아래가 고체이거나 바닥 타일이어야 함
        int footTileId = gameMap.TileGrid[pos.x, pos.y];
        bool hasFooting = footTileId != 0 || FloorTile.HasFloorTileAt(pos);
        
        if (!hasFooting)
        {
            // 사다리 위에 있는 경우는 예외
            FloorTile ladder = FloorTile.GetFloorTileAt(pos);
            if (ladder == null || !ladder.AllowsVerticalMovement())
                return false;
        }
        
        // 위 2칸이 비어있어야 함
        for (int i = 1; i <= EMPLOYEE_HEIGHT; i++)
        {
            if (!IsSpaceClear(pos.x, pos.y + i))
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 이동 비용을 계산합니다.
    /// 높이 차이에 따라 추가 시간이 부여됩니다.
    /// </summary>
    private float GetMovementCost(Vector2Int from, Vector2Int to)
    {
        float baseCost = 1f;

        // 바닥 타일이 있으면 그 속도 배율 적용
        FloorTile floorTile = FloorTile.GetFloorTileAt(to);
        if (floorTile != null)
        {
            float speedMultiplier = floorTile.GetMovementSpeedMultiplier();
            if (speedMultiplier > 0)
            {
                baseCost = baseCost / speedMultiplier;
            }
        }

        // 수직 이동은 높이 차이만큼 추가 비용
        int heightDifference = Mathf.Abs(to.y - from.y);
        if (heightDifference > 0)
        {
            // 높이 1칸당 기본 이동 비용 1을 추가
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