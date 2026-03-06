using UnityEngine;

public class BulletSpawn : MonoBehaviour
{
    public GameObject bulletPrefab;
    
    // 设置随机范围
    public float minInterval = 1f;
    public float maxInterval = 3f;

    private float timer = 0f;
    private float currentInterval; // 当前这次等待的时间
    private GameObject player;

    void Start()
    {
        player = null;
        // 初始化第一个随机间隔
        currentInterval = Random.Range(minInterval, maxInterval);
    }

    void Update()
    {
        if (player == null)
        {
            player = GameObject.FindWithTag("Player");
            return;
        }

        timer += Time.deltaTime;
        
        // 使用动态的 currentInterval 而不是固定的 spawnInterval
        if (timer >= currentInterval)
        {
            ShootAtPlayer();
        }
    }

    void ShootAtPlayer()
    {
        if (player == null)
        {
            player = GameObject.FindWithTag("Player");
        }

        if (player != null)
        {
            Vector3 direction = (player.transform.position - transform.position).normalized;
            GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            bullet.GetComponent<Bullet>().Init(direction);
        }

        // 核心逻辑：射击后重置计时器并获取下一个随机间隔
        timer = 0f;
        currentInterval = Random.Range(minInterval, maxInterval);
    }
}
