using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class PlantDatabaseUI : MonoBehaviour
{
    // UI引用
    public ScrollRect plantScrollRect;
    public GameObject plantItemPrefab;
    
    // 添加新预制体引用
    [Header("预制体引用")]
    public GameObject plantInfoItemPrefab; // 用于生成前置植物和更新植物信息项
    
    // 添加面板引用
    [Header("面板引用")]
    public Transform prerequisitePanelContent; // 前置植物面板内容区域
    public Transform updatePanelContent; // 更新植物面板内容区域
    
    // 添加颜色配置
    [Header("颜色配置")]
    public Color normalItemColor = Color.white; // 普通项目的颜色
    public Color selectedItemColor = new Color(0.8f, 0.8f, 0.8f); // 选中项目的颜色
    
    // PlantManager引用
    private PlantManager plantManager;
    
    // 添加Fire引用
    public Fire firePrefab; // 在Inspector中指定火的预制体
    
    // 添加列表存储生成的植物项
    private List<GameObject> generatedPlantItems = new List<GameObject>();
    
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

    // 在PlantDatabaseUI类中添加UI引用
    [Header("植物详情UI")]
    public TextMeshProUGUI plantNameText; // 植物名称文本
    public TextMeshProUGUI plantDescriptionText; // 植物介绍文本
    public Image plantPreviewImage; // 植物预览图

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
        
        // 清空列表
        generatedPlantItems.Clear();
        
        // 首先添加Fire特殊项目（如果有Fire预制体）
        if (firePrefab != null && firePrefab.growthStages.Count > 1)
        {
            // 实例化预制体
            GameObject fireItem = Instantiate(plantItemPrefab, contentTransform);
            
            // 添加到列表
            generatedPlantItems.Add(fireItem);
            
            // 获取Text组件并设置为"火"
            TextMeshProUGUI textComponent = fireItem.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = "火";
            }
            
            // 存储特殊处理的火数据
            PlantItemData fireItemData = fireItem.GetComponent<PlantItemData>() ?? fireItem.AddComponent<PlantItemData>();
            fireItemData.plantId = -1; // 特殊ID标记为火
            fireItemData.isFireItem = true; // 设置为火项目
            
            // 查找并保存Image组件引用
            fireItemData.itemImage = fireItem.GetComponentInChildren<Image>();
            
            // 如果Fire有第二个生长阶段，使用它作为plantStage
            if (firePrefab.growthStages.Count > 1)
            {
                fireItemData.plantStage = firePrefab.growthStages[1];
            }
            
            fireItemData.RefreshData();
            
            // 添加点击事件
            Button button = fireItem.GetComponent<Button>();
            if (button == null)
            {
                button = fireItem.AddComponent<Button>();
            }
            
            button.onClick.AddListener(() => OnPlantItemClicked(-1)); // 使用-1作为火的特殊ID
        }
        
        // 然后添加正常的植物数据库项目
        foreach (var entry in plantDatabase)
        {
            int plantId = entry.Key;
            Plant.PlantStage plantStage = entry.Value;
            
            // 实例化预制体
            GameObject plantItem = Instantiate(plantItemPrefab, contentTransform);
            
            // 添加到列表
            generatedPlantItems.Add(plantItem);
            
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
            itemData.plantStage = plantStage;
            
            // 查找并保存Image组件引用
            itemData.itemImage = plantItem.GetComponentInChildren<Image>();
            
            itemData.RefreshData();
            
            // 添加点击事件
            Button button = plantItem.GetComponent<Button>();
            if (button == null)
            {
                button = plantItem.AddComponent<Button>();
            }
            
            // 存储itemData的引用，以便在点击事件中使用
            int capturedPlantId = plantId;
            button.onClick.AddListener(() => OnPlantItemClicked(capturedPlantId));
        }
    }
    
    // 获取生成的所有植物项
    public List<GameObject> GetGeneratedPlantItems()
    {
        return generatedPlantItems;
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
            // 控制上按钮图片透明度
            Image upImage = upButton.GetComponent<Image>();
            if (upImage != null)
            {
                // 当滚动到顶部时（normalizedPosition接近1）透明度减小
                float alpha = plantScrollRect.verticalNormalizedPosition >= 0.99f ? 0.5f : 1f;
                Color color = upImage.color;
                color.a = alpha;
                upImage.color = color;
            }
        }
        
        if (downButton != null)
        {
            // 控制下按钮图片透明度
            Image downImage = downButton.GetComponent<Image>();
            if (downImage != null)
            {
                // 当滚动到底部时（normalizedPosition接近0）透明度减小
                float alpha = plantScrollRect.verticalNormalizedPosition <= 0.01f ? 0.5f : 1f;
                Color color = downImage.color;
                color.a = alpha;
                downImage.color = color;
            }
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
    
    // 清空面板内容
    private void ClearPanelContent(Transform panel)
    {
        if (panel == null) return;
        
        foreach (Transform child in panel)
        {
            Destroy(child.gameObject);
        }
    }
    
    // 检查植物是否在活跃列表中
    private bool IsPlantActive(string plantName)
    {
        foreach (Plant plant in plantManager.activePlants)
        {
            if (plant.plantName == plantName && !plant.isWithered)
            {
                return true;
            }
        }
        return false;
    }
    
    // 检查种子是否在活跃列表中
    private bool IsSeedActive(string seedName)
    {
        foreach (Plant plant in plantManager.activePlants)
        {
            if (plant.currentStage == 0 && plant.plantName == seedName && !plant.isWithered)
            {
                return true;
            }
        }
        return false;
    }

    // 处理植物项点击事件
    private void OnPlantItemClicked(int plantId)
    {
        // 使用生成的植物列表查找对应的PlantItemData
        PlantItemData itemData = null;
        
        // 先将所有项的颜色重置为正常颜色
        foreach (GameObject plantItem in generatedPlantItems)
        {
            PlantItemData plantItemData = plantItem.GetComponent<PlantItemData>();
            if (plantItemData != null && plantItemData.itemImage != null)
            {
                // 设置为正常颜色
                plantItemData.itemImage.color = normalItemColor;
            }
        }
        
        // 遍历生成的植物项列表，更高效地查找对应的数据
        foreach (GameObject plantItem in generatedPlantItems)
        {
            PlantItemData plantItemData = plantItem.GetComponent<PlantItemData>();
            if (plantItemData != null && plantItemData.plantId == plantId)
            {
                itemData = plantItemData;
                
                // 将被点击项的颜色变为选中颜色
                if (plantItemData.itemImage != null)
                {
                    plantItemData.itemImage.color = selectedItemColor;
                }
                
                break;
            }
        }
        
        if (itemData != null)
        {
            // 在点击时刷新数据，确保数据是最新的
            itemData.RefreshData();
            
            // 更新UI显示
            string plantTypeName = itemData.isFireItem ? "火" : itemData.plantStage.plantName;
            
            // 更新植物名称
            if (plantNameText != null)
            {
                plantNameText.text = plantTypeName;
            }
            
            // 更新植物介绍
            if (plantDescriptionText != null)
            {
                // 使用植物阶段中的描述
                plantDescriptionText.text = itemData.plantStage.plantDescription;
            }
            
            // 更新预览图
            if (plantPreviewImage != null)
            {
                // 从植物阶段的预览图路径加载图片
                if (!string.IsNullOrEmpty(itemData.plantStage.previewImagePath))
                {
                    Sprite previewSprite = Resources.Load<Sprite>(itemData.plantStage.previewImagePath);
                    if (previewSprite != null)
                    {
                        plantPreviewImage.sprite = previewSprite;
                        plantPreviewImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        plantPreviewImage.gameObject.SetActive(false);
                    }
                }
                else
                {
                    plantPreviewImage.gameObject.SetActive(false);
                }
            }
            
            // 更新前置植物面板
            UpdatePrerequisitePanel(itemData);
            
            // 更新更新植物面板
            UpdateUpdatePanel(itemData);
        }
    }
    
    // 更新前置植物面板
    private void UpdatePrerequisitePanel(PlantItemData itemData)
    {
        if (prerequisitePanelContent == null || plantInfoItemPrefab == null) return;
        
        // 清空现有内容
        ClearPanelContent(prerequisitePanelContent);
        
        // 如果是火项目，特殊处理
        if (itemData.isFireItem)
        {
            // 为火种子添加一个特殊项
            GameObject itemObj = Instantiate(plantInfoItemPrefab, prerequisitePanelContent);
            TextMeshProUGUI textComponent = itemObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = "火种子";
                // 检查火种子是否存在于活跃植物中
                if (IsSeedActive("火种子"))
                {
                    textComponent.fontStyle = FontStyles.Bold;
                }
            }
            return;
        }
        
        if (itemData.plantStage.stageType == StageType.Flower)
        {
            // 花类型：显示种子信息
            foreach (var pair in itemData.seedNameProbabilities)
            {
                string seedName = pair.Key;
                float probability = pair.Value;
                
                GameObject itemObj = Instantiate(plantInfoItemPrefab, prerequisitePanelContent);
                TextMeshProUGUI textComponent = itemObj.GetComponentInChildren<TextMeshProUGUI>();
                
                if (textComponent != null)
                {
                    // 获取种子中文名称
                    string chineseSeedName = itemData.GetChineseSeedName(seedName);
                    textComponent.text = $"{chineseSeedName}\n(概率: {probability:P0})";
                    
                    // 检查是否存在于活跃植物中，如果是则加粗
                    if (IsSeedActive(seedName))
                    {
                        textComponent.fontStyle = FontStyles.Bold;
                    }
                }
            }
        }
        else if (itemData.plantStage.stageType == StageType.Fruit)
        {
            // 果实类型：显示前置植物和数量要求
            if (itemData.plantStage.prerequisitePlantIDs != null && itemData.plantStage.prerequisitePlantIDs.Count > 0)
            {
                for (int i = 0; i < itemData.plantStage.prerequisitePlantIDs.Count; i++)
                {
                    int prereqId = itemData.plantStage.prerequisitePlantIDs[i];
                    float requiredCount = 1.0f;
                    
                    if (itemData.plantStage.prerequisiteWeights != null && i < itemData.plantStage.prerequisiteWeights.Count)
                    {
                        requiredCount = itemData.plantStage.prerequisiteWeights[i];
                    }
                    
                    string plantName = "未知";
                    if (plantManager.GetPlantDatabase().TryGetValue(prereqId, out Plant.PlantStage prereqStage))
                    {
                        plantName = prereqStage.plantName;
                    }
                    
                    // 获取当前活跃数量
                    int currentCount = plantManager.GetPlantCount(prereqId);
                    
                    // 根据需要的数量，生成对应数量的预制体
                    for (int j = 0; j < (int)requiredCount; j++)
                    {
                        GameObject itemObj = Instantiate(plantInfoItemPrefab, prerequisitePanelContent);
                        TextMeshProUGUI textComponent = itemObj.GetComponentInChildren<TextMeshProUGUI>();
                        
                        if (textComponent != null)
                        {
                            textComponent.text = plantName;
                            
                            // 如果当前数量满足要求，加粗文本
                            if (j < currentCount)
                            {
                                textComponent.fontStyle = FontStyles.Bold;
                            }
                        }
                    }
                }
            }
        }
    }
    
    // 更新更新植物面板
    private void UpdateUpdatePanel(PlantItemData itemData)
    {
        if (updatePanelContent == null || plantInfoItemPrefab == null) return;
        
        // 清空现有内容
        ClearPanelContent(updatePanelContent);
        
        // 如果是火项目或果实类型，跳过
        if (itemData.isFireItem || itemData.plantStage.stageType == StageType.Fruit)
        {
            return;
        }
        
        // 显示可更新植物信息
        foreach (var pair in itemData.updatePlantAvailability)
        {
            int updatePlantId = pair.Key;
            bool isAvailable = pair.Value;
            
            // 获取植物名称
            string plantName = "未知";
            if (plantManager.GetPlantDatabase().TryGetValue(updatePlantId, out Plant.PlantStage updateStage))
            {
                plantName = updateStage.plantName;
            }
            
            // 获取实时概率
            float probability = 0f;
            if (itemData.updatePlantProbabilities.TryGetValue(updatePlantId, out float prob))
            {
                probability = prob;
            }
            
            GameObject itemObj = Instantiate(plantInfoItemPrefab, updatePanelContent);
            TextMeshProUGUI textComponent = itemObj.GetComponentInChildren<TextMeshProUGUI>();
            
            if (textComponent != null)
            {
                // 显示植物名称和概率
                textComponent.text = $"{plantName}\n(概率: {probability:P0})";
                
                // 如果可更新，加粗文本
                if (isAvailable)
                {
                    textComponent.fontStyle = FontStyles.Bold;
                }
            }
        }
    }
}

