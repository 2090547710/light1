using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;

public class PngSequencePlayer : MonoBehaviour
{
    // 材质对象
    private Material pngMaterial;
    // 存储PNG序列的列表
    public List<Texture2D> pngs = new List<Texture2D>();
    // 当前帧索引
    public int currentFrame = 0;
    // 播放速度 (帧/秒)
    public float frameRate = 30;
    
    public float timer = 0;
    
    // PNG序列文件夹路径
    public string pngFolderPath = "你的PNG序列文件夹";
    
    // 动画完成一次循环的事件
    public event Action OnAnimationLooped;
    // 记录上一帧
    private int previousFrame = 0;
    
    // Start is called before the first frame update
    void Start()
    {
        timer = 0;
        
        // 获取渲染器的材质
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        pngMaterial = renderer.material;
        
        // 加载PNG序列（这里以从Resources文件夹加载为例）
        // 你也可以从其他位置加载图片
        UnityEngine.Object[] textures = Resources.LoadAll(pngFolderPath, typeof(Texture2D));
        foreach (UnityEngine.Object tex in textures)
        {
            pngs.Add((Texture2D)tex);
        }
        pngMaterial.mainTexture = pngs[0];
        previousFrame = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if (pngs.Count > 0)
        {
            // 根据时间更新帧
            timer += Time.deltaTime;
            if (timer >= 1.0f/frameRate)
            {
                // 保存前一帧
                previousFrame = currentFrame;
                // 切换到下一帧
                currentFrame = (currentFrame + 1) % pngs.Count;
                // 更新材质的主贴图
                pngMaterial.mainTexture = pngs[currentFrame];
                timer = 0;
                
                // 检测是否完成一次循环
                if (previousFrame > currentFrame)
                {
                    // 触发动画循环完成事件
                    OnAnimationLooped?.Invoke();
                }
            }
        }
    }
}
