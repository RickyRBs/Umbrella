using UnityEngine;

public class UmbrellaInfo : MonoBehaviour
{
    // 在 Inspector 中为这个字段赋值场景中的玩家对象
    public GameObject player;
    
    // 方便全局访问，使用单例模式
    public static UmbrellaInfo Instance;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
}