using UnityEngine;

[System.Serializable]
public struct EmployeeNeeds
{
    [Range(0, 100)]
    public float hunger;
    [Range(0, 100)]
    public float fatigue;
}
