using UnityEngine;

public class BulletReverse : MonoBehaviour
{
    public float speed = 2f;
    private Vector3 moveDir;

    public void Init(Vector3 direction)
    {
        direction.y = 0;
        moveDir = direction.normalized;

        if (moveDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDir);
        }
    }

    void Update()
    {
        transform.position += moveDir * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("wall"))
        {
            Destroy(gameObject);
        }
        // --- 新增逻辑：碰到玩家反转方向 ---
        else if (other.CompareTag("Player")) 
        {
            ReverseDirection();
        }
    }

    private void ReverseDirection()
    {
        // 1. 方向取反
        moveDir = -moveDir;

        // 2. 重新调整物体的朝向（让子弹模型也转头）
        if (moveDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDir);
        }

        // 提示：如果你的玩家也有 Collider 并且子弹会立刻再次触发碰撞，
        // 可以在这里稍微把子弹的位置往 moveDir 方向挪一点点。
        // transform.position += moveDir * 0.1f; 
    }
}