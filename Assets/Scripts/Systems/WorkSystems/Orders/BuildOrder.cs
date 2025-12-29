using UnityEngine;

/// <summary>
/// 건설 작업 명령
/// 건설 현장(ConstructionSite)에서 건물을 완성하는 작업
/// 
/// 저장 위치: Assets/Scripts/Systems/WorkSystems/Orders/BuildOrder.cs
/// </summary>
[System.Serializable]
public class BuildOrder : IWorkTarget
{
    public ConstructionSite constructionSite;
    public BuildingData buildingData;
    public Vector3 position;
    public int priority;
    public bool completed;
    public Employee assignedWorker;
    
    // IWorkTarget 구현
    public Vector3 GetWorkPosition() => position;
    public WorkType GetWorkType() => WorkType.Building;
    public float GetWorkTime() => buildingData != null ? buildingData.constructionTime : 5f;
    public bool IsWorkAvailable() => !completed && constructionSite != null && !constructionSite.IsCompleted;
    
    public void CompleteWork(Employee worker)
    {
        if (completed) return;
        
        completed = true;
        assignedWorker = null;
        
        if (constructionSite != null)
        {
            constructionSite.CompleteConstruction();
        }
        
        Debug.Log($"[BuildOrder] 건설 완료: {buildingData?.buildingName}");
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}