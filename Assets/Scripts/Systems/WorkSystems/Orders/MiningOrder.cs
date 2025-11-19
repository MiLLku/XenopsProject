using UnityEngine;

/// <summary>
/// 채광 작업 명령
/// </summary>
[System.Serializable]
public class MiningOrder : IWorkTarget
{
    public Vector3Int position;
    public int tileID;
    public int priority;
    public bool completed;
    public Employee assignedWorker;
    
    // IWorkTarget 구현
    public Vector3 GetWorkPosition() => new Vector3(position.x + 0.5f, position.y + 0.5f, 0);
    public WorkType GetWorkType() => WorkType.Mining;
    public float GetWorkTime() => 3f;
    public bool IsWorkAvailable() => !completed;
    
    public void CompleteWork(Employee worker)
    {
        completed = true;
        assignedWorker = null;
        
        // 실제 채광 실행
        if (MapGenerator.instance != null)
        {
            GameMap gameMap = MapGenerator.instance.GameMapInstance;
            MapRenderer mapRenderer = MapGenerator.instance.MapRendererInstance;
            ResourceManager resourceManager = MapGenerator.instance.ResourceManagerInstance;
            
            // 드롭 아이템 생성
            GameObject dropPrefab = resourceManager.GetDropPrefab(tileID);
            if (dropPrefab != null)
            {
                Vector3 dropPos = GetWorkPosition();
                GameObject.Instantiate(dropPrefab, dropPos, Quaternion.identity);
            }
            
            // 타일 제거
            gameMap.SetTile(position.x, position.y, 0);
            gameMap.UnmarkTileOccupied(position.x, position.y);
            mapRenderer.UpdateTileVisual(position.x, position.y);
            
            Debug.Log($"[MiningOrder] 채광 완료: {position}");
        }
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}