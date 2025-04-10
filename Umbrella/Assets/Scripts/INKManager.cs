using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Ink.Runtime;
using UnityEngine.SceneManagement;

public class LinearInkController : MonoBehaviour
{
    [Header("Ink JSON Asset")]
    public TextAsset inkJSONAsset;

    [Header("UI")]
    public TMP_Text nameText;      // 🟢 角色名字显示框
    public TMP_Text storyText;     // 🟢 对话内容显示框
    public Button continueButton;  // 🟢 点击继续按钮

    public CharacterManager characterManager; // 🟢 控制立绘切图的系统
    public SimpleSceneManager SceneManager;

    private Story story;
    private bool isWaitingForInput = true; // 新增字段，防止多次触发

    void Start()
    {
        story = new Story(inkJSONAsset.text);
        ShowNextLine();
        continueButton.onClick.AddListener(ShowNextLine);
    }

    void Update()
    {
        // 检测任意按键按下，并确保只触发一次
        if (Input.anyKeyDown && isWaitingForInput)
        {
            isWaitingForInput = false; // 标记为已处理输入
            ShowNextLine();
        }
    }

    void ShowNextLine()
    {
        if (story.canContinue)
        {
            string text = story.Continue().Trim();

            // 🧠 提取说话人和对白
            if (text.Contains(":"))
            {
                string[] parts = text.Split(':');
                string speaker = parts[0].Trim();
                string dialogue = parts[1].Trim();

                nameText.text = speaker;
                storyText.text = dialogue;
            }
            else
            {
                // 没有冒号就认为是旁白
                nameText.text = "";
                storyText.text = text;
            }

            // 🎯 读取当前行的标签
            foreach (string tag in story.currentTags)
            {
                HandleTag(tag);
            }
        }
        else
        {
            if (SceneManager != null)
            {
                SceneManager.LoadNextScene();
            }
            continueButton.interactable = false;
        }

        isWaitingForInput = true; // 允许下一次输入
    }

    void HandleTag(string tag)
    {
        Debug.Log("处理标签：" + tag);

        string[] parts = tag.Split(' ');

        // 格式为：sprite 角色名 贴图tag
        if (parts.Length == 3 && parts[0] == "sprite")
        {
            string characterName = parts[1];
            string spriteTag = parts[2];

            Debug.Log($"换立绘：{characterName} → {spriteTag}");
            characterManager.ChangeSprite(characterName, spriteTag);
        }

        // 👉 你以后也可以在这里扩展更多标签功能，比如：
        // if (parts[0] == "hide") characterManager.HideCharacter(...)
        // if (parts[0] == "show") characterManager.ShowCharacter(...)
    }
}