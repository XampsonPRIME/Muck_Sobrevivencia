using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    public float treeDensity = 0.003f;
    public int maxTreesPerChunk = 3;
    public float mushroomDensity = 0.001f;
    public int rockClusterCount = 3;
    public int generationYieldInterval = 120;

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

    public float minDistanceBetweenObjects = 15f;
    public float minTreeDistance = 10f;

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
    static Material riverMaterial;

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
        GetComponent<MeshRenderer>().material = terrainMaterial;

        VillageSystem.PrepareVillageHeightsForChunk(GetWorldSeed(), offset, size, GetBaseHeight);

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
                        colors[i] = new Color(0, 0, 1); // areia
                        break;

                    case BiomeType.Forest:
                        colors[i] = new Color(0, 1, 0); // grama
                        break;

                    case BiomeType.Snow:
                        colors[i] = new Color(1, 0, 0); // neve
                        break;
                }
            }
        }

        BuildMesh();
        StartCoroutine(GenerateChunkDetails(offset));
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
        float h = GetBaseHeight(point);
        return VillageSystem.ApplyVillageFlatten(GetWorldSeed(), point, h);
    }

    float GetBaseHeight(Vector2 point)
    {
        float h = Mathf.PerlinNoise(point.x / terrainScale, point.y / terrainScale) * heightMultiplier;
        h += Mathf.PerlinNoise(point.x * 0.05f, point.y * 0.05f) * 2f;

        if (TryGetRiverBlend(point, out float riverBlend))
        {
            float riverDepthValue = RiverSystem.Instance != null ? RiverSystem.Instance.RiverDepth : riverDepth;
            float riverBedHeight = h - riverDepthValue * riverBlend;
            h = Mathf.Min(h, riverBedHeight);
        }
        return h;
    }

    int GetWorldSeed()
    {
        return LanMultiplayerManager.Instance != null ? LanMultiplayerManager.Instance.WorldSeed : 0;
    }

    bool TryGetRiverBlend(Vector2 point, out float blend)
    {
        if (!enableRiver || RiverSystem.Instance == null)
        {
            blend = 0f;
            return false;
        }

        return RiverSystem.Instance.TryGetBlend(point, GetBiome(point) == BiomeType.Forest, out blend);
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
        if (!enableRiver || RiverSystem.Instance == null)
            return false;

        return RiverSystem.Instance.IsRiverZone(point, GetBiome(point) == BiomeType.Forest, extraMargin);
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
        int treeCount = 0;
        int iterationsSinceYield = 0;


        for (int i = 0; i < vertices.Length; i += 10) // 🔥 menos spawn
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

            if (VillageSystem.IsReserved(GetWorldSeed(), worldPos, 2f))
                continue;

            // 🚫 zona ao redor
            if (player != null && Vector3.Distance(worldPos, player.position) < safeRadius)
                continue;

            // 🚫 na frente do player
            if (player != null && Vector3.Distance(worldPos, player.position) < forwardSafeDistance && IsInPlayerPath(worldPos))
                continue;

            if (normal.y < 0.85f)
                continue;

            Vector2 point = new Vector2(pos.x + offset.x, pos.z + offset.y);
            BiomeType biome = GetBiome(point);

            if (IsRiverZone(point, 1.5f))
                continue;

            float cluster = Mathf.PerlinNoise(point.x * 0.05f, point.y * 0.05f);



            if (biome == BiomeType.Forest && cluster < 0.5f)
                continue;

            float density = treeDensity;

            switch (biome)
            {
                case BiomeType.Desert:
                    density = 0.0003f; // 🔥 quase vazio
                    break;

                case BiomeType.Forest:
                    density = treeDensity;
                    break;

                case BiomeType.Snow:
                    density = treeDensity * 0.65f;
                    break;
            }


            // 🌲 ÁRVORES
            if (treeCount < maxTreesPerChunk && rng.Value() < density)
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

                    if (IsTooClose(groundPoint, minTreeDistance))
                        continue;

                    GameObject tree = Instantiate(
                        selected.prefab,
                        groundPoint,
                        Quaternion.Euler(0f, rng.Range(0f, 360f), 0f),
                        transform
                    );

                    AlignObjectBaseToGround(tree, groundPoint, selected.yOffset);

                    usedPositions.Add(groundPoint);
                    treeCount++;
                }
            }

            // 🍄
            if (biome == BiomeType.Forest && rng.Value() < mushroomDensity) // chance do cluster
            {
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

                    // 🚫 evita sobreposição
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
    }

    bool IsTooClose(Vector3 pos, float minDistance = -1f)
    {
        float distanceLimit = minDistance >= 0f ? minDistance : minDistanceBetweenObjects;

        foreach (var p in usedPositions)
        {
            if (Vector3.Distance(p, pos) < distanceLimit)
                return true;
        }
        return false;
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

                if (VillageSystem.IsReserved(GetWorldSeed(), spawnPos, 3f))
                    continue;

                if (IsRiverZone(new Vector2(spawnPos.x, spawnPos.z), 2f))
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

            if (VillageSystem.IsReserved(GetWorldSeed(), worldPos, 4f))
                continue;

            if (player != null && Vector3.Distance(worldPos, player.position) < safeRadius + 8f)
                continue;

            if (mesh.normals[i].y < 0.92f)
                continue;

            Vector2 point = new Vector2(localPos.x + offset.x, localPos.z + offset.y);
            if (GetBiome(point) != BiomeType.Forest)
                continue;

            if (IsRiverZone(point, 3f))
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
