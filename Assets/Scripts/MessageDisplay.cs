using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public enum MessageType
{
    Info,
    Warning,
    Error,
    Success
}

public class MessageEvent
{
    public string Message { get; private set; }
    public MessageType Type { get; private set; }
    public float Duration { get; private set; }

    public MessageEvent(string message, MessageType type = MessageType.Info, float duration = 3f)
    {
        Message = message;
        Type = type;
        Duration = duration;
    }
}

public static class MessageEventSystem
{
    public static event Action<MessageEvent> OnMessageReceived;
    
    public static void SendMessage(string message, MessageType type = MessageType.Info, float duration = 3f)
    {
        var messageEvent = new MessageEvent(message, type, duration);
        OnMessageReceived?.Invoke(messageEvent);
        
        // 同时保留Debug输出（可选）
        switch (type)
        {
            case MessageType.Warning:
                Debug.LogWarning(message);
                break;
            case MessageType.Error:
                Debug.LogError(message);
                break;
            default:
                Debug.Log(message);
                break;
        }
    }
    
    public static void SendMessageAt(string message, Transform target, MessageType type = MessageType.Info, float duration = 3f)
    {
        if (MessageManager.instance != null)
        {
            MessageManager.instance.SendMessage(message, target, type, duration);
        }
        else
        {
            SendMessage(message, type, duration);
        }
    }
}

public class MessageDisplay : MonoBehaviour
{
    [SerializeField] private int maxMessages = 5;
    [SerializeField] private Color infoColor = Color.white;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color errorColor = Color.red;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private float fadeTime = 0.5f;
    
    private TMP_Text textComponent;
    private CanvasGroup canvasGroup;
    private Queue<MessageEvent> messageQueue = new Queue<MessageEvent>();
    private Coroutine displayCoroutine;
    private bool isDisplaying = false;
    private Transform targetTransform;
    private Vector3 positionOffset;
    
    private void Awake()
    {
        textComponent = GetComponentInChildren<TMP_Text>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (textComponent == null)
        {
            Debug.LogError("MessageDisplay必须包含TMP_Text子组件");
        }
        
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // 初始时隐藏
        canvasGroup.alpha = 0;
    }
    
    private void OnEnable()
    {
        if (MessageManager.instance != null)
        {
            MessageManager.instance.RegisterMessageDisplay(this);
        }
        else
        {
            MessageEventSystem.OnMessageReceived += EnqueueMessage;
        }
    }
    
    private void OnDisable()
    {
        if (MessageManager.instance != null)
        {
            MessageManager.instance.UnregisterMessageDisplay(this);
        }
        else
        {
            MessageEventSystem.OnMessageReceived -= EnqueueMessage;
        }
        
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }
    }
    
    public void Initialize(string message, Transform container, MessageType type = MessageType.Info, float duration = 3f)
    {
        transform.SetParent(container, false);
        EnqueueMessage(new MessageEvent(message, type, duration));
    }
    
    public void AddMessage(string message, MessageType type = MessageType.Info, float duration = 3f)
    {
        EnqueueMessage(new MessageEvent(message, type, duration));
    }
    
    private void EnqueueMessage(MessageEvent messageEvent)
    {
        messageQueue.Enqueue(messageEvent);
        
        // 如果队列超过最大容量，移除最旧的消息
        while (messageQueue.Count > maxMessages)
        {
            messageQueue.Dequeue();
        }
        
        // 如果没有正在显示的消息，开始显示
        if (!isDisplaying)
        {
            displayCoroutine = StartCoroutine(DisplayMessagesCoroutine());
        }
    }
    
    private IEnumerator DisplayMessagesCoroutine()
    {
        isDisplaying = true;
        
        while (messageQueue.Count > 0)
        {
            MessageEvent currentEvent = messageQueue.Dequeue();
            
            // 设置文本和颜色
            if (textComponent != null)
            {
                textComponent.text = currentEvent.Message;
                
                // 设置颜色
                switch (currentEvent.Type)
                {
                    case MessageType.Warning:
                        textComponent.color = warningColor;
                        break;
                    case MessageType.Error:
                        textComponent.color = errorColor;
                        break;
                    case MessageType.Success:
                        textComponent.color = successColor;
                        break;
                    default:
                        textComponent.color = infoColor;
                        break;
                }
            }
            
            // 淡入
            float startTime = Time.time;
            while (Time.time < startTime + fadeTime)
            {
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, (Time.time - startTime) / fadeTime);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            
            // 显示一段时间
            yield return new WaitForSeconds(currentEvent.Duration);
            
            // 淡出
            startTime = Time.time;
            while (Time.time < startTime + fadeTime)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, (Time.time - startTime) / fadeTime);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            
            // 如果队列中还有消息，则短暂暂停后显示下一条
            if (messageQueue.Count > 0)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        isDisplaying = false;
        displayCoroutine = null;
    }
    
    public void SetTargetTransform(Transform target, Vector3 offset)
    {
        this.targetTransform = target;
        this.positionOffset = offset;
    }
    
    private void Update()
    {
        if (targetTransform != null)
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector3 worldPos = targetTransform.position + positionOffset;
                rectTransform.position = worldPos;
            }
        }
    }
    
    // 重置对象状态以便重用
    public void ResetState()
    {
        // 停止所有协程
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }
        
        // 清空消息队列
        messageQueue.Clear();
        
        // 重置显示状态
        isDisplaying = false;
        
        // 重置目标跟踪
        targetTransform = null;
        
        // 重置透明度
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
        }
    }
}
