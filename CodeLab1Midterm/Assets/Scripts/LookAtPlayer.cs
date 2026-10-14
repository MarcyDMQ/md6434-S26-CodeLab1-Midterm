using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    private Transform playerTransform;
    
    [Header("设置")]
    public GameObject replacementPrefab; // 在这里拖入你想生成的那个 Prefab

    void Update()
    {
        // 1. 自动看向玩家的逻辑
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (playerTransform != null)
        {
            Vector3 targetPosition = playerTransform.position;
            targetPosition.y = transform.position.y; // 锁定高度
            transform.LookAt(targetPosition);
        }
    }

    // 2. 碰撞检测逻辑
    void OnTriggerEnter(Collider other)
    {
        // 检查是否撞到了标签为 "goat1" 的物体
        if (other.CompareTag("goat1"))
        {
            SpawnReplacementAndDestroy();
        }
    }

    private void SpawnReplacementAndDestroy()
    {
        // 在当前位置生成新物体
        if (replacementPrefab != null)
        {
            Instantiate(replacementPrefab, transform.position, transform.rotation);
        }
        // 2. 触发游戏结束：调用 GameManager 的单例实例
        if (GameManager.instance != null)
        {
            GameManager.instance.GameOver();
        }
        else
        {
            Debug.LogWarning("场景中没有找到 GameManager！");
        }

        // 摧毁自己
        Destroy(gameObject);
    }
}
