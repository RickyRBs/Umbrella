using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // 如果你用的是 TextMeshPro

public class SceneTriggerZone : MonoBehaviour
{
    [Header("UI 提示文字")]
    public GameObject hintText; // Canvas 上的提示文字对象

    [Header("场景设置")]
    public string sceneToLoad = "NextScene"; // 要切换的场景名
    public KeyCode triggerKey = KeyCode.E; // 切换场景的按键

    private bool playerInZone = false;

    void Start()
    {
        if (hintText != null)
            hintText.SetActive(false); // 默认隐藏
    }

    void Update()
    {
        if (playerInZone && Input.GetKeyDown(triggerKey))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = true;
            if (hintText != null)
                hintText.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;
            if (hintText != null)
                hintText.SetActive(false);
        }
    }
}