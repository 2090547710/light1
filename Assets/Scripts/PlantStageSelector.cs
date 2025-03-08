using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PlantStageSelector : MonoBehaviour
{
    [Header("种子属性选择")]
    public SizeLevel size = SizeLevel.Medium;
    public GrowthRateLevel growthRate = GrowthRateLevel.Medium;
    
    [Header("获取的植物阶段信息")]
    [SerializeField] private int plantID;
    [SerializeField] private string plantName;
    [SerializeField] private StageType stageType;
    [SerializeField] private float plantGrowthRate;
    [SerializeField] private int lightSourceCount;
    [SerializeField] private string prerequisitePlants = "无";
    
    [Header("模式选择")]
    public bool seedMode = false;
    
    [Header("种子名称（格式: 尺寸+生长速度，例如SmallSlow）")]
    public string seedName = "";
    
    [System.Serializable]
    public class PlantCountEntry
    {
        public int plantId;
        public int count = 1;
    }
    
    public void UpdatePlantStageInfo()
    {
        if (PlantManager.Instance == null)
        {
            Debug.LogWarning("PlantManager实例不存在");
            return;
        }
        
        Plant.PlantStage stage;
        
        if (seedMode)
        {
            // 若输入了种子名称，则优先使用种子名称调用 GetSeedPlantStageFromName
            if (!string.IsNullOrEmpty(seedName))
            {
                stage = PlantManager.Instance.GetSeedPlantStageFromName(seedName);
            }
            else
            {
                // 如果种子名称为空，则使用尺寸和生长速度
                stage = PlantManager.Instance.GetSeedPlantStage(size, growthRate);
            }
        }
        else
        {
            // 获取植物阶段信息
            if (!string.IsNullOrEmpty(seedName))
            {
                // 使用种子名称获取植物阶段信息
                stage = PlantManager.Instance.GetPlantStageBySeedFromName(seedName);
            }
            else
            {
                // 如果种子名称为空，则使用尺寸和生长速度
                stage = PlantManager.Instance.GetPlantStageBySeed(size, growthRate);
            }
        }
        
        if (stage == null)
        {
            if (seedMode && !string.IsNullOrEmpty(seedName))
            {
                Debug.LogWarning("找不到匹配的种子阶段: " + seedName);
            }
            else
            {
                Debug.LogWarning($"找不到匹配的{(seedMode ? "种子" : "植物")}阶段: 尺寸={size}, 生长速度={growthRate}");
            }
            return;
        }
        
        // 更新显示信息
        plantID = stage.plantID;
        plantName = stage.plantName;
        stageType = stage.stageType;
        plantGrowthRate = stage.growthRate;
        lightSourceCount = stage.associatedLights != null ? stage.associatedLights.Count : 0;
        
        // 获取前置植物信息
        if (stage.prerequisitePlantIDs != null && stage.prerequisitePlantIDs.Count > 0)
        {
            prerequisitePlants = string.Join(", ", stage.prerequisitePlantIDs);
        }
        else
        {
            prerequisitePlants = "无";
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PlantStageSelector))]
public class PlantStageSelectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        PlantStageSelector selector = (PlantStageSelector)target;
        
        EditorGUILayout.Space();
        if (GUILayout.Button("获取植物阶段信息"))
        {
            selector.UpdatePlantStageInfo();
        }
    }
}
#endif 