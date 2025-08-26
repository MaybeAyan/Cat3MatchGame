using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // --- 单例模式核心 ---
    public static AudioManager Instance;

    [Header("音源组件")]
    public AudioSource bgmSource; // 用于播放背景音乐
    public AudioSource sfxSource; // 用于播放音效

    [Header("音频文件")]
    public AudioClip backgroundMusic;

    void Awake()
    {
        // --- 单例模式实现 ---
        if (Instance == null)
        {
            Instance = this;
            // 确保 AudioManager 在切换场景时不会被销毁
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 如果场景中已存在 AudioManager，则销毁当前这个，保证唯一性
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 游戏开始时加载设置并应用
        LoadAndApplyAudioSettings();
        PlayBGM();
    }

    // 加载并应用音频设置的方法
    private void LoadAndApplyAudioSettings()
    {
        // 读取保存的音量和静音状态，如果不存在则使用默认值
        float bgmVolume = PlayerPrefs.GetFloat("BGM_Volume", 0.1f); // 降低背景音乐默认音量到30%

        float sfxVolume = PlayerPrefs.GetFloat("SFX_Volume", 0.4f); // 音效默认音量设为70%

        // 将设置应用到 AudioSource 组件
        bgmSource.volume = bgmVolume;

        sfxSource.volume = sfxVolume;
    }


    // 播放背景音乐的方法
    public void PlayBGM()
    {
        bgmSource.clip = backgroundMusic;
        bgmSource.loop = true; // 设置为循环播放
        bgmSource.Play();
    }

    // 播放音效的公共方法
    // 任何其他脚本都可以通过 AudioManager.Instance.PlaySFX(clip) 来调用
    public void PlaySFX(AudioClip clip)
    {
        // 使用 PlayOneShot 可以让多个音效叠加播放而不会打断彼此
        sfxSource.PlayOneShot(clip);
    }
}