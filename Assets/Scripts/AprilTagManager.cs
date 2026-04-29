using UnityEngine;

public class AprilTagManager : MonoBehaviour
{
    public static AprilTagManager Instance { get; private set; }

    [Header("Components")]
    public AprilTagUdpReceiver udpReceiver;
    public Transform targetObject;

    [Header("Auto Setup")]
    public bool autoSetupOnStart = true;
    public GameObject targetPrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (autoSetupOnStart)
        {
            AutoSetup();
        }
    }

    public void AutoSetup()
    {
        // 创建 AprilTagReceiver
        GameObject receiver = GameObject.Find("AprilTagReceiver");
        if (receiver == null)
        {
            receiver = new GameObject("AprilTagReceiver");
        }

        udpReceiver = receiver.GetComponent<AprilTagUdpReceiver>();
        if (udpReceiver == null)
        {
            udpReceiver = receiver.AddComponent<AprilTagUdpReceiver>();
        }

        // 创建目标物体
        if (targetObject == null)
        {
            if (targetPrefab != null)
            {
                GameObject obj = Instantiate(targetPrefab, Vector3.up, Quaternion.identity);
                targetObject = obj.transform;
            }
            else
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "AprilTagCube";
                cube.transform.position = Vector3.up;
                targetObject = cube.transform;
            }
        }

        // 关联
        udpReceiver.targetObject = targetObject;

        Debug.Log("AprilTag 自动设置完成！");
    }
}
