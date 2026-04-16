using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class TerrainChunk : MonoBehaviour
{
    class ChunkRandom
    {
        readonly System.Random random;

        public ChunkRandom(int seed)
        {
            random = new System.Random(seed);
        }

        public float Value()
        {
            return (float)random.NextDouble();
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            return Mathf.Lerp(minInclusive, maxInclusive, Value());
        }
    }

    public enum BiomeType
    {
        Desert,
        Forest,
        Snow
    }

    [System.Serializable]
    public class TreeData
    {
        public GameObject prefab;
        public float yOffset;
        public BiomeType biome;
    }

    [Header("Config")]
    public int size = 50;
    public float terrainScale = 40f;
    public float heightMultiplier = 12f;

    public Transform player;
    public float safeZoneRadius = 10f;

    [Header("🌍 Bioma")]
    public float biomeScale = 0.003f;

    [Header("Vegetação")]
    public TreeData[] trees;
    public GameObject mushroomPrefab;
    public GameObject enchantedForestMushroomSinglePrefab;
    public GameObject enchantedForestMushroomClusterPrefab;
    [Range(0f, 1f)] public float enchantedForestClusterChance = 0.35f;
    public Vector2 enchantedForestMushroomScaleRange = new Vector2(0.8f, 1.25f);
    public float enchantedForestMushroomYOffset = 0.08f;
    public float enchantedForestMushroomMinDistance = 3.6f;

    public float treeDensity = 0.12f;
    public int maxTreesPerChunk = 22;
    public float forestTreeHeightMultiplier = 1.45f;
    public float forestTreeWidthMultiplier = 1.12f;
    public float mushroomDensity = 0.001f;
    public int rockClusterCount = 3;
    public int generationYieldInterval = 120;

    [Header("Grama Leve")]
    [Range(0f, 1f)] public float forestGrassDensity = 1f;
    public int maxForestGrassPerChunk = 2200;
    public float forestGrassMinDistance = 0.12f;
    public int forestGrassSampleStep = 2;
    [Range(0f, 1f)] public float forestGrassExtraCoverage = 0.8f;
    public Vector2 forestGrassWidthRange = new Vector2(1.2f, 1.8f);
    public Vector2 forestGrassHeightRange = new Vector2(0.42f, 0.62f);
    public float forestGrassSpawnJitter = 0.42f;
    public float forestGrassYOffset = 0.02f;
    public float forestGrassRoadPadding = 1.6f;
    public float forestGrassRenderDistance = 72f;
    public float forestGrassWindStrength = 0.08f;
    public float forestGrassWindSpeed = 1.65f;
    public float forestGrassBendStrength = 0.1f;
    public Color forestGrassBaseColor = new Color(0.36f, 0.46f, 0.12f, 1f);
    public Color forestGrassTipColor = new Color(0.54f, 0.62f, 0.2f, 1f);

    [Header("Rio")]
    public bool enableRiver = true;
    public float riverWidth = 4f;
    public float riverBankBlend = 2.5f;
    public float riverDepth = 2.4f;
    public float riverCurveScale = 0.015f;
    public float riverWorldWidth = 140f;
    public float riverWaterHeightOffset = 0.18f;
    public float riverVisualStep = 2f;
    public float riverTriggerStep = 5f;
    public float riverSurfaceThickness = 0.12f;
    public float riverWidthVariation = 0.8f;
    public float riverVisualWidthMultiplier = 0.58f;
    public float riverChunkOverlap = 6f;
    [Range(0f, 1f)] public float riverSurfaceSmoothing = 0.7f;
    public float riverMaxSegmentGap = 4.5f;

    public float minDistanceBetweenObjects = 12f;
    public float minTreeDistance = 6.2f;

    [Header("Rochas")]
    public GameObject rockSmallPrefab;
    public GameObject rockMediumPrefab;
    public GameObject rockLargePrefab;

    [Header("Animais")]
    public GameObject cowPrefab;
    public Item cowMeatItem;
    public GameObject cowMeatDropPrefab;
    public Material cowBodyMaterial;
    public Material cowSpotMaterial;
    public Material cowHoofMaterial;
    public float cowGroupChance = 0.35f;
    public int maxCowGroupsPerChunk = 1;
    public float minDistanceBetweenCowGroups = 18f;
    public float cowSpawnRadius = 5f;
    public float cowRespawnDelay = 25f;
    public float cowWanderRadius = 8f;


    [Header("Material")]
    public Material terrainMaterial;

    static readonly int UseFlatColorsId = Shader.PropertyToID("_UseFlatColors");
    static readonly int SandColorId = Shader.PropertyToID("_SandColor");
    static readonly int GrassColorId = Shader.PropertyToID("_GrassColor");
    static readonly int SnowColorId = Shader.PropertyToID("_SnowColor");
    static readonly int RoadColorId = Shader.PropertyToID("_RoadColor");
    static readonly int GrassBaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int GrassTipColorId = Shader.PropertyToID("_TipColor");
    static readonly int GrassWindStrengthId = Shader.PropertyToID("_WindStrength");
    static readonly int GrassWindSpeedId = Shader.PropertyToID("_WindSpeed");
    static readonly int GrassBendStrengthId = Shader.PropertyToID("_BendStrength");
    static readonly int GrassTimeOffsetId = Shader.PropertyToID("_WindTimeOffset");
    const int GrassBatchSize = 1023;

    bool alreadyGenerated = false;

    public float safeRadius = 12f;
    public float forwardSafeDistance = 8f;

    public float rockDensity = 0.5f; // base

    Mesh mesh;
    Vector3[] vertices;
    int[] triangles;
    Color[] colors;
    Vector2[] uvs;

    List<Vector3> usedPositions = new List<Vector3>();
    List<Vector3> cowGroupPositions = new List<Vector3>();
    List<Matrix4x4[]> forestGrassBatches = new List<Matrix4x4[]>();
    List<int> forestGrassBatchCounts = new List<int>();
    static Material riverMaterial;
    static Mesh forestGrassMesh;
    static GameObject cachedEnchantedForestSingleMushroom;
    static GameObject cachedEnchantedForestClusterMushroom;
    WorldHeightmapData sceneHeightmapData;
    RoadMaskData sceneRoadMaskData;
    RiverSystem sceneRiverSystem;
    Material forestGrassMaterialInstance;
    MaterialPropertyBlock forestGrassPropertyBlock;
    float forestGrassRenderDistanceSqr;

    int BuildChunkSeed(Vector2 offset, int salt)
    {
        int worldSeed = LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;

        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + worldSeed;
            hash = (hash * 31) + Mathf.RoundToInt(offset.x);
            hash = (hash * 31) + Mathf.RoundToInt(offset.y);
            hash = (hash * 31) + salt;
            return hash;
        }
    }

    public void Generate(Vector2 offset)
    {
        if (alreadyGenerated)
            return;

        alreadyGenerated = true;

        if (player == null)
            player = LanMultiplayerManager.FindWorldFocusTransform();

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        forestGrassRenderDistanceSqr = forestGrassRenderDistance * forestGrassRenderDistance;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        PrepareTerrainMaterial(meshRenderer);
        ApplySceneTerrainPalette(meshRenderer);
        sceneHeightmapData = SceneWorldDataResolver.ResolveHeightmapData(gameObject.scene);
        sceneRoadMaskData = SceneWorldDataResolver.ResolveRoadMaskData(gameObject.scene);
        sceneRiverSystem = ResolveSceneRiverSystem();

        vertices = new Vector3[(size + 1) * (size + 1)];
        colors = new Color[vertices.Length];
        uvs = new Vector2[vertices.Length];

        for (int z = 0; z <= size; z++)
        {
            for (int x = 0; x <= size; x++)
            {
                int i = z * (size + 1) + x;

                float worldX = x + offset.x;
                float worldZ = z + offset.y;

                Vector2 point = new Vector2(worldX, worldZ);

                float h = GetHeight(point);
                vertices[i] = new Vector3(x, h, z);

                uvs[i] = new Vector2(x / (float)size, z / (float)size);

                // 🌍 BIOMA
                BiomeType biome = GetBiome(point);

                switch (biome)
                {
                    case BiomeType.Desert:
                        colors[i] = new Color(0, 0, 1, 0); // areia
                        break;

                    case BiomeType.Forest:
                        colors[i] = new Color(0, 1, 0, 0); // grama
                        break;

                    case BiomeType.Snow:
                        colors[i] = new Color(1, 0, 0, 0); // neve
                        break;
                }

                if (IsRoadZone(point))
                    colors[i].a = 1f;
            }
        }

        BuildMesh();
        StartCoroutine(GenerateChunkDetails(offset));
    }

    void LateUpdate()
    {
        RenderForestGrass();
    }

    void OnDestroy()
    {
        if (forestGrassMaterialInstance != null)
            Destroy(forestGrassMaterialInstance);
    }

    void PrepareTerrainMaterial(MeshRenderer renderer)
    {
        if (renderer == null)
            return;

        if (terrainMaterial != null)
        {
            renderer.material = terrainMaterial;
        }
        else
        {
            Shader fallbackShader = Shader.Find("Custom/BiomeShader_URP") ??
                                    Shader.Find("Universal Render Pipeline/Lit") ??
                                    Shader.Find("Standard");

            if (fallbackShader != null)
                renderer.material = new Material(fallbackShader);
        }

        Material runtimeMaterial = renderer.material;
        if (runtimeMaterial == null)
            return;

        if (!runtimeMaterial.HasProperty("_UseFlatColors"))
        {
            Shader biomeShader = Shader.Find("Custom/BiomeShader_URP");
            if (biomeShader != null)
                runtimeMaterial.shader = biomeShader;
        }
    }

    void ApplySceneTerrainPalette(MeshRenderer renderer)
    {
        if (renderer == null)
            return;

        Material runtimeMaterial = renderer.material;
        if (runtimeMaterial == null || !runtimeMaterial.HasProperty("_UseFlatColors"))
            return;

        string sceneName = gameObject.scene.name;
        if (string.Equals(sceneName, "EnchantedForest", System.StringComparison.Ordinal))
        {
            runtimeMaterial.SetFloat(UseFlatColorsId, 1f);
            runtimeMaterial.SetColor(SandColorId, new Color(0.74f, 0.9f, 0.5f, 1f));
            runtimeMaterial.SetColor(GrassColorId, new Color(0.68f, 0.84f, 0.44f, 1f));
            runtimeMaterial.SetColor(SnowColorId, new Color(0.82f, 0.95f, 0.72f, 1f));
            runtimeMaterial.SetColor(RoadColorId, new Color(0.76f, 0.88f, 0.56f, 1f));
            return;
        }

        runtimeMaterial.SetFloat(UseFlatColorsId, 0f);
        runtimeMaterial.SetColor(RoadColorId, new Color(0.83f, 0.74f, 0.56f, 1f));
    }

    IEnumerator GenerateChunkDetails(Vector2 offset)
    {
        yield return null;
        SpawnRiverWater();
        yield return null;
        yield return SpawnVegetationAsync(offset);
        yield return null;
        yield return SpawnRockClustersAsync(offset);
        yield return null;
        yield return SpawnCowGroupsAsync(offset);
    }

    BiomeType GetBiome(Vector2 point)
    {
        if (IsEnchantedForestScene())
            return BiomeType.Forest;

        float biome = Mathf.PerlinNoise(point.x * biomeScale, point.y * biomeScale);

        if (biome < 0.33f)
            return BiomeType.Desert;

        if (biome < 0.66f)
            return BiomeType.Forest;

        return BiomeType.Snow;
    }

    bool IsInPlayerPath(Vector3 pos)
    {
        if (player == null)
            return false;

        Vector3 toObject = (pos - player.position).normalized;
        float dot = Vector3.Dot(player.forward, toObject);

        // 🔥 1 = na frente, 0 = lado, -1 = atrás
        return dot > 0.5f;
    }

    float GetHeight(Vector2 point)
    {
        return GetBaseHeight(point);
    }

    float GetBaseHeight(Vector2 point)
    {
        float h = GetTerrainSurfaceHeight(point);

        if (sceneHeightmapData != null && sceneHeightmapData.applyRoadFlattening && IsRoadZone(point))
            h = GetRoadFlattenedHeight(point, h);

        if (TryGetRiverBlend(point, out float riverBlend))
        {
            float riverDepthValue = sceneRiverSystem != null ? sceneRiverSystem.RiverDepth : riverDepth;
            float riverBedHeight = h - riverDepthValue * riverBlend;
            h = Mathf.Min(h, riverBedHeight);
        }
        return h;
    }

    float GetRoadFlattenedHeight(Vector2 point, float baseHeight)
    {
        float radius = 6f;
        float h0 = GetTerrainSurfaceHeight(point + new Vector2(-radius, 0f));
        float h1 = GetTerrainSurfaceHeight(point + new Vector2(radius, 0f));
        float h2 = GetTerrainSurfaceHeight(point + new Vector2(0f, -radius));
        float h3 = GetTerrainSurfaceHeight(point + new Vector2(0f, radius));
        float average = (baseHeight + h0 + h1 + h2 + h3) / 5f;
        return Mathf.Lerp(baseHeight, average, 0.75f);
    }

    float GetTerrainSurfaceHeight(Vector2 point)
    {
        if (sceneHeightmapData != null)
            return sceneHeightmapData.SampleWorldHeight(point);

        float h = Mathf.PerlinNoise(point.x / terrainScale, point.y / terrainScale) * heightMultiplier;
        h += Mathf.PerlinNoise(point.x * 0.05f, point.y * 0.05f) * 2f;
        return h;
    }

    int GetWorldSeed()
    {
        return LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;
    }

    bool TryGetRiverBlend(Vector2 point, out float blend)
    {
        if (!enableRiver || sceneRiverSystem == null)
        {
            blend = 0f;
            return false;
        }

        return sceneRiverSystem.TryGetBlend(point, GetBiome(point) == BiomeType.Forest, out blend);
    }

    float GetRiverDistance(Vector2 point)
    {
        float riverCenterX = GetRiverCenterX(point.y);
        return Mathf.Abs(point.x - riverCenterX);
    }

    float GetRiverCenterX(float worldZ)
    {
        float noise = Mathf.PerlinNoise(73.41f, worldZ * riverCurveScale);
        return (noise - 0.5f) * riverWorldWidth;
    }

    bool IsRiverZone(Vector2 point, float extraMargin = 0f)
    {
        if (!enableRiver || sceneRiverSystem == null)
            return false;

        return sceneRiverSystem.IsRiverZone(point, GetBiome(point) == BiomeType.Forest, extraMargin);
    }

    RiverSystem ResolveSceneRiverSystem()
    {
        RiverSystem[] riverSystems = FindObjectsByType<RiverSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < riverSystems.Length; i++)
        {
            if (riverSystems[i] != null && riverSystems[i].gameObject.scene == gameObject.scene)
                return riverSystems[i];
        }

        return null;
    }

    bool IsRoadZone(Vector2 point)
    {
        return sceneRoadMaskData != null && sceneRoadMaskData.IsRoad(point);
    }

    bool IsNearRoadZone(Vector2 point, float padding)
    {
        if (sceneRoadMaskData == null)
            return false;

        if (!sceneRoadMaskData.ContainsWorldPoint(point))
            return false;

        float edgeThreshold = Mathf.Max(0.42f, sceneRoadMaskData.roadThreshold * 0.9f);

        if (sceneRoadMaskData.SampleMask01(point) >= edgeThreshold)
            return true;

        if (padding <= 0.01f)
            return false;

        Vector2[] offsets =
        {
            Vector2.zero,
            new Vector2(padding, 0f),
            new Vector2(-padding, 0f),
            new Vector2(0f, padding),
            new Vector2(0f, -padding),
            new Vector2(padding * 0.72f, padding * 0.72f),
            new Vector2(-padding * 0.72f, padding * 0.72f),
            new Vector2(padding * 0.72f, -padding * 0.72f),
            new Vector2(-padding * 0.72f, -padding * 0.72f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            if (sceneRoadMaskData.SampleMask01(point + offsets[i]) >= edgeThreshold)
                return true;
        }

        return false;
    }

    void SpawnRiverWater()
    {
        return;
    }

    List<RiverSample> CollectRiverSamples()
    {
        List<RiverSample> samples = new List<RiverSample>();
        float step = Mathf.Max(0.75f, riverVisualStep);
        float overlap = Mathf.Max(step, riverChunkOverlap);

        for (float localZ = -overlap; localZ <= size + overlap; localZ += step)
        {
            float worldZ = transform.position.z + localZ;
            float worldX = GetRiverCenterX(worldZ);
            float localX = worldX - transform.position.x;
            float maxHalfWidth = Mathf.Max(riverWidth, GetRiverHalfWidth(worldZ));

            if (localX < -maxHalfWidth * 1.5f || localX > size + maxHalfWidth * 1.5f)
                continue;

            Vector2 riverPoint = new Vector2(worldX, worldZ);
            if (GetBiome(riverPoint) != BiomeType.Forest)
                continue;

            if (localZ < -riverChunkOverlap || localZ > size + riverChunkOverlap)
                continue;

            samples.Add(new RiverSample
            {
                localCenter = new Vector3(localX, GetRiverWaterHeight(riverPoint), localZ),
                halfWidth = GetRiverVisualHalfWidth(worldZ)
            });
        }

        return samples;
    }

    List<List<RiverSample>> SplitRiverSegments(List<RiverSample> samples)
    {
        List<List<RiverSample>> segments = new List<List<RiverSample>>();
        if (samples == null || samples.Count == 0)
            return segments;

        float maxGap = Mathf.Max(1.5f, riverMaxSegmentGap);
        List<RiverSample> currentSegment = new List<RiverSample> { samples[0] };

        for (int i = 1; i < samples.Count; i++)
        {
            float gap = Vector3.Distance(samples[i - 1].localCenter, samples[i].localCenter);
            if (gap > maxGap)
            {
                if (currentSegment.Count > 0)
                    segments.Add(currentSegment);

                currentSegment = new List<RiverSample>();
            }

            currentSegment.Add(samples[i]);
        }

        if (currentSegment.Count > 0)
            segments.Add(currentSegment);

        return segments;
    }

    void CreateRiverSurface(Transform riverRoot, List<RiverSample> samples, int segmentIndex)
    {
        GameObject riverSurface = new GameObject($"RiverSurface_{segmentIndex}");
        riverSurface.transform.SetParent(riverRoot, false);

        MeshFilter meshFilter = riverSurface.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = riverSurface.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = GetRiverMaterial();

        Mesh riverMesh = new Mesh
        {
            name = "RiverSurfaceMesh"
        };

        int bodyVertexCount = samples.Count * 2;
        Vector3[] riverVertices = new Vector3[bodyVertexCount];
        Vector2[] riverUvs = new Vector2[bodyVertexCount];
        List<int> riverTriangles = new List<int>((samples.Count - 1) * 6);

        float accumulatedLength = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            RiverSample current = samples[i];
            Vector3 tangent = GetRiverTangent(samples, i);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 offset = right * current.halfWidth;

            int vertexIndex = i * 2;
            riverVertices[vertexIndex] = current.localCenter - offset;
            riverVertices[vertexIndex + 1] = current.localCenter + offset;

            if (i > 0)
                accumulatedLength += Vector3.Distance(samples[i - 1].localCenter, current.localCenter);

            riverUvs[vertexIndex] = new Vector2(0f, accumulatedLength);
            riverUvs[vertexIndex + 1] = new Vector2(1f, accumulatedLength);

            if (i >= samples.Count - 1)
                continue;

            int triangleIndex = i * 6;
            riverTriangles.Add(vertexIndex);
            riverTriangles.Add(vertexIndex + 2);
            riverTriangles.Add(vertexIndex + 1);
            riverTriangles.Add(vertexIndex + 1);
            riverTriangles.Add(vertexIndex + 2);
            riverTriangles.Add(vertexIndex + 3);
        }

        riverMesh.vertices = riverVertices;
        riverMesh.triangles = riverTriangles.ToArray();
        riverMesh.uv = riverUvs;
        riverMesh.RecalculateNormals();
        riverMesh.RecalculateBounds();
        meshFilter.sharedMesh = riverMesh;
    }

    void CreateRiverTriggers(Transform riverRoot, List<RiverSample> samples, int segmentIndex)
    {
        float targetSpacing = Mathf.Max(2f, riverTriggerStep);
        float accumulatedDistance = 0f;

        for (int i = 0; i < samples.Count; i++)
        {
            if (i > 0)
                accumulatedDistance += Vector3.Distance(samples[i - 1].localCenter, samples[i].localCenter);

            bool isLast = i == samples.Count - 1;
            if (!isLast && accumulatedDistance < targetSpacing)
                continue;

            accumulatedDistance = 0f;

            if (samples[i].localCenter.z < 0f || samples[i].localCenter.z > size)
                continue;

            GameObject triggerObject = new GameObject($"RiverWater_{segmentIndex}");
            triggerObject.transform.SetParent(riverRoot, false);
            triggerObject.transform.localPosition = samples[i].localCenter;
            triggerObject.transform.localRotation = Quaternion.LookRotation(GetRiverTangent(samples, i), Vector3.up);

            BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(Mathf.Max(1.4f, samples[i].halfWidth * 2.2f), 2.4f, targetSpacing);

            triggerObject.AddComponent<RiverWaterSource>();
        }
    }

    float GetRiverWaterHeight(Vector2 point)
    {
        if (vertices == null || vertices.Length == 0)
            return riverWaterHeightOffset;

        float localX = point.x - transform.position.x;
        float localZ = point.y - transform.position.z;

        float centerHeight = SampleTerrainHeight(localX, localZ);
        float previousHeight = SampleTerrainHeight(localX, localZ - 1f);
        float nextHeight = SampleTerrainHeight(localX, localZ + 1f);
        float waterBase = Mathf.Min(centerHeight, Mathf.Min(previousHeight, nextHeight));

        return waterBase + riverWaterHeightOffset;
    }

    void SmoothRiverSamples(List<RiverSample> samples)
    {
        if (samples == null || samples.Count < 3)
            return;

        List<RiverSample> smoothed = new List<RiverSample>(samples.Count * 2);
        smoothed.Add(samples[0]);

        for (int i = 0; i < samples.Count - 1; i++)
        {
            RiverSample current = samples[i];
            RiverSample next = samples[i + 1];

            RiverSample q = new RiverSample
            {
                localCenter = Vector3.Lerp(current.localCenter, next.localCenter, 0.25f),
                halfWidth = Mathf.Lerp(current.halfWidth, next.halfWidth, 0.25f)
            };

            RiverSample r = new RiverSample
            {
                localCenter = Vector3.Lerp(current.localCenter, next.localCenter, 0.75f),
                halfWidth = Mathf.Lerp(current.halfWidth, next.halfWidth, 0.75f)
            };

            smoothed.Add(q);
            smoothed.Add(r);
        }

        smoothed.Add(samples[samples.Count - 1]);

        for (int i = 1; i < smoothed.Count - 1; i++)
        {
            Vector3 averagedCenter = (smoothed[i - 1].localCenter + smoothed[i].localCenter + smoothed[i + 1].localCenter) / 3f;
            float smoothedHeight = Mathf.Lerp(smoothed[i].localCenter.y, averagedCenter.y, riverSurfaceSmoothing);
            smoothed[i] = new RiverSample
            {
                localCenter = new Vector3(smoothed[i].localCenter.x, smoothedHeight, smoothed[i].localCenter.z),
                halfWidth = Mathf.Lerp(smoothed[i].halfWidth, (smoothed[i - 1].halfWidth + smoothed[i].halfWidth + smoothed[i + 1].halfWidth) / 3f, 0.5f)
            };
        }

        samples.Clear();
        samples.AddRange(smoothed);
    }

    Vector3 GetRiverTangent(List<RiverSample> samples, int index)
    {
        Vector3 tangent;

        if (index <= 0)
            tangent = samples[1].localCenter - samples[0].localCenter;
        else if (index >= samples.Count - 1)
            tangent = samples[index].localCenter - samples[index - 1].localCenter;
        else
            tangent = samples[index + 1].localCenter - samples[index - 1].localCenter;

        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.forward;

        return tangent.normalized;
    }

    Material GetRiverMaterial()
    {
        if (riverMaterial != null)
            return riverMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        riverMaterial = new Material(shader);
        riverMaterial.color = new Color(0.12f, 0.5f, 0.78f, 0.85f);

        if (riverMaterial.HasProperty("_Smoothness"))
            riverMaterial.SetFloat("_Smoothness", 0.9f);

        if (riverMaterial.HasProperty("_BaseColor"))
            riverMaterial.SetColor("_BaseColor", new Color(0.12f, 0.5f, 0.78f, 0.85f));

        return riverMaterial;
    }

    float GetRiverHalfWidth(float worldZ)
    {
        float variationNoise = Mathf.PerlinNoise(14.37f, worldZ * riverCurveScale * 1.4f);
        float widthMultiplier = Mathf.Lerp(1f - riverWidthVariation * 0.35f, 1f + riverWidthVariation, variationNoise);
        return Mathf.Max(1.5f, riverWidth * widthMultiplier);
    }

    float GetRiverVisualHalfWidth(float worldZ)
    {
        float baseHalfWidth = GetRiverHalfWidth(worldZ) * riverVisualWidthMultiplier;
        return Mathf.Max(0.55f, baseHalfWidth);
    }

    float SampleTerrainHeight(float localX, float localZ)
    {
        if (vertices == null || vertices.Length == 0)
            return 0f;

        float clampedX = Mathf.Clamp(localX, 0f, size);
        float clampedZ = Mathf.Clamp(localZ, 0f, size);

        int x0 = Mathf.FloorToInt(clampedX);
        int z0 = Mathf.FloorToInt(clampedZ);
        int x1 = Mathf.Min(x0 + 1, size);
        int z1 = Mathf.Min(z0 + 1, size);

        float tx = clampedX - x0;
        float tz = clampedZ - z0;

        float h00 = vertices[z0 * (size + 1) + x0].y;
        float h10 = vertices[z0 * (size + 1) + x1].y;
        float h01 = vertices[z1 * (size + 1) + x0].y;
        float h11 = vertices[z1 * (size + 1) + x1].y;

        float hx0 = Mathf.Lerp(h00, h10, tx);
        float hx1 = Mathf.Lerp(h01, h11, tx);
        return Mathf.Lerp(hx0, hx1, tz);
    }

    struct RiverSample
    {
        public Vector3 localCenter;
        public float halfWidth;
    }

    void BuildMesh()
    {
        triangles = new int[size * size * 6];

        int vert = 0;
        int tris = 0;

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                triangles[tris] = vert;
                triangles[tris + 1] = vert + size + 1;
                triangles[tris + 2] = vert + 1;

                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + size + 1;
                triangles[tris + 5] = vert + size + 2;

                vert++;
                tris += 6;
            }
            vert++;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.uv = uvs;

        mesh.RecalculateNormals();

        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    // 🌲 VEGETAÇÃO AJUSTADA
    IEnumerator SpawnVegetationAsync(Vector2 offset)
    {
        ChunkRandom rng = new ChunkRandom(BuildChunkSeed(offset, 101));
        usedPositions.Clear();
        forestGrassBatches.Clear();
        forestGrassBatchCounts.Clear();
        List<Vector3> grassPositions = new List<Vector3>();
        List<Matrix4x4> grassMatrices = new List<Matrix4x4>(Mathf.Max(maxForestGrassPerChunk, 1024));
        int treeCount = 0;
        int iterationsSinceYield = 0;


        for (int i = 0; i < vertices.Length; i += 2)
        {
            iterationsSinceYield++;
            if (iterationsSinceYield >= Mathf.Max(20, generationYieldInterval))
            {
                iterationsSinceYield = 0;
                yield return null;
            }

            Vector3 pos = vertices[i];
            Vector3 normal = mesh.normals[i];

            Vector3 worldPos = pos + transform.position;
            bool isNearPlayer = player != null && Vector3.Distance(worldPos, player.position) < safeRadius;
            bool isInPlayerPath = player != null &&
                                  Vector3.Distance(worldPos, player.position) < forwardSafeDistance &&
                                  IsInPlayerPath(worldPos);

            if (normal.y < 0.78f)
                continue;

            Vector2 point = new Vector2(pos.x + offset.x, pos.z + offset.y);
            BiomeType biome = GetBiome(point);

            if (IsRiverZone(point, 1.5f))
                continue;

            if (IsRoadZone(point))
                continue;

            float cluster = Mathf.PerlinNoise(point.x * 0.05f, point.y * 0.05f);

            if (isNearPlayer || isInPlayerPath)
                continue;

            if (biome == BiomeType.Forest && cluster < 0.18f)
                continue;

            float density = treeDensity;
            int maxTreesForChunk = maxTreesPerChunk;
            float minTreeSpacing = minTreeDistance;

            switch (biome)
            {
                case BiomeType.Desert:
                    density = 0.01f;
                    break;

                case BiomeType.Forest:
                    density = treeDensity;
                    break;

                case BiomeType.Snow:
                    density = treeDensity * 0.8f;
                    break;
            }

            if (IsEnchantedForestScene() && biome == BiomeType.Forest)
            {
                density = Mathf.Clamp01(density * 1.55f);
                maxTreesForChunk = Mathf.Max(maxTreesPerChunk, 34);
                minTreeSpacing = Mathf.Min(minTreeDistance, 5f);
            }


            // 🌲 ÁRVORES
            if (treeCount < maxTreesForChunk && rng.Value() < density)
            {
                List<TreeData> validTrees = new List<TreeData>();

                foreach (var tree in trees)
                {
                    if (tree.biome == biome)
                    {
                        validTrees.Add(tree);
                    }

                }

                if (validTrees.Count > 0)
                {
                    TreeData selected = validTrees[rng.Range(0, validTrees.Count)];
                    Vector3 groundPoint = GetGroundPoint(worldPos);

                    if (IsTooClose(groundPoint, minTreeSpacing))
                        continue;

                    GameObject tree = Instantiate(
                        selected.prefab,
                        groundPoint,
                        Quaternion.Euler(0f, rng.Range(0f, 360f), 0f),
                        transform
                    );

                    if (biome == BiomeType.Forest)
                    {
                        Vector3 scale = tree.transform.localScale;
                        tree.transform.localScale = new Vector3(
                            scale.x * forestTreeWidthMultiplier,
                            scale.y * forestTreeHeightMultiplier,
                            scale.z * forestTreeWidthMultiplier
                        );
                    }

                    AlignObjectBaseToGround(tree, groundPoint, selected.yOffset);

                    usedPositions.Add(groundPoint);
                    treeCount++;
                }
            }

            // 🍄
            if (!IsEnchantedForestScene())
                continue;

            float mushroomSpawnChance = Mathf.Clamp01(mushroomDensity * 8f);
            if (biome == BiomeType.Forest && rng.Value() < mushroomSpawnChance)
            {
                if (IsEnchantedForestScene() && TrySpawnEnchantedForestMushroom(rng, worldPos))
                {
                    continue;
                }

                int mushroomCount = 3;

                for (int m = 0; m < mushroomCount; m++)
                {
                    Vector3 offsetPos = new Vector3(
                        rng.Range(-1.5f, 1.5f),
                        0,
                        rng.Range(-1.5f, 1.5f)
                    );

                    Vector3 finalPos = worldPos + offsetPos;
                    Vector3 finalGroundPoint = GetGroundPoint(finalPos);

                    if (IsTooClose(finalGroundPoint))
                        continue;

                    Instantiate(
                        mushroomPrefab,
                        finalGroundPoint + Vector3.up * 0.2f,
                        Quaternion.Euler(0f, rng.Range(0f, 360f), 0f),
                        transform
                    );

                    usedPositions.Add(finalGroundPoint);
                }
            }
        }

        GenerateForestGrassMatrices(offset, grassMatrices, grassPositions);
        CacheForestGrassBatches(grassMatrices);
    }

    bool TrySpawnEnchantedForestMushroom(ChunkRandom rng, Vector3 worldPos)
    {
        GameObject mushroomToSpawn = rng.Value() < enchantedForestClusterChance
            ? GetEnchantedForestClusterMushroomPrefab()
            : GetEnchantedForestSingleMushroomPrefab();

        if (mushroomToSpawn == null)
            return false;

        Vector3 offsetPos = new Vector3(
            rng.Range(-2.4f, 2.4f),
            0f,
            rng.Range(-2.4f, 2.4f)
        );

        Vector3 finalGroundPoint = GetGroundPoint(worldPos + offsetPos);
        if (IsTooClose(finalGroundPoint, enchantedForestMushroomMinDistance))
            return false;

        GameObject mushroomInstance = Instantiate(
            mushroomToSpawn,
            finalGroundPoint + Vector3.up * enchantedForestMushroomYOffset,
            Quaternion.Euler(0f, rng.Range(0f, 360f), 0f),
            transform
        );

        float minScale = Mathf.Min(enchantedForestMushroomScaleRange.x, enchantedForestMushroomScaleRange.y);
        float maxScale = Mathf.Max(enchantedForestMushroomScaleRange.x, enchantedForestMushroomScaleRange.y);
        float scaleMultiplier = rng.Range(minScale, maxScale);
        mushroomInstance.transform.localScale *= scaleMultiplier;

        if (mushroomInstance.GetComponent<BreathingScale>() == null)
        {
            BreathingScale breathing = mushroomInstance.AddComponent<BreathingScale>();
            breathing.speed = rng.Range(0.9f, 1.5f);
            breathing.amplitude = rng.Range(0.03f, 0.07f);
            breathing.verticalBias = 1.1f;
        }

        usedPositions.Add(finalGroundPoint);
        return true;
    }

    GameObject GetEnchantedForestSingleMushroomPrefab()
    {
        if (enchantedForestMushroomSinglePrefab != null)
            return enchantedForestMushroomSinglePrefab;

        if (cachedEnchantedForestSingleMushroom == null)
            cachedEnchantedForestSingleMushroom = Resources.Load<GameObject>("World/Scenes/EnchantedForest/cocumelom");

        return cachedEnchantedForestSingleMushroom;
    }

    GameObject GetEnchantedForestClusterMushroomPrefab()
    {
        if (enchantedForestMushroomClusterPrefab != null)
            return enchantedForestMushroomClusterPrefab;

        if (cachedEnchantedForestClusterMushroom == null)
            cachedEnchantedForestClusterMushroom = Resources.Load<GameObject>("World/Scenes/EnchantedForest/cocumeloscluster");

        return cachedEnchantedForestClusterMushroom;
    }

    bool IsEnchantedForestScene()
    {
        return string.Equals(gameObject.scene.name, "EnchantedForest", System.StringComparison.Ordinal);
    }

    bool IsTooClose(Vector3 pos, float minDistance = -1f)
    {
        float distanceLimit = minDistance >= 0f ? minDistance : minDistanceBetweenObjects;

        return IsTooCloseToPositions(usedPositions, pos, distanceLimit);
    }

    bool IsTooCloseToPositions(List<Vector3> positions, Vector3 pos, float minDistance)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            if (Vector3.Distance(positions[i], pos) < minDistance)
                return true;
        }

        return false;
    }

    void CacheForestGrassBatches(List<Matrix4x4> matrices)
    {
        forestGrassBatches.Clear();
        forestGrassBatchCounts.Clear();

        for (int i = 0; i < matrices.Count; i += GrassBatchSize)
        {
            int count = Mathf.Min(GrassBatchSize, matrices.Count - i);
            Matrix4x4[] batch = new Matrix4x4[count];
            matrices.CopyTo(i, batch, 0, count);
            forestGrassBatches.Add(batch);
            forestGrassBatchCounts.Add(count);
        }
    }

    void GenerateForestGrassMatrices(Vector2 offset, List<Matrix4x4> grassMatrices, List<Vector3> grassPositions)
    {
        ChunkRandom grassRng = new ChunkRandom(BuildChunkSeed(offset, 111));
        float grassDensity = forestGrassDensity;
        int maxGrassForChunk = maxForestGrassPerChunk;
        int sampleStep = Mathf.Max(1, forestGrassSampleStep);

        if (IsEnchantedForestScene())
        {
            grassDensity = Mathf.Clamp01(grassDensity);
            maxGrassForChunk = Mathf.Max(maxGrassForChunk, 2800);
        }

        int secondaryOffset = sampleStep > 1 ? Mathf.Max(1, sampleStep / 2) : 0;
        int passCount = secondaryOffset > 0 ? 2 : 1;

        for (int pass = 0; pass < passCount && grassMatrices.Count < maxGrassForChunk; pass++)
        {
            int startOffset = pass == 0 ? 0 : secondaryOffset;
            float passCoverage = pass == 0 ? 1f : forestGrassExtraCoverage;

            for (int z = startOffset; z <= size && grassMatrices.Count < maxGrassForChunk; z += sampleStep)
            {
                for (int x = startOffset; x <= size && grassMatrices.Count < maxGrassForChunk; x += sampleStep)
                {
                    int index = z * (size + 1) + x;
                    if (index < 0 || index >= vertices.Length)
                        continue;

                    if (mesh.normals[index].y < 0.72f)
                        continue;

                    if (grassRng.Value() > grassDensity * passCoverage)
                        continue;

                    float localX = x + grassRng.Range(-forestGrassSpawnJitter, forestGrassSpawnJitter);
                    float localZ = z + grassRng.Range(-forestGrassSpawnJitter, forestGrassSpawnJitter);
                    Vector2 point = new Vector2(offset.x + localX, offset.y + localZ);

                    if (GetBiome(point) != BiomeType.Forest)
                        continue;

                    if (IsRiverZone(point, 0.45f))
                        continue;

                    if (IsNearRoadZone(point, forestGrassRoadPadding))
                        continue;

                    float height = SampleTerrainHeight(localX, localZ);
                    Vector3 grassGroundPoint = new Vector3(
                        transform.position.x + localX,
                        transform.position.y + height + forestGrassYOffset,
                        transform.position.z + localZ
                    );

                    if (player != null && Vector3.Distance(grassGroundPoint, player.position) <= 0.8f)
                        continue;

                    if (IsTooCloseToPositions(grassPositions, grassGroundPoint, forestGrassMinDistance))
                        continue;

                    float grassWidthScale = grassRng.Range(
                        Mathf.Min(forestGrassWidthRange.x, forestGrassWidthRange.y),
                        Mathf.Max(forestGrassWidthRange.x, forestGrassWidthRange.y));
                    float grassHeightScale = grassRng.Range(
                        Mathf.Min(forestGrassHeightRange.x, forestGrassHeightRange.y),
                        Mathf.Max(forestGrassHeightRange.x, forestGrassHeightRange.y));

                    Matrix4x4 matrix = Matrix4x4.TRS(
                        grassGroundPoint,
                        Quaternion.Euler(0f, grassRng.Range(0f, 360f), 0f),
                        new Vector3(grassWidthScale, grassHeightScale, grassWidthScale)
                    );

                    grassMatrices.Add(matrix);
                    grassPositions.Add(grassGroundPoint);
                }
            }
        }
    }

    void RenderForestGrass()
    {
        if (forestGrassBatchCounts.Count == 0)
            return;

        if (player == null)
            player = LanMultiplayerManager.FindWorldFocusTransform();

        if (player != null)
        {
            Vector3 closestPoint = new Vector3(
                Mathf.Clamp(player.position.x, transform.position.x, transform.position.x + size),
                player.position.y,
                Mathf.Clamp(player.position.z, transform.position.z, transform.position.z + size)
            );

            if ((closestPoint - player.position).sqrMagnitude > forestGrassRenderDistanceSqr)
                return;
        }

        Material grassMaterial = GetForestGrassMaterial();
        Mesh grassMesh = GetForestGrassMesh();
        if (grassMaterial == null || grassMesh == null)
            return;

        if (forestGrassPropertyBlock == null)
            forestGrassPropertyBlock = new MaterialPropertyBlock();

        forestGrassPropertyBlock.Clear();
        forestGrassPropertyBlock.SetColor(GrassBaseColorId, forestGrassBaseColor);
        forestGrassPropertyBlock.SetColor(GrassTipColorId, forestGrassTipColor);
        forestGrassPropertyBlock.SetFloat(GrassWindStrengthId, forestGrassWindStrength);
        forestGrassPropertyBlock.SetFloat(GrassWindSpeedId, forestGrassWindSpeed);
        forestGrassPropertyBlock.SetFloat(GrassBendStrengthId, forestGrassBendStrength);
        forestGrassPropertyBlock.SetFloat(GrassTimeOffsetId, transform.position.x * 0.031f + transform.position.z * 0.017f);

        for (int i = 0; i < forestGrassBatches.Count; i++)
        {
            Graphics.DrawMeshInstanced(
                grassMesh,
                0,
                grassMaterial,
                forestGrassBatches[i],
                forestGrassBatchCounts[i],
                forestGrassPropertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                null,
                LightProbeUsage.Off
            );
        }
    }

    Material GetForestGrassMaterial()
    {
        if (forestGrassMaterialInstance != null)
            return forestGrassMaterialInstance;

        Shader shader = Shader.Find("Custom/ForestGrassInstanced");
        if (shader == null)
            return null;

        forestGrassMaterialInstance = new Material(shader)
        {
            enableInstancing = true
        };
        forestGrassMaterialInstance.name = "ForestGrassRuntime";
        return forestGrassMaterialInstance;
    }

    Mesh GetForestGrassMesh()
    {
        if (forestGrassMesh != null)
            return forestGrassMesh;

        forestGrassMesh = new Mesh
        {
            name = "ForestGrassTuft"
        };

        List<Vector3> meshVertices = new List<Vector3>();
        List<Vector2> meshUvs = new List<Vector2>();
        List<int> meshTriangles = new List<int>();

        AddGrassSpike(meshVertices, meshUvs, meshTriangles, 0f, new Vector3(0f, 0f, 0.02f), 0.48f, 0.28f, 0.92f);
        AddGrassSpike(meshVertices, meshUvs, meshTriangles, 42f, new Vector3(0.1f, 0f, 0.02f), 0.36f, 0.2f, 0.82f);
        AddGrassSpike(meshVertices, meshUvs, meshTriangles, -46f, new Vector3(-0.1f, 0f, 0.01f), 0.35f, 0.2f, 0.8f);
        AddGrassSpike(meshVertices, meshUvs, meshTriangles, 92f, new Vector3(0.04f, 0f, -0.1f), 0.32f, 0.18f, 0.76f);
        AddGrassSpike(meshVertices, meshUvs, meshTriangles, -96f, new Vector3(-0.05f, 0f, -0.1f), 0.31f, 0.18f, 0.74f);
        AddGrassSpike(meshVertices, meshUvs, meshTriangles, 138f, new Vector3(0.13f, 0f, -0.04f), 0.26f, 0.16f, 0.68f);
        AddGrassSpike(meshVertices, meshUvs, meshTriangles, -142f, new Vector3(-0.13f, 0f, -0.03f), 0.26f, 0.16f, 0.68f);

        forestGrassMesh.SetVertices(meshVertices);
        forestGrassMesh.SetUVs(0, meshUvs);
        forestGrassMesh.SetTriangles(meshTriangles, 0);
        forestGrassMesh.RecalculateNormals();
        forestGrassMesh.RecalculateBounds();

        return forestGrassMesh;
    }

    void AddGrassSpike(
        List<Vector3> meshVertices,
        List<Vector2> meshUvs,
        List<int> meshTriangles,
        float angleY,
        Vector3 offset,
        float width,
        float depth,
        float height)
    {
        Quaternion rotation = Quaternion.Euler(0f, angleY, 0f);
        Vector3 baseA = new Vector3(-width * 0.5f, 0f, -depth * 0.35f);
        Vector3 baseB = new Vector3(width * 0.5f, 0f, -depth * 0.35f);
        Vector3 baseC = new Vector3(0f, 0f, depth * 0.55f);
        Vector3 tip = new Vector3(0f, height, 0.02f);

        AddGrassTriangle(meshVertices, meshUvs, meshTriangles, rotation * baseA + offset, rotation * baseB + offset, rotation * tip + offset);
        AddGrassTriangle(meshVertices, meshUvs, meshTriangles, rotation * baseB + offset, rotation * baseC + offset, rotation * tip + offset);
        AddGrassTriangle(meshVertices, meshUvs, meshTriangles, rotation * baseC + offset, rotation * baseA + offset, rotation * tip + offset);
    }

    void AddGrassTriangle(
        List<Vector3> meshVertices,
        List<Vector2> meshUvs,
        List<int> meshTriangles,
        Vector3 baseLeft,
        Vector3 baseRight,
        Vector3 tip)
    {
        int start = meshVertices.Count;
        meshVertices.Add(baseLeft);
        meshVertices.Add(baseRight);
        meshVertices.Add(tip);

        meshUvs.Add(new Vector2(0f, 0f));
        meshUvs.Add(new Vector2(1f, 0f));
        meshUvs.Add(new Vector2(0.5f, 1f));

        meshTriangles.Add(start);
        meshTriangles.Add(start + 1);
        meshTriangles.Add(start + 2);
    }

    IEnumerator SpawnRockClustersAsync(Vector2 offset)
    {
        ChunkRandom rng = new ChunkRandom(BuildChunkSeed(offset, 202));
        float density = rockDensity;
        int iterationsSinceYield = 0;

        Vector2 centerPoint = new Vector2(offset.x + size / 2, offset.y + size / 2);
        BiomeType biome = GetBiome(centerPoint);

        // 🎯 densidade por bioma (bem mais controlado)
        switch (biome)
        {
            case BiomeType.Desert:
                density = 0.08f; // 🔥 quase vazio
                break;

            case BiomeType.Forest:
                density = 0.25f;
                break;

            case BiomeType.Snow:
                density = 0.15f;
                break;
        }

        int rockCount = 0;
        int maxRocksPerChunk = 2; // 🔥 limite baixo

        for (int c = 0; c < rockClusterCount; c++)
        {
            iterationsSinceYield++;
            if (iterationsSinceYield >= Mathf.Max(4, generationYieldInterval / 8))
            {
                iterationsSinceYield = 0;
                yield return null;
            }

            if (rockCount >= maxRocksPerChunk)
                break;

            // 🎯 chance de nem gerar cluster
            if (rng.Value() > density)
                continue;

            float x = rng.Range(0f, size);
            float z = rng.Range(0f, size);

            int index = (int)z * (size + 1) + (int)x;

            if (index < 0 || index >= vertices.Length)
                continue;

            Vector3 basePos = vertices[index] + transform.position;
            Vector2 basePoint = new Vector2(basePos.x, basePos.z);

            if (IsRiverZone(basePoint, 2f))
                continue;

            if (IsRoadZone(basePoint))
                continue;

            // 🚫 evita spawn perto do player
            if (player != null && Vector3.Distance(basePos, player.position) < 14f)
                continue;

            // 🚫 evita spawn em terreno inclinado
            if (mesh.normals[index].y < 0.9f)
                continue;

            // 🎯 sempre 1 pedra por cluster (controle total)
            int rocks = 1;

            for (int i = 0; i < rocks; i++)
            {
                if (rockCount >= maxRocksPerChunk)
                    break;

                Vector3 offsetPos = new Vector3(
                    rng.Range(-10f, 10f), // 🔥 bem espalhado
                    0,
                    rng.Range(-10f, 10f)
                );

                Vector3 spawnPos = basePos + offsetPos;

                spawnPos = GetGroundPoint(spawnPos);

                if (IsRiverZone(new Vector2(spawnPos.x, spawnPos.z), 2f))
                    continue;

                if (IsRoadZone(new Vector2(spawnPos.x, spawnPos.z)))
                    continue;

                // 🚫 evita perto do player
                if (player != null && Vector3.Distance(spawnPos, player.position) < 14f)
                    continue;

                // 🚫 evita na frente do player
                if (player != null && Vector3.Distance(spawnPos, player.position) < 12f && IsInPlayerPath(spawnPos))
                    continue;

                // 🚫 evita sobreposição
                if (IsTooClose(spawnPos))
                    continue;

                GameObject rock = Instantiate(
                    GetRandomRock(rng),
                    spawnPos,
                    Quaternion.Euler(0f, rng.Range(0f, 360f), 0f),
                    transform
                );

                AlignObjectBaseToGround(rock, spawnPos);

                rockCount++;
            }
        }
    }

    void AlignObjectBaseToGround(GameObject obj, Vector3 groundPoint, float extraYOffset = 0f)
    {
        if (obj == null)
            return;

        float lowestY = float.MaxValue;
        bool foundBounds = false;

        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null || !col.enabled || col.isTrigger)
                    continue;

                lowestY = Mathf.Min(lowestY, col.bounds.min.y);
                foundBounds = true;
            }
        }

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rend = renderers[i];
                if (rend == null || !rend.enabled)
                    continue;

                lowestY = Mathf.Min(lowestY, rend.bounds.min.y);
                foundBounds = true;
            }
        }

        if (!foundBounds)
            return;

        float groundOffset = groundPoint.y - lowestY;
        obj.transform.position += Vector3.up * (groundOffset + extraYOffset);
    }

    Vector3 GetGroundPoint(Vector3 worldPos)
    {
        Collider terrainCollider = GetComponent<Collider>();

        if (terrainCollider != null)
        {
            Ray ray = new Ray(worldPos + Vector3.up * 50f, Vector3.down);

            if (terrainCollider.Raycast(ray, out RaycastHit hit, 100f))
                return hit.point;
        }

        if (Physics.Raycast(worldPos + Vector3.up * 50f, Vector3.down, out RaycastHit fallbackHit, 100f, ~0, QueryTriggerInteraction.Ignore))
            return fallbackHit.point;

        return worldPos;
    }

    IEnumerator SpawnCowGroupsAsync(Vector2 offset)
    {
        ChunkRandom rng = new ChunkRandom(BuildChunkSeed(offset, 303));
        int desiredGroups = Mathf.Max(0, maxCowGroupsPerChunk);

        if (cowPrefab == null || desiredGroups == 0)
            yield break;

        cowGroupPositions.Clear();
        List<Vector3> validPositions = new List<Vector3>();
        int iterationsSinceYield = 0;

        for (int i = 0; i < vertices.Length; i += 18)
        {
            iterationsSinceYield++;
            if (iterationsSinceYield >= Mathf.Max(20, generationYieldInterval))
            {
                iterationsSinceYield = 0;
                yield return null;
            }

            Vector3 localPos = vertices[i];
            Vector3 worldPos = localPos + transform.position;

            if (player != null && Vector3.Distance(worldPos, player.position) < safeRadius + 8f)
                continue;

            if (mesh.normals[i].y < 0.92f)
                continue;

            Vector2 point = new Vector2(localPos.x + offset.x, localPos.z + offset.y);
            if (GetBiome(point) != BiomeType.Forest)
                continue;

            if (IsRiverZone(point, 3f))
                continue;

            if (IsRoadZone(point))
                continue;

            if (IsTooClose(worldPos))
                continue;

            validPositions.Add(worldPos);
        }

        int groupsSpawned = 0;

        while (groupsSpawned < desiredGroups && validPositions.Count > 0)
        {
            yield return null;

            if (rng.Value() > cowGroupChance)
                break;

            int index = rng.Range(0, validPositions.Count);
            Vector3 chosenPos = validPositions[index];

            CreateCowSpawnPoint(chosenPos);
            cowGroupPositions.Add(chosenPos);
            usedPositions.Add(chosenPos);
            groupsSpawned++;

            validPositions.RemoveAll(pos => Vector3.Distance(pos, chosenPos) < minDistanceBetweenCowGroups);
        }
    }

    bool IsNearExistingCowGroup(Vector3 pos)
    {
        foreach (Vector3 existing in cowGroupPositions)
        {
            if (Vector3.Distance(existing, pos) < minDistanceBetweenCowGroups)
                return true;
        }

        return false;
    }

    void CreateCowSpawnPoint(Vector3 worldPos)
    {
        GameObject spawnObject = new GameObject("Cow Spawn Point");
        spawnObject.transform.SetParent(transform, true);
        spawnObject.transform.position = worldPos;

        CowSpawnPoint spawnPoint = spawnObject.AddComponent<CowSpawnPoint>();
        spawnPoint.cowPrefab = cowPrefab;
        spawnPoint.cowsPerGroup = 2;
        spawnPoint.spawnRadius = cowSpawnRadius;
        spawnPoint.respawnDelay = cowRespawnDelay;
        spawnPoint.cowWanderRadius = cowWanderRadius;
        spawnPoint.meatItemData = cowMeatItem;
        spawnPoint.meatDropPrefab = cowMeatDropPrefab;
        spawnPoint.bodyMaterial = cowBodyMaterial;
        spawnPoint.spotMaterial = cowSpotMaterial;
        spawnPoint.hoofMaterial = cowHoofMaterial;
    }

    GameObject GetRandomRock(ChunkRandom rng)
    {
        float r = rng.Value();

        if (r < 0.2f) return rockLargePrefab;
        if (r < 0.5f) return rockMediumPrefab;
        return rockSmallPrefab;
    }
}
