using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 5f;
    private Vector3 moveDir;

    public void Init(Vector3 direction)
    {
        direction.y = 0;
        moveDir = direction.normalized;

        // 【新增逻辑】在生成瞬间，让物体面朝移动方向
        if (moveDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDir);
        }
    }

    void Update()
    {
        // 保持你原有的逻辑，不做任何变动
        transform.position += moveDir * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("wall"))
        {
            Destroy(gameObject); //destroy the bullet
        }
    }
}
