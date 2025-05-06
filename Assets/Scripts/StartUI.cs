using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;
using UnityEngine.EventSystems;

public class StartUI : MonoBehaviour
{
    // 添加单例模式
    public static StartUI Instance { get; private set; }
    
    // 是否当前显示开始界面
    private bool isUIActive = true;
    
    // 当前选中的按钮
    private Button currentSelectedButton;
    
    // 按钮颜色设置
    [Tooltip("按钮正常状态颜色")]
    public Color normalButtonColor = Color.white;
    
    [Tooltip("按钮选中状态颜色")]
    public Color selectedButtonColor = new Color(0.8f, 0.8f, 1f);
    
    [Tooltip("开始界面主Panel")]
    public GameObject startUIPanel;
    
    [Tooltip("开始新游戏Panel")]
    public GameObject newGamePanel;
    
    [Tooltip("继续游戏Panel")]
    public GameObject continueGamePanel;
    
    [Tooltip("警告信息Panel")]
    public GameObject warningPanel;
    
    [Tooltip("选项面板")]
    public GameObject optionsPanel;
    
    [Tooltip("警告信息文本")]
    public TextMeshProUGUI warningText;
    
    [Tooltip("音乐音量滑动条")]
    public Slider musicVolumeSlider;
    
    [Tooltip("音效音量滑动条")]
    public Slider sfxVolumeSlider;
    
    [Tooltip("选项按钮")]
    public Button optionsButton;
    
    [Tooltip("选项面板关闭按钮")]
    public Button optionsCloseButton;
    
    [Tooltip("退出游戏按钮")]
    public Button exitButton;
    
    // 新游戏按钮
    private Button newGameButton;
    
    // 继续游戏按钮
    private Button continueGameButton;
    
    // 警告确认按钮
    private Button warningYesButton;
    
    // 警告取消按钮
    private Button warningNoButton;
    
    // 当前警告面板操作类型
    private WarningType currentWarningType = WarningType.None;
    
    // 警告类型枚举
    private enum WarningType
    {
        None,
        NewGame,
        ExitGame
    }
    
    // 存档路径
    private string saveDataPath;
    
    // 是否存在存档数据
    private bool hasSaveData = false;
    
    // 音量设置保存路径
    private string volumeSettingsPath;
    
    [System.Serializable]
    private class VolumeSettings
    {
        public float musicVolume = 0.5f;
        public float sfxVolume = 0.7f;
    }
    
    private VolumeSettings volumeSettings = new VolumeSettings();
    
    // 在顶部添加事件
    public static event System.Action<bool> OnStartUIVisibilityChanged;
    
    private void Awake()
    {
        // 设置单例
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
        
        // 如果未指定startUIPanel，默认使用当前游戏对象的第一个子对象
        if (startUIPanel == null && transform.childCount > 0)
        {
            startUIPanel = transform.GetChild(0).gameObject;
            Debug.Log("未指定startUIPanel，自动设置为第一个子对象: " + startUIPanel.name);
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {
        // 设置存储路径
        saveDataPath = Path.Combine(Application.persistentDataPath, "levelsavedata.json");
        volumeSettingsPath = Path.Combine(Application.persistentDataPath, "volumesettings.json");
        
        // 检查是否存在存档数据
        hasSaveData = File.Exists(saveDataPath);
        
        // 加载音量设置
        LoadVolumeSettings();
        
        // 初始化界面
        InitUI();
        
        // 绑定按钮事件
        BindButtonEvents();
        
        // 显示UI时禁用交互
        if (PlantInteraction.Instance != null)
        {
            PlantInteraction.Instance.DisableInteraction();
        }
        
        // 显示开始界面
        ShowStartUI();
    }

    private void InitUI()
    {
        // 确保startUIPanel激活
        if (startUIPanel != null)
        {
            startUIPanel.SetActive(true);
        }
        
        // 设置其他Panel可见性
        if (newGamePanel != null)
        {
            newGamePanel.SetActive(true);
        }
        
        if (continueGamePanel != null)
        {
            continueGamePanel.SetActive(hasSaveData);
        }
        
        if (warningPanel != null)
        {
            warningPanel.SetActive(false);
        }
        
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
        }
        
        // 初始化音量滑动条
        if (musicVolumeSlider != null && AudioManager.Instance != null)
        {
            musicVolumeSlider.value = volumeSettings.musicVolume;
            AudioManager.Instance.SetMusicVolume(volumeSettings.musicVolume);
        }
        
        if (sfxVolumeSlider != null && AudioManager.Instance != null)
        {
            sfxVolumeSlider.value = volumeSettings.sfxVolume;
            AudioManager.Instance.SetSFXVolume(volumeSettings.sfxVolume);
        }
        
        // 初始化所有按钮颜色为正常状态
        InitializeButtonColors();
    }
    
    // 初始化所有按钮颜色
    private void InitializeButtonColors()
    {
        // 设置所有按钮为正常颜色
        SetButtonColor(newGameButton, normalButtonColor);
        SetButtonColor(continueGameButton, normalButtonColor);
        SetButtonColor(optionsButton, normalButtonColor);
        SetButtonColor(optionsCloseButton, normalButtonColor);
        SetButtonColor(exitButton, normalButtonColor);
        SetButtonColor(warningYesButton, normalButtonColor);
        SetButtonColor(warningNoButton, normalButtonColor);
    }
    
    // 设置按钮颜色
    private void SetButtonColor(Button button, Color color)
    {
        if (button != null)
        {
            Image buttonImage = button.GetComponentInChildren<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = color;
            }
        }
    }
    
