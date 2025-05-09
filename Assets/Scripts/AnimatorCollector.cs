using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 收集所有子对象身上的Animator组件并添加到PlantInteraction的animatorList中
/// </summary>
public class AnimatorCollector : MonoBehaviour
{
    [Tooltip("要添加Animator的目标对象，如果为空则使用所有子对象")]
    public Transform targetRoot;

    [Tooltip("是否包含非激活状态的游戏对象")]
    public bool includeInactive = true;

    // Start is called before the first frame update
    void Start()
    {
        // 获取PlantInteraction实例
        PlantInteraction plantInteraction = PlantInteraction.Instance;
        
        if (plantInteraction == null)
        {
            Debug.LogError("无法找到PlantInteraction实例，请确保场景中有PlantInteraction组件并且已经初始化");
            return;
        }
        
        // 确定要搜索的根对象
        Transform root = targetRoot != null ? targetRoot : transform;
        
        // 获取所有子对象的Animator组件
        Animator[] animators = root.GetComponentsInChildren<Animator>(includeInactive);
        
        // 添加到PlantInteraction的animatorList中
        foreach (Animator animator in animators)
        {
            if (animator != null && !plantInteraction.animatorList.Contains(animator))
            {
                plantInteraction.animatorList.Add(animator);
                Debug.Log($"已添加Animator: {animator.gameObject.name}");
            }
        }
        
        Debug.Log($"共收集了 {animators.Length} 个Animator组件");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
