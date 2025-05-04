using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public enum MessageType
{
    Auto,   // 自动消失
    Click   // 点击消失
}

public class MessageEvent
{
    public string Message { get; private set; }
    public MessageType Type { get; private set; }
    public float Duration { get; private set; }

    public MessageEvent(string message, MessageType type = MessageType.Auto, float duration = 3f)
    {
        Message = message;
        Type = type;
        Duration = duration;
    }
}

public static class MessageEventSystem
{
    public static event Action<MessageEvent> OnMessageReceived;
    
    public static void SendMessage(string message, MessageType type = MessageType.Auto, float duration = 3f)
    {
        var messageEvent = new MessageEvent(message, type, duration);
        OnMessageReceived?.Invoke(messageEvent);
        
        // 同时保留Debug输出
        Debug.Log(message);
    }
    
    public static void SendMessageAt(string message, Transform target, MessageType type = MessageType.Auto, float duration = 3f)
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
    [SerializeField] private float fadeTime = 0.5f;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button closeButton;
    [SerializeField]private TMP_Text textComponent;
    
    private CanvasGroup canvasGroup;
    private Queue<MessageEvent> messageQueue = new Queue<MessageEvent>();
    private Coroutine displayCoroutine;
    private bool isDisplaying = false;
    private Transform targetTransform;
    private Vector3 positionOffset;
    private MessageEvent currentMessage;
    
    // 添加目标Transform的公共访问器
    public Transform TargetTransform => targetTransform;
    
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
        
        if (backgroundImage == null)
        {
            backgroundImage = GetComponentInChildren<Image>();
            if (backgroundImage == null)
            {
                Debug.LogWarning("MessageDisplay未找到背景图像组件");
            }
        }
        
        // 直接在当前GameObject上添加Button组件
        if (closeButton == null)
        {
            // 检查是否已有Button组件
            closeButton = GetComponent<Button>();
            if (closeButton == null)
            {
                // 直接添加到当前GameObject
                closeButton = gameObject.AddComponent<Button>();
            }
        }
        
        // 添加按钮点击监听
        closeButton.onClick.AddListener(OnCloseButtonClicked);
        
        // 初始时隐藏
        canvasGroup.alpha = 0;
        
        // 使按钮初始不可交互而不是隐藏，避免影响现有显示
        closeButton.interactable = false;
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
    
    public void Initialize(string message, Transform container, MessageType type = MessageType.Auto, float duration = 3f)
    {
        transform.SetParent(container, false);
        EnqueueMessage(new MessageEvent(message, type, duration));
    }
    
    public void AddMessage(string message, MessageType type = MessageType.Auto, float duration = 3f)
    {
        EnqueueMessage(new MessageEvent(message, type, duration));
    }
    
    private void EnqueueMessage(MessageEvent messageEvent)
    {
        // 如果是自动消失类型的消息，并且当前有消息正在显示
        if (messageEvent.Type == MessageType.Auto && isDisplaying)
        {
            // 如果当前显示的是Click类型消息，将其重新加到队首
            if (currentMessage != null && currentMessage.Type == MessageType.Click)
            {
                // 创建一个临时队列
                Queue<MessageEvent> tempQueue = new Queue<MessageEvent>();
                
                // 先将当前消息放入临时队列
                tempQueue.Enqueue(currentMessage);
                
                // 将原队列中的消息加入临时队列
                while (messageQueue.Count > 0)
                {
                    tempQueue.Enqueue(messageQueue.Dequeue());
                }
                
                // 用临时队列替换原队列
                messageQueue = tempQueue;
            }
            
            // 停止当前显示的协程
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }
            isDisplaying = false;
        }
        
        // 仅将Click类型消息加入队列，Auto类型消息直接处理
        if (messageEvent.Type == MessageType.Click)
        {
            messageQueue.Enqueue(messageEvent);
            
            // 如果队列超过最大容量，移除最旧的消息
            while (messageQueue.Count > maxMessages)
            {
                messageQueue.Dequeue();
            }
        }
        
