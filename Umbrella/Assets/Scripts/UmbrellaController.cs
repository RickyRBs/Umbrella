using UnityEngine;

public class UmbrellaController : MonoBehaviour
{
    public float launchSpeed = 5f;
    public GameObject player;  // If not set during instantiation, try to get it automatically

    public static UmbrellaController currentUmbrella;

    private Rigidbody rb;
    private bool isHovering = false;

    void Start()
    {
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
            transform.rotation = Quaternion.LookRotation(launchDirection);
            rb.linearVelocity = launchDirection * launchSpeed;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && !isHovering)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
                isHovering = true;
            }
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            Vector3 umbrellaPos = transform.position;
            Debug.Log("T key pressed, teleporting player to umbrella position: " + umbrellaPos);
            if (player != null)
            {
                // Temporarily disable CharacterController if the player has one
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.enabled = false;
                }
                player.transform.position = umbrellaPos;
                Debug.Log("Teleport successful, player's new position: " + player.transform.position);
                if (cc != null)
                {
                    cc.enabled = true;
                }
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