// 用于存储植物项数据的组件
public class PlantItemData : MonoBehaviour
{
    public int plantId;
    public Plant.PlantStage plantStage; // 存储对应的PlantStage
    public bool isFireItem = false; // 标记是否为火项目
    
    // 新增：保存植物项的Image组件引用
    public Image itemImage;
    
    // 存储前置植物ID和对应活跃数量
    public Dictionary<int, int> prerequisitePlantCounts = new Dictionary<int, int>();
    
    // 存储更新植物ID和是否可更新状态
    public Dictionary<int, bool> updatePlantAvailability = new Dictionary<int, bool>();
    
    // 存储更新植物ID和实时概率
    public Dictionary<int, float> updatePlantProbabilities = new Dictionary<int, float>();
    
    // 新增：存储种子名称和概率
    public Dictionary<string, float> seedNameProbabilities = new Dictionary<string, float>();

    // 在Awake或Start中初始化数据
    private void Start()
    {

    }
    
    // 在销毁时取消订阅事件
    private void OnDestroy()
    {

    }
    
    // 刷新所有字典数据
    public void RefreshData()
    {
        // 如果是火项目，特殊处理
        if (isFireItem)
        {
            // 清空所有字典，火不需要这些数据
            prerequisitePlantCounts.Clear();
            updatePlantAvailability.Clear();
            updatePlantProbabilities.Clear();
            
            // 对于火，种子概率是100%
            seedNameProbabilities.Clear();
            seedNameProbabilities["火种子"] = 1.0f;
            return;
        }
        
        // 正常植物的处理逻辑
        var plantDatabase = PlantManager.Instance.GetPlantDatabase();
        if (plantDatabase.TryGetValue(plantId, out Plant.PlantStage stage))
        {
            plantStage = stage;
            
            // 更新前置植物ID和对应活跃数量字典
            prerequisitePlantCounts.Clear();
            if (plantStage.prerequisitePlantIDs != null)
            {
                for (int i = 0; i < plantStage.prerequisitePlantIDs.Count; i++)
                {
                    int prereqId = plantStage.prerequisitePlantIDs[i];
                    int count = PlantManager.Instance.GetPlantCount(prereqId);
                    prerequisitePlantCounts[prereqId] = count;
                }
            }
            
            // 更新更新植物ID和是否可更新状态字典
            updatePlantAvailability.Clear();
            var updatablePlants = PlantManager.Instance.GetUpdatablePlants();
            if (plantStage.updatePlantIDs != null)
            {
                for (int i = 0; i < plantStage.updatePlantIDs.Count; i++)
                {
                    int updateId = plantStage.updatePlantIDs[i];
                    bool isUpdatable = updatablePlants.Contains(updateId);
                    updatePlantAvailability[updateId] = isUpdatable;
                }
            }
            
            // 计算并更新更新植物ID和实时概率字典
            updatePlantProbabilities.Clear();
            if (plantStage.updatePlantIDs != null && plantStage.updateWeights != null)
            {
                for (int i = 0; i < plantStage.updatePlantIDs.Count; i++)
                {
                    int updateId = plantStage.updatePlantIDs[i];
                    float weight = i < plantStage.updateWeights.Count ? plantStage.updateWeights[i] : 1.0f;
                    bool isUpdatable = updatablePlants.Contains(updateId);
                    
                    // 计算实时概率：如果在updatablePlants中则使用权重，否则为0
                    float probability = isUpdatable ? weight : 0f;
                    updatePlantProbabilities[updateId] = probability;
                }
                
                // 归一化概率（如果所有概率都不为0）
                float totalProbability = updatePlantProbabilities.Values.Sum();
                if (totalProbability > 0)
                {
                    foreach (var key in updatePlantProbabilities.Keys.ToList())
                    {
                        updatePlantProbabilities[key] /= totalProbability;
                    }
                }
            }
            
            // 新增：如果是花类型，则获取对应的种子名称和概率
            seedNameProbabilities.Clear();
            if (plantStage.stageType == StageType.Flower)
            {
                // 查找所有可能成长为当前花的种子映射
                foreach (var mapping in PlantManager.Instance.seedMappings)
                {
                    int targetPlantIndex = mapping.targetPlantIdList.IndexOf(plantId);
                    if (targetPlantIndex >= 0)
                    {
                        // 获取原始种子名称
                        string seedName = mapping.size.ToString() + mapping.growthRate.ToString();
                        
                        // 计算该种子变成当前花的概率
                        float probabilityToThisFlower = 0f;
                        
                        // 获取种子的总权重
                        float totalWeightForThisSeed = 0f;
                        for (int i = 0; i < mapping.targetPlantIdList.Count; i++)
                        {
                            if (i < mapping.weightList.Count)
                            {
                                totalWeightForThisSeed += mapping.weightList[i];
                            }
                            else
                            {
                                totalWeightForThisSeed += 1.0f; // 默认权重
                            }
                        }
                        
                        // 获取当前花在该种子映射中的权重
                        float currentFlowerWeight = targetPlantIndex < mapping.weightList.Count ? 
                                                   mapping.weightList[targetPlantIndex] : 1.0f;
                        
                        // 计算概率 = 当前花权重 / 总权重
                        if (totalWeightForThisSeed > 0)
                        {
                            probabilityToThisFlower = currentFlowerWeight / totalWeightForThisSeed;
                        }
                        
                        // 添加到字典，使用英文种子名称
                        seedNameProbabilities[seedName] = probabilityToThisFlower;
                    }
                }
            }
        }
    }
    
