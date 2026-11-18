using UnityEngine;

public class DemoSceneSetup : MonoBehaviour
{
    [SerializeField] private bool buildSceneAtRuntime = true;

    private void Awake()
    {
        if (buildSceneAtRuntime && transform.childCount == 0)
        {
            BuildSceneObjects();
        }
    }

    public void BuildEditableScene()
    {
        buildSceneAtRuntime = false;
        BuildSceneObjects();
    }

    private void BuildSceneObjects()
    {
        BuildLighting();
        BuildArena();
        BuildPlayer();
    }

    private void BuildLighting()
    {
        RenderSettings.ambientLight = new Color(0.58f, 0.58f, 0.62f);

        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.SetParent(transform, true);
        Light directionalLight = lightObject.AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 1.15f;
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
    }

    private void BuildArena()
    {
        CreateCube(
            "Floor",
            new Vector3(0f, -0.5f, 13f),
            new Vector3(26f, 1f, 34f),
            new Color(0.18f, 0.20f, 0.24f)
        );

        CreateCube(
            "Back Wall",
            new Vector3(0f, 3f, 29f),
            new Vector3(26f, 7f, 1f),
            new Color(0.28f, 0.08f, 0.10f)
        );

        CreateCube(
            "Left Wall",
            new Vector3(-13f, 3f, 13f),
            new Vector3(1f, 7f, 34f),
            new Color(0.12f, 0.12f, 0.16f)
        );

        CreateCube(
            "Right Wall",
            new Vector3(13f, 3f, 13f),
            new Vector3(1f, 7f, 34f),
            new Color(0.12f, 0.12f, 0.16f)
        );

        CreateTarget("Walker", new Vector3(-4f, 1f, 12f), 30f, false, new Color(0.35f, 0.72f, 0.40f));
        CreateTarget("Sprinter", new Vector3(0f, 1f, 14f), 45f, false, new Color(0.92f, 0.50f, 0.12f));
        CreateTarget("Thrower", new Vector3(4f, 1f, 12f), 60f, false, new Color(0.40f, 0.55f, 0.92f));
        CreateTarget("Boss", new Vector3(0f, 2f, 23f), 250f, true, new Color(0.62f, 0.12f, 0.18f));
    }

    private void BuildPlayer()
    {
        GameObject player = new GameObject("Player");
        player.transform.SetParent(transform, true);
        player.transform.position = new Vector3(0f, 0f, 0f);

        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.8f;
        characterController.radius = 0.35f;
        characterController.center = new Vector3(0f, 0.9f, 0f);

        GameObject cameraObject = new GameObject("Player Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        Camera playerCamera = cameraObject.AddComponent<Camera>();
        playerCamera.clearFlags = CameraClearFlags.SolidColor;
        playerCamera.backgroundColor = new Color(0.07f, 0.07f, 0.10f);
        playerCamera.fieldOfView = 68f;
        cameraObject.AddComponent<AudioListener>();

        GameObject throwPointObject = new GameObject("Throw Point");
        throwPointObject.transform.SetParent(cameraObject.transform, false);
        throwPointObject.transform.localPosition = new Vector3(0f, -0.18f, 0.75f);

        SimpleFirstPersonController controller = player.AddComponent<SimpleFirstPersonController>();
        controller.Configure(cameraObject.transform);

        ThrowingCard weapon = player.AddComponent<ThrowingCard>();
        weapon.Configure(null, throwPointObject.transform, playerCamera);
    }

    private void CreateTarget(string targetName, Vector3 position, float health, bool boss, Color color)
    {
        GameObject target = GameObject.CreatePrimitive(boss ? PrimitiveType.Cube : PrimitiveType.Capsule);
        target.transform.SetParent(transform, true);
        target.transform.position = position;
        target.transform.localScale = boss
            ? new Vector3(3.5f, 4f, 2f)
            : new Vector3(1.3f, 1.3f, 1.3f);

        ZombieHealth zombieHealth = target.AddComponent<ZombieHealth>();
        zombieHealth.Configure(targetName, health, boss, color);
    }

    private void CreateCube(string objectName, Vector3 position, Vector3 scale, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(transform, true);
        cube.name = objectName;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().material.color = color;
    }

    private void OnGUI()
    {
        GUIStyle crosshairStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26,
            fontStyle = FontStyle.Bold
        };
        crosshairStyle.normal.textColor = Color.white;

        GUI.Label(
            new Rect(Screen.width * 0.5f - 18f, Screen.height * 0.5f - 18f, 36f, 36f),
            "+",
            crosshairStyle
        );

        GUIStyle instructionsStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 15,
            padding = new RectOffset(12, 12, 8, 8)
        };

        GUI.Box(
            new Rect(18f, Screen.height - 100f, 455f, 78f),
            "WASD: Move    Mouse: Look    Left Click: Throw card\n" +
            "Aim at the colored targets. Jokers remove 50% of a target's\n" +
            "current health. ESC unlocks the mouse.",
            instructionsStyle
        );
    }
}
