using UnityEngine;

public class ZombieHealth : MonoBehaviour, ICardDamageable
{
    [SerializeField] private float maximumHealth = 50f;
    [SerializeField] private bool isBoss;
    [SerializeField] private string displayName = "Zombie";

    [SerializeField] private TextMesh healthLabel;

    public bool IsBoss => isBoss;
    public float CurrentHealth { get; private set; }

    private void Awake()
    {
        CurrentHealth = maximumHealth;

        if (healthLabel == null)
        {
            healthLabel = GetComponentInChildren<TextMesh>(true);
        }

        RefreshLabel();
    }

    public void Configure(string newName, float newMaximumHealth, bool boss, Color color)
    {
        displayName = newName;
        maximumHealth = newMaximumHealth;
        CurrentHealth = maximumHealth;
        isBoss = boss;

        Renderer targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            targetRenderer.material.color = color;
        }

        CreateHealthLabel();
        RefreshLabel();
    }

    public void TakeDamage(float amount)
    {
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        RefreshLabel();

        Debug.Log(
            displayName + " took " + amount.ToString("0.##") +
            " damage. Remaining health: " + CurrentHealth.ToString("0.##")
        );

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    private void CreateHealthLabel()
    {
        GameObject labelObject = new GameObject("Health Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = Vector3.up * 1.35f;

        healthLabel = labelObject.AddComponent<TextMesh>();
        healthLabel.anchor = TextAnchor.MiddleCenter;
        healthLabel.alignment = TextAlignment.Center;
        healthLabel.characterSize = 0.10f;
        healthLabel.fontSize = 60;
        healthLabel.color = Color.black;

        labelObject.AddComponent<FaceCameraBillboard>();
    }

    private void RefreshLabel()
    {
        gameObject.name = displayName + " (" + CurrentHealth.ToString("0.#") + " HP)";

        if (healthLabel != null)
        {
            healthLabel.text =
                displayName + "\n" +
                CurrentHealth.ToString("0.#") + " / " + maximumHealth.ToString("0.#") + " HP";
        }
    }

    private void Die()
    {
        Debug.Log(displayName + " was defeated by the card weapon.");
        Destroy(gameObject);
    }
}
