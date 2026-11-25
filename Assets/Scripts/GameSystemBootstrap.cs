using UnityEngine;

public static class GameSystemBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSystems()
    {
        ThrowingCard weapon = Object.FindAnyObjectByType<ThrowingCard>();
        EnemyController enemyController = Object.FindAnyObjectByType<EnemyController>();

        if (weapon == null || enemyController == null)
        {
            return;
        }

        PlayerHealth playerHealth = weapon.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = weapon.gameObject.AddComponent<PlayerHealth>();
        }

        ModifierManager modifierManager =
            Object.FindAnyObjectByType<ModifierManager>();

        RoundManager roundManager = Object.FindAnyObjectByType<RoundManager>();

        if (modifierManager == null || roundManager == null)
        {
            GameObject systemObject = new GameObject("Round & Modifier System");

            if (modifierManager == null)
            {
                modifierManager = systemObject.AddComponent<ModifierManager>();
            }

            if (roundManager == null)
            {
                roundManager = systemObject.AddComponent<RoundManager>();
            }
        }

        roundManager.AutoConfigure(enemyController, weapon, modifierManager);
    }
}
