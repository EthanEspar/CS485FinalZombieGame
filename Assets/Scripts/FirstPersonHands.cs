using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class FirstPersonHands : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private bool showHands = true;
    [SerializeField] private Color gloveColor = new Color(0.12f, 0.16f, 0.22f);
    [SerializeField] private Color cuffColor = new Color(0.45f, 0.08f, 0.08f);

    [Header("Hand Positions")]
    [SerializeField] private Vector3 leftIdlePosition = new Vector3(-0.34f, -0.34f, 0.68f);
    [SerializeField] private Vector3 rightIdlePosition = new Vector3(0.34f, -0.34f, 0.68f);
    [SerializeField] private Vector3 leftIdleRotation = new Vector3(12f, -15f, -8f);
    [SerializeField] private Vector3 rightIdleRotation = new Vector3(12f, 15f, 8f);
    [SerializeField] private float handScale = 1f;

    private Transform handsRoot;
    private Transform leftHand;
    private Transform rightHand;
    private Material gloveMaterial;
    private Material cuffMaterial;
    private Coroutine animationRoutine;

    private void Start()
    {
        EnsureHandsBuilt();
        ResetHands();
    }

    public void Configure(Camera newPlayerCamera)
    {
        playerCamera = newPlayerCamera;
        EnsureHandsBuilt();
        ResetHands();
    }

    public void PlayThrow(float duration)
    {
        EnsureHandsBuilt();

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(ThrowHandsRoutine(duration));
    }

    public void PlayShuffle(float duration, int passes)
    {
        EnsureHandsBuilt();

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(ShuffleHandsRoutine(duration, passes));
    }

    private void EnsureHandsBuilt()
    {
        if (handsRoot != null)
        {
            handsRoot.gameObject.SetActive(showHands);
            return;
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera == null)
        {
            return;
        }

        CreateMaterials();

        GameObject rootObject = new GameObject("First Person Hands");
        rootObject.transform.SetParent(playerCamera.transform, false);
        handsRoot = rootObject.transform;
        handsRoot.localPosition = Vector3.zero;
        handsRoot.localRotation = Quaternion.identity;
        handsRoot.localScale = Vector3.one * handScale;

        leftHand = BuildHand("Left Hand", true);
        rightHand = BuildHand("Right Hand", false);
        handsRoot.gameObject.SetActive(showHands);
    }

    private void CreateMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        gloveMaterial = new Material(shader)
        {
            color = gloveColor
        };
        cuffMaterial = new Material(shader)
        {
            color = cuffColor
        };
    }

    private Transform BuildHand(string handName, bool left)
    {
        GameObject handObject = new GameObject(handName);
        handObject.transform.SetParent(handsRoot, false);
        Transform hand = handObject.transform;

        CreatePart(
            hand,
            "Palm",
            new Vector3(0f, 0f, 0f),
            new Vector3(0.17f, 0.10f, 0.22f),
            gloveMaterial
        );

        for (int i = 0; i < 4; i++)
        {
            float x = -0.060f + i * 0.040f;
            CreatePart(
                hand,
                "Finger " + (i + 1),
                new Vector3(x, 0.015f, 0.155f),
                new Vector3(0.032f, 0.042f, 0.115f - i * 0.006f),
                gloveMaterial
            );
        }

        float thumbDirection = left ? 1f : -1f;
        GameObject thumb = CreatePart(
            hand,
            "Thumb",
            new Vector3(thumbDirection * 0.105f, -0.005f, 0.035f),
            new Vector3(0.052f, 0.050f, 0.105f),
            gloveMaterial
        );
        thumb.transform.localRotation = Quaternion.Euler(0f, thumbDirection * 32f, 0f);

        CreatePart(
            hand,
            "Cuff",
            new Vector3(0f, 0f, -0.155f),
            new Vector3(0.19f, 0.115f, 0.105f),
            cuffMaterial
        );

        return hand;
    }

    private GameObject CreatePart(
        Transform parent,
        string partName,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            partCollider.enabled = false;
            Destroy(partCollider);
        }

        Renderer partRenderer = part.GetComponent<Renderer>();
        if (partRenderer != null)
        {
            partRenderer.sharedMaterial = material;
            partRenderer.shadowCastingMode = ShadowCastingMode.Off;
            partRenderer.receiveShadows = false;
        }

        return part;
    }

    private IEnumerator ThrowHandsRoutine(float duration)
    {
        float animationDuration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;

        Vector3 rightWindupPosition = rightIdlePosition + new Vector3(0.14f, -0.06f, -0.12f);
        Vector3 rightReleasePosition = rightIdlePosition + new Vector3(-0.38f, 0.20f, 0.28f);
        Quaternion idleRotation = Quaternion.Euler(rightIdleRotation);
        Quaternion releaseRotation = Quaternion.Euler(rightIdleRotation + new Vector3(-25f, -18f, 70f));

        while (elapsed < animationDuration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / animationDuration);

            if (normalizedTime < 0.35f)
            {
                float phase = SmoothStep(normalizedTime / 0.35f);
                rightHand.localPosition = Vector3.Lerp(rightIdlePosition, rightWindupPosition, phase);
                rightHand.localRotation = Quaternion.Slerp(idleRotation, Quaternion.Euler(rightIdleRotation + new Vector3(8f, 18f, -20f)), phase);
            }
            else
            {
                float phase = SmoothStep((normalizedTime - 0.35f) / 0.65f);
                rightHand.localPosition = Vector3.Lerp(rightWindupPosition, rightReleasePosition, phase);
                rightHand.localRotation = Quaternion.Slerp(idleRotation, releaseRotation, phase);
            }

            leftHand.localPosition = leftIdlePosition + Vector3.up * Mathf.Sin(normalizedTime * Mathf.PI) * 0.025f;
            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetHands();
        animationRoutine = null;
    }

    private IEnumerator ShuffleHandsRoutine(float duration, int passes)
    {
        float animationDuration = Mathf.Max(0.15f, duration);
        int shufflePasses = Mathf.Max(1, passes);
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / animationDuration);
            float phase = normalizedTime * shufflePasses * Mathf.PI * 2f;
            float sideMotion = Mathf.Sin(phase) * 0.11f;
            float lift = Mathf.Abs(Mathf.Cos(phase)) * 0.045f;

            leftHand.localPosition = leftIdlePosition + new Vector3(sideMotion, lift + 0.07f, 0.08f);
            rightHand.localPosition = rightIdlePosition + new Vector3(-sideMotion, 0.07f - lift * 0.25f, 0.08f);
            leftHand.localRotation = Quaternion.Euler(leftIdleRotation + new Vector3(0f, 0f, sideMotion * 85f));
            rightHand.localRotation = Quaternion.Euler(rightIdleRotation + new Vector3(0f, 0f, -sideMotion * 85f));

            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetHands();
        animationRoutine = null;
    }

    private void ResetHands()
    {
        if (handsRoot == null || leftHand == null || rightHand == null)
        {
            return;
        }

        handsRoot.localScale = Vector3.one * handScale;
        handsRoot.gameObject.SetActive(showHands);
        leftHand.localPosition = leftIdlePosition;
        leftHand.localRotation = Quaternion.Euler(leftIdleRotation);
        rightHand.localPosition = rightIdlePosition;
        rightHand.localRotation = Quaternion.Euler(rightIdleRotation);
    }

    private float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
