using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maximumHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private bool pauseGameOnDeath = true;

    public float CurrentHealth => currentHealth;
    public float MaximumHealth => maximumHealth;
    public bool IsDead => currentHealth <= 0f;

    private void Awake()
    {
        currentHealth = maximumHealth;
    }

    private void Update()
    {
        if (IsDead && Input.GetKeyDown(KeyCode.Return))
        {
            PlayAgain();
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, amount));

        if (IsDead)
        {
            SimpleFirstPersonController controller =
                GetComponent<SimpleFirstPersonController>();
            ThrowingCard weapon = GetComponent<ThrowingCard>();

            if (controller != null)
            {
                controller.enabled = false;
            }

            if (weapon != null)
            {
                weapon.enabled = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (pauseGameOnDeath)
            {
                Time.timeScale = 0f;
            }

            Debug.Log("Player was defeated.");
        }
    }

    public void PlayAgain()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Scene currentScene = SceneManager.GetActiveScene();

#if UNITY_EDITOR
        if (currentScene.buildIndex < 0 && !string.IsNullOrEmpty(currentScene.path))
        {
            EditorSceneManager.LoadSceneAsyncInPlayMode(
                currentScene.path,
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            return;
        }
#endif

        if (currentScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(currentScene.buildIndex);
        }
        else
        {
            SceneManager.LoadScene(currentScene.name);
        }
    }

    public void Heal(float amount)
    {
        if (!IsDead)
        {
            currentHealth = Mathf.Min(
                maximumHealth,
                currentHealth + Mathf.Max(0f, amount)
            );
        }
    }

    private void OnGUI()
    {
        // ThrowingCard draws the consolidated gameplay HUD. Keep this fallback
        // for scenes where PlayerHealth is used without the card weapon.
        if (GetComponent<ThrowingCard>() == null)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 17,
                padding = new RectOffset(12, 12, 8, 8)
            };

            GUI.Box(
                new Rect(18f, 230f, 250f, 48f),
                "Player HP: " + currentHealth.ToString("0") +
                " / " + maximumHealth.ToString("0"),
                style
            );
        }

        if (IsDead)
        {
            float panelWidth = 420f;
            float panelHeight = 220f;
            Rect panel = new Rect(
                Screen.width * 0.5f - panelWidth * 0.5f,
                Screen.height * 0.5f - panelHeight * 0.5f,
                panelWidth,
                panelHeight
            );

            GUIStyle gameOverStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold
            };

            GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17
            };

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };

            GUI.Box(panel, string.Empty);
            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 18f, panel.width - 40f, 55f),
                "GAME OVER",
                gameOverStyle
            );
            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 72f, panel.width - 40f, 35f),
                "You were defeated by the zombies.",
                messageStyle
            );

            if (GUI.Button(
                    new Rect(panel.x + 70f, panel.y + 125f, panel.width - 140f, 60f),
                    "PLAY AGAIN",
                    buttonStyle))
            {
                PlayAgain();
            }

            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 188f, panel.width - 40f, 24f),
                "Press Enter to restart",
                messageStyle
            );
        }
    }
}
