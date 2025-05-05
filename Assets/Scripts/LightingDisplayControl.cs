using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LightingDisplayControl : MonoBehaviour
{
    public Button toggleButton;
    public TextMeshProUGUI buttonText;
    
    private bool isDisplaying = true;
    
    // Start is called before the first frame update
    void Start()
    {
        // 初始化状态
        isDisplaying = LightingManager.showLightingQuads;
        UpdateButtonText();
        
        // 为按钮添加监听
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleLightingDisplay);
        }
        else
        {
            Debug.LogWarning("未设置toggleButton引用");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 添加按键Z控制功能
        if (Input.GetKeyDown(KeyCode.Z))
        {
            // 直接调用已有的切换方法
            ToggleLightingDisplay();
        }
    }
    
    // 切换光照范围显示状态
    public void ToggleLightingDisplay()
    {
        isDisplaying = !isDisplaying;
        
        // 调用LightingManager方法切换显示
        LightingManager.ToggleLightingQuads(isDisplaying);
        
        // 更新按钮文本
        UpdateButtonText();
    }
    
    // 更新按钮文本
    private void UpdateButtonText()
    {
        if (buttonText != null)
        {
            buttonText.text = isDisplaying ? "隐藏光照范围" : "显示光照范围";
        }
    }
    
    // 在脚本销毁时移除监听器
    private void OnDestroy()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleLightingDisplay);
        }
    }
}
