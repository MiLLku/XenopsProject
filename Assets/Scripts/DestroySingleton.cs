using UnityEngine;

/// <summary>
/// 씬이 전환될 때 파괴되는 일반적인 싱글턴 패턴의 기반 클래스
/// 상속받는 클래스는 해당 씬 내에서 유일한 인스턴스를 보장
/// </summary>
/// <typeparam name="T">싱글턴으로 만들 클래스 타입</typeparam>
public class DestroySingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    // 외부에서 이 클래스의 유일한 인스턴스에 접근하기 위한 프로퍼티
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        // 인스턴스가 이미 존재하고, 그것이 자기 자신이 아니라면
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // 중복된 자기 자신을 파괴
            return;
        }
        // 인스턴스가 없다면 자기 자신을 인스턴스로 할당
        Instance = this as T;
    }
}