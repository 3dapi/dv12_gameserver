using UnityEngine;

public class Mover : MonoBehaviour
{
    [SerializeField] float speed = 4f;      // 초당 이동 거리(미터)

    void Update()
    {
        Vector2 dir = SimpleInput.Move();
        Vector3 move = new Vector3(dir.x, 0f, dir.y) * (speed * Time.deltaTime);
        transform.position += move;
    }
}
