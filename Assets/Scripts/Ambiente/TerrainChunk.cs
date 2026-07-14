using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using TMPro;
using UnityEngine.UI;

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
    public GameObject gravetoPrefab;
    [Range(0f, 1f)] public float gravetoDensity = 0.004f;
    public int maxGravetosPerChunk = 4;
    public float minGravetoDistance = 5f;
    public float gravetoYOffset = 0.08f;
    public GameObject pedraPrefab;
    [Range(0f, 1f)] public float pedraDensity = 0.0025f;
    public int maxPedrasPerChunk = 3;
    public float minPedraDistance = 5f;
    public float pedraYOffset = 0.08f;
    [Range(0f, 1f)] public float wheatGroupChance = 0.004f;
    public int maxWheatGroupsPerChunk = 1;
    public int wheatPerGroup = 4;
    public float minWheatGroupDistance = 18f;
    public float wheatGroupRadius = 1.35f;
    public float wheatYOffset = 0.05f;

    public float treeDensity = 0.004f;
    public int maxTreesPerChunk = 1;
    public float forestTreeHeightMultiplier = 1.15f;
    public float forestTreeWidthMultiplier = 1.02f;
    public float treeExclusionPadding = 0.75f;
    public float mushroomDensity = 0.001f;
    public int rockClusterCount = 3;
    public int generationYieldInterval = 60;

    [Header("Grama Leve")]
    [Range(0f, 1f)] public float forestGrassDensity = 0.25f;
    public int maxForestGrassPerChunk = 420;
    public float forestGrassMinDistance = 0.35f;
    public int forestGrassSampleStep = 5;
    [Range(0f, 1f)] public float forestGrassExtraCoverage = 0.15f;
    public Vector2 forestGrassWidthRange = new Vector2(1.2f, 1.8f);
    public Vector2 forestGrassHeightRange = new Vector2(0.42f, 0.62f);
    public float forestGrassSpawnJitter = 0.42f;
    public float forestGrassYOffset = 0.02f;
    public float forestGrassRoadPadding = 1.6f;
    public float forestGrassRenderDistance = 30f;
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

    public float minDistanceBetweenObjects = 10f;
    public float minTreeDistance = 28f;

    [Header("Respawn de Recursos")]
    public Vector2 groundPickupRespawnDelayRange = new Vector2(120f, 240f);
    public Vector2 woodRespawnDelayRange = new Vector2(180f, 300f);
    public Vector2 stoneResourceRespawnDelayRange = new Vector2(180f, 300f);
    public Vector2 ironOreRespawnDelayRange = new Vector2(240f, 420f);

    [Header("Rochas")]
    public GameObject rockSmallPrefab;
    public GameObject rockMediumPrefab;
    public GameObject rockLargePrefab;
    [Range(0f, 1f)] public float ironOreChance = 0.55f;
    public int maxIronOrePerChunk = 1;
    public float minIronOreDistance = 20f;

    [Header("Animais")]
    public GameObject cowPrefab;
    public Item cowMeatItem;
    public Item cowLeatherItem;
    public GameObject cowMeatDropPrefab;
    public GameObject cowLeatherDropPrefab;
    public Material cowBodyMaterial;
    public Material cowSpotMaterial;
    public Material cowHoofMaterial;
    public float cowGroupChance = 0.35f;
    public int maxCowGroupsPerChunk = 1;
    public float minDistanceBetweenCowGroups = 18f;
    public float cowSpawnRadius = 5f;
    public float cowRespawnDelay = 25f;
    public float cowWanderRadius = 8f;
    public GameObject chickenPrefab;
    [Range(0f, 1f)] public float chickenGroupChance = 0.28f;
    public int maxChickenGroupsPerChunk = 1;
    public int chickensPerGroup = 3;
    public float minDistanceBetweenChickenGroups = 16f;
    public float chickenSpawnRadius = 4.5f;
    public float chickenRespawnDelay = 35f;
    public float chickenWanderRadius = 7f;
    public GameObject boarPrefab;
    [Range(0f, 1f)] public float forestBoarGroupChance = 0.42f;
    [Range(0f, 1f)] public float plainsBoarGroupChance = 0.24f;
    public int maxBoarGroupsPerChunk = 1;
    public int boarsPerGroup = 1;
    public float minDistanceBetweenBoarGroups = 22f;
    public float boarSpawnRadius = 4.5f;
    public float boarRespawnDelay = 55f;
    public float boarPatrolRadius = 10f;

    [Header("Golem de Terra")]
    public GameObject earthGolemPrefab;
    [Range(0f, 1f)] public float desertEarthGolemGroupChance = 0.1f;
    public int maxEarthGolemGroupsPerChunk = 1;
    public int earthGolemsPerGroup = 1;
    public float minDistanceBetweenEarthGolemGroups = 42f;
    public float earthGolemSpawnRadius = 3f;
    public float earthGolemRespawnDelay = 180f;
    public float earthGolemPatrolRadius = 8f;

    [Header("Inimigos da Floresta")]
    public GameObject forestMushroomMonsterPrefab;
    [Range(0f, 1f)] public float forestMushroomSpawnChance = 0.55f;
    public int maxForestMushroomGroupsPerChunk = 1;
    public int forestMushroomEnemiesPerGroup = 1;
    public float minDistanceBetweenForestMushroomGroups = 20f;
    public float forestMushroomSpawnRadius = 4.5f;
    public float forestMushroomRespawnDelay = 40f;

    [Header("Totens Ancestrais")]
    [Range(0f, 1f)] public float commonAncestralTotemChance = 0.18f;
    [Range(0f, 1f)] public float rareAncestralTotemChance = 0.07f;
    [Range(0f, 1f)] public float legendaryAncestralTotemChance = 0.025f;
    public int maxAncestralTotemsPerChunk = 1;
    public float minDistanceBetweenAncestralTotems = 44f;
    public float ancestralTotemMinPlayerDistance = 34f;


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

    public float rockDensity = 0.25f; // base

    Mesh mesh;
    Vector3[] vertices;
    int[] triangles;
    Color[] colors;
    Vector2[] uvs;

    List<Vector3> usedPositions = new List<Vector3>();
    List<Vector3> cowGroupPositions = new List<Vector3>();
    List<Vector3> chickenGroupPositions = new List<Vector3>();
    List<Vector3> boarGroupPositions = new List<Vector3>();
    List<Vector3> earthGolemGroupPositions = new List<Vector3>();
    List<Vector3> forestMushroomGroupPositions = new List<Vector3>();
    List<Vector3> ancestralTotemPositions = new List<Vector3>();
    List<Matrix4x4[]> forestGrassBatches = new List<Matrix4x4[]>();
    List<int> forestGrassBatchCounts = new List<int>();
    static Material riverMaterial;
    static Mesh forestGrassMesh;
    static GameObject cachedEnchantedForestSingleMushroom;
    static GameObject cachedEnchantedForestClusterMushroom;
    static Material wheatStemMaterial;
    static Material wheatHeadMaterial;
    static Material wheatBandMaterial;
    WorldHeightmapData sceneHeightmapData;
    RoadMaskData sceneRoadMaskData;
    TreeExclusionMaskData sceneTreeExclusionMaskData;
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
        sceneTreeExclusionMaskData = SceneWorldDataResolver.ResolveTreeExclusionMaskData(gameObject.scene);
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
        yield return SpawnIronOreNodesAsync(offset);
        yield return null;
        yield return SpawnCowGroupsAsync(offset);
        yield return null;
        yield return SpawnChickenGroupsAsync(offset);
        yield return null;
        yield return SpawnBoarGroupsAsync(offset);
        yield return null;
        yield return SpawnEarthGolemGroupsAsync(offset);
        yield return null;
        yield return SpawnForestMushroomGroupsAsync(offset);
        yield return null;
        yield return SpawnAncestralTotemsAsync(offset);
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

    bool IsTreeBlockedZone(Vector2 point, float padding)
    {
        if (sceneTreeExclusionMaskData == null)
            return false;

        if (!sceneTreeExclusionMaskData.ContainsWorldPoint(point))
            return false;

        if (sceneTreeExclusionMaskData.IsTreeBlocked(point))
            return true;

        if (padding <= 0.01f)
            return false;

        Vector2[] offsets =
        {
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
            if (sceneTreeExclusionMaskData.IsTreeBlocked(point + offsets[i]))
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
        int gravetoCount = 0;
        int pedraCount = 0;
        int wheatGroupCount = 0;
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

            if (IsTreeBlockedZone(point, treeExclusionPadding))
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
                density = Mathf.Clamp01(density * 0.35f);
                maxTreesForChunk = Mathf.Max(maxTreesPerChunk, 2);
                minTreeSpacing = Mathf.Max(minTreeDistance, 24f);
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

                    GameObject tree = MeshyOakTreeRuntimeFactory.Spawn(
                        groundPoint,
                        Quaternion.Euler(0f, rng.Range(0f, 360f), 0f),
                        transform,
                        selected.prefab
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

                    AlignObjectBaseToGround(
                        tree,
                        groundPoint,
                        selected.yOffset - MeshyOakTreeRuntimeFactory.GroundSinkDepth
                    );
                    EnsureTreeIsCollectable(tree);
                    ConfigureResourceRespawn(tree, woodRespawnDelayRange);

                    usedPositions.Add(groundPoint);
                    treeCount++;
                }
            }

            // 🍄
            // Gravetos
            if (gravetoPrefab != null &&
                gravetoCount < maxGravetosPerChunk &&
                biome == BiomeType.Forest &&
                rng.Value() < gravetoDensity)
            {
                Vector3 groundPoint = GetGroundPoint(worldPos);

                if (!IsTooClose(groundPoint, minGravetoDistance))
                {
                    GameObject graveto = Instantiate(
                        gravetoPrefab,
                        groundPoint + Vector3.up * gravetoYOffset,
                        Quaternion.Euler(0f, rng.Range(0f, 360f), 0f),
                        transform
                    );

                    float scale = rng.Range(0.85f, 1.15f);
                    graveto.transform.localScale *= scale;
                    ConfigurePickupRespawn(graveto);
                    usedPositions.Add(groundPoint);
                    gravetoCount++;
                }
            }

            if (wheatGroupCount < maxWheatGroupsPerChunk &&
                biome == BiomeType.Forest &&
                rng.Value() < wheatGroupChance)
            {
                Vector3 groundPoint = GetGroundPoint(worldPos);

                if (!IsTooClose(groundPoint, minWheatGroupDistance))
                {
                    SpawnWheatGroup(rng, groundPoint);
                    usedPositions.Add(groundPoint);
                    wheatGroupCount++;
                }
            }

            if (pedraPrefab != null &&
                pedraCount < maxPedrasPerChunk &&
                rng.Value() < pedraDensity)
            {
                Vector3 groundPoint = GetGroundPoint(worldPos);

                if (!IsTooClose(groundPoint, minPedraDistance))
                {
                    GameObject pedra = Instantiate(
                        pedraPrefab,
                        groundPoint + Vector3.up * pedraYOffset,
                        Quaternion.Euler(0f, rng.Range(0f, 360f), 0f),
                        transform
                    );

                    float scale = rng.Range(1.15f, 1.45f);
                    pedra.transform.localScale *= scale;
                    ConfigurePickupRespawn(pedra);
                    usedPositions.Add(groundPoint);
                    pedraCount++;
                }
            }

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
            maxGrassForChunk = Mathf.Max(maxGrassForChunk, 1100);
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
        int maxRocksPerChunk = 4;

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

                ScalePickaxeResourceRock(rock, rng);
                AlignObjectBaseToGround(rock, spawnPos);
                ConfigureResourceRespawn(rock, stoneResourceRespawnDelayRange);

                rockCount++;
            }

        }
    }

    IEnumerator SpawnIronOreNodesAsync(Vector2 offset)
    {
        if (maxIronOrePerChunk <= 0 || ironOreChance <= 0f)
            yield break;

        ChunkRandom rng = new ChunkRandom(BuildChunkSeed(offset, 808));
        if (rng.Value() > ironOreChance)
            yield break;

        int spawned = 0;
        int maxAttempts = Mathf.Max(8, maxIronOrePerChunk * 8);

        for (int attempt = 0; attempt < maxAttempts && spawned < maxIronOrePerChunk; attempt++)
        {
            if (attempt > 0 && attempt % 6 == 0)
                yield return null;

            float x = rng.Range(0f, size);
            float z = rng.Range(0f, size);
            int index = (int)z * (size + 1) + (int)x;

            if (index < 0 || index >= vertices.Length)
                continue;

            if (mesh.normals[index].y < 0.82f)
                continue;

            Vector3 spawnPos = GetGroundPoint(vertices[index] + transform.position);
            Vector2 point = new Vector2(spawnPos.x, spawnPos.z);

            if (IsRiverZone(point, 2f) || IsRoadZone(point))
                continue;

            if (player != null && Vector3.Distance(spawnPos, player.position) < 12f)
                continue;

            if (IsTooClose(spawnPos, minIronOreDistance))
                continue;

            GameObject ore = CreateIronOreNode(spawnPos, rng);
            usedPositions.Add(spawnPos);
            spawned++;
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

    public bool TryGetForestBossSpawnPoint(int seedSalt, float minPlayerDistance, out Vector3 spawnPoint, out float score)
    {
        spawnPoint = Vector3.zero;
        score = float.MaxValue;

        if (vertices == null || vertices.Length == 0 || mesh == null || mesh.normals == null || mesh.normals.Length == 0)
            return false;

        bool foundCandidate = false;
        int worldSeed = LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;

        for (int i = 0; i < vertices.Length; i += 14)
        {
            Vector3 localPos = vertices[i];
            Vector3 worldPos = localPos + transform.position;

            if (player != null && Vector3.Distance(worldPos, player.position) < Mathf.Max(safeRadius + 18f, minPlayerDistance))
                continue;

            if (mesh.normals[i].y < 0.8f)
                continue;

            Vector2 point = new Vector2(worldPos.x, worldPos.z);
            if (GetBiome(point) != BiomeType.Forest)
                continue;

            if (IsRiverZone(point, 2.6f))
                continue;

            if (IsNearRoadZone(point, 2.2f))
                continue;

            Vector3 groundedPoint = GetGroundPoint(worldPos);
            if (player != null && Vector3.Distance(groundedPoint, player.position) < minPlayerDistance)
                continue;

            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + worldSeed;
                hash = (hash * 31) + seedSalt;
                hash = (hash * 31) + Mathf.RoundToInt(point.x * 100f);
                hash = (hash * 31) + Mathf.RoundToInt(point.y * 100f);
                float candidateScore = (hash & 0x7fffffff) / (float)int.MaxValue;

                if (!foundCandidate || candidateScore < score)
                {
                    score = candidateScore;
                    spawnPoint = groundedPoint;
                    foundCandidate = true;
                }
            }
        }

        return foundCandidate;
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
        spawnPoint.meatItemData = cowMeatItem != null ? cowMeatItem : CowMeatItemRegistry.GetOrCreate();
        spawnPoint.leatherItemData = cowLeatherItem != null ? cowLeatherItem : CowLeatherItemRegistry.GetOrCreate();
        spawnPoint.meatDropPrefab = cowMeatDropPrefab;
        spawnPoint.leatherDropPrefab = cowLeatherDropPrefab;
        spawnPoint.bodyMaterial = cowBodyMaterial;
        spawnPoint.spotMaterial = cowSpotMaterial;
        spawnPoint.hoofMaterial = cowHoofMaterial;
    }

    IEnumerator SpawnChickenGroupsAsync(Vector2 offset)
    {
        ChunkRandom rng = new ChunkRandom(BuildChunkSeed(offset, 404));
        int desiredGroups = Mathf.Max(0, maxChickenGroupsPerChunk);

        if (desiredGroups == 0)
            yield break;

        chickenGroupPositions.Clear();
        List<Vector3> validPositions = new List<Vector3>();
        int iterationsSinceYield = 0;

        for (int i = 0; i < vertices.Length; i += 16)
        {
            iterationsSinceYield++;
            if (iterationsSinceYield >= Mathf.Max(20, generationYieldInterval))
            {
                iterationsSinceYield = 0;
                yield return null;
            }

            Vector3 localPos = vertices[i];
            Vector3 worldPos = localPos + transform.position;

            if (player != null && Vector3.Distance(worldPos, player.position) < safeRadius + 10f)
                continue;

            if (mesh.normals[i].y < 0.9f)
                continue;

            Vector2 point = new Vector2(localPos.x + offset.x, localPos.z + offset.y);
            BiomeType biome = GetBiome(point);
            if (!IsChickenBiome(biome))
                continue;

            if (IsRiverZone(point, 2.4f))
                continue;

            if (IsNearRoadZone(point, 1.6f))
                continue;

            Vector3 groundPoint = GetGroundPoint(worldPos);
            if (IsTooClose(groundPoint, minDistanceBetweenObjects * 0.75f))
                continue;

            validPositions.Add(groundPoint);
        }

        int groupsSpawned = 0;

        while (groupsSpawned < desiredGroups && validPositions.Count > 0)
        {
            yield return null;

            if (rng.Value() > chickenGroupChance)
                break;

            int index = rng.Range(0, validPositions.Count);
            Vector3 chosenPos = validPositions[index];

            CreateChickenSpawnPoint(chosenPos);
            chickenGroupPositions.Add(chosenPos);
            usedPositions.Add(chosenPos);
            groupsSpawned++;

            validPositions.RemoveAll(pos => Vector3.Distance(pos, chosenPos) < minDistanceBetweenChickenGroups);
        }
    }

    bool IsChickenBiome(BiomeType biome)
    {
        return biome == BiomeType.Forest || biome == BiomeType.Desert;
    }

    void CreateChickenSpawnPoint(Vector3 worldPos)
    {
        GameObject spawnObject = new GameObject("Wild Chicken Spawn Point");
        spawnObject.transform.SetParent(transform, true);
        spawnObject.transform.position = worldPos;

        WildChickenSpawnPoint spawnPoint = spawnObject.AddComponent<WildChickenSpawnPoint>();
        spawnPoint.chickenPrefab = chickenPrefab;
        spawnPoint.chickensPerGroup = Mathf.Max(1, chickensPerGroup);
        spawnPoint.spawnRadius = chickenSpawnRadius;
        spawnPoint.respawnDelay = chickenRespawnDelay;
        spawnPoint.chickenWanderRadius = chickenWanderRadius;
        spawnPoint.featherItemData = FeatherItemRegistry.GetOrCreate();
        spawnPoint.rawMeatItemData = RawChickenMeatItemRegistry.GetOrCreate();
    }

    IEnumerator SpawnBoarGroupsAsync(Vector2 offset)
    {
        int desiredGroups = Mathf.Max(0, maxBoarGroupsPerChunk);
        if (desiredGroups == 0)
            yield break;

        ChunkRandom rng = new ChunkRandom(BuildChunkSeed(offset, 515));
        boarGroupPositions.Clear();
        List<Vector3> validPositions = new List<Vector3>();
        List<float> positionChances = new List<float>();
        int iterationsSinceYield = 0;

        for (int i = 0; i < vertices.Length; i += 16)
        {
            iterationsSinceYield++;
            if (iterationsSinceYield >= Mathf.Max(20, generationYieldInterval))
            {
                iterationsSinceYield = 0;
                yield return null;
            }

            Vector3 localPos = vertices[i];
            Vector3 worldPos = localPos + transform.position;

            if (player != null && Vector3.Distance(worldPos, player.position) < safeRadius + 16f)
                continue;

            if (mesh.normals[i].y < 0.86f)
                continue;

            Vector2 point = new Vector2(localPos.x + offset.x, localPos.z + offset.y);
            BiomeType biome = GetBiome(point);
            if (!IsBoarBiome(biome))
                continue;

            if (IsRiverZone(point, 2.8f))
                continue;

            if (IsNearRoadZone(point, 1.8f))
                continue;

            Vector3 groundPoint = GetGroundPoint(worldPos);
            if (IsTooClose(groundPoint, minDistanceBetweenBoarGroups * 0.7f))
                continue;

            validPositions.Add(groundPoint);
            positionChances.Add(GetBoarSpawnChance(biome));
        }

        int groupsSpawned = 0;
        while (groupsSpawned < desiredGroups && validPositions.Count > 0)
        {
            yield return null;

            int index = rng.Range(0, validPositions.Count);
            Vector3 chosenPos = validPositions[index];
            float spawnChance = positionChances[index];

            if (rng.Value() <= spawnChance)
            {
                CreateBoarSpawnPoint(chosenPos);
                boarGroupPositions.Add(chosenPos);
                usedPositions.Add(chosenPos);
                groupsSpawned++;
            }

            for (int i = validPositions.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(validPositions[i], chosenPos) < minDistanceBetweenBoarGroups)
                {
                    validPositions.RemoveAt(i);
                    positionChances.RemoveAt(i);
                }
            }
        }
    }

    bool IsBoarBiome(BiomeType biome)
    {
        return biome == BiomeType.Forest || biome == BiomeType.Desert;
    }

    float GetBoarSpawnChance(BiomeType biome)
    {
        return biome == BiomeType.Forest
            ? Mathf.Clamp01(forestBoarGroupChance)
            : Mathf.Clamp01(plainsBoarGroupChance);
    }

    void CreateBoarSpawnPoint(Vector3 worldPos)
    {
        GameObject spawnObject = new GameObject("Wild Boar Spawn Point");
        spawnObject.transform.SetParent(transform, true);
        spawnObject.transform.position = worldPos;

        WildBoarSpawnPoint spawnPoint = spawnObject.AddComponent<WildBoarSpawnPoint>();
        spawnPoint.boarPrefab = boarPrefab;
        spawnPoint.boarsPerGroup = Mathf.Max(1, boarsPerGroup);
        spawnPoint.spawnRadius = boarSpawnRadius;
        spawnPoint.respawnDelay = boarRespawnDelay;
        spawnPoint.boarPatrolRadius = boarPatrolRadius;
        spawnPoint.thickLeatherItemData = ThickLeatherItemRegistry.GetOrCreate();
        spawnPoint.sharpTuskItemData = SharpTuskItemRegistry.GetOrCreate();
        spawnPoint.boarMeatItemData = BoarMeatItemRegistry.GetOrCreate();
        spawnPoint.trophyItemData = BoarTrophyItemRegistry.GetOrCreate();
    }

    IEnumerator SpawnEarthGolemGroupsAsync(Vector2 offset)
    {
        int desiredGroups = Mathf.Max(0, maxEarthGolemGroupsPerChunk);
        if (desiredGroups == 0)
            yield break;

        ChunkRandom rng = new ChunkRandom(BuildChunkSeed(offset, 616));
        earthGolemGroupPositions.Clear();
        List<Vector3> validPositions = new List<Vector3>();
        int iterationsSinceYield = 0;

        for (int i = 0; i < vertices.Length; i += 20)
        {
            iterationsSinceYield++;
            if (iterationsSinceYield >= Mathf.Max(20, generationYieldInterval))
            {
                iterationsSinceYield = 0;
                yield return null;
            }

            Vector3 localPos = vertices[i];
            Vector3 worldPos = localPos + transform.position;

            if (player != null && Vector3.Distance(worldPos, player.position) < safeRadius + 28f)
                continue;

            if (mesh.normals[i].y < 0.84f)
                continue;

            Vector2 point = new Vector2(localPos.x + offset.x, localPos.z + offset.y);
            if (GetBiome(point) != BiomeType.Desert)
                continue;

            if (IsRiverZone(point, 3.2f))
                continue;

            if (IsNearRoadZone(point, 2.4f))
                continue;

            Vector3 groundPoint = GetGroundPoint(worldPos);
            if (IsTooClose(groundPoint, minDistanceBetweenEarthGolemGroups * 0.7f))
                continue;

            validPositions.Add(groundPoint);
        }

        int groupsSpawned = 0;
        while (groupsSpawned < desiredGroups && validPositions.Count > 0)
        {
            yield return null;

            int index = rng.Range(0, validPositions.Count);
            Vector3 chosenPos = validPositions[index];

            if (rng.Value() <= desertEarthGolemGroupChance)
            {
                CreateEarthGolemSpawnPoint(chosenPos);
                earthGolemGroupPositions.Add(chosenPos);
                usedPositions.Add(chosenPos);
                groupsSpawned++;
            }

            validPositions.RemoveAll(pos => Vector3.Distance(pos, chosenPos) < minDistanceBetweenEarthGolemGroups);
        }
    }

    void CreateEarthGolemSpawnPoint(Vector3 worldPos)
    {
        GameObject spawnObject = new GameObject("Earth Golem Spawn Point");
        spawnObject.transform.SetParent(transform, true);
        spawnObject.transform.position = worldPos;

        EarthGolemSpawnPoint spawnPoint = spawnObject.AddComponent<EarthGolemSpawnPoint>();
        spawnPoint.golemPrefab = earthGolemPrefab;
        spawnPoint.golemsPerGroup = Mathf.Max(1, earthGolemsPerGroup);
        spawnPoint.spawnRadius = earthGolemSpawnRadius;
        spawnPoint.respawnDelay = earthGolemRespawnDelay;
        spawnPoint.golemPatrolRadius = earthGolemPatrolRadius;
        spawnPoint.stoneFragmentItemData = StoneFragmentItemRegistry.GetOrCreate();
        spawnPoint.resilientMossItemData = ResilientMossItemRegistry.GetOrCreate();
        spawnPoint.ironOreItemData = IronItemRegistry.GetOrCreate();
        spawnPoint.earthCoreItemData = EarthCoreItemRegistry.GetOrCreate();
    }

    IEnumerator SpawnForestMushroomGroupsAsync(Vector2 offset)
    {
        int desiredGroups = Mathf.Max(0, maxForestMushroomGroupsPerChunk);
        GameObject prefab = forestMushroomMonsterPrefab != null ? forestMushroomMonsterPrefab : ForestMushroomMonsterFactory.LoadPrefab();

        if (prefab == null || desiredGroups == 0)
            yield break;

        ChunkRandom rng = new ChunkRandom(BuildChunkSeed(offset, 171));
        forestMushroomGroupPositions.Clear();
        List<Vector3> validPositions = new List<Vector3>();
        int iterationsSinceYield = 0;

        for (int i = 0; i < vertices.Length; i += 16)
        {
            iterationsSinceYield++;
            if (iterationsSinceYield >= Mathf.Max(20, generationYieldInterval))
            {
                iterationsSinceYield = 0;
                yield return null;
            }

            Vector3 localPos = vertices[i];
            Vector3 worldPos = localPos + transform.position;

            if (player != null && Vector3.Distance(worldPos, player.position) < safeRadius + 12f)
                continue;

            if (mesh.normals[i].y < 0.84f)
                continue;

            Vector2 point = new Vector2(localPos.x + offset.x, localPos.z + offset.y);
            if (GetBiome(point) != BiomeType.Forest)
                continue;

            if (IsRiverZone(point, 2.4f))
                continue;

            if (IsNearRoadZone(point, 1.8f))
                continue;

            if (IsTooClose(worldPos, minDistanceBetweenForestMushroomGroups * 0.75f))
                continue;

            validPositions.Add(worldPos);
        }

        int groupsSpawned = 0;

        while (groupsSpawned < desiredGroups && validPositions.Count > 0)
        {
            yield return null;

            if (rng.Value() > forestMushroomSpawnChance)
                break;

            int index = rng.Range(0, validPositions.Count);
            Vector3 chosenPos = validPositions[index];

            CreateForestMushroomSpawnPoint(chosenPos, prefab);
            forestMushroomGroupPositions.Add(chosenPos);
            usedPositions.Add(chosenPos);
            groupsSpawned++;

            validPositions.RemoveAll(pos => Vector3.Distance(pos, chosenPos) < minDistanceBetweenForestMushroomGroups);
        }
    }

    void CreateForestMushroomSpawnPoint(Vector3 worldPos, GameObject prefab)
    {
        GameObject spawnObject = new GameObject("Forest Mushroom Spawn Point");
        spawnObject.transform.SetParent(transform, true);
        spawnObject.transform.position = worldPos;

        ForestMushroomMonsterSpawnPoint spawnPoint = spawnObject.AddComponent<ForestMushroomMonsterSpawnPoint>();
        spawnPoint.mushroomMonsterPrefab = prefab;
        spawnPoint.enemiesPerGroup = Mathf.Max(1, forestMushroomEnemiesPerGroup);
        spawnPoint.spawnRadius = forestMushroomSpawnRadius;
        spawnPoint.respawnDelay = forestMushroomRespawnDelay;
    }

    IEnumerator SpawnAncestralTotemsAsync(Vector2 offset)
    {
        int desiredTotems = Mathf.Max(0, maxAncestralTotemsPerChunk);
        if (desiredTotems == 0)
            yield break;

        ChunkRandom rng = new ChunkRandom(BuildChunkSeed(offset, 727));
        ancestralTotemPositions.Clear();
        List<Vector3> validPositions = new List<Vector3>();
        List<AncestralTotemTier> validTiers = new List<AncestralTotemTier>();
        int iterationsSinceYield = 0;

        for (int i = 0; i < vertices.Length; i += 22)
        {
            iterationsSinceYield++;
            if (iterationsSinceYield >= Mathf.Max(20, generationYieldInterval))
            {
                iterationsSinceYield = 0;
                yield return null;
            }

            Vector3 localPos = vertices[i];
            Vector3 worldPos = localPos + transform.position;

            if (player != null && Vector3.Distance(worldPos, player.position) < safeRadius + ancestralTotemMinPlayerDistance)
                continue;

            if (mesh.normals[i].y < 0.84f)
                continue;

            Vector2 point = new Vector2(localPos.x + offset.x, localPos.z + offset.y);
            if (IsRiverZone(point, 3.2f))
                continue;

            if (IsNearRoadZone(point, 2.4f))
                continue;

            Vector3 groundPoint = GetGroundPoint(worldPos);
            if (IsTooClose(groundPoint, minDistanceBetweenAncestralTotems * 0.7f))
                continue;

            BiomeType biome = GetBiome(point);
            if (!TryRollAncestralTotemTier(biome, rng, out AncestralTotemTier tier))
                continue;

            validPositions.Add(groundPoint);
            validTiers.Add(tier);
        }

        int spawned = 0;
        while (spawned < desiredTotems && validPositions.Count > 0)
        {
            yield return null;

            int index = rng.Range(0, validPositions.Count);
            Vector3 chosenPos = validPositions[index];
            AncestralTotemTier tier = validTiers[index];

            CreateAncestralTotem(chosenPos, tier);
            ancestralTotemPositions.Add(chosenPos);
            usedPositions.Add(chosenPos);
            spawned++;

            for (int i = validPositions.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(validPositions[i], chosenPos) < minDistanceBetweenAncestralTotems)
                {
                    validPositions.RemoveAt(i);
                    validTiers.RemoveAt(i);
                }
            }
        }
    }

    bool TryRollAncestralTotemTier(BiomeType biome, ChunkRandom rng, out AncestralTotemTier tier)
    {
        float commonChance = Mathf.Clamp01(commonAncestralTotemChance);
        float rareChance = Mathf.Clamp01(rareAncestralTotemChance);
        float legendaryChance = Mathf.Clamp01(legendaryAncestralTotemChance);

        if (biome == BiomeType.Forest)
        {
            rareChance *= 0.65f;
            legendaryChance *= 0.25f;
        }
        else if (biome == BiomeType.Desert)
        {
            commonChance *= 0.65f;
            rareChance *= 1.1f;
            legendaryChance *= 1.35f;
        }
        else
        {
            commonChance *= 0.45f;
            rareChance *= 0.9f;
            legendaryChance *= 1.15f;
        }

        float roll = rng.Value();
        if (roll <= legendaryChance)
        {
            tier = AncestralTotemTier.Legendary;
            return true;
        }

        roll = rng.Value();
        if (roll <= rareChance)
        {
            tier = AncestralTotemTier.Rare;
            return true;
        }

        roll = rng.Value();
        if (roll <= commonChance)
        {
            tier = AncestralTotemTier.Common;
            return true;
        }

        tier = AncestralTotemTier.Common;
        return false;
    }

    void CreateAncestralTotem(Vector3 worldPos, AncestralTotemTier tier)
    {
        GameObject totemObject = new GameObject($"Ancestral Totem {tier}");
        totemObject.transform.SetParent(transform, true);
        totemObject.transform.position = worldPos;
        totemObject.transform.rotation = Quaternion.Euler(0f, Mathf.Abs(worldPos.x + worldPos.z) % 360f, 0f);

        AncestralTotem totem = totemObject.AddComponent<AncestralTotem>();
        totem.Configure(tier);
    }

    GameObject GetRandomRock(ChunkRandom rng)
    {
        float r = rng.Value();

        if (r < 0.2f) return rockLargePrefab;
        if (r < 0.5f) return rockMediumPrefab;
        return rockSmallPrefab;
    }

    GameObject CreateIronOreNode(Vector3 worldPos, ChunkRandom rng)
    {
        GameObject oreRoot = new GameObject("IronOreNode");
        oreRoot.transform.SetParent(transform, true);
        oreRoot.transform.position = worldPos;
        oreRoot.transform.rotation = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
        oreRoot.transform.localScale = Vector3.one;

        float oreDiameter = rng.Range(2.55f, 3.25f);
        bool usingMeshyIron = MeshyIronOreRuntimeVisual.TryAttachTo(oreRoot, oreDiameter);
        if (!usingMeshyIron)
        {
            oreRoot.transform.localScale = new Vector3(1.35f, 1.25f, 1.35f);

            CreateIronOrePiece(oreRoot.transform, new Vector3(-0.28f, 0.13f, 0.05f), new Vector3(0.85f, 0.34f, 0.72f), new Color(0.24f, 0.24f, 0.22f, 1f));
            CreateIronOrePiece(oreRoot.transform, new Vector3(0.22f, 0.1f, -0.18f), new Vector3(0.62f, 0.3f, 0.54f), new Color(0.18f, 0.18f, 0.17f, 1f));
            CreateIronOrePiece(oreRoot.transform, new Vector3(0.08f, 0.25f, 0.18f), new Vector3(0.34f, 0.12f, 0.42f), new Color(0.62f, 0.38f, 0.16f, 1f));
            CreateIronOrePiece(oreRoot.transform, new Vector3(-0.18f, 0.23f, -0.14f), new Vector3(0.28f, 0.1f, 0.38f), new Color(0.76f, 0.52f, 0.24f, 1f));
        }

        SphereCollider collider = oreRoot.AddComponent<SphereCollider>();
        collider.radius = usingMeshyIron ? Mathf.Max(0.85f, oreDiameter * 0.56f) : 1.05f;
        collider.center = usingMeshyIron ? new Vector3(0f, Mathf.Max(0.3f, oreDiameter * 0.28f), 0f) : new Vector3(0f, 0.34f, 0f);

        ResourceNode resource = oreRoot.AddComponent<ResourceNode>();
        resource.itemName = IronItemRegistry.ItemName;
        resource.icon = IronItemRegistry.GetSprite();
        resource.itemData = IronItemRegistry.GetOrCreate();
        resource.requiredTool = ToolType.Pickaxe;
        resource.allowEmptyHand = false;
        resource.maxHealth = 6;
        resource.minDrop = 2;
        resource.maxDrop = 5;
        resource.emptyHandDamage = 0;
        resource.emptyHandMinDrop = 0;
        resource.emptyHandMaxDrop = 0;
        resource.respawnDelayRange = ironOreRespawnDelayRange;

        return oreRoot;
    }

    void SpawnWheatGroup(ChunkRandom rng, Vector3 centerGroundPoint)
    {
        int count = Mathf.Max(1, wheatPerGroup);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + rng.Range(-22f, 22f);
            float distance = rng.Range(0.25f, wheatGroupRadius);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
            Vector3 groundPoint = GetGroundPoint(centerGroundPoint + offset);
            GameObject wheat = CreateWheatPickup(groundPoint + Vector3.up * wheatYOffset, rng, i);
            ConfigurePickupRespawn(wheat);
        }
    }

    GameObject CreateWheatPickup(Vector3 worldPosition, ChunkRandom rng, int index)
    {
        GameObject wheat = new GameObject($"Trigo_{index + 1}");
        wheat.transform.SetParent(transform, false);
        wheat.transform.position = worldPosition;
        wheat.transform.rotation = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);

        Item item = wheat.AddComponent<Item>();
        item.itemName = WheatItemRegistry.ItemName;
        item.itemType = ItemType.Resource;
        item.toolType = ToolType.None;
        item.toolDamage = 0;
        item.icon = WheatItemRegistry.GetSprite();

        CapsuleCollider collider = wheat.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.72f, 0f);
        collider.radius = 0.28f;
        collider.height = 1.45f;

        int stemCount = 5;
        for (int i = 0; i < stemCount; i++)
        {
            float spread = Mathf.Lerp(-0.34f, 0.34f, stemCount <= 1 ? 0f : i / (float)(stemCount - 1));
            float lean = spread * 22f + rng.Range(-5f, 5f);
            Transform stem = CreateWheatPart(
                "Stem",
                PrimitiveType.Cylinder,
                wheat.transform,
                new Vector3(spread * 0.12f, 0.43f, 0f),
                Quaternion.Euler(0f, 0f, -lean),
                new Vector3(0.025f, 0.43f, 0.025f),
                GetWheatStemMaterial()
            );

            Transform head = CreateWheatPart(
                "Head",
                PrimitiveType.Cylinder,
                wheat.transform,
                new Vector3(spread * 0.24f, 0.93f, 0f),
                Quaternion.Euler(78f, 0f, -lean),
                new Vector3(0.075f, 0.22f, 0.075f),
                GetWheatHeadMaterial()
            );

            CreateWheatKernels(head, rng);

            CreateWheatPart(
                "Awn",
                PrimitiveType.Cube,
                wheat.transform,
                new Vector3(spread * 0.33f, 1.18f, 0f),
                Quaternion.Euler(0f, 0f, -lean),
                new Vector3(0.012f, 0.36f, 0.012f),
                GetWheatHeadMaterial()
            );

            _ = stem;
        }

        Transform band = CreateWheatPart(
            "Band",
            PrimitiveType.Cylinder,
            wheat.transform,
            new Vector3(0f, 0.24f, 0f),
            Quaternion.Euler(88f, 0f, 0f),
            new Vector3(0.16f, 0.055f, 0.16f),
            GetWheatBandMaterial()
        );
        _ = band;

        float scale = rng.Range(0.9f, 1.12f);
        wheat.transform.localScale = Vector3.one * scale;
        return wheat;
    }

    void CreateWheatKernels(Transform head, ChunkRandom rng)
    {
        if (head == null)
            return;

        for (int i = 0; i < 5; i++)
        {
            float y = Mathf.Lerp(-0.16f, 0.16f, i / 4f);
            float side = i % 2 == 0 ? -1f : 1f;
            CreateWheatPart(
                "Kernel",
                PrimitiveType.Sphere,
                head,
                new Vector3(side * 0.055f, y, 0f),
                Quaternion.Euler(0f, 0f, side * 18f + rng.Range(-5f, 5f)),
                new Vector3(0.09f, 0.055f, 0.055f),
                GetWheatHeadMaterial()
            );
        }
    }

    Transform CreateWheatPart(string partName, PrimitiveType primitive, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            DestroyImmediate(collider);

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        return part.transform;
    }

    Material GetWheatStemMaterial()
    {
        if (wheatStemMaterial == null)
        {
            wheatStemMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            wheatStemMaterial.name = "WheatStemRuntime";
            wheatStemMaterial.color = new Color(0.78f, 0.48f, 0.12f, 1f);
        }

        return wheatStemMaterial;
    }

    Material GetWheatHeadMaterial()
    {
        if (wheatHeadMaterial == null)
        {
            wheatHeadMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            wheatHeadMaterial.name = "WheatHeadRuntime";
            wheatHeadMaterial.color = new Color(0.98f, 0.68f, 0.16f, 1f);
        }

        return wheatHeadMaterial;
    }

    Material GetWheatBandMaterial()
    {
        if (wheatBandMaterial == null)
        {
            wheatBandMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            wheatBandMaterial.name = "WheatBandRuntime";
            wheatBandMaterial.color = new Color(0.55f, 0.29f, 0.08f, 1f);
        }

        return wheatBandMaterial;
    }

    void ConfigureResourceRespawn(GameObject resourceObject, Vector2 delayRange)
    {
        if (resourceObject == null)
            return;

        ResourceNode resource = resourceObject.GetComponent<ResourceNode>() ??
                                resourceObject.GetComponentInChildren<ResourceNode>();

        if (resource == null)
            return;

        resource.respawnAfterDepleted = true;
        resource.respawnDelayRange = delayRange;
    }

    void ScalePickaxeResourceRock(GameObject rock, ChunkRandom rng)
    {
        if (rock == null)
            return;

        ResourceNode resource = rock.GetComponent<ResourceNode>() ??
                                rock.GetComponentInChildren<ResourceNode>();

        if (resource == null || resource.requiredTool != ToolType.Pickaxe)
            return;

        float scale = rng.Range(1.45f, 1.9f);
        rock.transform.localScale = Vector3.Scale(
            rock.transform.localScale,
            new Vector3(scale, scale * 1.08f, scale));

        resource.allowEmptyHand = false;
        resource.maxHealth = Mathf.Max(resource.maxHealth, 5);
        resource.minDrop = Mathf.Max(resource.minDrop, 2);
        resource.maxDrop = Mathf.Max(resource.maxDrop, 4);
    }

    void EnsureTreeIsCollectable(GameObject tree)
    {
        if (tree == null)
            return;

        ResourceNode resource = tree.GetComponent<ResourceNode>() ??
                                tree.GetComponentInChildren<ResourceNode>();

        if (resource == null)
        {
            resource = tree.AddComponent<ResourceNode>();
        }

        resource.itemName = MeshyOakTreeRuntimeFactory.OakWoodItemName;
        resource.itemData = OakWoodItemRegistry.GetOrCreate();
        resource.maxHealth = Mathf.Max(resource.maxHealth, 4);
        resource.minDrop = Mathf.Max(resource.minDrop, 2);
        resource.maxDrop = 4;
        resource.requiredTool = ToolType.Axe;
        resource.allowEmptyHand = false;
        resource.emptyHandDamage = 0;
        resource.emptyHandMinDrop = 0;
        resource.emptyHandMaxDrop = 0;

        if (tree.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider collider = tree.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.7f, 0f);
            collider.size = new Vector3(1.8f, 3.4f, 1.8f);
        }
    }

    void ConfigurePickupRespawn(GameObject pickup)
    {
        if (pickup == null)
            return;

        PickupRespawner respawner = pickup.GetComponent<PickupRespawner>();
        if (respawner == null)
            respawner = pickup.AddComponent<PickupRespawner>();

        respawner.respawnDelayRange = groundPickupRespawnDelayRange;
    }

    void CreateIronOrePiece(Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = "OrePiece";
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = Quaternion.Euler(12f, 28f, -9f);
        piece.transform.localScale = localScale;

        Collider collider = piece.GetComponent<Collider>();
        if (collider != null)
            DestroyImmediate(collider);

        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }
}

