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
    // 채광할 타일의 타일 좌표를 반환 (직원은 작업 범위 내에서 작업)
    public Vector3 GetWorkPosition() => new Vector3(position.x, position.y, 0);
    public WorkType GetWorkType() => WorkType.Mining;
    public float GetWorkTime() => 3f;
    public bool IsWorkAvailable() => !completed;
    
    public void CompleteWork(Employee worker)
    {
        if (completed) return; // 중복 완료 방지
        
        completed = true;
        assignedWorker = null;
        
        // 실제 채광 실행
        if (MapGenerator.instance != null)
        {
            GameMap gameMap = MapGenerator.instance.GameMapInstance;
            MapRenderer mapRenderer = MapGenerator.instance.MapRendererInstance;
            ResourceManager resourceManager = MapGenerator.instance.ResourceManagerInstance;
            
            if (gameMap == null || mapRenderer == null || resourceManager == null)
            {
                Debug.LogError("[MiningOrder] 필수 컴포넌트가 null입니다!");
                return;
            }
            
            // 타일이 아직 존재하는지 확인
            if (position.x < 0 || position.x >= GameMap.MAP_WIDTH ||
                position.y < 0 || position.y >= GameMap.MAP_HEIGHT)
            {
                Debug.LogWarning($"[MiningOrder] 유효하지 않은 위치: {position}");
                return;
            }
            
            // 이미 제거된 타일인지 확인
            int currentTileID = gameMap.TileGrid[position.x, position.y];
            if (currentTileID == 0) // 이미 공기 타일
            {
                Debug.LogWarning($"[MiningOrder] 타일이 이미 제거됨: {position}");
                return;
            }
            
            // 드롭 아이템 생성
            GameObject dropPrefab = resourceManager.GetDropPrefab(tileID);
            if (dropPrefab != null && InventoryManager.instance != null)
            {
                // 인벤토리에 직접 추가 (아이템 드롭 대신)
                ClickableItem itemComponent = dropPrefab.GetComponent<ClickableItem>();
                if (itemComponent != null)
                {
                    ItemData itemData = itemComponent.GetItemData();
                    if (itemData != null)
                    {
                        InventoryManager.instance.AddItem(itemData, 1);
                    }
                }
                else
                {
                    // 아이템 프리팹을 월드에 생성
                    Vector3 dropPos = GetWorkPosition();
                    GameObject.Instantiate(dropPrefab, dropPos, Quaternion.identity);
                }
            }
            
            // 타일 제거
            gameMap.SetTile(position.x, position.y, 0);
            gameMap.UnmarkTileOccupied(position.x, position.y);
            mapRenderer.UpdateTileVisual(position.x, position.y);
            
            Debug.Log($"[MiningOrder] 채광 완료: {position} (TileID: {tileID})");
        }
        else
        {
            Debug.LogError("[MiningOrder] MapGenerator.instance가 null입니다!");
        }
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}