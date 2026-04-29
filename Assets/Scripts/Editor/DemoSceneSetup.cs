using UnityEngine;
using UnityEditor;
using SpaceFeedback;

/// <summary>
/// 一键初始化 Demo 场景
/// 已适配 ExperienceManager 架构
/// </summary>
public class DemoSceneSetup
{
    [MenuItem("Tools/初始化 AprilTag Demo 场景")]
    public static void SetupDemoScene()
    {
        // 1. 创建主控制器
        GameObject controllerObj = GameObject.Find("DemoController");
        if (controllerObj == null)
        {
            controllerObj = new GameObject("DemoController");
            Debug.Log("已创建 DemoController");
        }

        // 添加组件
        DemoController controller = controllerObj.GetComponent<DemoController>();
        if (controller == null)
            controller = controllerObj.AddComponent<DemoController>();
        PlayerStateTracker tracker = controllerObj.GetComponent<PlayerStateTracker>();
        if (tracker == null)
            tracker = controllerObj.AddComponent<PlayerStateTracker>();
        ExperienceManager experienceManager = controllerObj.GetComponent<ExperienceManager>();
        if (experienceManager == null)
            experienceManager = controllerObj.AddComponent<ExperienceManager>();
        DemoMemoryLogger logger = controllerObj.GetComponent<DemoMemoryLogger>();
        if (logger == null)
            logger = controllerObj.AddComponent<DemoMemoryLogger>();

        // 关联 ExperienceManager
        controller.experienceManager = experienceManager;

        // 2. 创建灯光控制器
        GameObject lightingObj = GameObject.Find("LightingController");
        if (lightingObj == null)
        {
            lightingObj = new GameObject("LightingController");
            Debug.Log("已创建 LightingController");
        }
        LightingController lighting = lightingObj.GetComponent<LightingController>();
        if (lighting == null)
            lighting = lightingObj.AddComponent<LightingController>();

        // 3. 创建后处理控制器
        GameObject postObj = GameObject.Find("PostProcessing");
        if (postObj == null)
        {
            postObj = new GameObject("PostProcessing");
            Debug.Log("已创建 PostProcessing");
        }
        PostProcessingController postProcessing = postObj.GetComponent<PostProcessingController>();
        if (postProcessing == null)
            postProcessing = postObj.AddComponent<PostProcessingController>();

        // 4. 创建音频控制器
        GameObject audioObj = GameObject.Find("AudioController");
        if (audioObj == null)
        {
            audioObj = new GameObject("AudioController");
            Debug.Log("已创建 AudioController");
        }
        AudioController audio = audioObj.GetComponent<AudioController>();
        if (audio == null)
            audio = audioObj.AddComponent<AudioController>();

        // 5. 创建三个触发区
        CreateZone("Zone_WallTrace", new Vector3(-5f, 1f, 0f), new Vector3(2f, 2f, 2f), Color.yellow, controller, 0);
        CreateZone("Zone_DoorNote", new Vector3(0f, 1f, 3f), new Vector3(2f, 2f, 2f), Color.cyan, controller, 1);
        CreateZone("Zone_LightView", new Vector3(5f, 2f, -3f), new Vector3(3f, 3f, 3f), Color.magenta, controller, 2);

        // 6. 设置相机引用
        if (tracker.xrCamera == null)
            tracker.xrCamera = Camera.main;
        if (tracker.playerRoot == null)
            tracker.playerRoot = controllerObj.transform;

        // 7. 创建主灯光并关联
        Light mainLight = GameObject.FindObjectOfType<Light>();
        if (mainLight == null)
        {
            GameObject lightObj = new GameObject("MainLight");
            mainLight = lightObj.AddComponent<Light>();
            mainLight.type = LightType.Directional;
            mainLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
        lighting.mainLight = mainLight;

        // 8. 创建环境音 AudioSource
        if (audio.ambientSource == null)
        {
            AudioSource ambient = audioObj.AddComponent<AudioSource>();
            ambient.loop = true;
            ambient.playOnAwake = true;
            ambient.volume = 0.5f;
            audio.ambientSource = ambient;
        }
        if (audio.uiSource == null)
        {
            AudioSource uiAudio = audioObj.AddComponent<AudioSource>();
            audio.uiSource = uiAudio;
        }

        Selection.activeGameObject = controllerObj;
        Debug.Log("===========================================");
        Debug.Log("Demo 场景初始化完成！");
        Debug.Log("请将三个触发区拖放到场景中合适的位置");
        Debug.Log("===========================================");
    }

    static void CreateZone(string name, Vector3 pos, Vector3 size, Color color, DemoController controller, int index)
    {
        GameObject zoneObj = GameObject.Find(name);
        if (zoneObj == null)
        {
            zoneObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zoneObj.name = name;
            zoneObj.transform.position = pos;
            zoneObj.transform.localScale = size;

            // 移除渲染，保留碰撞
            Renderer r = zoneObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.enabled = false;
            }

            // 添加触发器
            BoxCollider col = zoneObj.GetComponent<BoxCollider>();
            col.isTrigger = true;

            // 添加 ZoneTrigger
            ZoneTrigger trigger = zoneObj.AddComponent<ZoneTrigger>();
            trigger.zoneName = name;
            trigger.gizmoColor = color;
            trigger.stayDuration = 1f;

            // 关联到控制器
            switch (index)
            {
                case 0: controller.zoneWallTrace = trigger; break;
                case 1: controller.zoneDoorNote = trigger; break;
                case 2: controller.zoneLightView = trigger; break;
            }

            Debug.Log($"已创建: {name} at {pos}");
        }
    }
}