public static class MeshyIronOreRuntimeVisual
{
    const string ResourceRoot = "World/IronOre/MeshyIronOre/Meshy_AI_Stylized_fantasy_mini_0630231038_texture_fbx/";
    const string ModelResourcePath = ResourceRoot + "Meshy_AI_Stylized_fantasy_mini_0630231038_texture";
    const string AlbedoResourcePath = ResourceRoot + "Meshy_AI_Stylized_fantasy_mini_0630231038_texture";
    const string EmissionResourcePath = ResourceRoot + "Meshy_AI_Stylized_fantasy_mini_0630231038_texture_emission";
    const string MetallicResourcePath = ResourceRoot + "Meshy_AI_Stylized_fantasy_mini_0630231038_texture_metallic";
    const string NormalResourcePath = ResourceRoot + "Meshy_AI_Stylized_fantasy_mini_0630231038_texture_normal";
    const string MaterialResourcePath = "World/IronOre/MeshyIronOre/MeshyIronOre_Material";
    const string VisualName = "MeshyIronOreVisual";

    static GameObject cachedModel;
    static Material cachedMaterial;

    public static bool TryAttachTo(GameObject root, float targetDiameter)
    {
        if (root == null)
            return false;

        GameObject model = LoadModel();
        if (model == null)
            return false;

        GameObject visual = Object.Instantiate(model, root.transform);
        visual.name = VisualName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        visual.transform.localScale = Vector3.one;

        RemoveRuntimeOnlyComponents(visual);
        ApplyMaterial(visual);
        NormalizeVisual(root.transform, visual, targetDiameter);
        return true;
    }

