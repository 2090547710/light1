using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class AudioManager : MonoBehaviour
{
    // 单例实例
    public static AudioManager Instance { get; private set; }

    // 音频源：一个用于背景音乐，多个用于音效
    private AudioSource musicSource;
    private List<AudioSource> sfxSources = new List<AudioSource>();
    private int sfxSourceCount = 5; // 默认音效音频源数量

    // 音频剪辑字典
    private Dictionary<string, AudioClip> musicClips = new Dictionary<string, AudioClip>();
    private Dictionary<string, AudioClip> sfxClips = new Dictionary<string, AudioClip>();

    // 事件定义
    public static event Action<string> OnPlayMusic;
    public static event Action<string> OnStopMusic;
    public static event Action<string, float> OnPlaySFX;

    // 音量控制
    [SerializeField]
    [Range(0f, 1f)]
    private float musicVolume = 0.5f;

    [SerializeField]
    [Range(0f, 1f)]
    private float sfxVolume = 0.7f;

    // 添加缓存变量用于检测音量变化
    private float _lastMusicVolume;
    private float _lastSfxVolume;

    private void Awake()
    {
        Instance = this;
        // 初始化缓存变量
        _lastMusicVolume = musicVolume;
        _lastSfxVolume = sfxVolume;
        // 初始化音频源
        InitAudioSources();
        
        // 加载音频资源
        LoadAudioClips();
        
        // 订阅事件
        SubscribeEvents();
        
    }

    private void OnDestroy()
    {
        // 取消事件订阅
        UnsubscribeEvents();
    }

    private void InitAudioSources()
    {
        // 创建背景音乐音频源
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        
        // 创建音效音频源池
        for (int i = 0; i < sfxSourceCount; i++)
        {
            AudioSource sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.volume = sfxVolume;
            sfxSources.Add(sfxSource);
        }
    }

    private void LoadAudioClips()
    {
        // 从Resources文件夹加载音频文件
        // 注意：需要在Resources文件夹下创建Audio/Music和Audio/SFX文件夹
        
        // 加载背景音乐
        AudioClip[] musicFiles = Resources.LoadAll<AudioClip>("Audio/Music");
        foreach (AudioClip clip in musicFiles)
        {
            musicClips.Add(clip.name, clip);
        }
        
        // 加载音效
        AudioClip[] sfxFiles = Resources.LoadAll<AudioClip>("Audio/SFX");
        foreach (AudioClip clip in sfxFiles)
        {
            sfxClips.Add(clip.name, clip);
        }
    }

    private void SubscribeEvents()
    {
        OnPlayMusic += PlayMusic;
        OnStopMusic += StopMusic;
        OnPlaySFX += PlaySFX;
    }

    private void UnsubscribeEvents()
    {
        OnPlayMusic -= PlayMusic;
        OnStopMusic -= StopMusic;
        OnPlaySFX -= PlaySFX;
    }

    // 播放背景音乐
    private void PlayMusic(string musicName)
    {
        if (musicClips.TryGetValue(musicName, out AudioClip clip))
        {
            musicSource.clip = clip;
            musicSource.volume = musicVolume;
            musicSource.Play();
            musicSource.loop = true;
        }
        else
        {
            Debug.LogWarning($"音乐 {musicName} 不存在！");
        }
    }

    // 停止背景音乐
    private void StopMusic(string musicName)
    {
        // 如果musicName为空或与当前播放的音乐匹配，则停止播放
        if (string.IsNullOrEmpty(musicName) || 
            (musicSource.clip != null && musicSource.clip.name == musicName))
        {
            musicSource.Stop();
        }
    }

    // 播放音效
    private void PlaySFX(string sfxName, float volumeScale = 1.0f)
    {
        if (sfxClips.TryGetValue(sfxName, out AudioClip clip))
        {
            // 查找可用的音频源
            AudioSource source = GetAvailableSFXSource();
            if (source != null)
            {
                source.clip = clip;
                source.volume = sfxVolume * volumeScale;
                source.Play();
            }
        }
        else
        {
            Debug.LogWarning($"音效 {sfxName} 不存在！");
        }
    }

    // 获取可用的音效音频源
    private AudioSource GetAvailableSFXSource()
    {
        // 查找未在播放的音频源
        foreach (AudioSource source in sfxSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        
        // 如果没有可用的音频源，创建一个新的
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.loop = false;
        newSource.volume = sfxVolume;
        sfxSources.Add(newSource);
        
        return newSource;
    }

    // 静态方法，用于其他脚本调用
    public static void TriggerPlayMusic(string musicName)
    {
        OnPlayMusic?.Invoke(musicName);
    }

    public static void TriggerStopMusic(string musicName = "")
    {
        OnStopMusic?.Invoke(musicName);
    }

    public static void TriggerPlaySFX(string sfxName, float volumeScale = 1.0f)
    {
        OnPlaySFX?.Invoke(sfxName, volumeScale);
    }
    
    // 设置音乐音量
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        _lastMusicVolume = musicVolume;
        
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
    }
    
    // 设置音效音量
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        _lastSfxVolume = sfxVolume;
        
        foreach (AudioSource source in sfxSources)
        {
            if (source != null)
            {
                source.volume = sfxVolume;
            }
        }
    }
    
    // 获取音乐音量
    public float GetMusicVolume()
    {
        return musicVolume;
    }
    
    // 获取音效音量
    public float GetSFXVolume()
    {
        return sfxVolume;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // 检测音量变化并更新
        if (_lastMusicVolume != musicVolume)
        {
            _lastMusicVolume = musicVolume;
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }
        }
        
        if (_lastSfxVolume != sfxVolume)
        {
            _lastSfxVolume = sfxVolume;
            foreach (AudioSource source in sfxSources)
            {
                if (source != null)
                {
                    source.volume = sfxVolume;
                }
            }
        }
    }
}
