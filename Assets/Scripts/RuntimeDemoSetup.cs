using UnityEngine;
using SpaceFeedback;

/// <summary>
/// 运行时自动初始化 Demo 场景（仅在缺失时才创建）
/// </summary>
public class RuntimeDemoSetup : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("运行时自动创建缺失的对象（仅 Editor 使用）")]
    public bool enableAutoSetup = false;

    [Header("触发区颜色")]
    public Color wallTraceColor = Color.yellow;
    public Color doorNoteColor = Color.cyan;
    public Color lightViewColor = Color.magenta;

    [Header("默认位置（仅在创建时使用）")]
    public Vector3 wallTracePos = new Vector3(-8f, 1f, 0f);
    public Vector3 doorNotePos = new Vector3(0f, 1f, 3f);
    public Vector3 lightViewPos = new Vector3(8f, 2f, -5f);

    [Header("区域大小")]
    public Vector3 zoneSize = new Vector3(2.5f, 2.5f, 2.5f);

    private static bool initialized;

    void Awake()
    {
        // 仅在启用时运行
        if (!enableAutoSetup) return;

        // 使用静态变量确保整个游戏生命周期只初始化一次
        if (initialized) return;
        initialized = true;

        Debug.Log("[Setup] 检查并创建缺失的 Demo 组件...");

        // 检查并创建控制器
        EnsureDemoController();

        // 检查并创建灯光控制器
        EnsureLightingController();

        // 检查并创建后处理控制器
        EnsurePostProcessingController();

        // 检查并创建音频控制器
        EnsureAudioController();

        // 检查并创建触发区
        EnsureZones();

        Debug.Log("[Setup] Demo 组件检查完成！");
    }

    void EnsureDemoController()
    {
        var existing = FindObjectOfType<DemoController>();
        if (existing != null)
        {
            Debug.Log("[Setup] DemoController 已存在，跳过创建");
            return;
        }

        GameObject controllerObj = new GameObject("DemoController");
        DemoController controller = controllerObj.AddComponent<DemoController>();
        PlayerStateTracker tracker = controllerObj.AddComponent<PlayerStateTracker>();
        ExperienceManager experienceManager = controllerObj.AddComponent<ExperienceManager>();
        controllerObj.AddComponent<DemoMemoryLogger>();
        controllerObj.AddComponent<XRLocomotionSetup>();

        // 设置相机引用
        if (tracker.xrCamera == null)
            tracker.xrCamera = Camera.main;
        tracker.playerRoot = controllerObj.transform;

        Debug.Log("[Setup] DemoController 创建完成");
    }

    void EnsureLightingController()
    {
        var existing = FindObjectOfType<LightingController>();
        if (existing != null)
        {
            Debug.Log("[Setup] LightingController 已存在，跳过创建");

            // 确保灯光关联
            if (existing.mainLight == null)
            {
                Light existingLight = FindObjectOfType<Light>();
                if (existingLight != null)
                    existing.mainLight = existingLight;
            }
            return;
        }

        GameObject lightingObj = new GameObject("LightingController");
        LightingController lighting = lightingObj.AddComponent<LightingController>();

        Light mainLight = FindObjectOfType<Light>();
        if (mainLight == null)
        {
            GameObject lightObj = new GameObject("MainLight");
            mainLight = lightObj.AddComponent<Light>();
            mainLight.type = LightType.Directional;
            mainLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
        lighting.mainLight = mainLight;

        Debug.Log("[Setup] LightingController 创建完成");
    }

    void EnsurePostProcessingController()
    {
        var existing = FindObjectOfType<PostProcessingController>();
        if (existing != null)
        {
            Debug.Log("[Setup] PostProcessingController 已存在，跳过创建");
            return;
        }

        GameObject postObj = new GameObject("PostProcessing");
        postObj.AddComponent<PostProcessingController>();

        Debug.Log("[Setup] PostProcessingController 创建完成");
    }

    void EnsureAudioController()
    {
        var existing = FindObjectOfType<AudioController>();
        if (existing != null)
        {
            Debug.Log("[Setup] AudioController 已存在，跳过创建");
            return;
        }

        GameObject audioObj = new GameObject("AudioController");
        AudioSource ambient = audioObj.AddComponent<AudioSource>();
        ambient.loop = true;
        ambient.playOnAwake = false;
        ambient.volume = 0.5f;

        AudioController audio = audioObj.AddComponent<AudioController>();
        audio.ambientSource = ambient;
        audio.uiSource = audioObj.AddComponent<AudioSource>();

        Debug.Log("[Setup] AudioController 创建完成");
    }

    void EnsureZones()
    {
        DemoController controller = FindObjectOfType<DemoController>();
        if (controller == null)
        {
            Debug.LogWarning("[Setup] 需要先创建 DemoController，跳过触发区创建");
            return;
        }

        if (controller.zoneWallTrace == null)
            controller.zoneWallTrace = CreateZone("Zone_WallTrace", wallTracePos, zoneSize, wallTraceColor, 0);
        else
            Debug.Log("[Setup] Zone_WallTrace 已存在，跳过");

        if (controller.zoneDoorNote == null)
            controller.zoneDoorNote = CreateZone("Zone_DoorNote", doorNotePos, zoneSize, doorNoteColor, 1);
        else
            Debug.Log("[Setup] Zone_DoorNote 已存在，跳过");

        if (controller.zoneLightView == null)
            controller.zoneLightView = CreateZone("Zone_LightView", lightViewPos, zoneSize, lightViewColor, 2);
        else
            Debug.Log("[Setup] Zone_LightView 已存在，跳过");
    }

    ZoneTrigger CreateZone(string name, Vector3 pos, Vector3 size, Color color, int index)
    {
        GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zone.name = name;
        zone.transform.position = pos;
        zone.transform.localScale = size;

        Renderer r = zone.GetComponent<Renderer>();
        if (r != null) r.enabled = false;

        BoxCollider col = zone.GetComponent<BoxCollider>();
        col.isTrigger = true;

        ZoneTrigger trigger = zone.AddComponent<ZoneTrigger>();
        trigger.zoneName = name;
        trigger.gizmoColor = color;
        trigger.stayDuration = 4f;

        Debug.Log($"[Setup] 已创建: {name}");
        return trigger;
    }
}