    static GameObject LoadModel()
    {
        if (cachedModel == null)
            cachedModel = Resources.Load<GameObject>(ModelResourcePath);

        return cachedModel;
    }

    static Material LoadMaterial()
    {
        if (cachedMaterial != null)
            return cachedMaterial;

        cachedMaterial = Resources.Load<Material>(MaterialResourcePath);
        if (cachedMaterial != null)
            return cachedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return null;

        cachedMaterial = new Material(shader)
        {
            name = "MeshyIronOreRuntime"
        };

        Texture2D albedo = Resources.Load<Texture2D>(AlbedoResourcePath);
        Texture2D normal = Resources.Load<Texture2D>(NormalResourcePath);
        Texture2D metallic = Resources.Load<Texture2D>(MetallicResourcePath);
        Texture2D emission = Resources.Load<Texture2D>(EmissionResourcePath);

        SetTexture(cachedMaterial, "_BaseMap", albedo);
        SetTexture(cachedMaterial, "_MainTex", albedo);
        SetColor(cachedMaterial, "_BaseColor", Color.white);
        SetColor(cachedMaterial, "_Color", Color.white);

        SetTexture(cachedMaterial, "_BumpMap", normal);
        if (normal != null)
            cachedMaterial.EnableKeyword("_NORMALMAP");

        SetTexture(cachedMaterial, "_MetallicGlossMap", metallic);
        if (metallic != null)
            cachedMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");

        SetFloat(cachedMaterial, "_Metallic", 0.45f);
        SetFloat(cachedMaterial, "_Smoothness", 0.38f);
        SetFloat(cachedMaterial, "_Glossiness", 0.38f);

        SetTexture(cachedMaterial, "_EmissionMap", emission);
        if (emission != null)
        {
            cachedMaterial.EnableKeyword("_EMISSION");
            SetColor(cachedMaterial, "_EmissionColor", new Color(0.18f, 0.14f, 0.1f));
        }

        return cachedMaterial;
    }

