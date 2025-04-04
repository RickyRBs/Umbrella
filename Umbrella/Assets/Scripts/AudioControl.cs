using System.Collections;
using UnityEngine;

public class UmbrellaAudioController : MonoBehaviour
{
    [Header("Audio Source")]
    // 指定伞上用于播放音效的 AudioSource（如果没赋值则会自动获取）
    public AudioSource audioSource;

    [Header("Audio Clips")]
    // 各个事件对应的音效
    public AudioClip chargeSound;   // 蓄力时的音效（需要循环播放）
    public AudioClip launchSound;   // 发射音效
    public AudioClip stopSound;     // 停止飞行时的音效
    public AudioClip closeSound;    // 关闭/收回时的音效

    [Header("Fade Settings")]
    public float fadeDuration = 1f; // 淡出时长（秒）

    private Coroutine fadeCoroutine;

    void Awake()
    {
        if (audioSource == null)
        {
            // 如果未指定，则尝试获取当前物体的 AudioSource
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogError("UmbrellaAudioController: 没有找到 AudioSource 组件！");
            }
        }
    }

    /// <summary>
    /// 播放蓄力音效，循环播放
    /// </summary>
    public void PlayChargeSound()
    {
        if (chargeSound != null)
        {
            audioSource.clip = chargeSound;
            audioSource.loop = true;
            audioSource.volume = 1f;
            audioSource.Play();
        }
    }

    /// <summary>
    /// 淡出并停止蓄力音效
    /// </summary>
    public void StopChargeSound()
    {
        if (audioSource.isPlaying && audioSource.clip == chargeSound)
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOutAndStop());
        }
    }

    /// <summary>
    /// 播放发射音效（一次性）
    /// </summary>
    public void PlayLaunchSound()
    {
        PlayOneShot(launchSound);
    }

    /// <summary>
    /// 播放停止飞行时的音效（一次性）
    /// </summary>
    public void PlayStopSound()
    {
        PlayOneShot(stopSound);
    }

    /// <summary>
    /// 播放关闭/收回音效（一次性）
    /// </summary>
    public void PlayCloseSound()
    {
        PlayOneShot(closeSound);
    }

    /// <summary>
    /// 辅助方法：播放一次性音效
    /// </summary>
    private void PlayOneShot(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// 淡出当前播放的音效并停止播放
    /// </summary>
    IEnumerator FadeOutAndStop()
    {
        float startVolume = audioSource.volume;
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, timer / fadeDuration);
            yield return null;
        }
        audioSource.Stop();
        audioSource.volume = startVolume; // 重置音量，便于下次播放
    }
}