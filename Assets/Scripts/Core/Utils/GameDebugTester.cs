// Assets/Scripts/Debug/GameDebugTester.cs
using UnityEngine;

public class GameDebugTester : MonoBehaviour
{
    [Header("테스트 데이터")]
    public EmployeeData testEmployeeData;
    public BuildingData testBuildingData;
    public ItemData testItemData;

    [Header("설정")]
    public Vector3 spawnPosition = new Vector3(50, 50, 0);
    public int initialResourceAmount = 100;

    void Update()
    {
        // F1: 직원 스폰
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SpawnTestEmployee();
        }

        // F2: 자원 지급
        if (Input.GetKeyDown(KeyCode.F2))
        {
            GiveResources();
        }

        // F3: 즉시 이벤트 발생
        if (Input.GetKeyDown(KeyCode.F3))
        {
            TriggerEvent();
        }

        // F5: 빠른 저장
        if (Input.GetKeyDown(KeyCode.F5))
        {
            QuickSave();
        }

        // F9: 빠른 로드
        if (Input.GetKeyDown(KeyCode.F9))
        {
            QuickLoad();
        }

        // F10: 모든 직원 상태 출력
        if (Input.GetKeyDown(KeyCode.F10))
        {
            PrintAllEmployeeStatus();
        }

        // F11: 인벤토리 출력
        if (Input.GetKeyDown(KeyCode.F11))
        {
            PrintInventory();
        }

        // F12: 활성 이벤트 출력
        if (Input.GetKeyDown(KeyCode.F12))
        {
            PrintActiveEvents();
        }
    }

    void SpawnTestEmployee()
    {
        if (EmployeeManager.instance == null || testEmployeeData == null)
        {
            Debug.LogError("[Debug] EmployeeManager 또는 testEmployeeData 없음");
            return;
        }

        var employee = EmployeeManager.instance.SpawnEmployee(testEmployeeData, spawnPosition);
        Debug.Log($"[Debug] 직원 스폰: {employee?.Data?.employeeName}");
    }

    void GiveResources()
    {
        if (InventoryManager.instance == null || testItemData == null)
        {
            Debug.LogError("[Debug] InventoryManager 또는 testItemData 없음");
            return;
        }

        InventoryManager.instance.AddItem(testItemData, initialResourceAmount);
        Debug.Log($"[Debug] 자원 지급: {testItemData.itemName} x{initialResourceAmount}");
    }

    void TriggerEvent()
    {
        if (EventManager.instance == null)
        {
            Debug.LogError("[Debug] EventManager 없음");
            return;
        }

        EventManager.instance.TriggerRandomEvent();
    }

    void QuickSave()
    {
        if (SaveManager.instance == null)
        {
            Debug.LogError("[Debug] SaveManager 없음");
            return;
        }

        bool success = SaveManager.instance.Save("QuickSave");
        Debug.Log($"[Debug] 빠른 저장: {(success ? "성공" : "실패")}");
    }

    void QuickLoad()
    {
        if (SaveManager.instance == null)
        {
            Debug.LogError("[Debug] SaveManager 없음");
            return;
        }

        bool success = SaveManager.instance.Load("QuickSave");
        Debug.Log($"[Debug] 빠른 로드: {(success ? "성공" : "실패")}");
    }

    void PrintAllEmployeeStatus()
    {
        if (EmployeeManager.instance == null) return;

        Debug.Log("===== 직원 상태 =====");
        foreach (var emp in EmployeeManager.instance.AllEmployees)
        {
            Debug.Log($"[{emp.Data?.employeeName}] " +
                $"상태:{emp.State} " +
                $"HP:{emp.Stats.health}/{emp.Stats.maxHealth} " +
                $"멘탈:{emp.Stats.mental}/{emp.Stats.maxMental} " +
                $"배고픔:{emp.Needs.hunger:F1} " +
                $"피로:{emp.Needs.fatigue:F1}");
        }
    }

    void PrintInventory()
    {
        if (InventoryManager.instance == null) return;

        Debug.Log("===== 인벤토리 =====");
        foreach (var kvp in InventoryManager.instance.globalInventory)
        {
            Debug.Log($"[{kvp.Key?.itemName}] x{kvp.Value}");
        }
    }

    void PrintActiveEvents()
    {
        if (EventManager.instance == null) return;

        Debug.Log("===== 활성 이벤트 =====");
        var activeEvents = EventManager.instance.GetActivePersistentEvents();
        foreach (var evt in activeEvents)
        {
            Debug.Log($"[{evt.title}] 카테고리:{evt.category}");
        }
        Debug.Log($"다음 이벤트까지: {EventManager.instance.GetTimeUntilNextEvent():F0}초");
    }
}