    static void RemoveRuntimeOnlyComponents(GameObject visual)
    {
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = colliders.Length - 1; i >= 0; i--)
            Object.Destroy(colliders[i]);

        Animator[] animators = visual.GetComponentsInChildren<Animator>(true);
        for (int i = animators.Length - 1; i >= 0; i--)
            Object.Destroy(animators[i]);

        Animation[] animations = visual.GetComponentsInChildren<Animation>(true);
        for (int i = animations.Length - 1; i >= 0; i--)
            Object.Destroy(animations[i]);
    }

    static void ApplyMaterial(GameObject visual)
    {
        Material material = LoadMaterial();
        if (material == null)
            return;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                materials = new[] { material };
            }
            else
            {
                for (int j = 0; j < materials.Length; j++)
                    materials[j] = material;
            }

            renderer.sharedMaterials = materials;
            renderer.receiveShadows = true;
        }
    }

    static void NormalizeVisual(Transform root, GameObject visual, float targetDiameter)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (!TryCalculateLocalBounds(root, renderers, out Bounds bounds))
            return;

        float currentDiameter = Mathf.Max(bounds.size.x, bounds.size.z);
        if (currentDiameter > 0.001f)
            visual.transform.localScale = Vector3.one * (targetDiameter / currentDiameter);

        if (!TryCalculateLocalBounds(root, renderers, out bounds))
            return;

        visual.transform.localPosition += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
    }

    static bool TryCalculateLocalBounds(Transform root, Renderer[] renderers, out Bounds localBounds)
    {
        localBounds = default;
        bool initialized = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            Encapsulate(root.InverseTransformPoint(center + new Vector3(-extents.x, -extents.y, -extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(-extents.x, -extents.y, extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(-extents.x, extents.y, -extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(-extents.x, extents.y, extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(extents.x, -extents.y, -extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(extents.x, -extents.y, extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(extents.x, extents.y, -extents.z)), ref localBounds, ref initialized);
            Encapsulate(root.InverseTransformPoint(center + new Vector3(extents.x, extents.y, extents.z)), ref localBounds, ref initialized);
        }

        return initialized;
    }

    static void Encapsulate(Vector3 point, ref Bounds bounds, ref bool initialized)
    {
        if (!initialized)
        {
            bounds = new Bounds(point, Vector3.zero);
            initialized = true;
            return;
        }

        bounds.Encapsulate(point);
    }

    static void SetTexture(Material material, string propertyName, Texture texture)
    {
        if (texture != null && material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
    }

    static void SetColor(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, color);
    }

    static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }
}

public enum AncestralTotemTier
{
    Common = 1,
    Rare = 2,
    Legendary = 3
}

public class AncestralPowerDefinition
{
    const string IconRootPath = "Icons/AncestralPowers/";

    public string id;
    public string displayName;
    public string description;
    public AncestralTotemTier tier;
    public float maxHealthBonus;
    public float maxStaminaBonus;
    public float damageBonus;
    public float animalDamageBonus;
    public float resourceBonusChance;
    public float bonusDefense;
    public float healthRegenBonus;
    public float speedBonus;
    public float attackSpeedBonus;
    public float ignoreDamageChance;

    Sprite icon;

    public AncestralPowerDefinition(string id, string displayName, string description, AncestralTotemTier tier)
    {
        this.id = id;
        this.displayName = displayName;
        this.description = description;
        this.tier = tier;
    }

    public Sprite GetIcon()
    {
        if (icon != null)
            return icon;

        icon = Resources.Load<Sprite>(IconRootPath + id);
        return icon;
    }
}

public static class AncestralPowerCatalog
{
    static readonly List<AncestralPowerDefinition> definitions = new List<AncestralPowerDefinition>
    {
        new AncestralPowerDefinition("explorer_vigor", "Vigor do Explorador", "+10 Vida maxima", AncestralTotemTier.Common)
        {
            maxHealthBonus = 10f
        },
        new AncestralPowerDefinition("strong_lungs", "Pulmoes Fortes", "+10 Stamina maxima", AncestralTotemTier.Common)
        {
            maxStaminaBonus = 10f
        },
        new AncestralPowerDefinition("efficient_harvest", "Colheita Eficiente", "+5% chance de ganhar recurso extra", AncestralTotemTier.Common)
        {
            resourceBonusChance = 0.05f
        },
        new AncestralPowerDefinition("novice_hunter", "Cacador Iniciante", "+5% dano contra animais", AncestralTotemTier.Common)
        {
            animalDamageBonus = 0.05f
        },
        new AncestralPowerDefinition("warrior_spirit", "Espirito Guerreiro", "+15% dano", AncestralTotemTier.Rare)
        {
            damageBonus = 0.15f
        },
        new AncestralPowerDefinition("resistant_skin", "Pele Resistente", "+20 defesa", AncestralTotemTier.Rare)
        {
            bonusDefense = 20f
        },
        new AncestralPowerDefinition("natural_recovery", "Recuperacao Natural", "+1 HP por segundo", AncestralTotemTier.Rare)
        {
            healthRegenBonus = 1f
        },
        new AncestralPowerDefinition("improved_agility", "Agilidade Aprimorada", "+10% velocidade", AncestralTotemTier.Rare)
        {
            speedBonus = 0.1f
        },
        new AncestralPowerDefinition("ancestral_blood", "Sangue Ancestral", "+25% dano total", AncestralTotemTier.Legendary)
        {
            damageBonus = 0.25f
        },
        new AncestralPowerDefinition("earth_titan", "Tita da Terra", "+50 Vida maxima", AncestralTotemTier.Legendary)
        {
            maxHealthBonus = 50f
        },
        new AncestralPowerDefinition("storm_spirit", "Espirito da Tempestade", "+15% velocidade de ataque", AncestralTotemTier.Legendary)
        {
            attackSpeedBonus = 0.15f
        },
        new AncestralPowerDefinition("immortal_guardian", "Guardiao Imortal", "20% chance de ignorar dano recebido", AncestralTotemTier.Legendary)
        {
            ignoreDamageChance = 0.2f
        }
    };

    public static AncestralPowerDefinition Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i].id == id)
                return definitions[i];
        }

        return null;
    }

    public static List<AncestralPowerDefinition> BuildRewardPool(AncestralTotemTier tier, List<string> ownedIds)
    {
        List<AncestralPowerDefinition> pool = new List<AncestralPowerDefinition>();
        AddDefinitions(pool, tier, ownedIds);

        if (pool.Count == 0)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (!ownedIds.Contains(definitions[i].id))
                    pool.Add(definitions[i]);
            }
        }

        return pool;
    }

    static void AddDefinitions(List<AncestralPowerDefinition> pool, AncestralTotemTier tier, List<string> ownedIds)
    {
        for (int i = 0; i < definitions.Count; i++)
        {
            AncestralPowerDefinition definition = definitions[i];
            if (definition.tier == tier && !ownedIds.Contains(definition.id))
                pool.Add(definition);
        }
    }
}

