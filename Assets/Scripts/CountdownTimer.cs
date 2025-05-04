using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class CountdownTimer : MonoBehaviour
{
    [Tooltip("倒计时总时间（秒）")]
    [SerializeField] private float duration = 60f;
    
    [Tooltip("时间格式化，如：mm:ss 或 hh:mm:ss")]
    [SerializeField] private string timeFormat = "mm:ss";
    
    public UnityEvent onCountdownFinished;
    
    public UnityEvent<float> onCountdownUpdated;
    
    [Tooltip("显示倒计时的Text组件（UI.Text或TMP_Text）")]
    [SerializeField] private GameObject textDisplay;
    
    private Text uiText;
    private TMP_Text tmpText;
    private float remainingTime;
    private bool isRunning = false;
    
    private void Awake()
    {
        // 获取文本组件
        if (textDisplay != null)
        {
            uiText = textDisplay.GetComponent<Text>();
            tmpText = textDisplay.GetComponent<TMP_Text>();
            
            if (uiText == null && tmpText == null)
            {
                Debug.LogError("CountdownTimer: 文本显示对象必须包含Text或TMP_Text组件");
            }
        }
    }
    
    private void Start()
    {
        // 初始化剩余时间
        remainingTime = duration;
        
        // 初始化显示
        UpdateTimeDisplay();
    }
    

    // 开始倒计时
    public void StartCountdown()
    {
        if (!isRunning)
        {
            isRunning = true;
            StartCoroutine(CountdownCoroutine());
        }
    }
    

    // 暂停倒计时
    public void PauseCountdown()
    {
        isRunning = false;
    }
    
    // 继续倒计时
    public void ResumeCountdown()
    {
        if (!isRunning)
        {
            isRunning = true;
            StartCoroutine(CountdownCoroutine());
        }
    }
    
    // 停止并重置倒计时
    public void StopCountdown()
    {
        isRunning = false;
        remainingTime = duration;
        UpdateTimeDisplay();
    }
    
    // 设置倒计时时间
    public void SetCountdownTime(float seconds)
    {
        duration = seconds;
        remainingTime = seconds;
        UpdateTimeDisplay();
    }
    
    // 设置显示倒计时的文本组件
    public void SetTextDisplay(GameObject textGameObject)
    {
        if (textGameObject != null)
        {
            textDisplay = textGameObject;
            uiText = textDisplay.GetComponent<Text>();
            tmpText = textDisplay.GetComponent<TMP_Text>();
            
            if (uiText == null && tmpText == null)
            {
                Debug.LogError("CountdownTimer: 文本显示对象必须包含Text或TMP_Text组件");
                return;
            }
            
            UpdateTimeDisplay();
        }
    }
    
    // 获取剩余时间（秒）
    public int GetRemainingTime()
    {
        return Mathf.FloorToInt(remainingTime);
    }
    
    // 获取剩余时间（浮点数）
    public float GetRemainingTimeFloat()
    {
        return remainingTime;
    }
    
    // 检查计时器是否正在运行
    public bool IsRunning()
    {
        return isRunning;
    }
    
    private IEnumerator CountdownCoroutine()
    {
        while (isRunning && remainingTime > 0)
        {
            yield return new WaitForSeconds(1f);
            remainingTime -= 1f;
            
            // 触发更新事件
            onCountdownUpdated?.Invoke(remainingTime);
            
            // 更新显示
            UpdateTimeDisplay();
            
            // 检查是否结束
            if (remainingTime <= 0)
            {
                isRunning = false;
                onCountdownFinished?.Invoke();
                yield break;
            }
        }
    }
    
    private void UpdateTimeDisplay()
    {
        if (textDisplay == null) return;
        
        string formattedTime = FormatTime(remainingTime);
        
        // 更新对应的文本组件
        if (uiText != null)
        {
            uiText.text = formattedTime;
        }
        else if (tmpText != null)
        {
            tmpText.text = formattedTime;
        }
    }
    
    private string FormatTime(float timeInSeconds)
    {
        int hours = Mathf.FloorToInt(timeInSeconds / 3600);
        int minutes = Mathf.FloorToInt((timeInSeconds % 3600) / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        
        if (timeFormat.Contains("hh"))
        {
            return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
        }
        else if (timeFormat.Contains("mm"))
        {
            return string.Format("{0:00}:{1:00}", minutes, seconds);
        }
        else
        {
            return seconds.ToString();
        }
    }
}
