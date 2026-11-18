using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AutoCreateDemoScene
{
    private const string SceneDirectory = "Assets/Scenes";
    private const string ScenePath = SceneDirectory + "/CardThrowingEditable.unity";
    private const string ArtworkDirectory = "Assets/Resources";
    private const string ArtworkPath = ArtworkDirectory + "/Deck01Artwork.asset";
    private const string DeckPrefabDirectory = "Assets/Asset_PlayingCards/Prefabs/Deck01";

    static AutoCreateDemoScene()
    {
        EditorApplication.delayCall += EnsureEditableSceneExists;
    }

    private static void EnsureEditableSceneExists()
    {
        if (!File.Exists(ScenePath))
        {
            CreateEditableScene(false);
        }

        EnsureDeckArtworkLibraryExists();
    }

    [MenuItem("Tools/Card Throwing Demo/Rebuild Editable Scene")]
    private static void RebuildEditableScene()
    {
        bool shouldRebuild = EditorUtility.DisplayDialog(
            "Rebuild Card Throwing Scene?",
            "This replaces CardThrowingEditable.unity with a new editable demo scene.",
            "Rebuild",
            "Cancel"
        );

        if (shouldRebuild)
        {
            CreateEditableScene(true);
        }
    }

    private static void CreateEditableScene(bool replacingExistingScene)
    {
        Directory.CreateDirectory(SceneDirectory);

        if (replacingExistingScene && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single
        );

        GameObject root = new GameObject("Card Throwing Demo");
        DemoSceneSetup setup = root.AddComponent<DemoSceneSetup>();
        setup.BuildEditableScene();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AddSceneToBuildSettings();
        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log(
            "Created editable card demo scene. Expand Card Throwing Demo in the Hierarchy to edit the player, targets, and arena."
        );
    }

    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;

        foreach (EditorBuildSettingsScene buildScene in currentScenes)
        {
            if (buildScene.path == ScenePath)
            {
                return;
            }
        }

        EditorBuildSettingsScene[] updatedScenes =
            new EditorBuildSettingsScene[currentScenes.Length + 1];

        for (int i = 0; i < currentScenes.Length; i++)
        {
            updatedScenes[i] = currentScenes[i];
        }

        updatedScenes[updatedScenes.Length - 1] =
            new EditorBuildSettingsScene(ScenePath, true);

        EditorBuildSettings.scenes = updatedScenes;
    }

    [MenuItem("Tools/Card Throwing Demo/Build Deck01 Artwork Library")]
    private static void BuildDeckArtworkLibraryFromMenu()
    {
        BuildDeckArtworkLibrary();
    }

    private static void EnsureDeckArtworkLibraryExists()
    {
        if (AssetDatabase.LoadAssetAtPath<CardArtworkLibrary>(ArtworkPath) == null &&
            AssetDatabase.IsValidFolder(DeckPrefabDirectory))
        {
            BuildDeckArtworkLibrary();
        }
    }

    private static void BuildDeckArtworkLibrary()
    {
        if (!AssetDatabase.IsValidFolder(ArtworkDirectory))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        List<CardArtworkLibrary.Entry> entries =
            new List<CardArtworkLibrary.Entry>(53);

        ThrowingCard.Suit[] suits =
        {
            ThrowingCard.Suit.Hearts,
            ThrowingCard.Suit.Clubs,
            ThrowingCard.Suit.Diamonds,
            ThrowingCard.Suit.Spades
        };

        string[] suitNames = { "Heart", "Club", "Diamond", "Spade" };

        ThrowingCard.Rank[] ranks =
        {
            ThrowingCard.Rank.Ace,
            ThrowingCard.Rank.Two,
            ThrowingCard.Rank.Three,
            ThrowingCard.Rank.Four,
            ThrowingCard.Rank.Five,
            ThrowingCard.Rank.Six,
            ThrowingCard.Rank.Seven,
            ThrowingCard.Rank.Eight,
            ThrowingCard.Rank.Nine,
            ThrowingCard.Rank.Ten,
            ThrowingCard.Rank.Jack,
            ThrowingCard.Rank.Queen,
            ThrowingCard.Rank.King
        };

        string[] rankNames =
        {
            "A", "2", "3", "4", "5", "6", "7",
            "8", "9", "10", "J", "Q", "K"
        };

        for (int suitIndex = 0; suitIndex < suits.Length; suitIndex++)
        {
            for (int rankIndex = 0; rankIndex < ranks.Length; rankIndex++)
            {
                string prefabPath =
                    DeckPrefabDirectory + "/Deck01_" +
                    suitNames[suitIndex] + "_" + rankNames[rankIndex] + ".prefab";

                Sprite sprite = GetFrontSprite(prefabPath);
                if (sprite != null)
                {
                    entries.Add(new CardArtworkLibrary.Entry
                    {
                        suit = suits[suitIndex],
                        rank = ranks[rankIndex],
                        sprite = sprite
                    });
                }
            }
        }

        Sprite jokerSprite = GetFrontSprite(
            DeckPrefabDirectory + "/Deck01_Joker.prefab"
        );

        if (jokerSprite != null)
        {
            entries.Add(new CardArtworkLibrary.Entry
            {
                suit = ThrowingCard.Suit.None,
                rank = ThrowingCard.Rank.Joker,
                sprite = jokerSprite
            });
        }

        CardArtworkLibrary library =
            AssetDatabase.LoadAssetAtPath<CardArtworkLibrary>(ArtworkPath);

        if (library == null)
        {
            library = ScriptableObject.CreateInstance<CardArtworkLibrary>();
            AssetDatabase.CreateAsset(library, ArtworkPath);
        }

        library.SetEntries(entries.ToArray());
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Built Deck01 artwork library with " + entries.Count +
            " card images. The throwing weapon will load it automatically."
        );
    }

    private static Sprite GetFrontSprite(string prefabPath)
    {
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (cardPrefab == null)
        {
            Debug.LogWarning("Card artwork prefab was not found: " + prefabPath);
            return null;
        }

        SpriteRenderer[] renderers =
            cardPrefab.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer.gameObject.name == "Front")
            {
                return spriteRenderer.sprite;
            }
        }

        return renderers.Length > 0 ? renderers[0].sprite : null;
    }
}