public class AncestralPowerService : MonoBehaviour
{
    public int maxActivePowers = 10;

    readonly List<string> activePowerIds = new List<string>();
    PlayerMovement playerMovement;

    float appliedMaxHealthBonus;
    float appliedMaxStaminaBonus;
    float appliedHealthRegenBonus;
    float appliedSpeedMultiplier = 1f;

    public float DamageBonus { get; private set; }
    public float AnimalDamageBonus { get; private set; }
    public float ResourceBonusChance { get; private set; }
    public float BonusDefense { get; private set; }
    public float IgnoreDamageChance { get; private set; }
    public float AttackCooldownMultiplier { get; private set; } = 1f;
    public int ActivePowerCount => activePowerIds.Count;
    public int MaxActivePowers => maxActivePowers;

    public static AncestralPowerService GetOrCreate(PlayerMovement movement)
    {
        if (movement == null)
            return null;

        AncestralPowerService service = movement.GetComponent<AncestralPowerService>();
        if (service == null)
            service = movement.gameObject.AddComponent<AncestralPowerService>();

        service.ResolveReferences();
        return service;
    }

    void Awake()
    {
        ResolveReferences();
        NotifyHudChanged();
    }

    void ResolveReferences()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
    }

    public List<string> CapturePowerIds()
    {
        return new List<string>(activePowerIds);
    }

    public List<AncestralPowerDefinition> GetActivePowerDefinitions()
    {
        List<AncestralPowerDefinition> activeDefinitions = new List<AncestralPowerDefinition>();

        for (int i = 0; i < activePowerIds.Count; i++)
        {
            AncestralPowerDefinition definition = AncestralPowerCatalog.Find(activePowerIds[i]);
            if (definition != null)
                activeDefinitions.Add(definition);
        }

        return activeDefinitions;
    }

    public void LoadPowers(List<string> savedPowerIds)
    {
        ClearPowers(false);

        if (savedPowerIds == null)
            return;

        for (int i = 0; i < savedPowerIds.Count; i++)
        {
            string id = savedPowerIds[i];
            if (activePowerIds.Count >= maxActivePowers)
                break;

            if (AncestralPowerCatalog.Find(id) == null || activePowerIds.Contains(id))
                continue;

            activePowerIds.Add(id);
        }

        RecalculatePassiveEffects(false);
        NotifyHudChanged();
    }

    public void ClearPowers(bool showMessage)
    {
        RemoveAppliedStats();
        activePowerIds.Clear();
        ResetRuntimeBonuses();

        if (showMessage)
            MessageSystem.Instance?.ShowMessage("Poderes ancestrais perdidos.");

        NotifyHudChanged();
    }

    public bool GrantRandomPower(AncestralTotemTier tier)
    {
        if (activePowerIds.Count >= maxActivePowers)
        {
            MessageSystem.Instance?.ShowMessage("Limite de poderes ancestrais atingido.");
            return false;
        }

        List<AncestralPowerDefinition> pool = AncestralPowerCatalog.BuildRewardPool(tier, activePowerIds);
        if (pool.Count == 0)
        {
            MessageSystem.Instance?.ShowMessage("Nenhum poder ancestral novo disponivel.");
            return false;
        }

        AncestralPowerDefinition reward = pool[Random.Range(0, pool.Count)];
        activePowerIds.Add(reward.id);
        RecalculatePassiveEffects(true);

        string rewardMessage = $"{reward.displayName}: {reward.description}";
        MessageSystem.Instance?.ShowMessage(rewardMessage);
        DebugCommandChat.AddSystemMessage($"Poder ancestral coletado: {rewardMessage}");
        NotifyHudChanged();
        return true;
    }

    public float GetOutgoingDamageMultiplier(bool animalTarget)
    {
        return Mathf.Max(0.1f, 1f + DamageBonus + (animalTarget ? AnimalDamageBonus : 0f));
    }

    public bool TryIgnoreIncomingDamage()
    {
        return IgnoreDamageChance > 0f && Random.value < IgnoreDamageChance;
    }

    public bool TryRollBonusResource()
    {
        return ResourceBonusChance > 0f && Random.value < ResourceBonusChance;
    }

    void RecalculatePassiveEffects(bool increaseCurrentResources)
    {
        RemoveAppliedStats();
        ResetRuntimeBonuses();
        ResolveReferences();

        float maxHealthBonus = 0f;
        float maxStaminaBonus = 0f;
        float healthRegenBonus = 0f;
        float speedBonus = 0f;
        float attackSpeedBonus = 0f;

        for (int i = 0; i < activePowerIds.Count; i++)
        {
            AncestralPowerDefinition definition = AncestralPowerCatalog.Find(activePowerIds[i]);
            if (definition == null)
                continue;

            maxHealthBonus += definition.maxHealthBonus;
            maxStaminaBonus += definition.maxStaminaBonus;
            healthRegenBonus += definition.healthRegenBonus;
            speedBonus += definition.speedBonus;
            attackSpeedBonus += definition.attackSpeedBonus;
            DamageBonus += definition.damageBonus;
            AnimalDamageBonus += definition.animalDamageBonus;
            ResourceBonusChance += definition.resourceBonusChance;
            BonusDefense += definition.bonusDefense;
            IgnoreDamageChance += definition.ignoreDamageChance;
        }

        AttackCooldownMultiplier = Mathf.Max(0.35f, 1f - attackSpeedBonus);

        if (playerMovement == null)
            return;

        if (maxHealthBonus > 0f)
        {
            playerMovement.maxHealth += maxHealthBonus;
            if (increaseCurrentResources)
                playerMovement.currentHealth = Mathf.Min(playerMovement.maxHealth, playerMovement.currentHealth + maxHealthBonus);
            else
                playerMovement.currentHealth = Mathf.Clamp(playerMovement.currentHealth, 0f, playerMovement.maxHealth);

            appliedMaxHealthBonus = maxHealthBonus;
        }

        if (maxStaminaBonus > 0f)
        {
            playerMovement.maxStamina += maxStaminaBonus;
            if (increaseCurrentResources)
                playerMovement.currentStamina = Mathf.Min(playerMovement.maxStamina, playerMovement.currentStamina + maxStaminaBonus);
            else
                playerMovement.currentStamina = Mathf.Clamp(playerMovement.currentStamina, 0f, playerMovement.maxStamina);

            appliedMaxStaminaBonus = maxStaminaBonus;
        }

        if (healthRegenBonus > 0f)
        {
            playerMovement.healthRegenPerSecond += healthRegenBonus;
            appliedHealthRegenBonus = healthRegenBonus;
        }

        if (speedBonus > 0f)
        {
            appliedSpeedMultiplier = 1f + speedBonus;
            playerMovement.walkSpeed *= appliedSpeedMultiplier;
            playerMovement.runSpeed *= appliedSpeedMultiplier;
        }
    }

    void RemoveAppliedStats()
    {
        ResolveReferences();

        if (playerMovement == null)
            return;

        if (appliedSpeedMultiplier > 0f && !Mathf.Approximately(appliedSpeedMultiplier, 1f))
        {
            playerMovement.walkSpeed /= appliedSpeedMultiplier;
            playerMovement.runSpeed /= appliedSpeedMultiplier;
            appliedSpeedMultiplier = 1f;
        }

        if (appliedMaxHealthBonus > 0f)
        {
            playerMovement.maxHealth = Mathf.Max(1f, playerMovement.maxHealth - appliedMaxHealthBonus);
            playerMovement.currentHealth = Mathf.Clamp(playerMovement.currentHealth, 0f, playerMovement.maxHealth);
            appliedMaxHealthBonus = 0f;
        }

        if (appliedMaxStaminaBonus > 0f)
        {
            playerMovement.maxStamina = Mathf.Max(1f, playerMovement.maxStamina - appliedMaxStaminaBonus);
            playerMovement.currentStamina = Mathf.Clamp(playerMovement.currentStamina, 0f, playerMovement.maxStamina);
            appliedMaxStaminaBonus = 0f;
        }

        if (appliedHealthRegenBonus > 0f)
        {
            playerMovement.healthRegenPerSecond = Mathf.Max(0f, playerMovement.healthRegenPerSecond - appliedHealthRegenBonus);
            appliedHealthRegenBonus = 0f;
        }
    }

    void ResetRuntimeBonuses()
    {
        DamageBonus = 0f;
        AnimalDamageBonus = 0f;
        ResourceBonusChance = 0f;
        BonusDefense = 0f;
        IgnoreDamageChance = 0f;
        AttackCooldownMultiplier = 1f;
    }

    void NotifyHudChanged()
    {
        AncestralPowerHUD.RefreshAll();
    }
}

public class AncestralPowerHUD : MonoBehaviour
{
    public Vector2 panelAnchorPosition = new Vector2(360f, -72f);
    public Vector2 panelSize = new Vector2(326f, 176f);
    public int visiblePowerRows = 10;

    const int PowersPerRow = 5;
    const float PowerIconSize = 50f;
    const float PowerSlotGap = 6f;

    static readonly List<AncestralPowerHUD> instances = new List<AncestralPowerHUD>();

    readonly List<GameObject> rowObjects = new List<GameObject>();
    readonly List<TextMeshProUGUI> rowTexts = new List<TextMeshProUGUI>();
    readonly List<Image> rowMarkers = new List<Image>();
    readonly List<Image> rowIcons = new List<Image>();

    PlayerMovement trackedPlayer;
    AncestralPowerService powerService;
    Canvas canvas;
    RectTransform rootRect;
    CanvasGroup canvasGroup;
    TextMeshProUGUI titleText;
    TextMeshProUGUI countText;
    TextMeshProUGUI emptyText;
    float nextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (LanMultiplayerManager.IsDedicatedProcessRequested || LanMultiplayerManager.IsDedicatedRuntime)
            return;

        if (FindFirstObjectByType<AncestralPowerHUD>() != null)
            return;