    // 将英文种子名称转换为中文显示
    public string GetChineseSeedName(string seedName)
    {
        string displayName = seedName; // 默认值
        
        // 遍历所有尺寸枚举值，判断种子名字是否以该字符串开头
        foreach (string sizeStr in System.Enum.GetNames(typeof(SizeLevel)))
        {
            if (seedName.StartsWith(sizeStr, System.StringComparison.OrdinalIgnoreCase))
            {
                // 获取种子名称中除尺寸外的部分，作为生长速度字符串
                string growthRatePart = seedName.Substring(sizeStr.Length);
                
                // 尝试解析生长速度枚举（忽略大小写）
                if (System.Enum.TryParse(growthRatePart, true, out GrowthRateLevel growthRate) &&
                    System.Enum.TryParse(sizeStr, true, out SizeLevel sizeLevel))
                {
                    // 根据解析出的枚举值转换为中文显示
                    string sizeLevel_CN = "";
                    switch (sizeLevel)
                    {
                        case SizeLevel.Small:
                            sizeLevel_CN = "小型";
                            break;
                        case SizeLevel.Medium:
                            sizeLevel_CN = "中型";
                            break;
                        case SizeLevel.Large:
                            sizeLevel_CN = "大型";
                            break;
                    }
                    
                    string growthRate_CN = "";
                    switch (growthRate)
                    {
                        case GrowthRateLevel.Slow:
                            growthRate_CN = "缓慢";
                            break;
                        case GrowthRateLevel.Medium:
                            growthRate_CN = "普通";
                            break;
                        case GrowthRateLevel.Fast:
                            growthRate_CN = "野蛮";
                            break;
                    }
                    
                    // 组合成最终名称
                    displayName = $"{sizeLevel_CN}{growthRate_CN}种子";
                    break;
                }
            }
        }
        
        return displayName;
    }
}
