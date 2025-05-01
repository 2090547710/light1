using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlantDatabaseUI : MonoBehaviour
{
    // UI引用
    public ScrollRect plantScrollRect;
    public GameObject plantItemPrefab;
    
    // PlantManager引用
    private PlantManager plantManager;
    
    // 滚动控制参数
    [Header("滚动控制")]
    [Range(0.01f, 5f)]
    public float scrollStepDistance = 0.1f; // 每次滚动的距离（归一化值）
    public float scrollAnimationTime = 0.25f; // 滚动动画时间

    private bool isScrolling = false; // 是否正在滚动中

    // 控制按钮
    [Header("控制按钮")]
    public Button upButton;
    public Button downButton;

    private void Start()
    {
        plantManager = PlantManager.Instance;
        if (plantManager == null)
        {
            Debug.LogError("无法找到PlantManager实例");
            return;
        }
        
        // 禁用鼠标拖动滚动
        plantScrollRect.vertical = false; // 禁用垂直拖动
        plantScrollRect.horizontal = false;
        
        // 订阅植物数据库加载事件
        plantManager.OnPlantDatabaseLoaded += OnPlantDatabaseUpdated;
        plantManager.OnSeedMappingsLoaded += OnPlantDatabaseUpdated;
        
        // 如果数据库已经加载完成，立即生成列表
        if (plantManager.GetPlantDatabase().Count > 0)
        {
            GeneratePlantList();
        }

        // 初始化按钮状态
        UpdateButtonStates();
        
        // 添加滚动监听
        plantScrollRect.onValueChanged.AddListener(OnScrollPositionChanged);
    }
    
    // 当植物数据库更新时调用
    private void OnPlantDatabaseUpdated()
    {
        GeneratePlantList();
    }

    private void OnDestroy()
    {
        // 取消订阅以防止内存泄漏
        if (plantManager != null)
        {
            plantManager.OnPlantDatabaseLoaded -= OnPlantDatabaseUpdated;
            plantManager.OnSeedMappingsLoaded -= OnPlantDatabaseUpdated;
        }
    }
    
 
    // 生成植物列表UI
    public void GeneratePlantList()
    {
        if (plantScrollRect == null || plantItemPrefab == null)
        {
            Debug.LogError("植物滚动视图或预制体引用未设置");
            return;
        }
        
        // 获取植物数据库
        var plantDatabase = plantManager.GetPlantDatabase();
        if (plantDatabase == null || plantDatabase.Count == 0)
        {
            Debug.LogWarning("植物数据库为空");
            return;
        }
        
        // 清除现有内容
        Transform contentTransform = plantScrollRect.content;
        foreach (Transform child in contentTransform)
        {
            Destroy(child.gameObject);
        }
        
        // 遍历植物数据库，为每个植物生成列表项
        foreach (var entry in plantDatabase)
        {
            int plantId = entry.Key;
            Plant.PlantStage plantStage = entry.Value;
            
            // 实例化预制体
            GameObject plantItem = Instantiate(plantItemPrefab, contentTransform);
            
            // 获取Text组件并设置为植物名称
            TextMeshProUGUI textComponent = plantItem.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = plantStage.plantName;
            }
            else
            {
                Debug.LogWarning($"植物项预制体缺少TextMeshProUGUI组件: {plantId}");
            }
            
            // 存储植物ID，用于可能的点击事件
            PlantItemData itemData = plantItem.GetComponent<PlantItemData>() ?? plantItem.AddComponent<PlantItemData>();
            itemData.plantId = plantId;
        }
    }
    
    // 手动刷新植物列表
    public void RefreshPlantList()
    {
        GeneratePlantList();
    }

    /// <summary>
    /// 向下滚动一定距离
    /// </summary>
    public void ScrollDown()
    {
        if (plantScrollRect == null || isScrolling)
            return;
        
        StartCoroutine(SmoothScrollDown());
    }

    /// <summary>
    /// 向上滚动一定距离
    /// </summary>
    public void ScrollUp()
    {
        if (plantScrollRect == null || isScrolling)
            return;
        
        StartCoroutine(SmoothScrollUp());
    }

    // 平滑向下滚动的协程
    private IEnumerator SmoothScrollDown()
    {
        isScrolling = true;
        
        float startPos = plantScrollRect.verticalNormalizedPosition;
        float targetPos = Mathf.Max(0f, startPos - scrollStepDistance); // 确保不小于0
        float elapsedTime = 0f;
        
        while (elapsedTime < scrollAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / scrollAnimationTime;
            t = Mathf.SmoothStep(0f, 1f, t); // 使用平滑插值
            
            plantScrollRect.verticalNormalizedPosition = Mathf.Lerp(startPos, targetPos, t);
            yield return null;
        }
        
        plantScrollRect.verticalNormalizedPosition = targetPos; // 确保到达目标位置
        isScrolling = false;
        UpdateButtonStates();
        yield break;
    }

    // 平滑向上滚动的协程
    private IEnumerator SmoothScrollUp()
    {
        isScrolling = true;
        
        float startPos = plantScrollRect.verticalNormalizedPosition;
        float targetPos = Mathf.Min(1f, startPos + scrollStepDistance); // 确保不大于1
        float elapsedTime = 0f;
        
        while (elapsedTime < scrollAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / scrollAnimationTime;
            t = Mathf.SmoothStep(0f, 1f, t); // 使用平滑插值
            
            plantScrollRect.verticalNormalizedPosition = Mathf.Lerp(startPos, targetPos, t);
            yield return null;
        }
        
        plantScrollRect.verticalNormalizedPosition = targetPos; // 确保到达目标位置
        isScrolling = false;
        UpdateButtonStates();
        yield break;
    }

    // 滚动到指定位置
    public void ScrollToPosition(float normalizedPosition)
    {
        if (plantScrollRect == null || isScrolling)
            return;
        
        StartCoroutine(SmoothScrollToPosition(normalizedPosition));
    }

    // 平滑滚动到指定位置的协程
    private IEnumerator SmoothScrollToPosition(float normalizedPosition)
    {
        isScrolling = true;
        
        float startPos = plantScrollRect.verticalNormalizedPosition;
        float targetPos = Mathf.Clamp01(normalizedPosition); // 确保在0-1范围内
        float elapsedTime = 0f;
        
        while (elapsedTime < scrollAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / scrollAnimationTime;
            t = Mathf.SmoothStep(0f, 1f, t); // 使用平滑插值
            
            plantScrollRect.verticalNormalizedPosition = Mathf.Lerp(startPos, targetPos, t);
            yield return null;
        }
        
        plantScrollRect.verticalNormalizedPosition = targetPos; // 确保到达目标位置
        isScrolling = false;
        UpdateButtonStates();
        yield break;
    }

    // 添加新方法
    private void OnScrollPositionChanged(Vector2 position)
    {
        UpdateButtonStates();
    }

    // 更新按钮状态的方法
    private void UpdateButtonStates()
    {
        if (upButton != null)
        {
            // 当接近顶部时禁用上按钮游戏对象
            upButton.gameObject.SetActive(plantScrollRect.verticalNormalizedPosition < 0.99f);
        }
        
        if (downButton != null)
        {
            // 当接近底部时禁用下按钮游戏对象
            downButton.gameObject.SetActive(plantScrollRect.verticalNormalizedPosition > 0.01f);
        }
    }

    private void Update()
    {
        // 检测鼠标是否在viewport上以及滚轮输入
        if (IsMouseOverViewport() && !isScrolling)
        {
            float scrollDelta = Input.mouseScrollDelta.y;
            if (scrollDelta != 0)
            {
                // 向上滚动滚轮时，向下滚动内容
                float targetPos = plantScrollRect.verticalNormalizedPosition + scrollDelta * scrollStepDistance;
                targetPos = Mathf.Clamp01(targetPos);
                ScrollToPosition(targetPos);
            }
        }
    }

    // 检测鼠标是否在viewport上
    private bool IsMouseOverViewport()
    {
        if (plantScrollRect == null || plantScrollRect.viewport == null)
            return false;
        
        RectTransform viewportRect = plantScrollRect.viewport as RectTransform;
        Vector2 localMousePosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewportRect, 
            Input.mousePosition, 
            null, 
            out localMousePosition);
        
        return viewportRect.rect.Contains(localMousePosition);
    }
}

// 用于存储植物项数据的组件
public class PlantItemData : MonoBehaviour
{
    public int plantId;
}
