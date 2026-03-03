using UnityEngine;

/// <summary>
/// 채광 작업 명령.
/// 지정된 타일을 채굴하고, 드롭 아이템을 인벤토리에 추가합니다.
/// 채굴 후 타일은 AIR로 변경되며, 직원 낙하는 EmployeeMovement에서 자동 처리됩니다.
/// </summary>
[System.Serializable]
public class MiningOrder : IWorkTarget
{
    #region 상수

    private const float MINING_WORK_TIME = 3f;
    private const int AIR_TILE_ID = 0;

    #endregion

    #region 필드

    /// <summary>채굴 대상 타일 좌표</summary>
    public Vector3Int position;

    /// <summary>채굴 대상 타일 ID</summary>
    public int tileID;

    /// <summary>작업 우선순위</summary>
    public int priority;

    /// <summary>완료 여부</summary>
    public bool completed;

    /// <summary>배정된 직원</summary>
    public Employee assignedWorker;

    #endregion

    #region IWorkTarget 구현

    /// <inheritdoc/>
    public Vector3 GetWorkPosition() => new Vector3(position.x, position.y, 0);

    /// <inheritdoc/>
    public WorkType GetWorkType() => WorkType.Mining;

    /// <inheritdoc/>
    public float GetWorkTime() => MINING_WORK_TIME;

    /// <inheritdoc/>
    public bool IsWorkAvailable() => !completed;

    /// <inheritdoc/>
    public void CompleteWork(Employee worker)
    {
        if (completed) return;

        if (worker != null)
        {
            Vector3Int footTile = worker.GetFootTile();
            Debug.Log($"[MiningOrder] 채굴완료 직원발위치={footTile}, 채굴타겟={position}, " +
                      $"거리: dx={Mathf.Abs(position.x - footTile.x)}, dy={position.y - footTile.y}");
        }

        completed = true;
        assignedWorker = null;

        ExecuteMining();
    }

    /// <inheritdoc/>
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }

    #endregion

    #region 내부 헬퍼

    /// <summary>
    /// 실제 채광 로직을 실행합니다.
    /// 타일을 AIR로 변경하고, 드롭 아이템을 인벤토리에 추가합니다.
    /// </summary>
    private void ExecuteMining()
    {
        if (MapGenerator.instance == null)
        {
            Debug.LogError("[MiningOrder] MapGenerator.instance가 null입니다!");
            return;
        }

        GameMap gameMap = MapGenerator.instance.GameMapInstance;
        MapRenderer mapRenderer = MapGenerator.instance.MapRendererInstance;
        ResourceManager resourceManager = MapGenerator.instance.ResourceManagerInstance;

        if (gameMap == null || mapRenderer == null || resourceManager == null)
        {
            Debug.LogError("[MiningOrder] 필수 컴포넌트가 null입니다!");
            return;
        }

        if (position.x < 0 || position.x >= GameMap.MAP_WIDTH ||
            position.y < 0 || position.y >= GameMap.MAP_HEIGHT)
        {
            Debug.LogWarning($"[MiningOrder] 유효하지 않은 위치: {position}");
            return;
        }

        int currentTileID = gameMap.TileGrid[position.x, position.y];
        if (currentTileID == AIR_TILE_ID)
        {
            Debug.LogWarning($"[MiningOrder] 타일이 이미 제거됨: {position}");
            return;
        }

        DropMinedResource(resourceManager);

        gameMap.SetTile(position.x, position.y, AIR_TILE_ID);
        gameMap.UnmarkTileOccupied(position.x, position.y);
        mapRenderer.UpdateTileVisual(position.x, position.y);

        Debug.Log($"[MiningOrder] 채광 완료: {position} (TileID: {tileID})");
    }

    /// <summary>
    /// 채굴된 자원을 인벤토리에 추가하거나 월드에 드롭합니다.
    /// </summary>
    private void DropMinedResource(ResourceManager resourceManager)
    {
        GameObject dropPrefab = resourceManager.GetDropPrefab(tileID);
        if (dropPrefab == null || InventoryManager.instance == null) return;

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
            Vector3 dropPos = GetWorkPosition();
            GameObject.Instantiate(dropPrefab, dropPos, Quaternion.identity);
        }
    }

    #endregion
}
