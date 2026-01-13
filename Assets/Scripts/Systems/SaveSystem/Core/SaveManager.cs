using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 게임 저장/로드 관리자
/// DontDestroyOnLoad 싱글톤으로 씬 전환에도 유지됩니다.
/// </summary>
public class SaveManager : DontDestroySingleton<SaveManager>
{
    [Header("설정")]
    [SerializeField] private GameDatabase gameDatabase;
    [SerializeField] private string saveFolder = "Saves";
    [SerializeField] private string saveExtension = ".json";

    [Header("자동 저장")]
    [SerializeField] private bool enableAutoSave = true;
    [SerializeField] private float autoSaveInterval = 300f; // 5분

    [Header("디버그")]
    [SerializeField] private bool showDebugLogs = true;

    // 런타임 ID 카운터
    private int _nextInstanceId = 1;

    // 자동 저장 타이머
    private float _autoSaveTimer;

    // 저장 경로
    private string SaveFolderPath => Path.Combine(Application.persistentDataPath, saveFolder);

    private void Start()
    {
        // GameDatabase 초기화
        if (gameDatabase != null)
        {
            gameDatabase.Initialize();
        }
        else
        {
            Debug.LogError("[SaveManager] GameDatabase가 설정되지 않았습니다!");
        }

        // 저장 폴더 생성
        if (!Directory.Exists(SaveFolderPath))
        {
            Directory.CreateDirectory(SaveFolderPath);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 초기화 완료. 저장 경로: {SaveFolderPath}");
        }
    }

