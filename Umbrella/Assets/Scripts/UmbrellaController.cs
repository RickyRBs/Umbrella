//using UnityEngine;

//public class UmbrellaController : MonoBehaviour
//{
//    public float launchSpeed = 5f;
//    public GameObject player;

//    public static UmbrellaController currentUmbrella;

//    private Rigidbody rb;
//    private Vector3 launchOrigin;
//    private bool isHovering = false;
//    private bool isReturning = false;

//    void Start()
//    {
//        // 确保只存在一把伞
//        if (currentUmbrella != null)
//        {
//            Destroy(currentUmbrella.gameObject);
//        }
//        currentUmbrella = this;

//        if (player == null && UmbrellaInfo.Instance != null)
//        {
//            player = UmbrellaInfo.Instance.player;
//        }

//        rb = GetComponent<Rigidbody>();

//        if (rb != null && Camera.main != null)
//        {
//            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
//            Vector3 launchDirection = ray.direction;
//            launchDirection.y = 0f;
//            launchDirection.Normalize();

//            transform.rotation = Quaternion.LookRotation(launchDirection);
//            rb.linearVelocity = launchDirection * launchSpeed;
//            launchOrigin = transform.position;
//        }
//    }

//    void Update()
//    {
//        if (rb == null || player == null) return;

//        // 自动停止：超过10米
//        if (!isHovering && !isReturning && Vector3.Distance(transform.position, launchOrigin) >= 10f)
//        {
//            StopMovement();
//        }

//        // ⛔ 按 E 键控制行为
//        if (Input.GetKeyDown(KeyCode.E))
//        {
//            if (!isHovering && !isReturning)
//            {
//                StopMovement();
//            }
//            else if (isHovering && !isReturning)
//            {
//                ReturnToPlayer();
//            }
//        }

//        // ⏩ F 键传送玩家
//        if (Input.GetKeyDown(KeyCode.F))
//        {
//            if (player != null)
//            {
//                CharacterController cc = player.GetComponent<CharacterController>();
//                if (cc != null) cc.enabled = false;

//                player.transform.position = transform.position;

//                if (cc != null) cc.enabled = true;

//                Destroy(gameObject);
//            }
//        }
//    }

//    void StopMovement()
//    {
//        if (rb != null)
//        {
//            rb.linearVelocity = Vector3.zero;
//            rb.isKinematic = true;
//            isHovering = true;
//        }
//    }

//    void ReturnToPlayer()
//    {
//        if (rb != null && player != null)
//        {
//            rb.isKinematic = false;
//            Vector3 returnDir = (player.transform.position - transform.position).normalized;
//            returnDir.y = 0f;
//            rb.linearVelocity = returnDir * launchSpeed;

//            isReturning = true;
//            isHovering = false;
//        }
//    }

//    void OnTriggerEnter(Collider other)
//    {
//        // 检测是否触碰特定碰撞箱（Tag = "UmbrellaTarget"）
//        if (other.CompareTag("UmbrellaTarget"))
//        {
//            Destroy(gameObject);
//        }
//    }

//    private void OnDestroy()
//    {
//        if (currentUmbrella == this)
//        {
//            currentUmbrella = null;
//        }
//    }
//}