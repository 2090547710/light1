using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

public class MessageManager : MonoBehaviour
{
    public static MessageManager instance;

    [SerializeField] private GameObject messageDisplayPrefab;
    [SerializeField] private Canvas messageCanvas;
    [SerializeField] private Vector3 positionOffset = new Vector3(0, 80f, 0);
    [SerializeField] private int initialPoolSize = 5;
    [SerializeField] private int maxPoolSize = 20;
    [SerializeField] private int paddingLeft = 10;
    [SerializeField] private int paddingRight = 10;
    [SerializeField] private int paddingTop = 5;
    [SerializeField] private int paddingBottom = 5;
    
    private RectOffset messagePadding;
    // 提供公共访问器获取padding
    public RectOffset MessagePadding => messagePadding;

    private List<MessageDisplay> activeMessageDisplays = new List<MessageDisplay>();
    private Queue<GameObject> messageObjectPool = new Queue<GameObject>();
    // 添加一个Dictionary来跟踪每个transform对应的消息显示组件
    private Dictionary<Transform, MessageDisplay> transformToDisplayMap = new Dictionary<Transform, MessageDisplay>();

    private void Awake()
    {
        instance = this;
        
        // 在Awake中初始化RectOffset
        messagePadding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);

        if (messageCanvas == null)
        {
            // 创建Canvas如果未提供
            Debug.LogError("MessageCanvas未设置");
        }
        
        // 初始化对象池
        InitializeObjectPool();
    }

    private void InitializeObjectPool()
    {
        if (messageDisplayPrefab == null)
        {
            Debug.LogError("消息显示Prefab未设置");
            return;
        }
        
        // 预先创建对象
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject messageObj = Instantiate(messageDisplayPrefab, messageCanvas.transform);
            messageObj.SetActive(false);
            messageObjectPool.Enqueue(messageObj);
        }
    }

    private void OnEnable()
    {
        MessageEventSystem.OnMessageReceived += HandleGlobalMessage;
    }

    private void OnDisable()
    {
        MessageEventSystem.OnMessageReceived -= HandleGlobalMessage;
    }

    public void RegisterMessageDisplay(MessageDisplay display)
    {
        if (!activeMessageDisplays.Contains(display))
        {
            activeMessageDisplays.Add(display);
            
            // 如果有目标transform，将其添加到映射中
            if (display.targetTransform != null && !transformToDisplayMap.ContainsKey(display.targetTransform))
            {
                transformToDisplayMap.Add(display.targetTransform, display);
            }

            // 从对象池中移除该对象
            GameObject displayObj = display.gameObject;
            var tempQueue = new Queue<GameObject>();
            while (messageObjectPool.Count > 0)
            {
                var obj = messageObjectPool.Dequeue();
                if (obj != displayObj)
                {
                    tempQueue.Enqueue(obj);
                }
            }
            messageObjectPool = tempQueue;
        }
    }

    public void UnregisterMessageDisplay(MessageDisplay display)
    {
        if (display == null) return;

        if (activeMessageDisplays.Contains(display))
        {
            activeMessageDisplays.Remove(display);
        }

        // 从映射中移除所有指向这个显示器的条目
        var keysToRemove = transformToDisplayMap.Where(kvp => kvp.Value == display)
                                              .Select(kvp => kvp.Key)
                                              .ToList();
        foreach (var key in keysToRemove)
        {
            transformToDisplayMap.Remove(key);
        }

        // 将对象归还到池中
        GameObject displayObj = display.gameObject;
        if (messageObjectPool.Count < maxPoolSize)
        {
            display.ResetState();
            displayObj.SetActive(false);
            messageObjectPool.Enqueue(displayObj);
        }
        else
        {
            Destroy(displayObj);
        }
    }

    public void SendMessage(string message, Transform targetTransform = null, MessageType type = MessageType.Auto, float duration = 3f)
    {
        if (string.IsNullOrEmpty(message)) return;

        // 如果目标transform已经有关联的消息显示器，先检查它是否真的属于这个transform
        if (targetTransform != null && transformToDisplayMap.TryGetValue(targetTransform, out MessageDisplay existingDisplay))
        {
            // 检查这个显示器的目标transform是否真的匹配
            if (existingDisplay != null && 
                existingDisplay.gameObject.activeInHierarchy && 
                existingDisplay.targetTransform == targetTransform)
            {
                existingDisplay.AddMessage(message, type, duration);
                return;
            }
            else
            {
                // 如果显示器无效或目标不匹配，从映射中移除
                transformToDisplayMap.Remove(targetTransform);
            }
        }

        // 创建新的消息对象
        GameObject messageObj = GetMessageObjectFromPool(targetTransform);
        if (messageObj == null) return;

        MessageDisplay messageDisplay = messageObj.GetComponent<MessageDisplay>();
        if (messageDisplay == null) return;

        // 设置跟踪目标
        if (targetTransform != null)
        {
            // 确保在设置新目标之前，从任何现有的映射中移除这个显示器
            foreach (var kvp in transformToDisplayMap.ToList())
            {
                if (kvp.Value == messageDisplay)
                {
                    transformToDisplayMap.Remove(kvp.Key);
                }
            }

            messageDisplay.SetTargetTransform(targetTransform, positionOffset);
            transformToDisplayMap[targetTransform] = messageDisplay;
        }

        messageDisplay.Initialize(message, messageCanvas.transform, type, duration);
    }

    private GameObject GetMessageObjectFromPool(Transform targetTransform)
    {
        GameObject messageObj = null;
        
        // 尝试从池中获取对象
        if (messageObjectPool.Count > 0)
        {
            messageObj = messageObjectPool.Dequeue();
        }
        else
        {
            // 池为空，创建新对象
            messageObj = Instantiate(messageDisplayPrefab, messageCanvas.transform);
        }
        
        if (messageObj == null) return null;
        
        // 激活对象并设置初始位置
        messageObj.SetActive(true);
        
        // 设置初始位置，之后会在Update中更新
        if (targetTransform != null)
        {
            RectTransform rectTransform = messageObj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // 直接使用世界坐标加上偏移量
                Vector3 worldPos = targetTransform.position + positionOffset;
                
                // 设置UI元素位置
                rectTransform.position = worldPos;
            }
        }

        return messageObj;
    }

    private void HandleGlobalMessage(MessageEvent messageEvent)
    {
        // 全局消息显示在默认位置
        SendMessage(messageEvent.Message, null, messageEvent.Type, messageEvent.Duration);
    }
}

