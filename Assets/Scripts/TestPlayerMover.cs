using UnityEngine;

public class TestPlayerMover : MonoBehaviour
{
    public float speed = 5f;
    public float height = 1.6f;

    void Update()
    {
        float h = Input.GetAxis("Horizontal") * speed * Time.deltaTime;
        float v = Input.GetAxis("Vertical") * speed * Time.deltaTime;
        transform.Translate(h, 0, v);

        if (transform.position.y != height)
            transform.position = new Vector3(transform.position.x, height, transform.position.z);

        if (Input.GetKeyDown(KeyCode.P))
            Debug.Log($"当前位置: {transform.position}");
    }
}