        GameObject hudObject = new GameObject("Ancestral Power HUD");
        DontDestroyOnLoad(hudObject);
        hudObject.AddComponent<AncestralPowerHUD>();
    }

    public static void RefreshAll()
    {
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null)
                instances[i].Refresh();
        }
    }

    void OnEnable()
    {
        if (!instances.Contains(this))
            instances.Add(this);
    }

    void OnDisable()
    {
        instances.Remove(this);
    }

    void Start()
    {
        ResolveReferences();
        EnsureUI();
        Refresh();
    }

    void Update()
    {
        ResolveReferences();
        EnsureUI();

        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 0.25f;
            Refresh();
        }
    }

    void ResolveReferences()
    {
        PlayerMovement currentPlayer = LanMultiplayerManager.FindGameplayPlayer();
        if (currentPlayer == null)
            currentPlayer = SceneObjectCache.Find<PlayerMovement>(gameObject.scene, true);
        if (currentPlayer == null)
            currentPlayer = FindFirstObjectByType<PlayerMovement>();

        if (trackedPlayer == currentPlayer && powerService != null)
            return;

        trackedPlayer = currentPlayer;
        powerService = trackedPlayer != null ? AncestralPowerService.GetOrCreate(trackedPlayer) : null;
    }

    void EnsureUI()
    {
        if (canvas == null)
        {
            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 90;

            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (rootRect == null)
        {
            GameObject rootObject = new GameObject("AncestralPowerPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(Outline));
            rootObject.transform.SetParent(transform, false);

            rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);

            Image background = rootObject.GetComponent<Image>();
            background.color = new Color(0.06f, 0.055f, 0.035f, 0.78f);
            background.raycastTarget = false;

            Outline outline = rootObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.61f, 0.28f, 0.82f);
            outline.effectDistance = new Vector2(2f, -2f);

            canvasGroup = rootObject.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        rootRect.anchoredPosition = panelAnchorPosition;
        rootRect.sizeDelta = panelSize;

        titleText = EnsureText(rootRect, "PowerTitle", new Vector2(18f, -14f), new Vector2(160f, 30f), 24f, FontStyles.Bold, TextAlignmentOptions.Left);
        countText = EnsureText(rootRect, "PowerCount", new Vector2(panelSize.x - 106f, -15f), new Vector2(88f, 30f), 22f, FontStyles.Bold, TextAlignmentOptions.Right);
        emptyText = EnsureText(rootRect, "PowerEmpty", new Vector2(18f, -76f), new Vector2(panelSize.x - 36f, 48f), 17f, FontStyles.Italic, TextAlignmentOptions.Left);

        titleText.text = "Poderes";
        titleText.color = new Color(1f, 0.91f, 0.62f, 1f);

        countText.color = new Color(0.78f, 1f, 0.67f, 1f);
        emptyText.color = new Color(0.88f, 0.82f, 0.68f, 0.86f);
        emptyText.text = "Nenhum poder coletado";
        emptyText.raycastTarget = false;

        EnsureRows(Mathf.Max(1, visiblePowerRows));
    }

    TextMeshProUGUI EnsureText(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(objectName);
        GameObject textObject;
        TextMeshProUGUI text;

        if (existing != null)
        {
            textObject = existing.gameObject;
            text = textObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.margin = Vector4.zero;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    void EnsureRows(int rowCount)
    {
        for (int i = rowObjects.Count; i < rowCount; i++)
        {
            GameObject rowObject = new GameObject($"PowerRow{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rowObject.transform.SetParent(rootRect, false);

            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);

            Image rowBackground = rowObject.GetComponent<Image>();
            rowBackground.color = new Color(0.18f, 0.14f, 0.07f, 0.34f);
            rowBackground.raycastTarget = false;

            GameObject markerObject = new GameObject("TierMarker", typeof(RectTransform), typeof(Image));
            markerObject.transform.SetParent(rowObject.transform, false);
            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0f, 0.5f);
            markerRect.anchorMax = new Vector2(0f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = new Vector2(12f, 0f);
            markerRect.sizeDelta = new Vector2(8f, 20f);

            Image markerImage = markerObject.GetComponent<Image>();
            markerImage.color = Color.white;
            markerImage.enabled = false;
            markerImage.raycastTarget = false;

            GameObject iconObject = new GameObject("PowerIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(rowObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(PowerIconSize, PowerIconSize);

            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            TextMeshProUGUI rowText = EnsureText(rowObject.transform, "PowerText", new Vector2(50f, -4f), new Vector2(panelSize.x - 84f, 22f), 16f, FontStyles.Bold, TextAlignmentOptions.Left);
            rowText.color = new Color(0.97f, 0.91f, 0.74f, 1f);
            rowText.gameObject.SetActive(false);

            rowObjects.Add(rowObject);
            rowMarkers.Add(markerImage);
            rowIcons.Add(iconImage);
            rowTexts.Add(rowText);
        }

        float firstX = 18f;
        float firstY = -54f;
        float slotSize = PowerIconSize + 4f;

        for (int i = 0; i < rowObjects.Count; i++)
        {
            int column = i % PowersPerRow;
            int row = i / PowersPerRow;

            RectTransform rowRect = rowObjects[i].GetComponent<RectTransform>();
            rowRect.anchoredPosition = new Vector2(firstX + column * (slotSize + PowerSlotGap), firstY - row * (slotSize + PowerSlotGap));
            rowRect.sizeDelta = new Vector2(slotSize, slotSize);

            if (i < rowIcons.Count && rowIcons[i] != null)
            {
                RectTransform iconRect = rowIcons[i].GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(PowerIconSize, PowerIconSize);
            }

            if (i < rowMarkers.Count && rowMarkers[i] != null)
                rowMarkers[i].enabled = false;

            if (i < rowTexts.Count && rowTexts[i] != null)
                rowTexts[i].gameObject.SetActive(false);
        }
    }

    public void Refresh()
    {
        if (rootRect == null || titleText == null || countText == null)
            return;

        int powerCount = powerService != null ? powerService.ActivePowerCount : 0;
        int powerLimit = powerService != null ? powerService.MaxActivePowers : 10;
        countText.text = $"{powerCount} / {powerLimit}";

        if (canvasGroup != null)
            canvasGroup.alpha = powerCount > 0 ? 1f : 0.72f;

        List<AncestralPowerDefinition> powers = powerService != null ? powerService.GetActivePowerDefinitions() : new List<AncestralPowerDefinition>();
        int visibleRows = Mathf.Max(1, visiblePowerRows);
        EnsureRows(visibleRows);

        if (emptyText != null)
            emptyText.gameObject.SetActive(powers.Count == 0);

        for (int i = 0; i < rowObjects.Count; i++)
        {
            bool hasPower = i < powers.Count && i < visibleRows;
            rowObjects[i].SetActive(hasPower);

            if (!hasPower)
                continue;

            bool shouldCollapseRemaining = i == visibleRows - 1 && powers.Count > visibleRows;
            if (shouldCollapseRemaining)
            {
                int hiddenCount = powers.Count - visibleRows + 1;
                rowTexts[i].gameObject.SetActive(true);
                rowTexts[i].text = $"+{hiddenCount}";
                rowTexts[i].fontSize = 18f;
                rowTexts[i].alignment = TextAlignmentOptions.Center;
                if (i < rowIcons.Count)
                    rowIcons[i].enabled = false;
                continue;
            }

            AncestralPowerDefinition definition = powers[i];
            rowTexts[i].gameObject.SetActive(false);

            Color tierColor = GetTierColor(definition.tier);
            Image rowBackground = rowObjects[i].GetComponent<Image>();
            if (rowBackground != null)
                rowBackground.color = new Color(tierColor.r, tierColor.g, tierColor.b, 0.18f);

            if (i < rowIcons.Count)
            {
                Sprite icon = definition.GetIcon();
                rowIcons[i].sprite = icon;
                rowIcons[i].enabled = icon != null;
                rowIcons[i].color = Color.white;
            }
        }
    }

    Color GetTierColor(AncestralTotemTier tier)
    {
        switch (tier)
        {
            case AncestralTotemTier.Rare:
                return new Color(0.32f, 0.62f, 1f, 1f);
            case AncestralTotemTier.Legendary:
                return new Color(1f, 0.73f, 0.18f, 1f);
            default:
                return new Color(0.44f, 0.95f, 0.45f, 1f);
        }
    }
}

public class AncestralTotem : MonoBehaviour, IPlayerInteractable
{
    public AncestralTotemTier tier = AncestralTotemTier.Common;
    public int enemiesPerEvent = 3;
    public float enemySpawnRadius = 6f;
    public float checkCompletionInterval = 0.35f;

    [Header("Recompensa de XP")]
    public int commonXpReward = 150;
    public int rareXpReward = 350;
    public int legendaryXpReward = 750;

    readonly List<GameObject> summonedEnemies = new List<GameObject>();
    PlayerMovement activatingPlayer;
    bool isActive;
    bool isCompleted;
    bool visualBuilt;
    float nextCompletionCheckTime;

    public bool IsActive => isActive;
    public bool IsCompleted => isCompleted;

    public void Configure(AncestralTotemTier configuredTier)
    {
        tier = configuredTier;
        BuildVisual();
        EnsureCollider();
    }

    void Start()
    {
        if (!visualBuilt)
            BuildVisual();

        EnsureCollider();
    }

    public bool Interact(PlayerInteraction playerInteraction)
    {
        if (isCompleted)
        {
            MessageSystem.Instance?.ShowMessage("Este totem ja foi consumido.");
            return true;
        }

        if (isActive)
        {
            MessageSystem.Instance?.ShowMessage("Derrote os inimigos invocados pelo totem.");
            return true;
        }

        activatingPlayer = playerInteraction != null ? playerInteraction.GetComponent<PlayerMovement>() : null;
        if (activatingPlayer == null)
        {
            MessageSystem.Instance?.ShowMessage("Player nao encontrado para ativar o totem.");
            return true;
        }

        StartCoroutine(RunTotemEventRoutine());
        return true;
    }

    IEnumerator RunTotemEventRoutine()
    {
        isActive = true;
        MessageSystem.Instance?.ShowMessage($"{GetTierDisplayName()} ativado!");
        PlayActivationBurst();

        yield return new WaitForSeconds(0.35f);

        int count = Mathf.Max(1, enemiesPerEvent);
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = ResolveEnemySpawnPosition(i, count);
            GameObject enemy = SpawnEnemyForTier(i, spawnPosition);
            if (enemy != null)
                summonedEnemies.Add(enemy);

            PlaySummonBurst(spawnPosition);
            yield return new WaitForSeconds(0.22f);
        }

        nextCompletionCheckTime = Time.time + checkCompletionInterval;
        while (!isCompleted)
        {
            if (Time.time >= nextCompletionCheckTime)
            {
                nextCompletionCheckTime = Time.time + checkCompletionInterval;
                CleanupSummonedEnemies();

                if (summonedEnemies.Count == 0)
                    CompleteEvent();
            }

            yield return null;
        }
    }

    void CompleteEvent()
    {
        if (isCompleted)
            return;

        isCompleted = true;
        isActive = false;

        GrantCompletionExperience();
        SpawnRewardPickup();
        PlayerAnimationBridge.Trigger(activatingPlayer, PlayerAnimationBridge.VictoryTrigger);
        MessageSystem.Instance?.ShowMessage("Totem destruido. Colete o buff ancestral.");
        PlayActivationBurst();
        StartCoroutine(DestroyTotemRoutine());
    }

    void GrantCompletionExperience()
    {
        if (activatingPlayer == null)
            return;

        int xpReward = GetXpReward();
        if (xpReward <= 0)
            return;

        PlayerProgression progression = activatingPlayer.GetComponent<PlayerProgression>();
        if (progression == null)
            progression = activatingPlayer.gameObject.AddComponent<PlayerProgression>();

        progression.AddExperience(xpReward, GetTierDisplayName());
    }

    int GetXpReward()
    {
        switch (tier)
        {
            case AncestralTotemTier.Rare:
                return Mathf.Max(0, rareXpReward);

            case AncestralTotemTier.Legendary:
                return Mathf.Max(0, legendaryXpReward);

            default:
                return Mathf.Max(0, commonXpReward);
        }
    }

    void SpawnRewardPickup()
    {
        Vector3 spawnPosition = transform.position + Vector3.up * 0.55f;
        GameObject rewardObject = new GameObject($"Buff Ancestral {tier}");
        rewardObject.transform.position = spawnPosition;

        AncestralPowerPickup pickup = rewardObject.AddComponent<AncestralPowerPickup>();
        pickup.Initialize(tier, activatingPlayer);
    }

    IEnumerator DestroyTotemRoutine()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Transform visual = transform.Find("Visual");
        Vector3 startScale = visual != null ? visual.localScale : Vector3.one;
        float duration = 0.65f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (visual != null)
                visual.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            transform.position += Vector3.down * (Time.deltaTime * 0.35f);
            yield return null;
        }

        Destroy(gameObject);
    }

    void CleanupSummonedEnemies()
    {
        for (int i = summonedEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = summonedEnemies[i];
            if (enemy == null || IsEnemyDefeated(enemy))
                summonedEnemies.RemoveAt(i);
        }
    }

    bool IsEnemyDefeated(GameObject enemy)
    {
        WildBoar boar = enemy.GetComponent<WildBoar>();
        if (boar != null)
            return boar.IsDead || boar.CurrentHealth <= 0;

        WildChicken chicken = enemy.GetComponent<WildChicken>();
        if (chicken != null)
            return chicken.IsDead || chicken.CurrentHealth <= 0;

        EarthGolem golem = enemy.GetComponent<EarthGolem>();
        if (golem != null)
            return golem.IsDead || golem.CurrentHealth <= 0;

        return false;
    }

    GameObject SpawnEnemyForTier(int index, Vector3 spawnPosition)
    {
        switch (tier)
        {
            case AncestralTotemTier.Legendary:
                if (index == 0)
                    return SpawnBoar(spawnPosition, "Javali Colossal Invocado", 1.65f, 2.25f, 1.2f, 1.25f);

                return SpawnGolem(spawnPosition, "Golem de Terra Invocado", 1.1f, 1.45f, 1.18f, 1.22f);

            case AncestralTotemTier.Rare:
                if (index == 2)
                    return SpawnGolem(spawnPosition, "Golem Menor Invocado", 0.72f, 0.65f, 1.12f, 1.16f);

                return SpawnBoar(spawnPosition, "Javali Alfa Invocado", 1.25f, 1.45f, 1.12f, 1.16f);

            default:
                if (index == 0)
                    return SpawnBoar(spawnPosition, "Javali Jovem Invocado", 0.82f, 0.72f, 1.04f, 1.08f);

                return SpawnChicken(spawnPosition, "Galinha Selvagem Invocada", 1f, 1f, 1.08f);
        }
    }

    GameObject SpawnBoar(Vector3 position, string objectName, float visualScale, float healthMultiplier, float speedMultiplier, float abilitySpeedMultiplier)
    {
        GameObject enemyObject = new GameObject(objectName);
        enemyObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(GetFlatDirectionToPlayer(position), Vector3.up));
        enemyObject.transform.localScale = Vector3.one * Mathf.Max(0.35f, visualScale);

        WildBoar boar = enemyObject.AddComponent<WildBoar>();
        boar.maxHealth = Mathf.Max(1, Mathf.RoundToInt(boar.maxHealth * healthMultiplier));
        boar.patrolSpeed *= speedMultiplier;
        boar.chaseSpeed *= speedMultiplier;
        boar.chargeSpeed *= speedMultiplier;
        boar.attackCooldown = Mathf.Max(0.6f, boar.attackCooldown / Mathf.Max(0.1f, abilitySpeedMultiplier));
        boar.attackWindupDuration = Mathf.Max(0.12f, boar.attackWindupDuration / Mathf.Max(0.1f, abilitySpeedMultiplier));
        boar.minDamage = Mathf.Max(1, Mathf.RoundToInt(boar.minDamage * healthMultiplier * 0.9f));
        boar.maxDamage = Mathf.Max(boar.minDamage, Mathf.RoundToInt(boar.maxDamage * healthMultiplier * 0.9f));
        boar.thickLeatherItemData = ThickLeatherItemRegistry.GetOrCreate();
        boar.sharpTuskItemData = SharpTuskItemRegistry.GetOrCreate();
        boar.boarMeatItemData = BoarMeatItemRegistry.GetOrCreate();
        boar.trophyItemData = BoarTrophyItemRegistry.GetOrCreate();
        boar.SetSpawnData(null, position);
        boar.ForceTotemTarget(activatingPlayer);
        AttachTotemAggressor(enemyObject, 0f, 0, false);
        LanNetworkEntity.Ensure(boar, $"AncestralBoar|{tier}|{Time.frameCount}|{summonedEnemies.Count}");
        return enemyObject;
    }

    GameObject SpawnChicken(Vector3 position, string objectName, float visualScale, float healthMultiplier, float speedMultiplier)
    {
        GameObject enemyObject = new GameObject(objectName);
        enemyObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(GetFlatDirectionToPlayer(position), Vector3.up));
        enemyObject.transform.localScale = Vector3.one * Mathf.Max(0.35f, visualScale);

        WildChicken chicken = enemyObject.AddComponent<WildChicken>();
        chicken.maxHealth = Mathf.Max(1, Mathf.RoundToInt(chicken.maxHealth * healthMultiplier));
        chicken.moveSpeed *= speedMultiplier;
        chicken.fleeSpeed *= speedMultiplier;
        chicken.featherItemData = FeatherItemRegistry.GetOrCreate();
        chicken.rawMeatItemData = RawChickenMeatItemRegistry.GetOrCreate();
        chicken.SetSpawnData(null, position);
        AttachTotemAggressor(enemyObject, Mathf.Max(chicken.fleeSpeed, 5.4f) * speedMultiplier, GetSummonedChickenDamage(), true);
        LanNetworkEntity.Ensure(chicken, $"AncestralChicken|{tier}|{Time.frameCount}|{summonedEnemies.Count}");
        return enemyObject;
    }

    GameObject SpawnGolem(Vector3 position, string objectName, float visualScaleMultiplier, float healthMultiplier, float speedMultiplier, float abilitySpeedMultiplier)
    {
        GameObject enemyObject = new GameObject(objectName);
        enemyObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(GetFlatDirectionToPlayer(position), Vector3.up));

        EarthGolem golem = enemyObject.AddComponent<EarthGolem>();
        golem.suggestedLevel = tier == AncestralTotemTier.Rare ? 10 : 20;
        golem.visualScale = Mathf.Max(0.8f, golem.visualScale * visualScaleMultiplier);
        golem.maxHealth = Mathf.Max(1, Mathf.RoundToInt(golem.maxHealth * healthMultiplier));
        golem.patrolSpeed *= speedMultiplier;
        golem.chaseSpeed *= speedMultiplier;
        golem.groundSlamCooldown = Mathf.Max(1.2f, golem.groundSlamCooldown / Mathf.Max(0.1f, abilitySpeedMultiplier));
        golem.heavyPunchCooldown = Mathf.Max(1f, golem.heavyPunchCooldown / Mathf.Max(0.1f, abilitySpeedMultiplier));
        golem.groundSlamWindupDuration = Mathf.Max(0.2f, golem.groundSlamWindupDuration / Mathf.Max(0.1f, abilitySpeedMultiplier));
        golem.heavyPunchWindupDuration = Mathf.Max(0.12f, golem.heavyPunchWindupDuration / Mathf.Max(0.1f, abilitySpeedMultiplier));
        golem.groundSlamDamage = Mathf.Max(1, Mathf.RoundToInt(golem.groundSlamDamage * healthMultiplier * 0.75f));
        golem.heavyPunchDamage = Mathf.Max(1, Mathf.RoundToInt(golem.heavyPunchDamage * healthMultiplier * 0.75f));
        golem.stoneFragmentItemData = StoneFragmentItemRegistry.GetOrCreate();
        golem.resilientMossItemData = ResilientMossItemRegistry.GetOrCreate();
        golem.ironOreItemData = IronItemRegistry.GetOrCreate();
        golem.earthCoreItemData = EarthCoreItemRegistry.GetOrCreate();
        golem.SetSpawnData(null, position);
        golem.ForceTotemTarget(activatingPlayer);
        AttachTotemAggressor(enemyObject, 0f, 0, false);
        LanNetworkEntity.Ensure(golem, $"AncestralGolem|{tier}|{Time.frameCount}|{summonedEnemies.Count}");
        return enemyObject;
    }

    void AttachTotemAggressor(GameObject enemyObject, float chaseSpeed, int contactDamage, bool directChase)
    {
        if (enemyObject == null || activatingPlayer == null)
            return;

        TotemSummonedAggressor aggressor = enemyObject.AddComponent<TotemSummonedAggressor>();
        aggressor.Initialize(activatingPlayer, chaseSpeed, contactDamage, directChase);
    }

    int GetSummonedChickenDamage()
    {
        switch (tier)
        {
            case AncestralTotemTier.Legendary:
                return 26;

            case AncestralTotemTier.Rare:
                return 18;

            default:
                return 12;
        }
    }

    Vector3 ResolveEnemySpawnPosition(int index, int count)
    {
        float angle = count > 0 ? (360f / count) * index : 0f;
        Vector3 direction = Quaternion.Euler(0f, angle, 0f) * transform.forward;
        if (direction.sqrMagnitude < 0.001f)
            direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

        Vector3 desired = transform.position + direction.normalized * enemySpawnRadius;
        return ResolveGroundedPosition(desired);
    }

    Vector3 ResolveGroundedPosition(Vector3 desiredPosition)
    {
        Vector3 origin = desiredPosition + Vector3.up * 28f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return desiredPosition;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hits[i].normal.y < 0.35f)
                continue;

            if (hitCollider.GetComponentInParent<PlayerMovement>() != null ||
                hitCollider.GetComponentInParent<RemotePlayerReplica>() != null ||
                hitCollider.GetComponentInParent<AncestralTotem>() != null ||
                hitCollider.GetComponentInParent<EarthGolem>() != null ||
                hitCollider.GetComponentInParent<WildBoar>() != null ||
                hitCollider.GetComponentInParent<WildChicken>() != null ||
                hitCollider.GetComponentInParent<Cow>() != null ||
                hitCollider.GetComponentInParent<MiniKrug>() != null ||
                hitCollider.GetComponentInParent<BossEnemy>() != null)
                continue;

            return hits[i].point;
        }

        return desiredPosition;
    }

    Vector3 GetFlatDirectionToPlayer(Vector3 position)
    {
        Vector3 direction = activatingPlayer != null
            ? activatingPlayer.transform.position - position
            : transform.position - position;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return transform.forward.sqrMagnitude > 0.001f ? transform.forward.normalized : Vector3.forward;

        return direction.normalized;
    }

    void EnsureCollider()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = gameObject.AddComponent<CapsuleCollider>();

        capsule.center = new Vector3(0f, 1.05f, 0f);
        capsule.height = 2.2f;
        capsule.radius = 0.85f;
        capsule.isTrigger = false;
    }

    void BuildVisual()
    {
        Transform oldVisual = transform.Find("Visual");
        if (oldVisual != null)
        {
            if (Application.isPlaying)
                Destroy(oldVisual.gameObject);
            else
                DestroyImmediate(oldVisual.gameObject);
        }

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;

        Color stoneColor = new Color(0.34f, 0.31f, 0.25f, 1f);
        Color darkStoneColor = new Color(0.18f, 0.17f, 0.15f, 1f);
        Color glowColor = new Color(0.25f, 0.95f, 0.38f, 1f);
        Color runeColor = new Color(0.55f, 1f, 0.55f, 1f);

        if (tier == AncestralTotemTier.Rare)
        {
            stoneColor = new Color(0.28f, 0.31f, 0.38f, 1f);
            darkStoneColor = new Color(0.13f, 0.16f, 0.23f, 1f);
            glowColor = new Color(0.25f, 0.58f, 1f, 1f);
            runeColor = new Color(0.55f, 0.82f, 1f, 1f);
        }
        else if (tier == AncestralTotemTier.Legendary)
        {
            stoneColor = new Color(0.13f, 0.12f, 0.12f, 1f);
            darkStoneColor = new Color(0.07f, 0.055f, 0.045f, 1f);
            glowColor = new Color(1f, 0.72f, 0.1f, 1f);
            runeColor = new Color(1f, 0.22f, 0.08f, 1f);
        }

        CreatePiece(PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 0.18f, 0f), new Vector3(1.55f, 0.18f, 1.55f), Quaternion.identity, stoneColor, "Base");
        CreatePiece(PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.72f, 0.45f, 0.72f), Quaternion.identity, darkStoneColor, "Pedra Central");
        CreatePiece(PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.48f, 0.58f, 0.48f), Quaternion.identity, stoneColor, "Pilar");
        CreatePiece(PrimitiveType.Cube, visual.transform, new Vector3(0f, 1.75f, 0f), new Vector3(1f, 0.34f, 1f), Quaternion.Euler(0f, 45f, 0f), darkStoneColor, "Topo");
        CreatePiece(PrimitiveType.Cube, visual.transform, new Vector3(0f, 2.13f, 0f), new Vector3(0.42f, 0.58f, 0.42f), Quaternion.Euler(0f, 45f, 45f), glowColor, "Cristal");

        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 0.78f);
            CreatePiece(PrimitiveType.Cube, visual.transform, new Vector3(offset.x, 1.18f, offset.z), new Vector3(0.08f, 0.5f, 0.05f), Quaternion.Euler(0f, angle, 0f), runeColor, "Runa");
        }

        for (int i = 0; i < 3; i++)
        {
            float angle = 35f + i * 120f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 0.98f);
            CreatePiece(PrimitiveType.Cube, visual.transform, new Vector3(offset.x, 0.42f, offset.z), new Vector3(0.42f, 0.28f, 0.34f), Quaternion.Euler(12f, angle, -8f), stoneColor, "Pedra Lateral");
        }

        PointLight(visual.transform, glowColor);
        CreateTotemParticles(visual.transform, glowColor);
        visualBuilt = true;
    }

    void CreatePiece(PrimitiveType primitive, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Color color, string pieceName)
    {
        GameObject piece = GameObject.CreatePrimitive(primitive);
        piece.name = pieceName;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = localRotation;
        piece.transform.localScale = localScale;

        Collider collider = piece.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }

        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial(pieceName, color);
    }

    void PointLight(Transform parent, Color color)
    {
        GameObject lightObject = new GameObject("Totem Light");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.85f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = tier == AncestralTotemTier.Legendary ? 7f : 5f;
        light.intensity = tier == AncestralTotemTier.Legendary ? 2.2f : 1.35f;
        light.color = color;
    }

    void CreateTotemParticles(Transform parent, Color color)
    {
        GameObject particleObject = new GameObject("Totem Particles");
        particleObject.transform.SetParent(parent, false);
        particleObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.startLifetime = 1.35f;
        main.startSpeed = 0.25f;
        main.startSize = 0.08f;
        main.startColor = color;
        main.maxParticles = 40;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = tier == AncestralTotemTier.Legendary ? 16f : 9f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.72f;
        particles.Play();
    }

    void PlayActivationBurst()
    {
        PlaySummonBurst(transform.position + Vector3.up * 1.2f);
    }

    void PlaySummonBurst(Vector3 position)
    {
        GameObject burstObject = new GameObject("Ancestral Totem Burst");
        burstObject.transform.position = position;
        ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.duration = 0.35f;
        main.startLifetime = 0.55f;
        main.startSpeed = 2.1f;
        main.startSize = 0.12f;
        main.startColor = GetTierGlowColor();

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        particles.Play();
        Destroy(burstObject, 1.2f);
    }

    Color GetTierGlowColor()
    {
        if (tier == AncestralTotemTier.Rare)
            return new Color(0.25f, 0.58f, 1f, 1f);

        if (tier == AncestralTotemTier.Legendary)
            return new Color(1f, 0.72f, 0.1f, 1f);

        return new Color(0.25f, 0.95f, 0.38f, 1f);
    }

    string GetTierDisplayName()
    {
        switch (tier)
        {
            case AncestralTotemTier.Rare:
                return "Totem Ancestral Raro";

            case AncestralTotemTier.Legendary:
                return "Totem Ancestral Lendario";

            default:
                return "Totem Ancestral Comum";
        }
    }

    static Material CreateRuntimeMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        material.name = $"{name} Runtime";
        material.color = color;

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.55f);
        }

        return material;
    }
}

