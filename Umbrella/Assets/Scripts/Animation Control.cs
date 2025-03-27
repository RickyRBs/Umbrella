using UnityEngine;

public class TriggerMultipleAnimators : MonoBehaviour
{
    [Header("Animator List")]
    public Animator[] animators; // 拖入你想控制的 Animator 组件

    [Header("Trigger Name")]
    public string triggerName = "MyTrigger"; // 你要触发的 Trigger 参数名

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerAllAnimators();
            Debug.Log($"pressed Space");
        }
    }

    void TriggerAllAnimators()
    {
        foreach (Animator anim in animators)
        {
            if (anim != null)
            {
                anim.SetTrigger(triggerName);
                Debug.Log($"Triggered '{triggerName}' on {anim.gameObject.name}");
            }
        }
    }
}