    // 设置当前选中的按钮
    private void SetSelectedButton(Button button)
    {
        // 如果有之前选中的按钮，恢复为正常颜色
        if (currentSelectedButton != null)
        {
            SetButtonColor(currentSelectedButton, normalButtonColor);
        }
        
        // 设置新的当前按钮
        currentSelectedButton = button;
        
        // 更改当前按钮颜色
        if (currentSelectedButton != null)
        {
            SetButtonColor(currentSelectedButton, selectedButtonColor);
        }
    }
    
    // 为按钮添加事件处理脚本
    private void AddButtonHoverEvents(Button button)
    {
        if (button != null)
        {
            // 获取或添加EventTrigger组件
            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }
            
            // 添加鼠标进入事件
            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((data) => { OnButtonPointerEnter(button); });
            trigger.triggers.Add(entryEnter);
            
            // 添加鼠标离开事件
            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((data) => { OnButtonPointerExit(button); });
            trigger.triggers.Add(entryExit);
        }
    }
    
    // 鼠标进入按钮事件处理
    private void OnButtonPointerEnter(Button button)
    {
        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.fontStyle = FontStyles.Underline;
        }
    }
    
    // 鼠标离开按钮事件处理
    private void OnButtonPointerExit(Button button)
    {
        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.fontStyle = FontStyles.Normal;
        }
    }
    
    private void BindButtonEvents()
    {
        // 查找并绑定新游戏按钮
        if (newGamePanel != null)
        {
            newGameButton = newGamePanel.GetComponentInChildren<Button>();
            if (newGameButton != null)
            {
                // 添加鼠标悬停事件
                AddButtonHoverEvents(newGameButton);
                
                if (hasSaveData)
                {
                    // 如果有存档，点击显示警告
                    newGameButton.onClick.AddListener(() => {
                        SetSelectedButton(newGameButton);
                        ShowNewGameWarning();
                    });
                }
                else
                {
                    // 如果没有存档，直接开始新游戏
                    newGameButton.onClick.AddListener(() => {
                        SetSelectedButton(newGameButton);
                        StartNewGame();
                    });
                }
            }
            else
            {
                Debug.LogWarning("无法找到新游戏按钮");
            }
        }
        
        // 查找并绑定继续游戏按钮
        if (continueGamePanel != null && hasSaveData)
        {
            continueGameButton = continueGamePanel.GetComponentInChildren<Button>();
            if (continueGameButton != null)
            {
                // 添加鼠标悬停事件
                AddButtonHoverEvents(continueGameButton);
                
                continueGameButton.onClick.AddListener(() => {
                    SetSelectedButton(continueGameButton);
                    ContinueGame();
                });
            }
            else
            {
                Debug.LogWarning("无法找到继续游戏按钮");
            }
        }
        
        // 查找并绑定警告确认按钮
        if (warningPanel != null)
        {
            // 使用Find查找子对象中的按钮
            Transform yesButtonTransform = warningPanel.transform.Find("YesButton");
            Transform noButtonTransform = warningPanel.transform.Find("NoButton");
            
            if (yesButtonTransform != null)
            {
                warningYesButton = yesButtonTransform.GetComponent<Button>();
                if (warningYesButton != null)
                {
                    // 添加鼠标悬停事件
                    AddButtonHoverEvents(warningYesButton);
                    
                    warningYesButton.onClick.AddListener(() => {
                        SetSelectedButton(warningYesButton);
                        OnWarningYesButtonClicked();
                    });
                }
                else
                {
                    Debug.LogWarning("警告确认按钮组件不存在");
                }
            }
            else
            {
                Debug.LogWarning("无法找到警告确认按钮");
            }
            
            if (noButtonTransform != null)
            {
                warningNoButton = noButtonTransform.GetComponent<Button>();
                if (warningNoButton != null)
                {
                    // 添加鼠标悬停事件
                    AddButtonHoverEvents(warningNoButton);
                    
                    warningNoButton.onClick.AddListener(() => {
                        SetSelectedButton(warningNoButton);
                        CloseWarningMessage();
                    });
                }
                else
                {
                    Debug.LogWarning("警告取消按钮组件不存在");
                }
            }
            else
            {
                Debug.LogWarning("无法找到警告取消按钮");
            }
        }
        
        // 绑定选项按钮
        if (optionsButton != null)
        {
            // 添加鼠标悬停事件
            AddButtonHoverEvents(optionsButton);
            
            optionsButton.onClick.AddListener(() => {
                SetSelectedButton(optionsButton);
                ShowOptionsPanel();
            });
        }
        
        // 绑定选项面板关闭按钮
        if (optionsCloseButton != null)
        {
            // 添加鼠标悬停事件
            AddButtonHoverEvents(optionsCloseButton);
            
            optionsCloseButton.onClick.AddListener(() => {
                SetSelectedButton(optionsCloseButton);
                CloseOptionsPanel();
            });
        }
        
        // 绑定退出游戏按钮
        if (exitButton != null)
        {
            // 添加鼠标悬停事件
            AddButtonHoverEvents(exitButton);
            
            exitButton.onClick.AddListener(() => {
                SetSelectedButton(exitButton);
                ShowExitGameWarning();
            });
        }
    }
    
    // 处理警告确认按钮点击事件
    private void OnWarningYesButtonClicked()
    {
        switch (currentWarningType)
        {
            case WarningType.NewGame:
                StartNewGame();
                break;
            case WarningType.ExitGame:
                ExitGame();
                break;
            default:
                CloseWarningMessage();
                break;
        }
    }
    
    // 显示选项面板
    private void ShowOptionsPanel()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
        }
    }
    
    // 关闭选项面板
    private void CloseOptionsPanel()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(false);
            
            // 保存音量设置
            SaveVolumeSettings();
        }
    }
    
    // 音乐音量改变事件
    private void OnMusicVolumeChanged(float volume)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(volume);
            volumeSettings.musicVolume = volume;
        }
    }
    
    // 音效音量改变事件
    private void OnSFXVolumeChanged(float volume)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(volume);
            volumeSettings.sfxVolume = volume;

        }
    }
    
    // 加载音量设置
    private void LoadVolumeSettings()
    {
        if (File.Exists(volumeSettingsPath))
        {
            string json = File.ReadAllText(volumeSettingsPath);
            volumeSettings = JsonUtility.FromJson<VolumeSettings>(json);
        }
    }
    
    // 保存音量设置
    private void SaveVolumeSettings()
    {
        string json = JsonUtility.ToJson(volumeSettings);
        File.WriteAllText(volumeSettingsPath, json);
        Debug.Log("已保存音量设置");
    }
    
    // 显示新游戏警告信息
    private void ShowNewGameWarning()
    {
        currentWarningType = WarningType.NewGame;
        ShowWarningMessage("开始新游戏将删除所有已有存档数据，确定要继续吗？");
    }
    
    // 显示退出游戏警告信息
    private void ShowExitGameWarning()
    {
        currentWarningType = WarningType.ExitGame;
        ShowWarningMessage("确定要退出游戏吗？");
    }
    
    // 显示警告信息
    private void ShowWarningMessage(string message)
    {
        if (warningPanel != null)
        {
            warningPanel.SetActive(true);
            
            if (warningText != null)
            {
                warningText.text = message;
            }
        }
    }
    
    // 关闭警告信息
    private void CloseWarningMessage()
    {
        if (warningPanel != null)
        {
            warningPanel.SetActive(false);
            currentWarningType = WarningType.None;
        }
    }
    
    // 开始新游戏
    private void StartNewGame()
    {
        // 关闭警告面板
        if (warningPanel != null && warningPanel.activeSelf)
        {
            warningPanel.SetActive(false);
        }
        
        // 隐藏开始界面
        HideStartUI();
        
        // 启用交互
        if (PlantInteraction.Instance != null)
        {
            PlantInteraction.Instance.EnableInteraction();
        }
        
        // 调用LevelSelectUI创建新存档并加载第一关
        if (LevelSelectUI.Instance != null)
        {
            LevelSelectUI.Instance.CreateNewSaveData();
            LevelSelectUI.Instance.LoadLevel(0);
        }
        else
        {
            Debug.LogError("找不到LevelSelectUI实例");
        }
    }
    
    // 继续游戏
    private void ContinueGame()
    {
        
        // 隐藏开始界面
        HideStartUI();
        
        // 启用交互
        if (PlantInteraction.Instance != null)
        {
            PlantInteraction.Instance.EnableInteraction();
        }
        
        // 调用LevelSelectUI加载最后玩过的关卡
        if (LevelSelectUI.Instance != null)
        {
            LevelSelectUI.Instance.LoadLastPlayedLevel();
        }
        else
        {
            Debug.LogError("找不到LevelSelectUI实例");
        }
    }
    
    // 退出游戏
    private void ExitGame()
    {
        Debug.Log("退出游戏");
        
        // 保存设置
        SaveVolumeSettings();
        
        // 在编辑器中
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        // 在实际游戏中
        Application.Quit();
        #endif
    }

    // 公共方法：保存当前游戏并显示开始界面
    public void SaveGameAndShowUI()
    {        
        // 先保存当前游戏
        if (LevelSelectUI.Instance != null)
        {
            // 保存当前关卡状态
            LevelSelectUI.Instance.SaveCurrentLevel();
            Debug.Log("已保存当前游戏状态");
        }
        
        // 重置当前选中的按钮
        if (currentSelectedButton != null)
        {
            SetButtonColor(currentSelectedButton, normalButtonColor);
            currentSelectedButton = null;
        }
        
        // 确保所有按钮都恢复为正常颜色
        InitializeButtonColors();
        
        // 再显示开始界面
        ShowStartUI();
    }

    // Update is called once per frame
    void Update()
    {
        // 当按下Esc键时调用保存游戏并显示开始界面的方法
        if (Input.GetKeyDown(KeyCode.Escape) && !isUIActive)
        {
            SaveGameAndShowUI();
        }
    }

    // 公共方法：显示开始UI
    public void ShowStartUI()
    {
        if (!isUIActive && startUIPanel != null)
        {
            isUIActive = true;
            startUIPanel.SetActive(true);
            
            // 禁用交互
            if (PlantInteraction.Instance != null)
            {
                PlantInteraction.Instance.DisableInteraction();
            }
            
            // 触发UI显示事件
            OnStartUIVisibilityChanged?.Invoke(true);
        }
    }
    
    // 公共方法：隐藏开始UI
    public void HideStartUI()
    {
        if (isUIActive && startUIPanel != null)
        {
            isUIActive = false;
            startUIPanel.SetActive(false);
            
            // 启用交互
            if (PlantInteraction.Instance != null)
            {
                PlantInteraction.Instance.EnableInteraction();
            }
            
            // 触发UI隐藏事件
            OnStartUIVisibilityChanged?.Invoke(false);
        }
    }
}
