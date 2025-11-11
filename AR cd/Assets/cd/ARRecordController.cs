using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using TMPro;
using Vuforia;

public class ARRecordController : MonoBehaviour
{
    [System.Serializable]
    public class SongData
    {
        public string songName;
        public VideoClip videoClip;
        public AudioClip vocalClip;          // 人声音频
        public AudioClip accompanimentClip;  // 伴奏音频
        public GameObject characterModel;
        public Material backgroundMaterial;
        public AnimationClip danceAnimation; // 舞蹈动画
    }

    [Header("歌曲数据")]
    public List<SongData> songs = new List<SongData>();

    [Header("播放设置")]
    public bool resumeFromLastPosition = true; // ✅ 是否从上次暂停处继续播放

    [Header("UI组件")]
    public Text songNameText;
    public TextMeshProUGUI songNameTextTMP;
    public Button nextButton;
    public Button previousButton;
    public Button playPauseButton;
    public Text playPauseText;
    public TextMeshProUGUI playPauseTextTMP;
    public Button modeSwitchButton;
    public Text modeText;
    public TextMeshProUGUI modeTextTMP;
    public GameObject songListPanel;
    public Transform songListContent;
    public GameObject songButtonPrefab;

    [Header("进度控制")]
    public Slider progressSlider;
    public Text progressTimeText;
    public TextMeshProUGUI progressTimeTextTMP;
    public Text totalTimeText;
    public TextMeshProUGUI totalTimeTextTMP;

    [Header("音量控制")]
    public Slider volumeSlider;
    public Text volumeText;
    public TextMeshProUGUI volumeTextTMP;
    public Button muteButton;
    public Text muteButtonText;
    public TextMeshProUGUI muteButtonTextTMP;

    [Header("场景组件")]
    public VideoPlayer videoPlayer;
    public AudioSource vocalSource;
    public AudioSource accompanimentSource;
    public Renderer videoRenderer;
    public Transform characterSpawnPoint;

    [Header("Vuforia设置")]
    public ObserverBehaviour imageTarget;

    private int currentSongIndex = 0;
    private GameObject currentCharacter;
    private bool isPlaying = false;
    private bool isVocalMode = true;
    private bool isMuted = false;
    private Animator characterAnimator;
    private float previousVolume = 1f;
    private bool isSeeking = false;
    private float lastPlaybackTime = 0f; // ✅ 记录上次暂停的时间点

    // 新增：跟踪目标是否被识别
    private bool isTargetTracked = false;
    private bool isInitialized = false;

    void Start()
    {
        InitializeComponents();
        CreateSongListUI();

        // 初始状态：所有内容隐藏且暂停
        SetAllContentActive(false);
        PausePlayback();

        RegisterVuforiaEvents();
        isInitialized = true;
    }

    void InitializeComponents()
    {
        if (vocalSource == null)
            vocalSource = GetComponentInChildren<AudioSource>();
        if (accompanimentSource == null)
        {
            accompanimentSource = gameObject.AddComponent<AudioSource>();
            accompanimentSource.playOnAwake = false;
            accompanimentSource.volume = 0f;
        }

        if (videoPlayer == null)
            videoPlayer = GetComponentInChildren<VideoPlayer>();

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        }

        if (nextButton != null)
            nextButton.onClick.AddListener(NextSong);
        if (previousButton != null)
            previousButton.onClick.AddListener(PreviousSong);
        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(TogglePlayPause);
        if (modeSwitchButton != null)
            modeSwitchButton.onClick.AddListener(ToggleVocalAccompaniment);

