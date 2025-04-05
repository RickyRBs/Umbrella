using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CharacterManager : MonoBehaviour
{
    [System.Serializable]
    public class CharacterSprite
    {
        public string tagName;
        public Sprite sprite;
    }

    [System.Serializable]
    public class CharacterData
    {
        public string name;
        public Image image;
        public Animator animator;
        public List<CharacterSprite> sprites;
    }

    public List<CharacterData> characters;

    public void ShowCharacter(string name, string trigger = "")
    {
        var character = GetCharacter(name);
        if (character != null)
        {
            character.image.gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(trigger) && character.animator != null)
            {
                character.animator.SetTrigger(trigger);
            }
        }
    }

    public void HideCharacter(string name)
    {
        var character = GetCharacter(name);
        if (character != null)
        {
            character.image.gameObject.SetActive(false);
        }
    }

    public void ChangeSprite(string name, string tagName)
    {
        var character = GetCharacter(name);
        if (character == null)
        {
            Debug.LogWarning("没找到角色：" + name);
            return;
        }

        foreach (var item in character.sprites)
        {
            if (item.tagName == tagName && item.sprite != null)
            {
                character.image.sprite = item.sprite;
                Debug.Log($"成功换图：{name} → {tagName}");
                return;
            }
        }

        Debug.LogWarning($"找不到 {name} 的贴图 tag：{tagName}");
    }

    private CharacterData GetCharacter(string name)
    {
        return characters.Find(c => c.name == name);
    }
}