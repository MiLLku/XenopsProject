using UnityEngine;

// 싱글톤 파괴o
public class DestroySingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T instance;
    private void Awake() { instance = this as T; }
}
