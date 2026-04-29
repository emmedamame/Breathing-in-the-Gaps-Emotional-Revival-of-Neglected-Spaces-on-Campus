using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class AprilTagCubeRotator : MonoBehaviour
{
    [SerializeField] private int listenPort = 5005;
    [SerializeField] private float rotateLerpSpeed = 12f;
    [SerializeField] private Vector3 axisScale = new Vector3(1f, 1f, 1f);

    private UdpClient _udpClient;
    private IPEndPoint _remoteEndPoint;
    private Vector3 _targetEuler;

    private void Start()
    {
        _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        _udpClient = new UdpClient(listenPort);
        _udpClient.Client.ReceiveTimeout = 1;
    }

    private void Update()
    {
        if (_udpClient != null && _udpClient.Available > 0)
        {
            var bytes = _udpClient.Receive(ref _remoteEndPoint);
            var text = Encoding.UTF8.GetString(bytes);
            var parts = text.Split(',');
            if (parts.Length == 3)
            {
                var x = float.Parse(parts[0]) * axisScale.x;
                var y = float.Parse(parts[1]) * axisScale.y;
                var z = float.Parse(parts[2]) * axisScale.z;
                _targetEuler = new Vector3(x, y, z);
            }
        }

        var target = Quaternion.Euler(_targetEuler);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            target,
            rotateLerpSpeed * Time.deltaTime
        );
    }

    private void OnDestroy()
    {
        if (_udpClient != null)
        {
            _udpClient.Close();
            _udpClient = null;
        }
    }
}