    private void Update()
    {
        if (enableAutoSave)
        {
            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                Save("AutoSave");
            }
        }
    }

    #region 공개 API

    /// <summary>
    /// 새 인스턴스 ID를 발급합니다.
    /// </summary>
    public int GenerateInstanceId()
    {
        return _nextInstanceId++;
    }

    /// <summary>
    /// 게임을 저장합니다.
    /// </summary>
    public bool Save(string slotName)
    {
        try
        {
            SaveData data = CaptureGameState();
            data.saveName = slotName;
            data.saveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            data.nextInstanceId = _nextInstanceId;

            string json = JsonUtility.ToJson(data, true);
            string path = GetSavePath(slotName);

            File.WriteAllText(path, json);

            if (showDebugLogs)
            {
                Debug.Log($"[SaveManager] 저장 완료: {path}");
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 저장 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 게임을 로드합니다.
    /// </summary>
    public bool Load(string slotName)
    {
        string path = GetSavePath(slotName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[SaveManager] 세이브 파일 없음: {path}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // 버전 체크
            if (data.saveVersion > 1)
            {
                Debug.LogError($"[SaveManager] 지원하지 않는 세이브 버전: {data.saveVersion}");
                return false;
            }

            // 로드 시퀀스 시작
            StartCoroutine(LoadSequence(data));

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 로드 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 세이브 파일을 삭제합니다.
    /// </summary>
    public bool Delete(string slotName)
    {
        string path = GetSavePath(slotName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveManager] 삭제할 파일 없음: {path}");
            return false;
        }

        try
        {
            File.Delete(path);
            if (showDebugLogs)
            {
                Debug.Log($"[SaveManager] 삭제 완료: {path}");
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] 삭제 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 세이브 파일이 존재하는지 확인합니다.
    /// </summary>
    public bool SaveExists(string slotName)
    {
        return File.Exists(GetSavePath(slotName));
    }

    /// <summary>
    /// 모든 세이브 슬롯 목록을 반환합니다.
    /// </summary>
    public List<SaveSlotInfo> GetAllSaveSlots()
    {
        var slots = new List<SaveSlotInfo>();

        if (!Directory.Exists(SaveFolderPath))
            return slots;

        var files = Directory.GetFiles(SaveFolderPath, $"*{saveExtension}");

        foreach (var file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                slots.Add(new SaveSlotInfo
                {
                    slotName = data.saveName,
                    saveTime = DateTimeOffset.FromUnixTimeSeconds(data.saveTimestamp).LocalDateTime,
                    playTime = data.playTime,
                    filePath = file
                });
            }
            catch
            {
                // 손상된 파일 무시
            }
        }

        return slots.OrderByDescending(s => s.saveTime).ToList();
    }

    #endregion

    #region 상태 캡처

    private SaveData CaptureGameState()
    {
        SaveData data = new SaveData
        {
            saveVersion = 1,
            playTime = Time.time,
            map = CaptureMapData(),
            employees = CaptureEmployeeData(),
            buildings = CaptureBuildingData(),
            constructionSites = CaptureConstructionSiteData(),
            workOrders = CaptureWorkOrderData(),
            inventory = CaptureInventoryData(),
            droppedItems = CaptureDroppedItemData(),
            eventSystem = CaptureEventSystemData()
        };

        return data;
    }

    private EventSystemSaveData CaptureEventSystemData()
    {
        if (EventManager.instance == null)
        {
            return new EventSystemSaveData();
        }

        return EventManager.instance.CaptureSaveData();
    }

    private MapSaveData CaptureMapData()
    {
        if (MapGenerator.instance == null)
        {
            Debug.LogWarning("[SaveManager] MapGenerator가 없습니다.");
            return new MapSaveData();
        }

        GameMap gameMap = MapGenerator.instance.GameMapInstance;
        int width = GameMap.MAP_WIDTH;
        int height = GameMap.MAP_HEIGHT;

        int[] tiles = new int[width * height];
        int[] walls = new int[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                tiles[index] = gameMap.TileGrid[x, y];
                walls[index] = gameMap.WallGrid[x, y];
            }
        }

        var mapData = new MapSaveData
        {
            width = width,
            height = height,
            tileGrid = tiles,
            wallGrid = walls,
            entities = CaptureMapEntities()
        };

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 맵 캡처: {width}x{height} 타일");
        }

        return mapData;
    }

    private List<MapEntitySaveData> CaptureMapEntities()
    {
        var entities = new List<MapEntitySaveData>();

        // 나무, 광물 등 자연물 수집
        // ChoppableTree, 광석 등의 컴포넌트를 찾아서 저장
        var trees = GameObject.FindObjectsOfType<ChoppableTree>();
        foreach (var tree in trees)
        {
            entities.Add(new MapEntitySaveData
            {
                x = Mathf.FloorToInt(tree.transform.position.x),
                y = Mathf.FloorToInt(tree.transform.position.y),
                entityType = (int)TypeObjectTile.Plant,
                variantId = 0,
                remainingResource = 1f // TODO: 나무 체력 반영
            });
        }

        return entities;
    }

    private List<EmployeeSaveData> CaptureEmployeeData()
    {
        var result = new List<EmployeeSaveData>();

        if (EmployeeManager.instance == null) return result;

        foreach (var employee in EmployeeManager.instance.AllEmployees)
        {
            if (employee == null || employee.Data == null) continue;

            // Employee의 CreateSaveData 메서드 사용
            var saveData = employee.CreateSaveData();
            result.Add(saveData);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 직원 캡처: {result.Count}명");
        }

        return result;
    }

    private List<BuildingSaveData> CaptureBuildingData()
    {
        var result = new List<BuildingSaveData>();

        var buildings = GameObject.FindObjectsOfType<Building>();
        foreach (var building in buildings)
        {
            if (building == null || building.buildingData == null) continue;

            result.Add(new BuildingSaveData
            {
                instanceId = GenerateInstanceId(),
                dataId = building.buildingData.buildingID,
                gridX = Mathf.FloorToInt(building.transform.position.x),
                gridY = Mathf.FloorToInt(building.transform.position.y),
                currentHealth = building.buildingData.maxHealth, // TODO: 현재 체력
                isFunctional = true,
                extraData = ""
            });
        }

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 건물 캡처: {result.Count}개");
        }

        return result;
    }

    private List<ConstructionSiteSaveData> CaptureConstructionSiteData()
    {
        var result = new List<ConstructionSiteSaveData>();

        var sites = GameObject.FindObjectsOfType<ConstructionSite>();
        foreach (var site in sites)
        {
            if (site == null || site.BuildingData == null) continue;

            result.Add(new ConstructionSiteSaveData
            {
                instanceId = GenerateInstanceId(),
                buildingDataId = site.BuildingData.buildingID,
                gridX = site.GridPosition.x,
                gridY = site.GridPosition.y,
                state = (int)site.State,
                progress = site.Progress,
                workOrderId = -1 // TODO: 작업 연결
            });
        }

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 건설 현장 캡처: {result.Count}개");
        }

        return result;
    }

    private List<WorkOrderSaveData> CaptureWorkOrderData()
    {
        var result = new List<WorkOrderSaveData>();

        if (WorkSystemManager.instance == null) return result;

        foreach (var order in WorkSystemManager.instance.AllOrders)
        {
            if (order == null) continue;

            var saveData = new WorkOrderSaveData
            {
                orderId = order.orderId,
                orderName = order.orderName,
                workType = (int)order.workType,
                priority = order.priority,
                createdTime = order.createdTime,
                maxWorkers = order.maxAssignedWorkers,
                isActive = order.isActive,
                isPaused = order.isPaused
            };

            // 할당된 작업자 ID
            saveData.assignedWorkerIds = new List<int>();
            // TODO: 작업자 ID 수집

            // 작업 태스크들
            // TODO: TaskQueue에서 태스크 수집

            result.Add(saveData);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 작업물 캡처: {result.Count}개");
        }

        return result;
    }

    private InventorySaveData CaptureInventoryData()
    {
        var result = new InventorySaveData();

        if (InventoryManager.instance == null) return result;

        foreach (var kvp in InventoryManager.instance.globalInventory)
        {
            if (kvp.Key == null) continue;

            result.items.Add(new ItemStackSaveData
            {
                itemId = kvp.Key.itemID,
                amount = kvp.Value
            });
        }

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 인벤토리 캡처: {result.items.Count}종");
        }

        return result;
    }

    private List<DroppedItemSaveData> CaptureDroppedItemData()
    {
        var result = new List<DroppedItemSaveData>();

        // TODO: 드롭된 아이템 수집 (ItemDrop 컴포넌트 등)

        return result;
    }

    #endregion

    #region 상태 복원

    private IEnumerator LoadSequence(SaveData data)
    {
        if (showDebugLogs)
        {
            Debug.Log("[SaveManager] 로드 시퀀스 시작...");
        }

        // 1. 기존 상태 정리
        ClearCurrentState();
        yield return null;

        // 2. ID 카운터 복원
        _nextInstanceId = data.nextInstanceId;

        // 3. 맵 복원
        RestoreMapData(data.map);
        yield return null;

        // 4. 건물 복원
        RestoreBuildingData(data.buildings);
        yield return null;

        // 5. 건설 현장 복원
        RestoreConstructionSiteData(data.constructionSites);
        yield return null;

        // 6. 직원 복원
        RestoreEmployeeData(data.employees);
        yield return null;

        // 7. 작업 시스템 복원
        RestoreWorkOrderData(data.workOrders);
        yield return null;

        // 8. 인벤토리 복원
        RestoreInventoryData(data.inventory);
        yield return null;

        // 9. 드롭 아이템 복원
        RestoreDroppedItemData(data.droppedItems);
        yield return null;

        // 10. 이벤트 시스템 복원
        RestoreEventSystemData(data.eventSystem);
        yield return null;

        // 11. 연결 복구
        ReconnectReferences(data);

        if (showDebugLogs)
        {
            Debug.Log("[SaveManager] 로드 완료!");
        }
    }

    private void ClearCurrentState()
    {
        // 기존 직원 제거
        if (EmployeeManager.instance != null)
        {
            EmployeeManager.instance.RemoveAllEmployees();
        }

        // 기존 건물 제거
        var buildings = GameObject.FindObjectsOfType<Building>();
        foreach (var building in buildings)
        {
            Destroy(building.gameObject);
        }

        // 기존 건설 현장 제거
        var sites = GameObject.FindObjectsOfType<ConstructionSite>();
        foreach (var site in sites)
        {
            Destroy(site.gameObject);
        }

        // 인벤토리 초기화
        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.ClearInventory();
        }

        if (showDebugLogs)
        {
            Debug.Log("[SaveManager] 기존 상태 정리 완료");
        }
    }

    private void RestoreMapData(MapSaveData data)
    {
        if (data == null || MapGenerator.instance == null) return;

        GameMap gameMap = MapGenerator.instance.GameMapInstance;

        // 1D → 2D 변환
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                int index = y * data.width + x;
                gameMap.TileGrid[x, y] = data.tileGrid[index];
                gameMap.WallGrid[x, y] = data.wallGrid[index];
            }
        }

        // 비주얼 갱신
        MapGenerator.instance.MapRendererInstance?.RefreshAllTiles();

        // 맵 엔티티 복원
        RestoreMapEntities(data.entities);

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 맵 복원: {data.width}x{data.height}");
        }
    }

    private void RestoreMapEntities(List<MapEntitySaveData> entities)
    {
        if (entities == null) return;

        // TODO: 나무, 광물 등 자연물 복원
        foreach (var entity in entities)
        {
            // TypeObjectTile에 따라 적절한 프리팹 생성
        }
    }

    private void RestoreEmployeeData(List<EmployeeSaveData> employees)
    {
        if (employees == null || EmployeeManager.instance == null) return;

        foreach (var data in employees)
        {
            // EmployeeData SO 찾기 (템플릿 - 외형/프리팹용)
            EmployeeData templateData = GameDatabase.Instance?.GetEmployeeData(data.templateId);
            if (templateData == null)
            {
                Debug.LogWarning($"[SaveManager] EmployeeData 템플릿 없음: {data.templateId}");
                continue;
            }

            // 직원 생성 (위치 지정)
            Vector3 position = new Vector3(data.posX, data.posY, 0);
            Employee employee = EmployeeManager.instance.SpawnEmployee(templateData, position);

            if (employee != null)
            {
                // 저장된 데이터로 상태 복원 (성장 스탯 등)
                employee.RestoreFromSaveData(data);
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 직원 복원: {employees.Count}명");
        }
    }

    private void RestoreBuildingData(List<BuildingSaveData> buildings)
    {
        if (buildings == null) return;

        foreach (var data in buildings)
        {
            BuildingData buildingData = GameDatabase.Instance?.GetBuildingData(data.dataId);
            if (buildingData == null || buildingData.buildingPrefab == null)
            {
                Debug.LogWarning($"[SaveManager] BuildingData 없음: {data.dataId}");
                continue;
            }

            Vector3 position = new Vector3(data.gridX, data.gridY, 0);
            GameObject buildingObj = Instantiate(buildingData.buildingPrefab, position, Quaternion.identity);

            Building building = buildingObj.GetComponent<Building>();
            if (building != null)
            {
                building.Initialize(buildingData);
                // TODO: 체력 등 상태 복원
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 건물 복원: {buildings.Count}개");
        }
    }

    private void RestoreConstructionSiteData(List<ConstructionSiteSaveData> sites)
    {
        if (sites == null || ConstructionManager.instance == null) return;

        foreach (var data in sites)
        {
            BuildingData buildingData = GameDatabase.Instance?.GetBuildingData(data.buildingDataId);
            if (buildingData == null)
            {
                Debug.LogWarning($"[SaveManager] BuildingData 없음: {data.buildingDataId}");
                continue;
            }

            // 건설 현장 복원
            Vector3Int gridPos = new Vector3Int(data.gridX, data.gridY, 0);
            // TODO: ConstructionManager를 통해 건설 현장 복원
        }

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 건설 현장 복원: {sites.Count}개");
        }
    }

    private void RestoreWorkOrderData(List<WorkOrderSaveData> orders)
    {
        if (orders == null || WorkSystemManager.instance == null) return;

        // TODO: 작업물 복원

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 작업물 복원: {orders.Count}개");
        }
    }

    private void RestoreInventoryData(InventorySaveData data)
    {
        if (data == null || data.items == null || InventoryManager.instance == null) return;

        foreach (var itemStack in data.items)
        {
            ItemData itemData = GameDatabase.Instance?.GetItemData(itemStack.itemId);
            if (itemData != null)
            {
                InventoryManager.instance.AddItem(itemData, itemStack.amount);
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 인벤토리 복원: {data.items.Count}종");
        }
    }

    private void RestoreDroppedItemData(List<DroppedItemSaveData> items)
    {
        if (items == null) return;

        // TODO: 드롭 아이템 복원
    }

    private void RestoreEventSystemData(EventSystemSaveData data)
    {
        if (data == null || EventManager.instance == null) return;

        EventManager.instance.RestoreSaveData(data);

        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] 이벤트 시스템 복원: 지속형 이벤트 {data.activePersistentEvents?.Count ?? 0}개");
        }
    }

    private void ReconnectReferences(SaveData data)
    {
        // 직원 ↔ 작업물 연결 복구
        // TODO: instanceId를 사용하여 참조 복구

        if (showDebugLogs)
        {
            Debug.Log("[SaveManager] 참조 연결 복구 완료");
        }
    }

    #endregion

    #region 유틸리티

    private string GetSavePath(string slotName)
    {
        return Path.Combine(SaveFolderPath, $"{slotName}{saveExtension}");
    }

    #endregion

    #region 에디터 테스트

    [ContextMenu("Test Save")]
    private void TestSave()
    {
        Save("TestSave");
    }

    [ContextMenu("Test Load")]
    private void TestLoad()
    {
        Load("TestSave");
    }

    #endregion
}

/// <summary>
/// 세이브 슬롯 정보
/// </summary>
[Serializable]
public class SaveSlotInfo
{
    public string slotName;
    public DateTime saveTime;
    public float playTime;
    public string filePath;
}