        if (progressSlider != null)
        {
            progressSlider.onValueChanged.AddListener(OnProgressChanged);
            var eventTrigger = progressSlider.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            pointerDown.callback.AddListener((data) => { OnProgressSliderPointerDown(); });
            eventTrigger.triggers.Add(pointerDown);

            var pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            pointerUp.callback.AddListener((data) => { OnProgressSliderPointerUp(); });
            eventTrigger.triggers.Add(pointerUp);
        }

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            volumeSlider.value = 0.7f;
        }

        if (muteButton != null)
            muteButton.onClick.AddListener(ToggleMute);

        UpdateUI();
        UpdateVolumeUI();
    }

    void RegisterVuforiaEvents()
    {
        if (imageTarget != null)
            imageTarget.OnTargetStatusChanged += OnTargetStatusChanged;
    }

    void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus targetStatus)
    {
        if (targetStatus.Status == Status.TRACKED || targetStatus.Status == Status.EXTENDED_TRACKED)
            OnTargetFound();
        else
            OnTargetLost();
    }

    void Update()
    {
        // 只有在目标被识别时才更新播放逻辑
        if (isTargetTracked)
        {
            CheckForSongEnd();

            if (isPlaying && vocalSource.clip != null && videoPlayer != null && !isSeeking)
            {
                if (Mathf.Abs((float)videoPlayer.time - vocalSource.time) > 0.1f)
                    videoPlayer.time = vocalSource.time;
            }

            if (!isSeeking && vocalSource.clip != null)
                UpdateProgressUI();
        }
    }

    void UpdateProgressUI()
    {
        if (progressSlider != null && vocalSource.clip != null)
            progressSlider.value = vocalSource.time / vocalSource.clip.length;
        UpdateTimeDisplay();
    }

    void UpdateTimeDisplay()
    {
        if (vocalSource.clip == null) return;
        string currentTime = FormatTime(vocalSource.time);
        string totalTime = FormatTime(vocalSource.clip.length);
        if (progressTimeText != null) progressTimeText.text = currentTime;
        if (progressTimeTextTMP != null) progressTimeTextTMP.text = currentTime;
        if (totalTimeText != null) totalTimeText.text = totalTime;
        if (totalTimeTextTMP != null) totalTimeTextTMP.text = totalTime;
    }

    string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    void CreateSongListUI()
    {
        if (songListContent != null)
        {
            foreach (Transform child in songListContent)
                Destroy(child.gameObject);

            for (int i = 0; i < songs.Count; i++)
            {
                int index = i;
                if (songButtonPrefab != null)
                {
                    GameObject button = Instantiate(songButtonPrefab, songListContent);
                    TextMeshProUGUI btnTextTMP = button.GetComponentInChildren<TextMeshProUGUI>();
                    Text btnText = button.GetComponentInChildren<Text>();
                    if (btnTextTMP != null) btnTextTMP.text = $"{i + 1}. {songs[i].songName}";
                    if (btnText != null) btnText.text = $"{i + 1}. {songs[i].songName}";
                    button.GetComponent<Button>().onClick.AddListener(() => OnSongSelected(index));
                }
            }
        }
    }

    public void OnSongSelected(int songIndex)
    {
        // 只有在目标被识别时才允许切换歌曲
        if (isTargetTracked)
        {
            LoadSong(songIndex);
        }
        else
        {
            Debug.Log("请先扫描识别图以激活播放器");
        }
    }

    void LoadSong(int songIndex)
    {
        if (songIndex < 0 || songIndex >= songs.Count)
            return;

        SongData song = songs[songIndex];

        if (videoPlayer != null) videoPlayer.Stop();
        vocalSource.Stop();
        accompanimentSource.Stop();

        if (videoPlayer != null) videoPlayer.clip = song.videoClip;
        vocalSource.clip = song.vocalClip;
        accompanimentSource.clip = song.accompanimentClip;

        if (videoRenderer != null && song.backgroundMaterial != null)
            videoRenderer.material = song.backgroundMaterial;

        if (currentCharacter != null)
            Destroy(currentCharacter);

        if (song.characterModel != null && characterSpawnPoint != null)
        {
            currentCharacter = Instantiate(song.characterModel, characterSpawnPoint.position, characterSpawnPoint.rotation);
            currentCharacter.transform.SetParent(characterSpawnPoint);
            characterAnimator = currentCharacter.GetComponent<Animator>();
            if (characterAnimator != null)
            {
                characterAnimator.SetBool("IsDancing", true);
                if (song.danceAnimation != null)
                    characterAnimator.Play(song.danceAnimation.name);
            }
        }

        UpdateSongNameUI(song.songName);
        vocalSource.time = 0f;
        accompanimentSource.time = 0f;
        if (videoPlayer != null) videoPlayer.time = 0f;
        currentSongIndex = songIndex;
        UpdateButtonStates();

        if (progressSlider != null) progressSlider.value = 0f;
        UpdateTimeDisplay();

        // 只有在目标被识别时才自动播放
        if (isTargetTracked)
        {
            Play();
        }
    }

    void UpdateSongNameUI(string songName)
    {
        if (songNameText != null) songNameText.text = songName;
        if (songNameTextTMP != null) songNameTextTMP.text = songName;
    }

    public void TogglePlayPause()
    {
        // 只有在目标被识别时才允许控制播放
        if (isTargetTracked)
        {
            if (isPlaying) PausePlayback();
            else Play();
        }
        else
        {
            Debug.Log("请先扫描识别图以激活播放器");
        }
    }

    public void Play()
    {
        // 确保只有在目标被识别时才播放
        if (!isTargetTracked) return;

        isPlaying = true;
        vocalSource.Play();
        accompanimentSource.Play();
        if (videoPlayer != null) videoPlayer.Play();
        if (characterAnimator != null) characterAnimator.speed = 1f;
        UpdateUI();
    }

    public void PausePlayback()
    {
        isPlaying = false;

        // ✅ 记录暂停时播放位置
        lastPlaybackTime = vocalSource.time;

        vocalSource.Pause();
        accompanimentSource.Pause();
        if (videoPlayer != null) videoPlayer.Pause();
        if (characterAnimator != null) characterAnimator.speed = 0f;
        UpdateUI();
    }

    public void ToggleVocalAccompaniment()
    {
        // 只有在目标被识别时才允许切换模式
        if (isTargetTracked)
        {
            isVocalMode = !isVocalMode;
            if (isVocalMode)
            {
                vocalSource.volume = isMuted ? 0f : volumeSlider.value;
                accompanimentSource.volume = 0f;
            }
            else
            {
                vocalSource.volume = 0f;
                accompanimentSource.volume = isMuted ? 0f : volumeSlider.value;
            }
            UpdateUI();
        }
        else
        {
            Debug.Log("请先扫描识别图以激活播放器");
        }
    }

    void OnProgressChanged(float value)
    {
        // 只有在目标被识别时才允许拖动进度
        if (isTargetTracked && vocalSource.clip != null && isSeeking)
            SeekAudio(value * vocalSource.clip.length);
    }

    void OnProgressSliderPointerDown()
    {
        if (isTargetTracked)
            isSeeking = true;
    }

    void OnProgressSliderPointerUp()
    {
        if (isTargetTracked)
        {
            isSeeking = false;
            if (vocalSource.clip != null)
                SeekAudio(progressSlider.value * vocalSource.clip.length);
        }
    }

    void OnVolumeChanged(float volume)
    {
        if (!isMuted)
        {
            if (isVocalMode)
                vocalSource.volume = volume;
            else
                accompanimentSource.volume = volume;
        }
        previousVolume = volume;
        UpdateVolumeUI();
    }

    void ToggleMute()
    {
        // 只有在目标被识别时才允许静音
        if (isTargetTracked)
        {
            isMuted = !isMuted;
            if (isMuted)
            {
                previousVolume = volumeSlider.value;
                vocalSource.volume = 0f;
                accompanimentSource.volume = 0f;
            }
            else
            {
                vocalSource.volume = isVocalMode ? previousVolume : 0f;
                accompanimentSource.volume = isVocalMode ? 0f : previousVolume;
                volumeSlider.value = previousVolume;
            }
            UpdateVolumeUI();
        }
        else
        {
            Debug.Log("请先扫描识别图以激活播放器");
        }
    }

    void UpdateVolumeUI()
    {
        int percent = Mathf.RoundToInt(volumeSlider.value * 100);
        if (volumeText != null) volumeText.text = $"{percent}%";
        if (volumeTextTMP != null) volumeTextTMP.text = $"{percent}%";
        if (muteButtonText != null) muteButtonText.text = isMuted ? "取消静音" : "静音";
        if (muteButtonTextTMP != null) muteButtonTextTMP.text = isMuted ? "取消静音" : "静音";
    }

    public void NextSong()
    {
        if (isTargetTracked)
            LoadSong((currentSongIndex + 1) % songs.Count);
        else
            Debug.Log("请先扫描识别图以激活播放器");
    }

    public void PreviousSong()
    {
        if (isTargetTracked)
            LoadSong((currentSongIndex - 1 + songs.Count) % songs.Count);
        else
            Debug.Log("请先扫描识别图以激活播放器");
    }

    void UpdateButtonStates()
    {
        if (previousButton != null)
            previousButton.interactable = (currentSongIndex > 0) && isTargetTracked;
        if (nextButton != null)
            nextButton.interactable = (currentSongIndex < songs.Count - 1) && isTargetTracked;

        // 只有在目标被识别时才启用控制按钮
        if (playPauseButton != null)
            playPauseButton.interactable = isTargetTracked;
        if (modeSwitchButton != null)
            modeSwitchButton.interactable = isTargetTracked;
        if (progressSlider != null)
            progressSlider.interactable = isTargetTracked;
        if (volumeSlider != null)
            volumeSlider.interactable = isTargetTracked;
        if (muteButton != null)
            muteButton.interactable = isTargetTracked;
    }

    void UpdateUI()
    {
        if (playPauseText != null)
            playPauseText.text = isPlaying ? "暂停" : "播放";
        if (playPauseTextTMP != null)
            playPauseTextTMP.text = isPlaying ? "暂停" : "播放";
        if (modeText != null)
            modeText.text = isVocalMode ? "人声模式" : "伴奏模式";
        if (modeTextTMP != null)
            modeTextTMP.text = isVocalMode ? "人声模式" : "伴奏模式";
    }

    public void OnTargetFound()
    {
        Debug.Log("目标已识别，激活播放器");
        isTargetTracked = true;

        // 显示所有内容
        SetAllContentActive(true);

        // 加载当前歌曲（但不立即播放）
        if (isInitialized)
        {
            LoadSongWithoutPlay(currentSongIndex);

            // ✅ 根据设置决定从头或上次位置播放
            if (resumeFromLastPosition && lastPlaybackTime > 0f)
                SeekAudio(lastPlaybackTime);
            else
                SeekAudio(0f);

            // 自动开始播放
            Play();
        }

        UpdateButtonStates();
    }

    public void OnTargetLost()
    {
        Debug.Log("目标丢失，暂停播放并隐藏内容");
        isTargetTracked = false;

        // 隐藏所有内容
        SetAllContentActive(false);

        // 暂停播放
        PausePlayback();

        UpdateButtonStates();
    }

    // 新增：设置所有内容的激活状态
    void SetAllContentActive(bool active)
    {
        if (videoRenderer != null)
            videoRenderer.enabled = active;
        if (currentCharacter != null)
            currentCharacter.SetActive(active);

        // 可以添加其他需要隐藏/显示的内容
    }

    // 新增：加载歌曲但不播放
    void LoadSongWithoutPlay(int songIndex)
    {
        if (songIndex < 0 || songIndex >= songs.Count)
            return;

        SongData song = songs[songIndex];

        if (videoPlayer != null) videoPlayer.clip = song.videoClip;
        vocalSource.clip = song.vocalClip;
        accompanimentSource.clip = song.accompanimentClip;

        if (videoRenderer != null && song.backgroundMaterial != null)
            videoRenderer.material = song.backgroundMaterial;

        if (currentCharacter != null)
            Destroy(currentCharacter);

        if (song.characterModel != null && characterSpawnPoint != null)
        {
            currentCharacter = Instantiate(song.characterModel, characterSpawnPoint.position, characterSpawnPoint.rotation);
            currentCharacter.transform.SetParent(characterSpawnPoint);
            characterAnimator = currentCharacter.GetComponent<Animator>();
            if (characterAnimator != null)
            {
                characterAnimator.SetBool("IsDancing", true);
                if (song.danceAnimation != null)
                    characterAnimator.Play(song.danceAnimation.name);
            }
        }

        UpdateSongNameUI(song.songName);
        currentSongIndex = songIndex;
        UpdateButtonStates();

        if (progressSlider != null) progressSlider.value = 0f;
        UpdateTimeDisplay();
    }

    void CheckForSongEnd()
    {
        if (isPlaying && vocalSource.clip != null && vocalSource.time >= vocalSource.clip.length - 0.1f)
            NextSong();
    }

    void OnDestroy()
    {
        if (imageTarget != null)
            imageTarget.OnTargetStatusChanged -= OnTargetStatusChanged;
        if (progressSlider != null)
            progressSlider.onValueChanged.RemoveAllListeners();
        if (volumeSlider != null)
            volumeSlider.onValueChanged.RemoveAllListeners();
    }

    public void SeekAudio(float time)
    {
        if (vocalSource.clip == null) return;
        float t = Mathf.Clamp(time, 0f, vocalSource.clip.length);
        vocalSource.time = t;
        accompanimentSource.time = t;
        if (videoPlayer != null)
            videoPlayer.time = t;
        UpdateTimeDisplay();
    }
}