using UnityEngine;
using Hertzole.GoldPlayer;

public class PlayerAnimationSync : MonoBehaviour
{
    private Animator animator;
    private GoldPlayerController goldController;

    void Start()
    {
        animator = GetComponent<Animator>();
        // 从父对象中获取 GoldPlayerController 组件
        goldController = GetComponentInParent<GoldPlayerController>();
    }

    void Update()
    {
        if (animator != null && goldController != null)
        {
            // 用 GoldPlayerController 的 Velocity 来计算移动速度
            float speed = goldController.Velocity.magnitude;
            animator.SetFloat("Speed", speed);

            // 通过 CharacterController 的 isGrounded 属性判断是否在地面上
            bool grounded = goldController.Controller.isGrounded;
            animator.SetBool("Grounded", grounded);
        }
    }
}