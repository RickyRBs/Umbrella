using UnityEngine;

public class UmbrellaLauncher : MonoBehaviour
{
    public GameObject umbrellaPrefab;
    public GameObject player;  // 如果在 Inspector 中已设置，则可以用；否则 UmbrellaController 会自动查找

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            var umbrellaInstance = Instantiate(umbrellaPrefab, player.transform.position, Quaternion.identity);
            var controller = umbrellaInstance.GetComponent<UmbrellaController>();
            if (controller != null)
            {
                // 如果你希望在生成时就传入玩家引用，也可以赋值：
                controller.player = player;
            }
        }
    }
}