using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

public class FixedAutoPlayRecord : MonoBehaviour
{
    [Header("音频设置")]
    public AudioClip musicClip;
    public bool playOnDetection = true;
    public bool stopOnLost = true;

    [Header("唱片设置")]
    public Transform recordDisc;
    public float rotationSpeed = 30f;

    [Header("调试")]
    public bool showDebugLogs = true;

    private AudioSource audioSource;
    private ObserverBehaviour observerBehaviour;
    private bool isTracked = false;

    void Start()
    {
        InitializeComponents();
        SetupVuforiaEvents();
    }

    void Update()
    {
        // 旋转唱片
        if (isTracked && audioSource.isPlaying && recordDisc != null)
        {
            recordDisc.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        }
    }

    // 初始化组件
    private void InitializeComponents()
    {
        // 设置音频源
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.clip = musicClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        // 获取ObserverBehaviour
        observerBehaviour = GetComponent<ObserverBehaviour>();

        if (observerBehaviour == null)
        {
            Debug.LogError("未找到ObserverBehaviour组件！请确保此脚本附加在ImageTarget上。");
        }
    }

    // 设置Vuforia事件监听
    private void SetupVuforiaEvents()
    {
        if (observerBehaviour != null)
        {
            // 使用StatusChanged事件 - 这是最可靠的方式
            observerBehaviour.OnTargetStatusChanged += OnTargetStatusChanged;

            // 同时注册默认事件作为备份
            var defaultEventHandler = GetComponent<DefaultObserverEventHandler>();
            if (defaultEventHandler != null)
            {
                defaultEventHandler.OnTargetFound.AddListener(OnTargetFound);
                defaultEventHandler.OnTargetLost.AddListener(OnTargetLost);
            }

            Log("Vuforia事件监听器已注册");
        }
    }

    // Vuforia目标状态变化回调 - 主要检测方法
    private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus targetStatus)
    {
        bool nowTracked = (targetStatus.Status == Status.TRACKED ||
                          targetStatus.Status == Status.EXTENDED_TRACKED);

        if (nowTracked && !isTracked)
        {
            // 新检测到目标
            isTracked = true;
            OnTargetDetected();
        }
        else if (!nowTracked && isTracked)
        {
            // 目标丢失
            isTracked = false;
            OnTargetLost();
        }

        Log($"目标状态: {targetStatus.Status} - 追踪状态: {isTracked}");
    }

    // 目标被检测到
    private void OnTargetDetected()
    {
        Log("检测到目标 - OnTargetDetected");

        if (playOnDetection)
        {
            StartPlayback();
        }

        // 视觉反馈
        ShowDetectionEffect();
    }

    // 目标丢失
    private void OnTargetLost()
    {
        Log("目标丢失 - OnTargetLost");

        if (stopOnLost)
        {
            StopPlayback();
        }
    }

    // 备用检测方法 - 通过DefaultObserverEventHandler
    public void OnTargetFound()
    {
        Log("备用检测 - OnTargetFound");
        if (!isTracked)
        {
            isTracked = true;
            OnTargetDetected();
        }
    }

    

    // 开始播放
    public void StartPlayback()
    {
        if (musicClip == null)
        {
            Debug.LogWarning("未设置音乐文件！");
            return;
        }

        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();
            Log("开始播放音乐");

            // 视觉反馈
            StartCoroutine(ShowPlayEffect());
        }
    }

    // 停止播放
    public void StopPlayback()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            Log("停止播放音乐");
        }
    }

    // 显示检测效果
    private void ShowDetectionEffect()
    {
        if (recordDisc != null)
        {
            // 简单的脉冲动画
            StartCoroutine(PulseAnimation(recordDisc, 1.2f, 0.3f));
        }
    }

    // 脉冲动画协程
    private System.Collections.IEnumerator PulseAnimation(Transform target, float scaleMultiplier, float duration)
    {
        Vector3 originalScale = target.localScale;
        Vector3 targetScale = originalScale * scaleMultiplier;

        float time = 0;
        while (time < duration)
        {
            target.localScale = Vector3.Lerp(originalScale, targetScale, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        time = 0;
        while (time < duration)
        {
            target.localScale = Vector3.Lerp(targetScale, originalScale, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        target.localScale = originalScale;
    }

    // 显示播放效果协程
    private System.Collections.IEnumerator ShowPlayEffect()
    {
        // 这里可以添加播放时的特效
        yield return new WaitForSeconds(0.1f);
    }

    // 调试日志
    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[AR唱片] {message}");
        }
    }

    void OnDestroy()
    {
        // 清理事件监听
        if (observerBehaviour != null)
        {
            observerBehaviour.OnTargetStatusChanged -= OnTargetStatusChanged;
        }
    }
}