public class TotemSummonedAggressor : MonoBehaviour
{
    PlayerMovement target;
    WildChicken chicken;
    WildBoar boar;
    EarthGolem golem;
    bool directChase;
    float chaseSpeed;
    int contactDamage;
    float nextContactAttackTime;
    float nextForcedTargetTime;

    public void Initialize(PlayerMovement forcedTarget, float forcedChaseSpeed, int forcedContactDamage, bool useDirectChase)
    {
        target = forcedTarget;
        chaseSpeed = Mathf.Max(0f, forcedChaseSpeed);
        contactDamage = Mathf.Max(0, forcedContactDamage);
        directChase = useDirectChase;
        CacheComponents();
        ApplyAggressionSettings(true);
    }

    void Awake()
    {
        CacheComponents();
    }

    void Start()
    {
        CacheComponents();
        ResolveTarget();
        ApplyAggressionSettings(true);
    }

    void Update()
    {
        ResolveTarget();
        ApplyAggressionSettings(false);
    }

    void LateUpdate()
    {
        if (!directChase || chicken == null || chicken.IsDead)
            return;

        ResolveTarget();
        if (target == null || GameState.IsPlayerDead)
            return;

        ChaseAndAttackTarget();
    }

    void CacheComponents()
    {
        if (chicken == null)
            chicken = GetComponent<WildChicken>();

        if (boar == null)
            boar = GetComponent<WildBoar>();

        if (golem == null)
            golem = GetComponent<EarthGolem>();
    }

