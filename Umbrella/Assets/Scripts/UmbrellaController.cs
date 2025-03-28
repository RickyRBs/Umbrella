using UnityEngine;

public class UmbrellaController : MonoBehaviour
{
    public float launchSpeed = 5f;
    public GameObject player;  // If not set during instantiation, try to get it automatically
    private Vector3 launchOrigin; // 起点

    public static UmbrellaController currentUmbrella;

    private Rigidbody rb;
    private bool isHovering = false;

    void Start()
    {
        launchOrigin = transform.position;
        // Ensure only one umbrella exists at a time
        if (currentUmbrella != null)
        {
            Destroy(currentUmbrella.gameObject);
        }
        currentUmbrella = this;

        // If player is not assigned, try to get it from UmbrellaInfo
        if (player == null && UmbrellaInfo.Instance != null)
        {
            player = UmbrellaInfo.Instance.player;
            if (player == null)
            {
                Debug.LogWarning("UmbrellaController: Player retrieved from UmbrellaInfo is null!");
            }
        }

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            Vector3 launchDirection = ray.direction;
            launchDirection.y = 0f; // 不让雨伞往上飞
            launchDirection.Normalize();

            transform.rotation = Quaternion.LookRotation(launchDirection);
            rb.linearVelocity = launchDirection * launchSpeed;
        }
    }

    void Update()
    {
        // 停止飞行逻辑
        if (!isHovering && Vector3.Distance(transform.position, launchOrigin) >= 25f)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
                isHovering = true;
            }
        }

        // 手动停止
        if (Input.GetKeyDown(KeyCode.E) && !isHovering)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
                isHovering = true;
            }
        }

        // 传送
        if (Input.GetKeyDown(KeyCode.T))
        {
            Vector3 umbrellaPos = transform.position;
            Debug.Log("T key pressed, teleporting player to umbrella position: " + umbrellaPos);
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = false;

                player.transform.position = umbrellaPos;

                if (cc != null)
                    cc.enabled = true;
            }
            else
            {
                Debug.LogWarning("Teleport failed: player reference is null!");
            }
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // Clear static reference if the current instance is destroyed
        if (currentUmbrella == this)
        {
            currentUmbrella = null;
        }
    }
}