using UnityEngine;

// 싱글톤 파괴x
public class DontDestroySingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this as T;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }
}
