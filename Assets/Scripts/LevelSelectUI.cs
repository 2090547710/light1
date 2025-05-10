using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;

public class LevelSelectUI : MonoBehaviour
{
    [System.Serializable]
    public struct LevelInfo
    {
        public int levelID;
        public string levelName;
        public string plantDatabasePath;
        public string seedMappingPath;
        public string saveFilePath;
        public string plantDataSavePath;
        public int countdownDuration;
    }
    
    [System.Serializable]
    public class LevelSaveData
    {
        public int levelID;
        public string levelName;
        public string saveFilePath;
        public string plantDataSavePath;
        public int currentTime;
        public bool isCompleted;
    }
    
    [System.Serializable]
    public class SaveData
    {
        public List<LevelSaveData> levelSaveDataList = new List<LevelSaveData>();
        public int lastPlayedLevelID = -1; // 存储最后一次游玩的关卡ID
    }
    
    // 添加当前关卡ID和名称的属性

    public int currentLevelID = -1;
    
    public string currentLevelName = string.Empty;
    
    // 单例实例，方便其他脚本访问
    public static LevelSelectUI Instance;
    
    [Tooltip("关卡信息列表")]
    public List<LevelInfo> levels = new List<LevelInfo>();
       
    [Tooltip("倒计时计时器引用")]
    public CountdownTimer countdownTimer;
    
    private SaveData saveData;
    private string saveDataPath;
    
    public static event Action OnLevelLoaded;
    
    [Tooltip("显示当前关卡名称的Text组件")]
    public TextMeshProUGUI currentLevelNameText;
    
    private void Awake()
    {

        Instance = this;

    }
    
    private void Start()
    {
        // 设置存储路径
        saveDataPath = Path.Combine(Application.persistentDataPath, "levelsavedata.json");
        
        // 加载或创建SaveData
        LoadSaveData();
        
        // 检查并警告重复的levelID
        CheckDuplicateLevelIDs();
                
        // 添加倒计时结束事件监听
        if (countdownTimer != null)
        {
            countdownTimer.onCountdownFinished.AddListener(OnLevelFailed);
        }
        
        // 初始化关卡名称显示
        UpdateLevelNameText();
        
    }
    
    private void LoadSaveData()
    {
        saveData = new SaveData();
        
        if (File.Exists(saveDataPath))
        {
            string json = File.ReadAllText(saveDataPath);
            saveData = JsonUtility.FromJson<SaveData>(json);
        }
    }
    
    private void SaveGameData()
    {
        Debug.Log("保存游戏数据");
        string json = JsonUtility.ToJson(saveData);
        File.WriteAllText(saveDataPath, json);
    }
    
    private bool CheckLevelUnlockCondition(int levelIndex)
    {
        // 第一关始终可用
        if (levelIndex == 0) return true;
        Debug.Log("检查关卡解锁条件"+levelIndex);
        // 检查前一关是否已通关
        int previousLevelID = levels[levelIndex - 1].levelID;
        foreach (var levelData in saveData.levelSaveDataList)
        {
            if (levelData.levelID == previousLevelID && levelData.isCompleted)
            {
                return true;
            }
        }
        
        return false;
    }
    
