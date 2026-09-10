using System.Collections.Generic;
using UnityEngine;

public class RiverSystem : MonoBehaviour
{
    public static RiverSystem Instance { get; private set; }

    [Header("Spline")]
    public int controlPointCount = 8;
    public float controlPointSpacing = 48f;
    public float riverLength = 336f;
    public float lateralAmplitude = 34f;
    public float splineNoiseScale = 0.09f;

    [Header("Shape")]
    public float baseWidth = 7f;
    public float widthVariation = 0.25f;
    public float bankBlend = 4.5f;
    public float depth = 2.8f;

    [Header("Water")]
    public float sampleSpacing = 3f;
    public float waterInset = 0.72f;
    public float waterHeightOffset = 0.12f;
    public float triggerSpacing = 10f;
    public float refreshInterval = 0.5f;
    public float refreshMoveThreshold = 6f;

    [Header("Audio")]
    public AudioClip riverAmbientClip;
    [Range(0f, 1f)] public float ambientVolume = 0.55f;
    public float ambientMinDistance = 8f;
    public float ambientMaxDistance = 55f;
    [Range(0.1f, 3f)] public float ambientPitch = 1f;

    [Header("River End Prefab")]
    public GameObject riverEndPrefab;
    public Vector3 riverEndPrefabOffset = Vector3.zero;
    public Vector3 riverEndPrefabRotationOffset = Vector3.zero;
    public Vector3 riverEndPrefabScale = Vector3.one;

    readonly List<Vector3> controlPoints = new List<Vector3>();
    readonly List<RiverSamplePoint> samples = new List<RiverSamplePoint>();
    readonly List<Vector3> leftBank = new List<Vector3>();
    readonly List<Vector3> rightBank = new List<Vector3>();
    readonly List<bool> sampleValidity = new List<bool>();
    readonly List<GameObject> triggerObjects = new List<GameObject>();

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh riverMesh;
    Transform riverStartAnchor;
    Transform riverEndAnchor;
    Transform riverEndPrefabRoot;
    AudioSource ambientSource;
    Transform playerTransform;
    bool ambientPausedForCombat;
    static Material sharedRiverMaterial;
    float refreshTimer;
    bool initialized;
    Vector3 startOrigin;
    float baseRiverX;
    Vector3 lastRefreshPosition;

    float EffectiveWaterInset => Mathf.Clamp(waterInset, 0.1f, 1f);

    struct RiverQuery
    {
        public float distance;
        public Vector3 nearestPoint;
        public float halfWidth;
    }

    struct RiverSamplePoint
    {
        public Vector3 center;
        public Vector3 tangent;
        public float halfWidth;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Initialize(Vector3 origin)
    {
        if (initialized)
            return;

        initialized = true;
        startOrigin = origin;
        baseRiverX = origin.x;
        playerTransform = FindPlayerTransform();
        BuildVisualObjects();
        BuildSpline(origin);
        BuildSampledRiver();
        RefreshRiverHeights();
        lastRefreshPosition = playerTransform != null ? playerTransform.position : origin;
    }

    void Update()
    {
        if (!initialized)
            return;

        if (playerTransform == null)
            playerTransform = FindPlayerTransform();

        UpdateAmbientAudio();

        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshInterval)
            return;

        if (playerTransform != null)
        {
            float minDistance = Mathf.Max(1f, refreshMoveThreshold);
            if ((playerTransform.position - lastRefreshPosition).sqrMagnitude < minDistance * minDistance)
                return;
        }

        refreshTimer = 0f;
        RefreshRiverHeights();
        if (playerTransform != null)
            lastRefreshPosition = playerTransform.position;
    }

    Transform FindPlayerTransform()
    {
        if (playerTransform != null)
            return playerTransform;

        Transform focusTransform = LanMultiplayerManager.FindWorldFocusTransform();
        if (focusTransform != null)
            return focusTransform;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }

    void BuildVisualObjects()
    {
        GameObject surface = new GameObject("RiverSurface");
        surface.transform.SetParent(transform, false);
        meshFilter = surface.AddComponent<MeshFilter>();
        meshRenderer = surface.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = GetRiverMaterial();
        riverMesh = new Mesh { name = "RiverSplineMesh" };
        meshFilter.sharedMesh = riverMesh;

        riverStartAnchor = new GameObject("RiverStartAnchor").transform;
        riverStartAnchor.SetParent(transform, false);
        riverEndAnchor = new GameObject("RiverEndAnchor").transform;
        riverEndAnchor.SetParent(transform, false);
        riverEndPrefabRoot = new GameObject("RiverEndPrefab").transform;
        riverEndPrefabRoot.SetParent(transform, false);

        EnsureAmbientAudioSource();
    }

    void BuildSpline(Vector3 origin)
    {
        controlPoints.Clear();

        int count = Mathf.Max(4, controlPointCount);
        float spacing = Mathf.Max(8f, controlPointSpacing);
        float length = Mathf.Max(riverLength, spacing * (count - 1));
        float startZ = origin.z - length * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : i / (float)(count - 1);
            float z = startZ + t * length;
            float x = baseRiverX + SampleSplineOffset(z);
            controlPoints.Add(new Vector3(x, 0f, z));
        }
    }

    void BuildSampledRiver()
    {
        samples.Clear();
        if (controlPoints.Count < 4)
            return;

        int sampleCount = Mathf.Max(16, Mathf.CeilToInt(Mathf.Max(8f, riverLength) / Mathf.Max(1f, sampleSpacing)));
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector3 center = EvaluateSpline(t);
            Vector3 tangent = EvaluateTangent(t);
            float halfWidth = GetHalfWidth(t);

            samples.Add(new RiverSamplePoint
            {
                center = center,
                tangent = tangent,
                halfWidth = halfWidth
            });
        }
    }

    public bool TryGetBlend(Vector2 point, bool allowBiome, out float blend)
    {
        blend = 0f;
        if (!initialized || !allowBiome)
            return false;

        RiverQuery query = GetQuery(point);
        float influence = query.halfWidth + bankBlend;
        if (query.distance <= influence)
        {
            blend = Mathf.Clamp01(1f - query.distance / influence);
            blend = Mathf.SmoothStep(0f, 1f, blend);
        }

        return blend > 0f;
    }

    public bool IsRiverZone(Vector2 point, bool allowBiome, float extraMargin = 0f)
    {
        if (!initialized || !allowBiome)
            return false;

        RiverQuery query = GetQuery(point);
        return query.distance <= query.halfWidth + extraMargin;
    }

    public bool TryGetWaterSurfaceHeight(Vector3 worldPosition, out float waterHeight)
    {
        waterHeight = 0f;
        if (!initialized)
            return false;

        RiverQuery query = GetQuery(new Vector2(worldPosition.x, worldPosition.z));
        float halfWidth = query.halfWidth * EffectiveWaterInset;
        if (query.distance > halfWidth)
            return false;

        return TrySampleWaterHeight(query.nearestPoint, halfWidth, out waterHeight);
    }

    public float RiverDepth => depth;

    RiverQuery GetQuery(Vector2 point)
    {
        RiverQuery best = new RiverQuery
        {
            distance = float.MaxValue,
            nearestPoint = new Vector3(point.x, 0f, point.y),
            halfWidth = Mathf.Max(2.5f, baseWidth * 0.5f)
        };

        if (samples.Count == 0)
            return best;

        Vector3 queryPoint = new Vector3(point.x, 0f, point.y);

        for (int i = 1; i < samples.Count; i++)
        {
            Vector3 a = samples[i - 1].center;
            Vector3 b = samples[i].center;
            Vector3 closest = ClosestPointOnSegment(queryPoint, a, b);
            float distance = Vector2.Distance(new Vector2(queryPoint.x, queryPoint.z), new Vector2(closest.x, closest.z));
            if (distance >= best.distance)
                continue;

            float segmentLength = Mathf.Max(0.0001f, Vector3.Distance(a, b));
            float along = Mathf.Clamp01(Vector3.Distance(a, closest) / segmentLength);
            float width = Mathf.Lerp(samples[i - 1].halfWidth, samples[i].halfWidth, along);

            best.distance = distance;
            best.nearestPoint = closest;
            best.halfWidth = width;
        }

        return best;
    }

    void RefreshRiverHeights()
    {
        if (samples.Count < 2 || riverMesh == null)
            return;

        leftBank.Clear();
        rightBank.Clear();
        sampleValidity.Clear();

        for (int i = 0; i < samples.Count; i++)
        {
            RiverSamplePoint sample = samples[i];
            Vector3 right = Vector3.Cross(Vector3.up, sample.tangent).normalized;
            bool hasGround = TrySampleWaterHeight(sample.center, sample.halfWidth * EffectiveWaterInset, out float waterHeight);

            Vector3 center = new Vector3(sample.center.x, waterHeight, sample.center.z);
            samples[i] = new RiverSamplePoint
            {
                center = center,
                tangent = sample.tangent,
                halfWidth = sample.halfWidth
            };

            float visualHalfWidth = sample.halfWidth * EffectiveWaterInset;
            leftBank.Add(center - right * visualHalfWidth);
            rightBank.Add(center + right * visualHalfWidth);
            sampleValidity.Add(hasGround);
        }

        RebuildMesh();
        RebuildTriggers();
        UpdateRiverAnchors();
        RebuildRiverEndPrefab();
    }

    void UpdateRiverAnchors()
    {
        if (samples.Count == 0)
            return;

        if (riverStartAnchor != null)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                if (i < sampleValidity.Count && sampleValidity[i])
                {
                    riverStartAnchor.position = samples[i].center;
                    riverStartAnchor.rotation = Quaternion.LookRotation(samples[i].tangent, Vector3.up);
                    break;
                }
            }
        }

        if (riverEndAnchor != null)
        {
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (i < sampleValidity.Count && sampleValidity[i])
                {
                    riverEndAnchor.position = samples[i].center;
                    riverEndAnchor.rotation = Quaternion.LookRotation(samples[i].tangent, Vector3.up);
                    break;
                }
            }
        }
    }

    void RebuildRiverEndPrefab()
    {
        if (riverEndPrefabRoot == null)
            return;

        if (riverEndPrefab == null || riverEndAnchor == null)
        {
            ClearChildren(riverEndPrefabRoot);
            return;
        }

        Transform instanceTransform = riverEndPrefabRoot.childCount > 0 ? riverEndPrefabRoot.GetChild(0) : null;
        GameObject instance;

        if (instanceTransform == null)
        {
            instance = Instantiate(riverEndPrefab, riverEndPrefabRoot);
            instance.name = riverEndPrefab.name;
        }
        else
        {
            instance = instanceTransform.gameObject;
        }

        Quaternion targetRotation = riverEndAnchor.rotation * Quaternion.Euler(riverEndPrefabRotationOffset);
        Vector3 targetPosition = riverEndAnchor.position + riverEndPrefabOffset;

        instance.transform.rotation = targetRotation;
        instance.transform.localScale = riverEndPrefabScale;
        instance.transform.position = targetPosition;
        AlignPrefabToAnchor(instance, targetPosition);
    }

    bool TrySampleWaterHeight(Vector3 center, float offset, out float waterHeight)
    {
        bool hasCenter = TrySampleTerrainHeight(center, out float centerHeight);
        bool hasLeft = TrySampleTerrainHeight(center + Vector3.left * offset, out float leftHeight);
        bool hasRight = TrySampleTerrainHeight(center + Vector3.right * offset, out float rightHeight);

        if (!hasCenter && !hasLeft && !hasRight)
        {
            waterHeight = startOrigin.y;
            return false;
        }

        if (!hasLeft) leftHeight = centerHeight;
        if (!hasRight) rightHeight = centerHeight;

        float baseHeight = Mathf.Min(centerHeight, Mathf.Min(leftHeight, rightHeight));
        waterHeight = baseHeight + waterHeightOffset;
        return true;
    }

    bool TrySampleTerrainHeight(Vector3 worldPoint, out float height)
    {
        if (Physics.Raycast(worldPoint + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
        {
            height = hit.point.y;
            return true;
        }

        height = startOrigin.y;
        return false;
    }

    void EnsureAmbientAudioSource()
    {
        if (ambientSource != null)
            return;

        GameObject audioObject = new GameObject("RiverAmbientAudio");
        audioObject.transform.SetParent(transform, false);
        ambientSource = audioObject.AddComponent<AudioSource>();
        ambientSource.playOnAwake = false;
        ambientSource.loop = true;
        ambientSource.spatialBlend = 1f;
        ambientSource.rolloffMode = AudioRolloffMode.Linear;
        ambientSource.dopplerLevel = 0f;
        ambientSource.spread = 35f;
        ApplyAmbientAudioSettings();
    }

    void ApplyAmbientAudioSettings()
    {
        if (ambientSource == null)
            return;

        ambientSource.clip = riverAmbientClip;
        ambientSource.volume = ambientVolume;
        ambientSource.minDistance = Mathf.Max(0.1f, ambientMinDistance);
        ambientSource.maxDistance = Mathf.Max(ambientSource.minDistance + 0.1f, ambientMaxDistance);
        ambientSource.pitch = ambientPitch;
    }

    void UpdateAmbientAudio()
    {
        if (ambientSource == null)
            EnsureAmbientAudioSource();

        if (playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                playerTransform = playerObject.transform;
        }

        ApplyAmbientAudioSettings();

        if (ambientSource == null || riverAmbientClip == null)
        {
            if (ambientSource != null && ambientSource.isPlaying)
                ambientSource.Stop();
            return;
        }

        if (PlayerMovement.IsCombatMusicActive)
        {
            if (ambientSource.isPlaying)
                ambientSource.Pause();

            ambientPausedForCombat = true;
            return;
        }

        if (ambientPausedForCombat)
        {
            ambientPausedForCombat = false;
            ambientSource.UnPause();
        }

        if (!TryGetNearestWaterAnchor(playerTransform != null ? playerTransform.position : startOrigin, out Vector3 anchor))
        {
            if (ambientSource.isPlaying)
                ambientSource.Stop();
            return;
        }

        ambientSource.transform.position = anchor;

        if (!ambientSource.isPlaying)
            ambientSource.Play();
    }

    bool TryGetNearestWaterAnchor(Vector3 worldPosition, out Vector3 anchor)
    {
        anchor = Vector3.zero;
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < samples.Count; i++)
        {
            if (i >= sampleValidity.Count || !sampleValidity[i])
                continue;

            float distance = Vector2.SqrMagnitude(new Vector2(samples[i].center.x - worldPosition.x, samples[i].center.z - worldPosition.z));
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            anchor = samples[i].center;
            found = true;
        }

        return found;
    }

    void RebuildMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> triangles = new List<int>();
        float length = 0f;
        int previousBase = -1;
        Vector3 previousCenter = Vector3.zero;

        for (int i = 0; i < samples.Count; i++)
        {
            if (i >= sampleValidity.Count || !sampleValidity[i])
            {
                previousBase = -1;
                continue;
            }

            int vi = vertices.Count;
            vertices.Add(transform.InverseTransformPoint(leftBank[i]));
            vertices.Add(transform.InverseTransformPoint(rightBank[i]));

            if (previousBase >= 0)
                length += Vector3.Distance(previousCenter, samples[i].center);

            uv.Add(new Vector2(0f, length));
            uv.Add(new Vector2(1f, length));

            if (previousBase >= 0)
            {
                triangles.Add(previousBase);
                triangles.Add(vi);
                triangles.Add(previousBase + 1);
                triangles.Add(previousBase + 1);
                triangles.Add(vi);
                triangles.Add(vi + 1);
            }

            previousBase = vi;
            previousCenter = samples[i].center;
        }

        riverMesh.Clear();
        riverMesh.vertices = vertices.ToArray();
        riverMesh.triangles = triangles.ToArray();
        riverMesh.uv = uv.ToArray();
        riverMesh.RecalculateNormals();
        riverMesh.RecalculateBounds();
    }

    void RebuildTriggers()
    {
        float distanceAccumulator = 0f;
        float spacing = Mathf.Max(4f, triggerSpacing);
        int triggerIndex = 0;

        for (int i = 1; i < samples.Count; i++)
        {
            if (i >= sampleValidity.Count || !sampleValidity[i] || !sampleValidity[i - 1])
            {
                distanceAccumulator = 0f;
                continue;
            }

            distanceAccumulator += Vector3.Distance(samples[i - 1].center, samples[i].center);
            if (distanceAccumulator < spacing)
                continue;

            distanceAccumulator = 0f;

            GameObject trigger = GetOrCreateTrigger(triggerIndex);
            trigger.transform.position = samples[i].center;
            trigger.transform.rotation = Quaternion.LookRotation(samples[i].tangent, Vector3.up);
            trigger.SetActive(true);

            BoxCollider box = trigger.GetComponent<BoxCollider>();
            box.size = new Vector3(samples[i].halfWidth * 2f * EffectiveWaterInset, 2.4f, spacing);
            triggerIndex++;
        }

        for (int i = triggerIndex; i < triggerObjects.Count; i++)
        {
            if (triggerObjects[i] != null)
                triggerObjects[i].SetActive(false);
        }
    }

    GameObject GetOrCreateTrigger(int index)
    {
        while (triggerObjects.Count <= index)
        {
            GameObject trigger = new GameObject("RiverWater");
            trigger.transform.SetParent(transform, false);

            BoxCollider box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            trigger.AddComponent<RiverWaterSource>();
            triggerObjects.Add(trigger);
        }

        return triggerObjects[index];
    }

    void AlignPrefabToAnchor(GameObject riverObject, Vector3 anchor)
    {
        Renderer[] renderers = riverObject.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 xzOffset = new Vector3(anchor.x - bounds.center.x, 0f, anchor.z - bounds.center.z);
        float yOffset = anchor.y - bounds.center.y;
        riverObject.transform.position += xzOffset + Vector3.up * yOffset;
    }

    void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    float SampleSplineOffset(float worldZ)
    {
        float primary = (Mathf.PerlinNoise(17.13f, worldZ * splineNoiseScale) - 0.5f) * lateralAmplitude * 2f;
        float secondary = (Mathf.PerlinNoise(43.71f, worldZ * splineNoiseScale * 0.37f) - 0.5f) * lateralAmplitude * 0.85f;
        return primary + secondary;
    }

    Vector3 EvaluateSpline(float t)
    {
        int segmentCount = controlPoints.Count - 1;
        float scaled = Mathf.Clamp01(t) * segmentCount;
        int i = Mathf.Min(segmentCount - 1, Mathf.FloorToInt(scaled));
        float localT = scaled - i;

        Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)];
        Vector3 p1 = controlPoints[i];
        Vector3 p2 = controlPoints[Mathf.Min(i + 1, controlPoints.Count - 1)];
        Vector3 p3 = controlPoints[Mathf.Min(i + 2, controlPoints.Count - 1)];

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * localT +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * localT * localT +
            (-p0 + 3f * p1 - 3f * p2 + p3) * localT * localT * localT);
    }

    Vector3 EvaluateTangent(float t)
    {
        float delta = 0.01f;
        Vector3 prev = EvaluateSpline(Mathf.Clamp01(t - delta));
        Vector3 next = EvaluateSpline(Mathf.Clamp01(t + delta));
        Vector3 tangent = (next - prev).normalized;
        tangent.y = 0f;
        return tangent.sqrMagnitude < 0.0001f ? Vector3.forward : tangent.normalized;
    }

    float GetHalfWidth(float t)
    {
        float widthNoise = Mathf.PerlinNoise(41.91f, Mathf.Clamp01(t) * 3.7f);
        float multiplier = Mathf.Lerp(1f - widthVariation, 1f + widthVariation, widthNoise);
        return Mathf.Max(2.5f, baseWidth * multiplier * 0.5f);
    }

    Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float denominator = Vector3.Dot(ab, ab);
        if (denominator <= 0.0001f)
            return a;

        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / denominator);
        return a + ab * t;
    }

    Material GetRiverMaterial()
    {
        if (sharedRiverMaterial != null)
            return sharedRiverMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        sharedRiverMaterial = new Material(shader);
        sharedRiverMaterial.color = new Color(0.12f, 0.5f, 0.78f, 0.88f);

        if (sharedRiverMaterial.HasProperty("_Smoothness"))
            sharedRiverMaterial.SetFloat("_Smoothness", 0.92f);

        if (sharedRiverMaterial.HasProperty("_BaseColor"))
            sharedRiverMaterial.SetColor("_BaseColor", new Color(0.12f, 0.5f, 0.78f, 0.88f));

        return sharedRiverMaterial;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
