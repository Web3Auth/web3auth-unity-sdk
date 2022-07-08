using UnityEngine;

public class CubeRotater : MonoBehaviour
{
    public float speed = 30f;

    void Update()
    {
        transform.Rotate(speed * Time.deltaTime, 2 * speed * Time.deltaTime, -speed * Time.deltaTime);
    }
}