    public void LoadLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levels.Count)
        {
            Debug.LogError($"无效的关卡索引: {levelIndex}");
            return;
        }
        
        // 检查解锁条件
        if (!CheckLevelUnlockCondition(levelIndex))
        {
            Debug.LogWarning($"关卡 {levelIndex} 尚未解锁");
            return;
        }
        
        LevelInfo levelInfo = levels[levelIndex];
        int levelID = levelInfo.levelID;
        
        // 更新当前关卡ID和名称
        this.currentLevelID = levelID;
        this.currentLevelName = levelInfo.levelName;
        
        // 更新最后游玩的关卡ID
        saveData.lastPlayedLevelID = levelID;
        SaveGameData();
        
        // 更新UI显示
        UpdateLevelNameText();
        
        // 输出调试信息
        Debug.Log($"加载关卡: ID={currentLevelID}, 名称={currentLevelName}");
        
        // 查找是否有存档数据
        LevelSaveData levelSaveData = null;
        foreach (var saveDataItem in saveData.levelSaveDataList)
        {
            if (saveDataItem.levelID == levelID)
            {
                levelSaveData = saveDataItem;
                break;
            }
        }
        
        // 如果没有存档数据，使用默认关卡信息
        string savePath = levelInfo.saveFilePath;
        string plantDataPath = levelInfo.plantDataSavePath;
        int timeCount = levelInfo.countdownDuration;
        
        // 标记是否为新创建的存档
        bool isNewSave = false;
        
        // 如果有存档数据，使用存档数据
        if (levelSaveData != null)
        {
            savePath = levelSaveData.saveFilePath;
            plantDataPath = levelSaveData.plantDataSavePath;
            timeCount = levelSaveData.currentTime;
        }
        else
        {
            // 生成包含当前时间的路径，格式：原路径_yyyyMMdd_HHmmss
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            // 获取原始文件名（不含路径）
            string saveFileName = Path.GetFileName(levelInfo.saveFilePath);
            string plantFileName = Path.GetFileName(levelInfo.plantDataSavePath);
            
            // 获取文件扩展名
            string saveExt = Path.GetExtension(saveFileName);
            string plantExt = Path.GetExtension(plantFileName);
            
            // 获取文件名（不含扩展名）
            string saveFileNameWithoutExt = Path.GetFileNameWithoutExtension(saveFileName);
            string plantFileNameWithoutExt = Path.GetFileNameWithoutExtension(plantFileName);
            
            // 将文件放在Application.persistentDataPath下
            string tmpSavePath = Path.Combine(Application.persistentDataPath, $"{saveFileNameWithoutExt}_{timestamp}{saveExt}");
            string tmpPlantDataPath = Path.Combine(Application.persistentDataPath, $"{plantFileNameWithoutExt}_{timestamp}{plantExt}");
            
            // 创建新的存档数据
            levelSaveData = new LevelSaveData
            {
                levelID = levelID,
                levelName = levelInfo.levelName,
                saveFilePath = tmpSavePath,
                plantDataSavePath = tmpPlantDataPath,
                currentTime = levelInfo.countdownDuration,
                isCompleted = false
            };
            saveData.levelSaveDataList.Add(levelSaveData);
            SaveGameData();
            
            // 标记这是一个新创建的存档
            isNewSave = true;
            
            Debug.Log($"创建新的存档路径: \n场景对象: {savePath}\n植物数据: {plantDataPath}");
        }
        // 触发事件
        OnLevelLoaded?.Invoke();
        // 1. 加载植物数据库和种子映射
        if (PlantManager.Instance != null)
        {
            PlantManager.Instance.LoadPlantDatabase(levelInfo.plantDatabasePath);
            PlantManager.Instance.LoadSeedMappings(levelInfo.seedMappingPath);
            

        }
        else
        {
            Debug.LogError("找不到PlantManager实例");
        }
        
        // 2. 加载场景对象
        if (SceneObjectManager.Instance != null)
        {
            SceneObjectManager.Instance.LoadAllSceneObjects(savePath);
        }
        else
        {
            Debug.LogError("找不到SceneObjectManager实例");
        }
        // 加载植物数据
        if (!string.IsNullOrEmpty(plantDataPath))
        {
            // 使用协程延迟0.01s加载植物数据和启动倒计时
            StartCoroutine(DelayLoadPlantsAndStartCountdown(plantDataPath, timeCount, isNewSave));
        }
        else
        {
            // 如果没有植物数据，仅延迟启动倒计时
            StartCoroutine(DelayStartCountdown(timeCount, isNewSave));
        }
    }
    
    // 记录关卡完成状态的方法
    public void SetLevelCompleted(int levelID)
    {
        foreach (var levelData in saveData.levelSaveDataList)
        {
            if (levelData.levelID == levelID)
            {
                levelData.isCompleted = true;
                SaveGameData();
                break;
            }
        }
    }
    
    // 更新当前时间的方法
    public void UpdateCurrentTime(int levelID, int currentTime)
    {
        foreach (var levelData in saveData.levelSaveDataList)
        {
            if (levelData.levelID == levelID)
            {
                levelData.currentTime = currentTime;
                SaveGameData();
                break;
            }
        }
    }
    
    // 获取当前关卡信息的方法
    public bool GetCurrentLevelInfo(out LevelSaveData levelData)
    {
        foreach (var data in saveData.levelSaveDataList)
        {
            if (data.levelID == currentLevelID)
            {
                levelData = data;
                return true;
            }
        }
        
        levelData = null;
        return false;
    }

    // 保存当前关卡状态的方法
    public void SaveCurrentLevel()
    {
        // 检查当前是否有加载的关卡
        if (currentLevelID < 0)
        {
            Debug.LogWarning("没有加载任何关卡，无法保存");
            return;
        }
        
        // 更新最后游玩的关卡ID
        saveData.lastPlayedLevelID = currentLevelID;
        
        // 查找当前关卡的存档数据
        LevelSaveData currentLevelData = null;
        foreach (var levelData in saveData.levelSaveDataList)
        {
            if (levelData.levelID == currentLevelID)
            {
                currentLevelData = levelData;
                break;
            }
        }
        
        if (currentLevelData == null)
        {
            Debug.LogError($"无法找到当前关卡(ID={currentLevelID})的存档数据");
            return;
        }
        
        // 如果有倒计时计时器，更新当前时间
        if (countdownTimer != null)
        {
            int remainingTime = countdownTimer.GetRemainingTime();
            currentLevelData.currentTime = remainingTime;
        }
        
        // 保存存档数据
        SaveGameData();
        
        // 保存场景对象
        if (SceneObjectManager.Instance != null && !string.IsNullOrEmpty(currentLevelData.saveFilePath))
        {
            SceneObjectManager.Instance.SaveAllSceneObjects(currentLevelData.saveFilePath);
        }
        else
        {
            Debug.LogWarning("无法保存场景对象：SceneObjectManager实例为空或保存路径为空");
        }
        
        // 保存植物数据
        if (PlantManager.Instance != null && !string.IsNullOrEmpty(currentLevelData.plantDataSavePath))
        {
            PlantManager.Instance.SaveAllPlants(currentLevelData.plantDataSavePath);
        }
        else
        {
            Debug.LogWarning("无法保存植物数据：PlantManager实例为空或保存路径为空");
        }
        
        Debug.Log($"已保存关卡 {currentLevelName}(ID={currentLevelID})，剩余时间：{currentLevelData.currentTime}");
    }

    // 添加自动保存方法（可选，用于定期自动保存）
    public void AutoSaveCurrentLevel()
    {
        // 检查是否有正在进行的关卡
        if (currentLevelID >= 0 && countdownTimer != null)
        {
            SaveCurrentLevel();
            Debug.Log("已自动保存当前关卡状态");
        }
    }

    // 添加新的协程方法用于延迟加载植物数据和启动倒计时
    private IEnumerator DelayLoadPlantsAndStartCountdown(string plantDataPath, int timeCount, bool isNewSave)
    {
        // 等待0.1秒
        yield return new WaitForSeconds(0.01f);
        
        // 加载植物数据
        PlantManager.Instance.LoadAllPlants(plantDataPath);
        
        // 设置并启动倒计时
        if (countdownTimer != null)
        {
            countdownTimer.SetCountdownTime(timeCount);
            countdownTimer.StartCountdown();
        }
        else
        {
            Debug.LogError("找不到CountdownTimer引用");
        }
        
        // 如果是新创建的存档，在加载完成后保存一次
        if (isNewSave)
        {
            // 再等待一帧，确保所有内容都加载完成
            yield return null;
            SaveCurrentLevel();
            Debug.Log("新创建的存档加载完成后已自动保存");
        }
    }

    // 添加新的协程方法仅用于延迟启动倒计时（当没有植物数据时）
    private IEnumerator DelayStartCountdown(int timeCount, bool isNewSave)
    {
        // 等待0.1秒
        yield return new WaitForSeconds(0.01f);
        
        // 设置并启动倒计时
        if (countdownTimer != null)
        {
            countdownTimer.SetCountdownTime(timeCount);
            countdownTimer.StartCountdown();
        }
        else
        {
            Debug.LogError("找不到CountdownTimer引用");
        }
        
        // 如果是新创建的存档，在加载完成后保存一次
        if (isNewSave)
        {
            // 再等待一帧，确保所有内容都加载完成
            yield return null;
            SaveCurrentLevel();
            Debug.Log("新创建的存档加载完成后已自动保存");
        }
    }

    // 添加重新开始当前关卡的方法
    public void RestartCurrentLevel()
    {
        // 检查当前是否有加载的关卡
        if (currentLevelID < 0)
        {
            Debug.LogWarning("没有加载任何关卡，无法重新开始");
            return;
        }
        
        // 查找当前关卡在levels列表中的索引
        int levelIndex = -1;
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].levelID == currentLevelID)
            {
                levelIndex = i;
                break;
            }
        }
        
        if (levelIndex == -1)
        {
            Debug.LogError($"无法找到当前关卡(ID={currentLevelID})的信息");
            return;
        }
        
        // 找到saveData中的关卡数据并删除
        int saveDataIndex = -1;
        for (int i = 0; i < saveData.levelSaveDataList.Count; i++)
        {
            if (saveData.levelSaveDataList[i].levelID == currentLevelID)
            {
                saveDataIndex = i;
                break;
            }
        }
        
        if (saveDataIndex != -1)
        {
            // 保存关卡是否已完成的状态
            bool isCompleted = saveData.levelSaveDataList[saveDataIndex].isCompleted;
            
            // 获取文件路径并删除文件
            string saveFilePath = saveData.levelSaveDataList[saveDataIndex].saveFilePath;
            string plantDataSavePath = saveData.levelSaveDataList[saveDataIndex].plantDataSavePath;
            
            // 删除文件
            if (!string.IsNullOrEmpty(saveFilePath) && File.Exists(saveFilePath))
            {
                try
                {
                    File.Delete(saveFilePath);
                    Debug.Log($"已删除场景存档文件: {saveFilePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"删除场景存档文件失败: {e.Message}");
                }
            }
            
            if (!string.IsNullOrEmpty(plantDataSavePath) && File.Exists(plantDataSavePath))
            {
                try
                {
                    File.Delete(plantDataSavePath);
                    Debug.Log($"已删除植物数据存档文件: {plantDataSavePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"删除植物数据存档文件失败: {e.Message}");
                }
            }
            
            // 从列表中移除当前关卡的存档数据
            saveData.levelSaveDataList.RemoveAt(saveDataIndex);
            
            // 保存游戏数据
            SaveGameData();
            
            Debug.Log($"重新开始关卡: ID={currentLevelID}, 名称={currentLevelName}");
            
            // 重新加载关卡
            LoadLevel(levelIndex);
            
            // 如果关卡之前是已完成状态，重新设置完成状态
            if (isCompleted)
            {
                SetLevelCompleted(currentLevelID);
            }
        }
        else
        {
            // 如果找不到存档数据，直接重新加载关卡
            Debug.Log($"未找到关卡存档数据，直接重新加载关卡: ID={currentLevelID}");
           // 重新加载关卡
            LoadLevel(levelIndex);
        }
    }

    // 完成当前关卡并加载下一关
    public void CompleteAndLoadNextLevel()
    {
        // 检查当前是否有加载的关卡
        if (currentLevelID < 0)
        {
            Debug.LogWarning("没有加载任何关卡，无法完成并加载下一关");
            return;
        }
        
        // 查找当前关卡在levels列表中的索引
        int currentLevelIndex = -1;
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].levelID == currentLevelID)
            {
                currentLevelIndex = i;
                break;
            }
        }
        
        if (currentLevelIndex == -1)
        {
            Debug.LogError($"无法找到当前关卡(ID={currentLevelID})的信息");
            return;
        }
        
        // 标记当前关卡为已完成
        SetLevelCompleted(currentLevelID);
        Debug.Log($"关卡 {currentLevelName}(ID={currentLevelID}) 已标记为完成");
        
        // 检查是否有下一关
        int nextLevelIndex = currentLevelIndex + 1;
        if (nextLevelIndex >= levels.Count)
        {
            Debug.Log("已完成所有关卡，没有下一关可加载");
            return;
        }
        
        // 保存当前状态（可选，取决于游戏逻辑是否需要）
        SaveCurrentLevel();
        
        // 加载下一关
        Debug.Log($"正在加载下一关: ID={levels[nextLevelIndex].levelID}, 名称={levels[nextLevelIndex].levelName}");
        LoadLevel(nextLevelIndex);
    }

    // 检查重复的levelID
    private void CheckDuplicateLevelIDs()
    {
        HashSet<int> uniqueLevelIDs = new HashSet<int>();
        List<int> duplicateLevelIDs = new List<int>();
        
        foreach (var level in levels)
        {
            if (!uniqueLevelIDs.Add(level.levelID))
            {
                // 如果无法添加到HashSet，说明已存在相同ID
                duplicateLevelIDs.Add(level.levelID);
            }
        }
        
        if (duplicateLevelIDs.Count > 0)
        {
            string duplicateIDs = string.Join(", ", duplicateLevelIDs);
            Debug.LogError($"检测到重复的关卡ID: {duplicateIDs}。这可能导致关卡加载和存档出现问题！");
        }
    }

    // 关卡失败方法
    public void OnLevelFailed()
    {
        // 开始新游戏
        // 创建新的存档数据
        CreateNewSaveData();
        // 加载第一关
        LoadLevel(0);
        // 显示失败消息（可选，根据需求添加UI显示）
        MessageManager.instance.SendMessage("时间用完了噼！", PlayerPathfinding.Instance.transform, MessageType.Auto, 1f);
    }

    public void CreateNewSaveData()
    {
        // 删除所有关卡存档文件
        if (saveData != null && saveData.levelSaveDataList.Count > 0)
        {
            foreach (var levelData in saveData.levelSaveDataList)
            {
                // 删除场景存档文件
                if (!string.IsNullOrEmpty(levelData.saveFilePath) && File.Exists(levelData.saveFilePath))
                {
                    try
                    {
                        File.Delete(levelData.saveFilePath);
                        Debug.Log($"已删除场景存档文件: {levelData.saveFilePath}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"删除场景存档文件失败: {e.Message}");
                    }
                }
                
                // 删除植物数据存档文件
                if (!string.IsNullOrEmpty(levelData.plantDataSavePath) && File.Exists(levelData.plantDataSavePath))
                {
                    try
                    {
                        File.Delete(levelData.plantDataSavePath);
                        Debug.Log($"已删除植物数据存档文件: {levelData.plantDataSavePath}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"删除植物数据存档文件失败: {e.Message}");
                    }
                }
            }
        }
        
        // 创建新的SaveData对象
        saveData = new SaveData();
        saveData.lastPlayedLevelID = -1; // 确保重置lastPlayedLevelID
        
        // 删除存档文件
        if (File.Exists(saveDataPath))
        {
            try
            {
                File.Delete(saveDataPath);
                Debug.Log($"已删除存档文件: {saveDataPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"删除存档文件失败: {e.Message}");
            }
        }
        
        // 保存空的SaveData
        SaveGameData();
        
        // 重置当前关卡信息
        currentLevelID = -1;
        currentLevelName = string.Empty;
        
        // 更新UI显示
        UpdateLevelNameText();
        
        Debug.Log("已创建新的存档数据，所有进度已重置");
    }

    // 加载上一关的方法
    public void LoadPreviousLevel()
    {
        // 检查当前是否有加载的关卡
        if (currentLevelID < 0)
        {
            Debug.LogWarning("没有加载任何关卡，无法加载上一关");
            return;
        }
        
        // 查找当前关卡在levels列表中的索引
        int currentLevelIndex = -1;
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].levelID == currentLevelID)
            {
                currentLevelIndex = i;
                break;
            }
        }
        
        if (currentLevelIndex == -1)
        {
            Debug.LogError($"无法找到当前关卡(ID={currentLevelID})的信息");
            return;
        }
        
        // 检查是否有上一关
        int previousLevelIndex = currentLevelIndex - 1;
        if (previousLevelIndex < 0)
        {
            Debug.Log("当前已是第一关，没有上一关可加载");
            return;
        }
        
        // 保存当前状态
        SaveCurrentLevel();
        
        // 加载上一关
        Debug.Log($"正在加载上一关: ID={levels[previousLevelIndex].levelID}, 名称={levels[previousLevelIndex].levelName}");
        LoadLevel(previousLevelIndex);
    }

    private void UpdateLevelNameText()
    {
        if (currentLevelNameText != null)
        {
            if (currentLevelID >= 0 && !string.IsNullOrEmpty(currentLevelName))
            {
                currentLevelNameText.text = currentLevelName;
            }
            else
            {
                currentLevelNameText.text = "未选择关卡";
            }
        }
    }

    // 添加一个新方法，用于加载最后一次游玩的关卡
    public void LoadLastPlayedLevel()
    {
        // 检查是否需要加载最后一次游玩的关卡
        if (saveData.lastPlayedLevelID >= 0)
        {
            // 查找关卡在levels列表中的索引
            int levelIndex = -1;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].levelID == saveData.lastPlayedLevelID)
                {
                    levelIndex = i;
                    break;
                }
            }
            
            if (levelIndex >= 0)
            {
                // 自动加载最后一次游玩的关卡
                LoadLevel(levelIndex);
                Debug.Log($"已自动加载上次游玩的关卡: ID={saveData.lastPlayedLevelID}");
            }
            else
            {
                Debug.LogWarning($"无法找到上次游玩的关卡(ID={saveData.lastPlayedLevelID})");
            }
        }
    }

    void Update()
    {
        // if(File.Exists(saveDataPath))
        // {
        //     Debug.Log("存档文件路径: " + saveDataPath);
        // }
    }
}
