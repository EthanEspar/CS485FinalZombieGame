using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    private class ActiveEnemy
    {
        public GameObject enemy;
        public int spawnIndex;
    }

    [Header("Existing Project References")]
    [Tooltip("Assign the existing enemyController object. Its endless spawning will be disabled.")]
    [SerializeField] private EnemyController endlessSpawnerToDisable;
    [SerializeField] private ThrowingCard cardWeapon;
    [SerializeField] private ModifierManager modifierManager;

    [Header("Zombie Spawning")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private string spawnPointTag = "SpawnPoint";
    [SerializeField] private float spawnHeightOffset = 0.5f;
    [SerializeField] private int maxAliveEnemies = 3;
    [SerializeField] private float timeBetweenSpawns = 1f;

    [Header("Round Settings")]
    [SerializeField] private int startingEnemies = 5;
    [SerializeField] private int extraEnemiesPerRound = 2;
    [SerializeField] private float timeBeforeNextRound = 1.5f;
    [SerializeField] private int modifierChoicesPerRound = 3;

    private readonly List<ActiveEnemy> activeEnemies = new List<ActiveEnemy>();
    private readonly HashSet<int> occupiedSpawnIndices = new HashSet<int>();
    private readonly List<ModifierData> currentChoices = new List<ModifierData>();

    private Transform[] spawnPoints;
    private int currentRound;
    private int enemiesThisRound;
    private int enemiesSpawned;
    private int enemiesDefeated;
    private float nextSpawnTime;
    private bool roundRunning;
    private bool choosingModifier;

    public int CurrentRound => currentRound;
    public int EnemiesRemaining => Mathf.Max(0, enemiesThisRound - enemiesDefeated);

    public void AutoConfigure(
        EnemyController enemyController,
        ThrowingCard weapon,
        ModifierManager newModifierManager)
    {
        endlessSpawnerToDisable = enemyController;
        cardWeapon = weapon;
        modifierManager = newModifierManager;

        if (endlessSpawnerToDisable != null)
        {
            enemyPrefabs = endlessSpawnerToDisable.EnemyPrefabs;
            spawnPointTag = endlessSpawnerToDisable.SpawnPointTag;
            spawnHeightOffset = endlessSpawnerToDisable.SpawnHeightOffset;
            maxAliveEnemies = Mathf.Max(1, endlessSpawnerToDisable.MaxAliveEnemies);
            endlessSpawnerToDisable.enabled = false;
        }

        ResolveSpawnPoints();
    }

    private void Awake()
    {
        if (cardWeapon == null)
        {
            cardWeapon = FindAnyObjectByType<ThrowingCard>();
        }

        if (modifierManager == null)
        {
            modifierManager = FindAnyObjectByType<ModifierManager>();
        }

        if (endlessSpawnerToDisable != null)
        {
            endlessSpawnerToDisable.enabled = false;
        }
    }

    private IEnumerator Start()
    {
        // Always begin a new run unpaused. This also handles Play Mode being
        // entered after a modifier screen left Time.timeScale at zero.
        Time.timeScale = 1f;

        yield return null;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            ResolveSpawnPoints();
        }

        yield return new WaitForSecondsRealtime(0.5f);
        StartNextRound();
    }

    private void Update()
    {
        RemoveDefeatedEnemies();

        if (!roundRunning || choosingModifier)
        {
            return;
        }

        if (enemiesSpawned < enemiesThisRound &&
            activeEnemies.Count < maxAliveEnemies &&
            Time.time >= nextSpawnTime)
        {
            if (SpawnEnemy())
            {
                enemiesSpawned++;
                nextSpawnTime = Time.time + timeBetweenSpawns;
            }
        }

        if (enemiesSpawned >= enemiesThisRound && activeEnemies.Count == 0)
        {
            FinishRound();
        }
    }

    private void ResolveSpawnPoints()
    {
        GameObject[] taggedPoints;

        try
        {
            taggedPoints = GameObject.FindGameObjectsWithTag(spawnPointTag);
        }
        catch (UnityException)
        {
            taggedPoints = new GameObject[0];
        }

        spawnPoints = new Transform[taggedPoints.Length];
        for (int i = 0; i < taggedPoints.Length; i++)
        {
            spawnPoints[i] = taggedPoints[i].transform;
        }

        if (spawnPoints.Length > 0)
        {
            maxAliveEnemies = Mathf.Clamp(maxAliveEnemies, 1, spawnPoints.Length);
        }
    }

    private void StartNextRound()
    {
        currentRound++;
        enemiesThisRound = startingEnemies +
            (currentRound - 1) * extraEnemiesPerRound;
        enemiesSpawned = 0;
        enemiesDefeated = 0;
        nextSpawnTime = Time.time;
        roundRunning = true;

        Debug.Log(
            "Round " + currentRound + " started with " +
            enemiesThisRound + " zombies."
        );
    }

    private bool SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("RoundManager needs at least one enemy prefab.");
            return false;
        }

        Transform spawnPoint = GetAvailableSpawnPoint(out int spawnIndex);
        if (spawnPoint == null)
        {
            return false;
        }

        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        if (prefab == null)
        {
            return false;
        }

        Vector3 spawnPosition = spawnPoint.position;
        Collider spawnCollider = spawnPoint.GetComponent<Collider>();
        if (spawnCollider != null)
        {
            spawnPosition.y = spawnCollider.bounds.max.y;
        }

        spawnPosition += Vector3.up * spawnHeightOffset;
        GameObject enemy = Instantiate(prefab, spawnPosition, spawnPoint.rotation);
        ConfigureZombieAi(enemy);

        activeEnemies.Add(new ActiveEnemy
        {
            enemy = enemy,
            spawnIndex = spawnIndex
        });
        occupiedSpawnIndices.Add(spawnIndex);
        return true;
    }

    private Transform GetAvailableSpawnPoint(out int spawnIndex)
    {
        spawnIndex = -1;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("No objects are tagged SpawnPoint.");
            return null;
        }

        List<int> available = new List<int>();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (!occupiedSpawnIndices.Contains(i))
            {
                available.Add(i);
            }
        }

        if (available.Count == 0)
        {
            return null;
        }

        spawnIndex = available[Random.Range(0, available.Count)];
        return spawnPoints[spawnIndex];
    }

    private void ConfigureZombieAi(GameObject enemy)
    {
        if (enemy.GetComponent<ZombieAiController>() == null)
        {
            enemy.AddComponent<ZombieAiController>();
        }
    }

    private void RemoveDefeatedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i].enemy != null)
            {
                continue;
            }

            occupiedSpawnIndices.Remove(activeEnemies[i].spawnIndex);
            activeEnemies.RemoveAt(i);
            enemiesDefeated++;
        }
    }

    private void FinishRound()
    {
        roundRunning = false;
        choosingModifier = true;
        currentChoices.Clear();

        if (modifierManager != null)
        {
            currentChoices.AddRange(
                modifierManager.GetRandomChoices(modifierChoicesPerRound)
            );
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("Round " + currentRound + " cleared. Choose a modifier.");
    }

    private void ChooseModifier(ModifierData choice)
    {
        if (modifierManager != null)
        {
            modifierManager.ApplyModifier(choice);
        }

        choosingModifier = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        StartCoroutine(BeginNextRoundAfterDelay());
    }

    private IEnumerator BeginNextRoundAfterDelay()
    {
        yield return new WaitForSeconds(timeBeforeNextRound);
        StartNextRound();
    }

    private void OnGUI()
    {
        GUIStyle statusStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 18,
            padding = new RectOffset(12, 12, 8, 8)
        };

        GUI.Box(
            new Rect(Screen.width - 285f, 18f, 265f, 72f),
            "Round: " + currentRound + "\n" +
            "Zombies remaining: " + EnemiesRemaining,
            statusStyle
        );

        if (!choosingModifier)
        {
            return;
        }

        float panelWidth = Mathf.Min(920f, Screen.width - 40f);
        float panelHeight = 330f;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight
        );

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            wordWrap = true,
            padding = new RectOffset(14, 14, 12, 12)
        };

        GUI.Box(panel, string.Empty);
        GUI.Label(
            new Rect(panel.x + 20f, panel.y + 18f, panel.width - 40f, 48f),
            "Round " + currentRound + " Complete!",
            titleStyle
        );
        GUI.Label(
            new Rect(panel.x + 20f, panel.y + 63f, panel.width - 40f, 45f),
            "Choose one modifier. Your choices stack for the rest of the run.",
            titleStyle
        );

        if (currentChoices.Count == 0)
        {
            if (GUI.Button(
                    new Rect(panel.x + panel.width * 0.5f - 120f, panel.y + 145f, 240f, 80f),
                    "Continue",
                    buttonStyle))
            {
                ChooseModifier(null);
            }

            return;
        }

        float gap = 16f;
        float buttonWidth = (panel.width - 50f - gap * (currentChoices.Count - 1)) /
            currentChoices.Count;

        for (int i = 0; i < currentChoices.Count; i++)
        {
            ModifierData choice = currentChoices[i];
            Rect buttonRect = new Rect(
                panel.x + 25f + i * (buttonWidth + gap),
                panel.y + 120f,
                buttonWidth,
                165f
            );

            string label = choice.displayName + "\n\n" + choice.description;
            if (GUI.Button(buttonRect, label, buttonStyle))
            {
                ChooseModifier(choice);
            }
        }
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }
}