        // 如果没有正在显示的消息，开始显示
        if (!isDisplaying)
        {
            // 对于Auto类型消息，如果不在队列中，直接显示
            if (messageEvent.Type == MessageType.Auto)
            {
                currentMessage = messageEvent;
                displayCoroutine = StartCoroutine(DisplayAutoMessageCoroutine(messageEvent));
            }
            else
            {
                displayCoroutine = StartCoroutine(DisplayMessagesCoroutine());
            }
        }
        else if (messageEvent.Type == MessageType.Auto)
        {
            // 如果有消息正在显示，但是新消息是Auto类型，停止当前显示并直接显示新消息
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }
            isDisplaying = false;
            currentMessage = messageEvent;
            displayCoroutine = StartCoroutine(DisplayAutoMessageCoroutine(messageEvent));
        }
    }
    
    private IEnumerator DisplayAutoMessageCoroutine(MessageEvent messageEvent)
    {
        isDisplaying = true;
        
        // 设置文本
        if (textComponent != null)
        {
            textComponent.text = messageEvent.Message;
            
            // 等待一帧以确保ContentSizeFitter更新了文本尺寸
            yield return null;
            
            // 根据文本尺寸调整背景图像大小
            UpdateBackgroundSize();
        }
        
        // 关闭按钮不可交互
        closeButton.interactable = false;
        
        // 淡入
        float startTime = Time.time;
        while (Time.time < startTime + fadeTime)
        {
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, (Time.time - startTime) / fadeTime);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        
        // 自动消失类型，显示一段时间后淡出
        yield return new WaitForSeconds(messageEvent.Duration);
        
        // 淡出
        startTime = Time.time;
        while (Time.time < startTime + fadeTime)
        {
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, (Time.time - startTime) / fadeTime);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        
        // 如果队列中还有消息，开始显示队列中的消息
        if (messageQueue.Count > 0)
        {
            yield return new WaitForSeconds(0.5f);
            displayCoroutine = StartCoroutine(DisplayMessagesCoroutine());
        }
        else
        {
            isDisplaying = false;
            displayCoroutine = null;
            currentMessage = null;
        }
    }
    
    private IEnumerator DisplayMessagesCoroutine()
    {
        isDisplaying = true;
        
        while (messageQueue.Count > 0)
        {
            currentMessage = messageQueue.Dequeue();
            
            // 设置文本
            if (textComponent != null)
            {
                textComponent.text = currentMessage.Message;
                
                // 等待一帧以确保ContentSizeFitter更新了文本尺寸
                yield return null;
                
                // 根据文本尺寸调整背景图像大小
                UpdateBackgroundSize();
            }
            
            // 根据消息类型设置关闭按钮的可交互性
            closeButton.interactable = true; // 点击消息总是可以点击关闭
            
            // 淡入
            float startTime = Time.time;
            while (Time.time < startTime + fadeTime)
            {
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, (Time.time - startTime) / fadeTime);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            
            // 点击消失类型，等待按钮被点击(按钮变为不可交互状态)
            yield return new WaitUntil(() => !closeButton.interactable);
        }
        
        isDisplaying = false;
        displayCoroutine = null;
        currentMessage = null;
    }
    
    // 关闭按钮点击事件
    private void OnCloseButtonClicked()
    {
        if (currentMessage != null && currentMessage.Type == MessageType.Click)
        {
            // 设置按钮为不可交互状态表示消息已被关闭
            closeButton.interactable = false;
            
            // 淡出当前消息
            StartCoroutine(FadeOutCurrent());
        }
    }
    
    private IEnumerator FadeOutCurrent()
    {
        // 淡出
        float startTime = Time.time;
        while (Time.time < startTime + fadeTime)
        {
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, (Time.time - startTime) / fadeTime);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
    
    private void UpdateBackgroundSize()
    {
        if (backgroundImage != null && textComponent != null)
        {
            // 获取文本的首选尺寸
            float textWidth = textComponent.preferredWidth;
            float textHeight = textComponent.preferredHeight;
            
            // 从MessageManager获取padding
            RectOffset padding = (MessageManager.instance != null) 
                ? MessageManager.instance.MessagePadding 
                : new RectOffset(10, 10, 5, 5); // 默认值，以防MessageManager不可用
            
            // 添加padding
            float backgroundWidth = textWidth + padding.left + padding.right;
            float backgroundHeight = textHeight + padding.top + padding.bottom;
            
            // 为点击类型的消息，确保背景足够大以容纳关闭按钮
            if (currentMessage != null && currentMessage.Type == MessageType.Click)
            {
                backgroundWidth = Mathf.Max(backgroundWidth, 100);  // 确保最小宽度
                backgroundHeight = Mathf.Max(backgroundHeight, 50); // 确保最小高度
            }
            
            // 更新背景图像尺寸
            RectTransform bgRectTransform = backgroundImage.rectTransform;
            bgRectTransform.sizeDelta = new Vector2(backgroundWidth, backgroundHeight);
        }
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
        currentMessage = null;
        
        // 重置目标跟踪
        targetTransform = null;
        
        // 重置透明度
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
        }
        
        // 设置按钮为不可交互
        if (closeButton != null)
        {
            closeButton.interactable = false;
        }
    }
}
