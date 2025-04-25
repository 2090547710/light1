using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    // 波浪动画Quad预制体
    public GameObject wavePrefab;
    // 海面Quad
    public Transform seaQuad;
    // 要生成的波浪数量
    public int waveCount = 10;
    // 波浪生成间隔范围（秒）
    public Vector2 waveSpawnTimeRange = new Vector2(0.5f, 2.0f);
    
    // 波浪的父对象
    private Transform waveParent;
    // 对象池
    private List<GameObject> wavePool = new List<GameObject>();
    // 存储每个波浪的PngSequencePlayer组件
    private List<PngSequencePlayer> waveAnimators = new List<PngSequencePlayer>();
    
    // Start is called before the first frame update
    void Start()
    {
        // 在当前物体的子物体中查找layer=9的物体作为seaQuad
        foreach (Transform child in transform)
        {
            if (child.gameObject.layer == 9)
            {
                seaQuad = child;
                break;
            }
        }
        
        // 创建波浪父对象
        GameObject waveParentObj = new GameObject("WaveContainer");
        waveParent = waveParentObj.transform;
        waveParent.SetParent(transform);
        waveParent.localPosition = Vector3.zero;
        
        // 创建对象池
        CreateWavePool();
        
        // 改为逐渐生成波浪而不是一次性全部显示
        StartCoroutine(SpawnWavesOverTime());
    }
    
    void CreateWavePool()
    {
        for (int i = 0; i < waveCount; i++)
        {
            GameObject wave = Instantiate(wavePrefab);
            wave.SetActive(false);
            // 设置父对象为专用容器而不是seaQuad
            wave.transform.SetParent(waveParent);
            wavePool.Add(wave);
            
            // 获取PngSequencePlayer组件并添加到列表
            PngSequencePlayer animator = wave.GetComponent<PngSequencePlayer>();
            if (animator != null)
            {
                waveAnimators.Add(animator);
                // 确保材质正确初始化，避免第一次显示问题
                MeshRenderer rendererComponent = wave.GetComponent<MeshRenderer>();
                Material material = rendererComponent.material;
                if (animator.pngs.Count > 0)
                {
                    material.mainTexture = animator.pngs[0];
                }
                // 订阅动画循环完成事件
                animator.OnAnimationLooped += () => RepositionWave(wave);
            }
            
            // 确保所有波浪使用同一材质（支持GPU Instancing）
            MeshRenderer renderer = wave.GetComponent<MeshRenderer>();
            renderer.material.enableInstancing = true;
        }
    }
    
    IEnumerator SpawnWavesOverTime()
    {
        if (seaQuad == null) yield break;
        
        // 获取海面的尺寸
        MeshRenderer seaRenderer = seaQuad.GetComponent<MeshRenderer>();
        Vector3 seaSize = seaRenderer.bounds.size;
        
        foreach (GameObject wave in wavePool)
        {
            // 随机位置（使用局部坐标）
            float randomX = Random.Range(-seaSize.x/2, seaSize.x/2);
            float randomZ = Random.Range(-seaSize.z/2, seaSize.z/2);
            wave.transform.localPosition = new Vector3(randomX, 0.01f, randomZ);
            
            // 确保波浪的PngSequencePlayer已经初始化
            PngSequencePlayer animator = wave.GetComponent<PngSequencePlayer>();
            if (animator != null)
            {
                animator.currentFrame = 0;
                if (animator.pngs.Count > 0)
                {
                    MeshRenderer renderer = wave.GetComponent<MeshRenderer>();
                    renderer.material.mainTexture = animator.pngs[0];
                }
            }
            
            wave.SetActive(true);
            
            // 随机等待时间
            float waitTime = Random.Range(waveSpawnTimeRange.x, waveSpawnTimeRange.y);
            yield return new WaitForSeconds(waitTime);
        }
    }
    
    void PositionWaves()
    {
        // 此方法现在由SpawnWavesOverTime协程替代
        // 保留此方法以便需要时立即放置所有波浪
        if (seaQuad == null) return;
        
        // 获取海面的尺寸
        MeshRenderer seaRenderer = seaQuad.GetComponent<MeshRenderer>();
        Vector3 seaSize = seaRenderer.bounds.size;
        
        foreach (GameObject wave in wavePool)
        {
            // 随机位置（使用局部坐标）
            float randomX = Random.Range(-seaSize.x/2, seaSize.x/2);
            float randomZ = Random.Range(-seaSize.z/2, seaSize.z/2);
            wave.transform.localPosition = new Vector3(randomX, 0.01f, randomZ);
            
            wave.SetActive(true);
        }
    }
    
    // 重新定位单个波浪
    void RepositionWave(GameObject wave)
    {
        if (seaQuad == null) return;
        
        // 获取海面的尺寸
        MeshRenderer seaRenderer = seaQuad.GetComponent<MeshRenderer>();
        Vector3 seaSize = seaRenderer.bounds.size;
        
        // 随机位置（使用局部坐标）
        float randomX = Random.Range(-seaSize.x/2, seaSize.x/2);
        float randomZ = Random.Range(-seaSize.z/2, seaSize.z/2);
        wave.transform.localPosition = new Vector3(randomX, 0.1f, randomZ);
        
       

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