    void ResolveTarget()
    {
        if (target != null && target.gameObject.activeInHierarchy && !GameState.IsPlayerDead)
            return;

        target = LanMultiplayerManager.FindGameplayPlayer();
    }

    void ApplyAggressionSettings(bool immediate)
    {
        if (target == null || Time.time < nextForcedTargetTime)
            return;

        nextForcedTargetTime = Time.time + 0.35f;

        if (boar != null && !boar.IsDead)
            boar.ForceTotemTarget(target, immediate);

        if (golem != null && !golem.IsDead)
            golem.ForceTotemTarget(target, immediate);

        if (chicken != null && directChase)
        {
            chicken.fleeDistance = 0f;
            chicken.fleeDuration = 0f;
            chicken.moveSpeed = Mathf.Max(chicken.moveSpeed, chaseSpeed * 0.75f);
            chicken.fleeSpeed = Mathf.Max(chicken.fleeSpeed, chaseSpeed);
            chicken.wanderRadius = Mathf.Max(chicken.wanderRadius, 28f);
        }
    }

    void ChaseAndAttackTarget()
    {
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance <= 0.001f)
            return;

        Vector3 direction = toTarget / distance;
        float speed = Mathf.Max(1f, chaseSpeed > 0f ? chaseSpeed : chicken.fleeSpeed);

        if (distance > 1.05f)
        {
            Vector3 nextPosition = transform.position + direction * speed * Time.deltaTime;
            if (TryResolveGround(nextPosition, out Vector3 groundedPosition))
                nextPosition = groundedPosition;

            transform.position = nextPosition;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Mathf.Max(8f, chicken.rotationSpeed) * Time.deltaTime);

        if (contactDamage <= 0 || distance > 1.45f || Time.time < nextContactAttackTime)
            return;

        nextContactAttackTime = Time.time + 0.8f;
        target.TakeDamage(contactDamage);
        target.ApplyKnockback(direction, 2.25f, 0.12f);
    }

    bool TryResolveGround(Vector3 position, out Vector3 groundedPosition)
    {
        Vector3 origin = position + Vector3.up * 16f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 44f, ~0, QueryTriggerInteraction.Ignore);
        groundedPosition = position;

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hits[i].normal.y < 0.35f)
                continue;

            if (hitCollider.GetComponentInParent<PlayerMovement>() != null ||
                hitCollider.GetComponentInParent<RemotePlayerReplica>() != null ||
                hitCollider.GetComponentInParent<WildChicken>() == chicken ||
                hitCollider.GetComponentInParent<WildBoar>() == boar ||
                hitCollider.GetComponentInParent<EarthGolem>() == golem)
                continue;

            groundedPosition = hits[i].point;
            return true;
        }

        return false;
    }
}

public class AncestralPowerPickup : MonoBehaviour
{
    public AncestralTotemTier tier = AncestralTotemTier.Common;
    public float collectRadius = 1.65f;
    public float hoverHeight = 1.05f;
    public float bobHeight = 0.16f;
    public float bobSpeed = 2.8f;
    public float rotationSpeed = 80f;
    public float collectDelay = 0.65f;

    PlayerMovement preferredPlayer;
    Transform visualRoot;
    Transform spriteRoot;
    TextMesh label;
    Vector3 basePosition;
    float phaseOffset;
    float spawnTime;
    float nextFailedCollectMessageTime;
    bool collected;

    public void Initialize(AncestralTotemTier pickupTier, PlayerMovement player)
    {
        tier = pickupTier;
        preferredPlayer = player;
        BuildVisual();
        EnsureCollider();
        SetupPosition();
    }

    void Start()
    {
        if (visualRoot == null)
            BuildVisual();

        EnsureCollider();
        SetupPosition();
    }

    void Update()
    {
        if (collected)
            return;

        Animate();

        if (Time.time < spawnTime + collectDelay)
            return;

        TryCollect();
    }

    void SetupPosition()
    {
        if (spawnTime <= 0f)
        {
            spawnTime = Time.time;
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        basePosition = ResolveGroundPosition(transform.position) + Vector3.up * hoverHeight;
        transform.position = basePosition;
    }

    void BuildVisual()
    {
        if (visualRoot != null)
            return;

        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = Vector3.zero;
        visualRoot = visualObject.transform;

        GameObject spriteObject = new GameObject("BuffSprite");
        spriteObject.transform.SetParent(visualRoot, false);
        spriteObject.transform.localPosition = Vector3.zero;
        spriteObject.transform.localScale = Vector3.one * 0.72f;
        spriteRoot = spriteObject.transform;

        SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateBuffSprite(GetTierColor());
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 20;

        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        halo.name = "BuffGlow";
        halo.transform.SetParent(visualRoot, false);
        halo.transform.localPosition = Vector3.zero;
        halo.transform.localScale = Vector3.one * 0.42f;

        Collider haloCollider = halo.GetComponent<Collider>();
        if (haloCollider != null)
            Destroy(haloCollider);

        Renderer haloRenderer = halo.GetComponent<Renderer>();
        if (haloRenderer != null)
            haloRenderer.sharedMaterial = CreatePickupMaterial(GetTierColor() * 0.85f);

        GameObject labelObject = new GameObject("BuffLabel");
        labelObject.transform.SetParent(visualRoot, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.58f, 0f);
        label = labelObject.AddComponent<TextMesh>();
        label.text = GetTierLabel();
        label.characterSize = 0.13f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = new Color(1f, 0.94f, 0.72f, 1f);

        Light light = visualObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = GetTierColor();
        light.range = tier == AncestralTotemTier.Legendary ? 5.2f : 3.6f;
        light.intensity = tier == AncestralTotemTier.Legendary ? 1.8f : 1.15f;

        CreateIdleParticles(visualObject.transform);
    }

    void EnsureCollider()
    {
        SphereCollider collider = GetComponent<SphereCollider>();
        if (collider == null)
            collider = gameObject.AddComponent<SphereCollider>();

        collider.isTrigger = true;
        collider.radius = collectRadius;
        collider.center = Vector3.zero;
    }

    void Animate()
    {
        float bob = Mathf.Sin((Time.time * bobSpeed) + phaseOffset) * bobHeight;
        transform.position = basePosition + Vector3.up * bob;

        if (visualRoot != null)
            visualRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        Camera camera = RuntimeCameraCache.Main;
        if (camera != null)
        {
            if (spriteRoot != null)
                spriteRoot.rotation = camera.transform.rotation;

            if (label != null)
                label.transform.rotation = camera.transform.rotation;
        }
    }

    void TryCollect()
    {
        PlayerMovement player = ResolveCollector();
        if (player == null)
            return;

        if (Vector3.Distance(transform.position, player.transform.position) > collectRadius)
            return;

        AncestralPowerService powerService = AncestralPowerService.GetOrCreate(player);
        if (powerService == null)
            return;

        if (!powerService.GrantRandomPower(tier))
        {
            if (Time.time >= nextFailedCollectMessageTime)
            {
                nextFailedCollectMessageTime = Time.time + 1.2f;
                MessageSystem.Instance?.ShowMessage("Nao foi possivel coletar este buff agora.");
            }

            return;
        }

        collected = true;
        MessageSystem.Instance?.ShowMessage("Buff ancestral coletado.");
        StartCoroutine(CollectRoutine());
    }

    PlayerMovement ResolveCollector()
    {
        if (preferredPlayer != null && preferredPlayer.gameObject.activeInHierarchy && !GameState.IsPlayerDead)
            return preferredPlayer;

        PlayerMovement gameplayPlayer = LanMultiplayerManager.FindGameplayPlayer();
        if (gameplayPlayer != null && !GameState.IsPlayerDead)
        {
            preferredPlayer = gameplayPlayer;
            return gameplayPlayer;
        }

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].isActiveAndEnabled)
                return players[i];
        }

        return null;
    }

    IEnumerator CollectRoutine()
    {
        float duration = 0.32f;
        float elapsed = 0f;
        Vector3 startScale = visualRoot != null ? visualRoot.localScale : Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (visualRoot != null)
                visualRoot.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            transform.position += Vector3.up * (Time.deltaTime * 1.6f);
            yield return null;
        }

        Destroy(gameObject);
    }

    Vector3 ResolveGroundPosition(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * 12f;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return position;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hits[i].normal.y < 0.35f)
                continue;

            if (hitCollider.GetComponentInParent<PlayerMovement>() != null ||
                hitCollider.GetComponentInParent<RemotePlayerReplica>() != null ||
                hitCollider.GetComponentInParent<AncestralPowerPickup>() != null ||
                hitCollider.GetComponentInParent<AncestralTotem>() != null ||
                hitCollider.GetComponentInParent<WildChicken>() != null ||
                hitCollider.GetComponentInParent<WildBoar>() != null ||
                hitCollider.GetComponentInParent<EarthGolem>() != null)
                continue;

            return hits[i].point;
        }

        return position;
    }

    void CreateIdleParticles(Transform parent)
    {
        GameObject particleObject = new GameObject("BuffParticles");
        particleObject.transform.SetParent(parent, false);
        particleObject.transform.localPosition = Vector3.zero;

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.startLifetime = 0.8f;
        main.startSpeed = 0.18f;
        main.startSize = 0.045f;
        main.startColor = GetTierColor();
        main.maxParticles = 26;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = tier == AncestralTotemTier.Legendary ? 14f : 9f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.36f;

        particles.Play();
    }

    Color GetTierColor()
    {
        if (tier == AncestralTotemTier.Rare)
            return new Color(0.25f, 0.62f, 1f, 1f);

        if (tier == AncestralTotemTier.Legendary)
            return new Color(1f, 0.72f, 0.12f, 1f);

        return new Color(0.32f, 1f, 0.42f, 1f);
    }

    string GetTierLabel()
    {
        switch (tier)
        {
            case AncestralTotemTier.Rare:
                return "Buff Raro";

            case AncestralTotemTier.Legendary:
                return "Buff Lendario";

            default:
                return "Buff Comum";
        }
    }

    static Material CreatePickupMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        material.name = "AncestralBuffPickupRuntime";
        material.color = new Color(color.r, color.g, color.b, 0.48f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.7f);
        }

        return material;
    }

    static Sprite CreateBuffSprite(Color color)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "AncestralBuffSprite";
        texture.filterMode = FilterMode.Point;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                float distance = Vector2.Distance(p, center);
                float ring = Mathf.Abs(distance - 23f);
                bool insideCore = distance < 11f;
                bool inOuterRing = ring < 2.1f;
                bool inCross = Mathf.Abs(x - center.x) < 2f || Mathf.Abs(y - center.y) < 2f;
                bool inDiamond = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y) < 22f && distance > 14f;

                Color pixel = Color.clear;
                if (inOuterRing || insideCore || (inCross && distance < 25f) || (inDiamond && distance < 25f))
                {
                    float alpha = insideCore ? 0.95f : 0.78f;
                    pixel = new Color(color.r, color.g, color.b, alpha);
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
    }
}
