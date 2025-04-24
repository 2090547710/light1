using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Fire : Plant
{
    // 灯塔位置相对于原点的偏移
    private Vector3 lighthousePosition;
       
    // 重新实现Start方法
    void Start()
    {
        // 初始化灯塔位置
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.beginPoint != null)
            {
                // 使用lighthouse的transform.position作为灯塔位置
                lighthousePosition = GameManager.Instance.beginPoint.transform.position;
            }
            else
            {
                // 若lighthouse未设置，回退到原来的计算方式
                Debug.LogWarning("GameManager的lighthouse未设置");
            }
        }
        else
        {
            Debug.LogError("GameManager实例不存在，无法初始化灯塔位置");
        }
        // 初始化当前阶段
        if(currentStage==0){
            lightSources.Clear();
            if (growthStages.Count > 0 && currentStage <= growthStages.Count)
            {
                Grow();
            }   
        }
        
        // 创建并设置名称显示
        CreateNameDisplay();
    }
    
    // 重载Grow方法
    public override void Grow(bool useAnimation = true)
    {
        // 首先检查基本条件
        if (currentStage >= maxStages) return;

        // 添加碰撞检测逻辑
        if (currentStage < growthStages.Count && HasCollisionWithOtherPlants())
        {
            Debug.Log($"火植物 {plantName} 无法生长，检测到与其他植物的碰撞");
            return;
        }
               
        // 继续其他生长逻辑
        // 禁用并移除所有现有光源组件
        lightSources.ForEach(l => {
            l.RemoveLighting();
            LightingManager.tree.Remove(l.gameObject);
            Destroy(l);
        });
        lightSources.Clear();
        
        ApplyStageConfig(currentStage);
        currentStage++;

    }
    
    // 重载TryBloom方法
    public override void TryBloom()
    {
        if(isWithered){
            return;
        }
        
        // 调用PlantManager中的方法来确保只有一个火
        bool isOnlyFire = PlantManager.Instance.EnsureSingleFire(this);

        // 如果不是唯一的火，判定安全区，使其他火枯萎
        if (!isOnlyFire)
        {
            // 检查是否有路径从灯塔到火的位置
            bool hasPathFromLighthouse = false;
            
            // 确保QuadTree已初始化
            if (PlayerPathfinding.quadTree != null)
            {
                // 尝试找到从灯塔到火的路径
                List<Vector3> path = PlayerPathfinding.quadTree.FindPath(lighthousePosition, transform.position);
                hasPathFromLighthouse = (path != null && path.Count > 0);
                
                if (hasPathFromLighthouse)
                {
                    Debug.Log($"火植物 {plantName} 成功找到从灯塔的路径，可以开花");
                }
                else
                {
                    Debug.Log($"火植物 {plantName} 无法找到从灯塔的路径，不能开花");
                    return;
                }
            }
            else
            {
                Debug.LogWarning("四叉树未初始化，无法检查从灯塔到火的路径");
                return;
            }
        
            // 使其他火枯萎
            PlantManager.Instance.WitherOtherFires(this);
        }
 
        Grow();
        Debug.Log($"火植物成功开花！");
        // 火成长后，检查所有植物是否在火光源范围内
        PlantManager.Instance.CheckPlantsInFireLight(this);
    }

    // 在Fire类中添加保存特有参数的逻辑
    public override PlantSaveData GetSaveData()
    {
        PlantSaveData saveData = base.GetSaveData();
        
        // 保存Fire特有参数
        saveData.plantType = "Fire";
        // 可以添加更多Fire特有参数的保存
        
        return saveData;
    }


    // 在Fire类中添加可以考虑旋转的方法
    public bool IsPositionInFireLight(Vector3 position)
    {
        // 检查位置是否在任何火光源范围内
        foreach (Lighting fireLight in lightSources)
        {
            // 使用支持旋转的点在区域内检测
            if (fireLight.IsPointInRotatedBounds(position))
            {
                return true;
            }
        }
        
        return false;
    }
} 