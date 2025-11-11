using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;

public class ARMusicPlayerController : MonoBehaviour
{
    [System.Serializable]
    public class SongData
    {
        public string songName;
        public AudioClip vocalClip;      // 人声音频
        public AudioClip accompanimentClip; // 伴奏音频
        public VideoClip danceVideo;     // 舞蹈视频
        public string animationTrigger;  // 动画触发器名称
    }

    // 歌曲数据配置
    public List<SongData> songList = new List<SongData>();
    public int currentSongIndex = 0;

    // 组件引用
    public AudioSource vocalSource;
    public AudioSource accompanimentSource;
    public VideoPlayer videoPlayer;
    public Animator characterAnimator;

    // UI引用
    [Header("UI References")]
    public Transform songListContainer;
    public GameObject songButtonPrefab;
    public Slider progressSlider;
    public Button playPauseButton;
    public Text playPauseText;
    public Slider volumeSlider;
    public Button modeSwitchButton;
    public Text modeText;

    // 播放状态
    private bool isPlaying = false;
    private bool isVocalMode = true;
    private float currentVolume = 1f;

    void Start()
    {
        InitializeComponents();
        CreateSongListUI();
        LoadSong(0);

        // 初始设置为暂停状态，等待Vuforia识别
        PausePlayback();
    }

    void InitializeComponents()
    {
        // 配置VideoPlayer
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.isLooping = true;

        // 配置AudioSource
        vocalSource.playOnAwake = false;
        accompanimentSource.playOnAwake = false;
        accompanimentSource.volume = 0f; // 初始隐藏伴奏

        // 设置UI事件监听
        progressSlider.onValueChanged.AddListener(OnProgressChanged);
        playPauseButton.onClick.AddListener(TogglePlayPause);
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        modeSwitchButton.onClick.AddListener(ToggleVocalAccompaniment);

        volumeSlider.value = currentVolume;
    }

    void Update()
    {
        if (isPlaying)
        {
            UpdateProgressDisplay();
        }
    }

    void CreateSongListUI()
    {
        // 清空现有列表
        foreach (Transform child in songListContainer)
        {
            Destroy(child.gameObject);
        }

        // 创建歌曲按钮
        for (int i = 0; i < songList.Count; i++)
        {
            int songIndex = i;
            GameObject button = Instantiate(songButtonPrefab, songListContainer);
            button.GetComponentInChildren<Text>().text = $"{i + 1}. {songList[songIndex].songName}";
            button.GetComponent<Button>().onClick.AddListener(() => OnSongSelected(songIndex));
        }
    }

    public void OnSongSelected(int songIndex)
    {
        currentSongIndex = songIndex;
        LoadSong(songIndex);
        if (isPlaying)
        {
            Play();
        }
    }

    void LoadSong(int songIndex)
    {
        if (songIndex < 0 || songIndex >= songList.Count) return;

        SongData song = songList[songIndex];

        // 加载音频
        vocalSource.clip = song.vocalClip;
        accompanimentSource.clip = song.accompanimentClip;

        // 加载视频
        videoPlayer.clip = song.danceVideo;

        // 触发动画
        if (characterAnimator != null && !string.IsNullOrEmpty(song.animationTrigger))
        {
            characterAnimator.SetTrigger(song.animationTrigger);
        }

        // 重置进度
        progressSlider.value = 0f;
        videoPlayer.time = 0f;
    }

    public void TogglePlayPause()
    {
        if (isPlaying)
        {
            PausePlayback();
        }
        else
        {
            Play();
        }
    }

    public void Play()
    {
        isPlaying = true;

        vocalSource.Play();
        accompanimentSource.Play();
        videoPlayer.Play();

        if (characterAnimator != null)
        {
            characterAnimator.speed = 1f;
        }

        playPauseText.text = "暂停";
    }

    public void PausePlayback()
    {
        isPlaying = false;

        vocalSource.Pause();
        accompanimentSource.Pause();
        videoPlayer.Pause();

        if (characterAnimator != null)
        {
            characterAnimator.speed = 0f;
        }

        playPauseText.text = "播放";
    }

    void UpdateProgressDisplay()
    {
        if (vocalSource.clip != null && vocalSource.clip.length > 0)
        {
            float progress = vocalSource.time / vocalSource.clip.length;
            progressSlider.value = progress;
        }
    }

    void OnProgressChanged(float value)
    {
        if (vocalSource.clip != null && vocalSource.clip.length > 0)
        {
            float newTime = value * vocalSource.clip.length;
            vocalSource.time = newTime;
            accompanimentSource.time = newTime;
            videoPlayer.time = newTime;
        }
    }

    void OnVolumeChanged(float value)
    {
        currentVolume = value;
        vocalSource.volume = value;
        accompanimentSource.volume = value;
        videoPlayer.SetDirectAudioVolume(0, value);
    }

    public void ToggleVocalAccompaniment()
    {
        isVocalMode = !isVocalMode;

        if (isVocalMode)
        {
            // 人声模式：人声音量正常，伴奏静音
            vocalSource.volume = currentVolume;
            accompanimentSource.volume = 0f;
            modeText.text = "人声模式";
        }
        else
        {
            // 伴奏模式：人声静音，伴奏音量正常
            vocalSource.volume = 0f;
            accompanimentSource.volume = currentVolume;
            modeText.text = "伴奏模式";
        }
    }

    // Vuforia目标识别事件处理
    public void OnTargetFound()
    {
        // 识别图找到时的处理
        Debug.Log("目标已识别，准备播放");
    }

    public void OnTargetLost()
    {
        // 识别图丢失时的处理
        PausePlayback();
        Debug.Log("目标丢失，暂停播放");
    }

    public void NextSong()
    {
        currentSongIndex = (currentSongIndex + 1) % songList.Count;
        LoadSong(currentSongIndex);
        if (isPlaying) Play();
    }

    public void PreviousSong()
    {
        currentSongIndex = (currentSongIndex - 1 + songList.Count) % songList.Count;
        LoadSong(currentSongIndex);
        if (isPlaying) Play();
    }
}
