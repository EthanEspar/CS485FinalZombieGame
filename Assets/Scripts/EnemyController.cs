using UnityEngine;
using System.Collections.Generic;

public class EnemyController : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private string enemyPrefabFolderPath = "Assets/Prefabs";
    [SerializeField] private int maxAliveEnemies = 5;
    [SerializeField] private float respawnDelaySeconds = 5f;
    [SerializeField] private string spawnPointTag = "SpawnPoint";
    [SerializeField] private float spawnHeightOffset = 0.5f;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private readonly Queue<float> scheduledRespawns = new Queue<float>();
    private readonly Dictionary<GameObject, int> enemySpawnIndices = new Dictionary<GameObject, int>();
    private readonly HashSet<int> occupiedSpawnIndices = new HashSet<int>();
    private Transform[] spawnPoints;
    [SerializeField] private bool autoConfigureZombieAi = true;
    [SerializeField] private GameObject[] enemyPrefabs = new GameObject[0];

    public GameObject[] EnemyPrefabs => enemyPrefabs;
    public string SpawnPointTag => spawnPointTag;
    public float SpawnHeightOffset => spawnHeightOffset;
    public int MaxAliveEnemies => maxAliveEnemies;

    private void Awake()
    {
        GameObject[] taggedSpawnPoints;
        try
        {
            taggedSpawnPoints = GameObject.FindGameObjectsWithTag(spawnPointTag);
        }
        catch (UnityException)
        {
            taggedSpawnPoints = new GameObject[0];
        }

        spawnPoints = new Transform[taggedSpawnPoints.Length];

        for (int i = 0; i < taggedSpawnPoints.Length; i++)
        {
            spawnPoints[i] = taggedSpawnPoints[i].transform;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(enemyPrefabFolderPath))
        {
            enemyPrefabs = new GameObject[0];
            return;
        }

        string[] prefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { enemyPrefabFolderPath });
        List<GameObject> zombiePrefabs = new List<GameObject>();

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null && prefab.GetComponent<ZombieHealth>() != null)
            {
                zombiePrefabs.Add(prefab);
            }
        }

        enemyPrefabs = zombiePrefabs.ToArray();
    }
#endif

    private void Update()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
            {
                activeEnemies.RemoveAt(i);
                scheduledRespawns.Enqueue(Time.time + respawnDelaySeconds);
            }
        }

        if (enemySpawnIndices.Count > 0)
        {
            List<GameObject> removedEnemies = null;
            foreach (KeyValuePair<GameObject, int> pair in enemySpawnIndices)
            {
                if (pair.Key != null)
                {
                    continue;
                }

                occupiedSpawnIndices.Remove(pair.Value);
                removedEnemies ??= new List<GameObject>();
                removedEnemies.Add(pair.Key);
            }

            if (removedEnemies != null)
            {
                for (int i = 0; i < removedEnemies.Count; i++)
                {
                    enemySpawnIndices.Remove(removedEnemies[i]);
                }
            }
        }

        while (activeEnemies.Count < maxAliveEnemies && scheduledRespawns.Count > 0 && scheduledRespawns.Peek() <= Time.time)
        {
            if (!SpawnEnemy())
            {
                break;
            }

            scheduledRespawns.Dequeue();
        }

        while (activeEnemies.Count < maxAliveEnemies && scheduledRespawns.Count == 0)
        {
            if (!SpawnEnemy())
            {
                break;
            }
        }
    }

    private bool SpawnEnemy()
    {
        GameObject prefabToSpawn = GetRandomEnemyPrefab();
        if (prefabToSpawn == null)
        {
            return false;
        }

        Transform spawnPoint = GetAvailableSpawnPoint(out int spawnIndex);
        if (spawnPoint == null)
        {
            return false;
        }

        Vector3 spawnPosition = GetSpawnPosition(spawnPoint);
        Quaternion spawnRotation = spawnPoint.rotation;

        GameObject spawnedEnemy = Instantiate(prefabToSpawn, spawnPosition, spawnRotation);
        ConfigureZombieAi(spawnedEnemy);
        activeEnemies.Add(spawnedEnemy);
        enemySpawnIndices[spawnedEnemy] = spawnIndex;
        occupiedSpawnIndices.Add(spawnIndex);
        return true;
    }

    private GameObject GetRandomEnemyPrefab()
    {
        if (enemyPrefabs != null && enemyPrefabs.Length > 0)
        {
            int randomIndex = Random.Range(0, enemyPrefabs.Length);
            return enemyPrefabs[randomIndex];
        }

        return enemyPrefab;
    }

    private Transform GetAvailableSpawnPoint(out int spawnIndex)
    {
        spawnIndex = -1;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        List<int> availableIndices = new List<int>();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (!occupiedSpawnIndices.Contains(i))
            {
                availableIndices.Add(i);
            }
        }

        if (availableIndices.Count == 0)
        {
            return null;
        }

        int randomListIndex = Random.Range(0, availableIndices.Count);
        spawnIndex = availableIndices[randomListIndex];
        return spawnPoints[spawnIndex];
    }

    private Vector3 GetSpawnPosition(Transform spawnPoint)
    {
        Vector3 position = spawnPoint.position;
        Collider spawnCollider = spawnPoint.GetComponent<Collider>();

        if (spawnCollider != null)
        {
            position.y = spawnCollider.bounds.max.y;
        }

        return position + Vector3.up * spawnHeightOffset;
    }

    private void ConfigureZombieAi(GameObject enemy)
    {
        if (!autoConfigureZombieAi)
        {
            return;
        }

        if (enemy.GetComponent<ZombieAiController>() == null)
        {
            enemy.AddComponent<ZombieAiController>();
        }
    }
}
