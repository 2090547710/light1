using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleAnimatorController : MonoBehaviour
{
    public Animator[] animators;
    public GameObject[] panels;
    private bool arePanelsEnabled = false;
    
    // Start is called before the first frame update
    void Start()
    {
        // 初始化时可以选择是否隐藏所有动画对象
        // HideAll();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    // 显示并播放所有动画
    public void PlayAll()
    {
        foreach (Animator animator in animators)
        {
            if (animator != null)
            {
                animator.gameObject.SetActive(true);
                animator.SetTrigger("Show");
            }
        }
    }
    
    // 隐藏所有动画对象
    public void HideAll()
    {
        foreach (Animator animator in animators)
        {
            if (animator != null && animator.gameObject != null)
            {
                animator.SetTrigger("Hide");
            }
        }
    }
    
    // 播放指定索引的动画
    public void PlayAt(int index)
    {
        if (index >= 0 && index < animators.Length && animators[index] != null)
        {
            animators[index].gameObject.SetActive(true);
            animators[index].SetTrigger("Show");
        }
    }
    
    // 隐藏指定索引的动画
    public void HideAt(int index)
    {
        if (index >= 0 && index < animators.Length && animators[index] != null)
        {
            animators[index].SetTrigger("Hide");
        }
    }
    
    // 重置所有动画器
    public void ResetAll()
    {
        foreach (Animator animator in animators)
        {
            if (animator != null)
            {
                animator.Rebind();
            }
        }
    }
    
    // 启用所有面板
    public void EnableAllPanels()
    {
        foreach (GameObject panel in panels)
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }
        arePanelsEnabled = true;
    }
    
    // 禁用所有面板
    public void DisableAllPanels()
    {
        foreach (GameObject panel in panels)
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }
        arePanelsEnabled = false;
    }
    
    // 启用指定索引的面板
    public void EnablePanelAt(int index)
    {
        if (index >= 0 && index < panels.Length && panels[index] != null)
        {
            panels[index].SetActive(true);
        }
    }
    
    // 禁用指定索引的面板
    public void DisablePanelAt(int index)
    {
        if (index >= 0 && index < panels.Length && panels[index] != null)
        {
            panels[index].SetActive(false);
        }
    }
    
    // 根据当前状态切换所有面板的显示/隐藏
    public void ToggleAllPanels()
    {
        if (arePanelsEnabled)
        {
            DisableAllPanels();
        }
        else
        {
            EnableAllPanels();
        }
    }
}
