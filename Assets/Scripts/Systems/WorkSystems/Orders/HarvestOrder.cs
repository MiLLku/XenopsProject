using UnityEngine;

/// <summary>
/// 수확 작업 명령 (나무, 식물 등)
/// </summary>
[System.Serializable]
public class HarvestOrder : IWorkTarget
{
    public IHarvestable target;
    public Vector3 position;
    public int priority;
    public bool completed;
    public Employee assignedWorker;
    
    // IWorkTarget 구현
    public Vector3 GetWorkPosition() => position;
    public WorkType GetWorkType() => target?.GetHarvestType() ?? WorkType.Gardening;
    public float GetWorkTime() => target?.GetHarvestTime() ?? 2f;
    public bool IsWorkAvailable() => !completed && target != null && target.CanHarvest();
    
    public void CompleteWork(Employee worker)
    {
        if (target != null)
        {
            target.Harvest();
        }
        completed = true;
        assignedWorker = null;
    }
    
    public void CancelWork(Employee worker)
    {
        assignedWorker = null;
    }
}