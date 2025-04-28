using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlantInfoUI : MonoBehaviour
{
    public static PlantInfoUI Instance { get; private set; }
    
    [Header("UI组件")]
    public GameObject infoPanel;
    public TextMeshProUGUI allInfoText; // 新增一个统一显示所有信息的文本组件
    public SequentialAnimator infoAnimator; // 添加对SequentialAnimator的引用

    // 添加当前选中的植物属性
    private Plant currentPlant;
    // 提供公共访问属性
    public Plant CurrentPlant => currentPlant;
    
    // 更新信息的计时器
    private float updateTimer = 0f;
    private const float updateInterval = 1f; // 每多少秒更新一次
    private const float timeThreshold = 120f; // 时间阈值，超过则显示"一万年"
        
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // 订阅交互事件
        PlayerPathfinding.OnPlantClicked += ShowPlantInfo;
        // 订阅植物铲除事件
        QuadTreeTester.OnPlantRemoved += OnPlantRemoved;
    }
    
    private void Update()
    {
        // 如果有当前植物并且信息面板处于显示状态，则每秒更新信息
        if (currentPlant != null && infoPanel.activeSelf)
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                UpdatePlantInfo();
            }
        }
    }
    
    private void OnDestroy()
    {
        // 取消订阅交互事件
        PlayerPathfinding.OnPlantClicked -= ShowPlantInfo;
        // 取消订阅植物铲除事件
        QuadTreeTester.OnPlantRemoved -= OnPlantRemoved;
    }
    
    // 当植物被铲除时调用
    private void OnPlantRemoved(Plant plant)
    {
        // 检查被铲除的植物是否是当前显示的植物
        if (currentPlant == plant)
        {
            // 隐藏所有图片
            if (infoAnimator != null)
            {
                infoAnimator.HideAllImages();
            }
            
            // 重置当前植物和隐藏面板
            currentPlant = null;
        }
    }
    
    // 显示植物信息
    public void ShowPlantInfo(Plant plant)
    {
        if (plant == null || infoPanel == null) return;
        
        // 显示信息面板
        infoPanel.SetActive(true);
        
        // 清空当前文本
        allInfoText.text = "";
        
        // 延迟0.1秒设置当前植物
        StartCoroutine(DelaySetCurrentPlant(plant));
        
        // 重置更新计时器
        updateTimer = 0f;
    }
    
    // 延迟设置当前植物并更新信息的协程
    private IEnumerator DelaySetCurrentPlant(Plant plant)
    {
        // 等待0.1秒
        yield return new WaitForSeconds(0.1f);
        
        // 设置当前植物
        currentPlant = plant;
        
        // 更新植物信息
        UpdatePlantInfo();
    }
    
    // 获取格式化的时间文本
    private string GetFormattedTimeText(float seconds)
    {
        if (seconds > timeThreshold)
        {
            return "一万年";
        }
        else
        {
            return $"{seconds:F0}秒";
        }
    }
    
    // 获取计时器剩余时间
    private string GetRemainingTimeText(float currentTimer, float interval)
    {
        float remainingTime = interval - currentTimer;
        if (remainingTime < 0) remainingTime = 0;
        
        if (interval > timeThreshold)
        {
            return "一万年";
        }
        else
        {
            return $"{remainingTime:F0}秒";
        }
    }
    
    // 更新植物信息
    private void UpdatePlantInfo()
    {
        if (currentPlant == null) return;
        
        // 创建一个StringBuilder来构建完整信息
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        // 处理植物名称
        string displayName = currentPlant.plantName;
        
        // 当currentStage=1时，解析并替换名称
        if (currentPlant.currentStage == 1)
        {
            string seedName = currentPlant.plantName.Trim();
            
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
        }
        
        // 如果植物已枯萎，在名称后添加"已枯萎"标记
        if (currentPlant.isWithered)
        {
            displayName += "已经枯萎了";
        }
        
        // 植物名称（加粗）
        sb.AppendLine($"<b>植物名字</b>是{displayName}噼！\n");
        
        // 获取生长计时器当前值和间隔
        float growthInterval = 60f / currentPlant.growthRate; // 转换为秒
        string growthTimeText = GetRemainingTimeText(currentPlant.growthTimer, growthInterval);
        
        // 生长冷却（加粗）
        sb.AppendLine($"<b>生长冷却</b>是{growthTimeText}噼！\n");
        
        // 获取枯萎计时器当前值和间隔
        float witherInterval = 60f / currentPlant.witherRate; // 转换为秒
        string witherTimeText = GetRemainingTimeText(currentPlant.witherTimer, witherInterval);
        
        // 枯萎冷却（加粗）
        sb.AppendLine($"<b>枯萎冷却</b>是{witherTimeText}噼！");
        
        // 设置完整的信息文本
        allInfoText.text = sb.ToString();
    }
}
