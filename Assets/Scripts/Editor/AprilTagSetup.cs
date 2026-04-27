using UnityEngine;
using UnityEditor;

public class AprilTagSetup
{
    [MenuItem("Tools/Setup AprilTag Scene")]
    public static void Setup()
    {
        // 创建 AprilTagReceiver
        GameObject receiver = GameObject.Find("AprilTagReceiver");
        if (receiver == null)
        {
            receiver = new GameObject("AprilTagReceiver");
            Debug.Log("已创建 AprilTagReceiver");
        }

        // 挂载脚本
        AprilTagUdpReceiver udp = receiver.GetComponent<AprilTagUdpReceiver>();
        if (udp == null)
        {
            udp = receiver.AddComponent<AprilTagUdpReceiver>();
            Debug.Log("已添加 AprilTagUdpReceiver");
        }

        // 创建测试 Cube
        GameObject cube = GameObject.Find("AprilTagCube");
        if (cube == null)
        {
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "AprilTagCube";
            cube.transform.position = new Vector3(0, 1, 0);
            Debug.Log("已创建测试 Cube");
        }

        // 关联 Target Object
        udp.targetObject = cube.transform;

        Selection.activeGameObject = receiver;
        Debug.Log("AprilTag 场景设置完成！请按 Play 运行。");
    }
}
