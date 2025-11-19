
using UnityEngine;

/// <summary>
/// 철거 작업 명령
/// </summary>
[System.Serializable]
public class DemolishOrder : IWorkTarget
{
    public Building building;
    public Vector3 position;
    public int priority;
    public bool completed;
    public Employee assignedWorker;
    
    // IWorkTarget 구현
    public Vector3 GetWorkPosition() => position;
    public WorkType GetWorkType() => WorkType.Demolish;
    public float GetWorkTime() => 5f;
    public bool IsWorkAvailable() => !completed && building != null;
    
    public void CompleteWork(Employee worker)
    {
        if (building != null)
        {
            // 자원 일부 반환
            if (InventoryManager.instance != null && building.buildingData != null)
            {
                foreach (var cost in building.buildingData.requiredResources)
                {
                    int returnAmount = Mathf.Max(1, cost.amount / 2);
                    InventoryManager.instance.AddItem(cost.item, returnAmount);
                }
            }
            
            // 점유 해제
            if (MapGenerator.instance != null)
            {
                GameMap gameMap = MapGenerator.instance.GameMapInstance;
                Vector2Int cellPos = new Vector2Int(
                    Mathf.FloorToInt(building.transform.position.x),
                    Mathf.FloorToInt(building.transform.position.y)
                );
                
                for (int y = 0; y < building.buildingData.size.y; y++)
                {
                    for (int x = 0; x < building.buildingData.size.x; x++)
                    {
                        gameMap.UnmarkTileOccupied(cellPos.x + x, cellPos.y + y);
                    }
                }
            }
            
            GameObject.Destroy(building.gameObject);
            Debug.Log($"[DemolishOrder] 철거 완료: {building.buildingData.buildingName}");
        }
        
        completed = true;
        assignedWorker = null;
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}