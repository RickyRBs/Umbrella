using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleSceneManager : MonoBehaviour
{
    [Header("下一个场景名称（需添加进 Build Settings）")]
    public string nextSceneName;

    public void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("你还没填下一个场景的名字！");
        }
    }

}