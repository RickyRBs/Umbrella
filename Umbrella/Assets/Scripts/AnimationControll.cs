using UnityEngine;

namespace StarterAssets
{
    public class ThirdPersonAnimationController : MonoBehaviour
    {
        // 内部引用
        private Animator _animator;
        private bool _hasAnimator;

        // 动画参数的哈希ID
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

        // 动画驱动数据，外部可以设置这些值来驱动动画
        [Header("Animation Data")]
        [Tooltip("平滑混合速度，用于控制 Idle 到 Walk/Run 的过渡")]
        public float AnimationBlend = 0f;
        [Tooltip("动作快慢参数")]
        public float MotionSpeed = 0f;
        [Tooltip("是否在地面上")]
        public bool Grounded = true;
        [Tooltip("是否处于起跳状态")]
        public bool Jump = false;
        [Tooltip("是否处于自由落体状态")]
        public bool FreeFall = false;

        private void Awake()
        {
            _hasAnimator = TryGetComponent(out _animator);
            if (_hasAnimator)
            {
                AssignAnimationIDs();
            }
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        /// <summary>
        /// 更新 Animator 的各个参数。你可以在 Update() 中调用此方法，
        /// 或者在你的主控制器里设置好数据后调用一次。
        /// </summary>
        public void UpdateAnimationParameters()
        {
            if (!_hasAnimator) { return; }

            _animator.SetFloat(_animIDSpeed, AnimationBlend);
            _animator.SetFloat(_animIDMotionSpeed, MotionSpeed);
            _animator.SetBool(_animIDGrounded, Grounded);
            _animator.SetBool(_animIDJump, Jump);
            _animator.SetBool(_animIDFreeFall, FreeFall);
        }

    }
}