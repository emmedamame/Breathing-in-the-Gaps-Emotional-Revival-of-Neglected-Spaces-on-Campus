using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class AprilTagUdpReceiver : MonoBehaviour
{
    [Header("UDP Settings")]
    public int listenPort = 5052;

    [Header("Target Object")]
    public Transform targetObject;

    [Header("Follow Settings")]
    public float positionLerpSpeed = 10f;
    public float rotationLerpSpeed = 10f;
    public bool followRotation = true;

    [Header("Position Offset")]
    public Vector3 positionOffset = Vector3.zero;

    [Header("Position Limit")]
    public bool clampPosition = true;
    public Vector3 minPosition = new Vector3(-5f, -5f, -5f);
    public Vector3 maxPosition = new Vector3(5f, 5f, 5f);

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool running;

    private readonly object dataLock = new object();

    private bool tagVisible;
    private Vector3 receivedPosition;
    private Vector3 receivedEuler;

    private Vector3 initialTargetPosition;

    [Serializable]
    private class AprilTagPacket
    {
        public bool visible;
        public int id;
        public float x;
        public float y;
        public float z;
        public float rx;
        public float ry;
        public float rz;
    }

    private void Start()
    {
        if (targetObject == null)
            targetObject = transform;

        initialTargetPosition = targetObject.position;
        receivedPosition = initialTargetPosition;
        receivedEuler = targetObject.eulerAngles;

        StartUdpReceiver();
    }

    private void StartUdpReceiver()
    {
        try
        {
            udpClient = new UdpClient(listenPort);
            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log("AprilTag UDP Receiver started. Listening on port: " + listenPort);
        }
        catch (Exception e)
        {
            Debug.LogError("AprilTag UDP Receiver start failed: " + e.Message);
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(data);

                AprilTagPacket packet = JsonUtility.FromJson<AprilTagPacket>(json);

                lock (dataLock)
                {
                    tagVisible = packet.visible;

                    if (packet.visible)
                    {
                        Vector3 posFromTag = new Vector3(packet.x, packet.y, packet.z);
                        Vector3 finalPosition = initialTargetPosition + posFromTag + positionOffset;

                        if (clampPosition)
                        {
                            finalPosition.x = Mathf.Clamp(finalPosition.x, minPosition.x, maxPosition.x);
                            finalPosition.y = Mathf.Clamp(finalPosition.y, minPosition.y, maxPosition.y);
                            finalPosition.z = Mathf.Clamp(finalPosition.z, minPosition.z, maxPosition.z);
                        }

                        receivedPosition = finalPosition;
                        receivedEuler = new Vector3(packet.rx, packet.ry, packet.rz);
                    }
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            catch (Exception e)
            {
                Debug.LogWarning("AprilTag UDP receive error: " + e.Message);
            }
        }
    }

    private void Update()
    {
        if (targetObject == null)
            return;

        bool visible;
        Vector3 targetPos;
        Vector3 targetEuler;

        lock (dataLock)
        {
            visible = tagVisible;
            targetPos = receivedPosition;
            targetEuler = receivedEuler;
        }

        if (!visible)
            return;

        targetObject.position = Vector3.Lerp(
            targetObject.position,
            targetPos,
            Time.deltaTime * positionLerpSpeed
        );

        if (followRotation)
        {
            Quaternion targetRotation = Quaternion.Euler(targetEuler);

            targetObject.rotation = Quaternion.Slerp(
                targetObject.rotation,
                targetRotation,
                Time.deltaTime * rotationLerpSpeed
            );
        }
    }

    private void OnApplicationQuit()
    {
        StopUdpReceiver();
    }

    private void OnDestroy()
    {
        StopUdpReceiver();
    }

    private void StopUdpReceiver()
    {
        running = false;

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }

        if (receiveThread != null)
        {
            if (receiveThread.IsAlive)
                receiveThread.Join(200);

            receiveThread = null;
        }
    }
}
