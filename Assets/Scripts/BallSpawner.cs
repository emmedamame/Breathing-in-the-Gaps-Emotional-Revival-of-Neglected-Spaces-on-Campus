using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject ballPrefab;

    [Header("Spawn")]
    [SerializeField] private float initialSpawnDelaySeconds = 1f;
    [SerializeField] private float spawnIntervalSeconds = 0.5f;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector3 spawnExtents = new Vector3(0.2f, 0.2f, 0.2f);

    [Header("Ball Lifetime (optional)")]
    [SerializeField] private float autoDestroyAfterSeconds = 15f;

    [Header("Random Color")]
    [SerializeField] private bool randomizeColor = true;
    [SerializeField] private Gradient colorGradient = DefaultGradient();

    [Header("Random Scale")]
    [SerializeField] private bool randomizeScale = true;
    [SerializeField] private Vector2 uniformScaleRange = new Vector2(0.06f, 0.14f);

    private Coroutine _spawnRoutine;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Reset()
    {
        spawnPoint = transform;
        initialSpawnDelaySeconds = 1f;
        spawnIntervalSeconds = 0.5f;
        spawnExtents = new Vector3(0.2f, 0.2f, 0.2f);
        autoDestroyAfterSeconds = 15f;
        randomizeColor = true;
        randomizeScale = true;
        uniformScaleRange = new Vector2(0.06f, 0.14f);
    }

    private void OnEnable()
    {
        _spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }

    private System.Collections.IEnumerator SpawnLoop()
    {
        if (initialSpawnDelaySeconds > 0f)
            yield return new WaitForSeconds(initialSpawnDelaySeconds);

        while (enabled)
        {
            if (ballPrefab != null)
                SpawnOne();

            yield return new WaitForSeconds(Mathf.Max(0.01f, spawnIntervalSeconds));
        }
    }

    private void SpawnOne()
    {
        if (spawnPoint == null) spawnPoint = transform;

        var localOffset = new Vector3(
            Random.Range(-spawnExtents.x, spawnExtents.x),
            Random.Range(-spawnExtents.y, spawnExtents.y),
            Random.Range(-spawnExtents.z, spawnExtents.z)
        );
        var pos = spawnPoint.TransformPoint(localOffset);
        var rot = spawnPoint.rotation;

        var ball = Instantiate(ballPrefab, pos, rot);
        if (ball.GetComponent<BallEliminationReporter>() == null)
            ball.AddComponent<BallEliminationReporter>();

        if (randomizeScale)
        {
            var min = Mathf.Min(uniformScaleRange.x, uniformScaleRange.y);
            var max = Mathf.Max(uniformScaleRange.x, uniformScaleRange.y);
            var s = Random.Range(min, max);
            ball.transform.localScale = Vector3.one * s;
        }

        if (randomizeColor)
        {
            var color = colorGradient.Evaluate(Random.value);
            ApplyColor(ball, color);
        }

        if (autoDestroyAfterSeconds > 0f)
            Destroy(ball, autoDestroyAfterSeconds);
    }

    private static void ApplyColor(GameObject go, Color color)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var mat = r.material;
            if (mat == null) continue;

            if (mat.HasProperty(BaseColor))
                mat.SetColor(BaseColor, color);
            else if (mat.HasProperty(ColorId))
                mat.SetColor(ColorId, color);
        }
    }

    private static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.red, 0f),
                new GradientColorKey(Color.yellow, 0.2f),
                new GradientColorKey(Color.green, 0.4f),
                new GradientColorKey(Color.cyan, 0.6f),
                new GradientColorKey(Color.blue, 0.8f),
                new GradientColorKey(Color.magenta, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            }
        );
        return g;
    }
}

