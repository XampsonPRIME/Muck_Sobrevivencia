using System;
using System.Collections.Generic;
using UnityEngine;

public static class VillageSystem
{
    public class VillageSite
    {
        public string siteId;
        public VillageDefinition definition;
        public Vector2 centerXZ;
        public float rotationY;
        public float reservationRadius;
        public float flattenRadius;
        public float flattenBlendRadius;
        public bool hasFlattenHeight;
        public float flattenHeight;
    }

    static readonly List<VillageSite> sites = new List<VillageSite>();
    static int initializedSeed = int.MinValue;

    public static IReadOnlyList<VillageSite> GetSites(int worldSeed)
    {
        EnsureInitialized(worldSeed);
        return sites;
    }

    public static void EnsureInitialized(int worldSeed)
    {
        if (initializedSeed == worldSeed && sites.Count > 0)
            return;

        initializedSeed = worldSeed;
        sites.Clear();

        VillageDefinition[] definitions = Resources.LoadAll<VillageDefinition>("Villages");
        Array.Sort(definitions, (a, b) => string.Compare(GetVillageKey(a), GetVillageKey(b), StringComparison.OrdinalIgnoreCase));

        if (definitions.Length == 0)
        {
            sites.Add(CreateFallbackSite(worldSeed, 0));
            return;
        }

        for (int i = 0; i < definitions.Length; i++)
            sites.Add(CreateSiteFromDefinition(definitions[i], worldSeed, i));
    }

    public static void PrepareVillageHeightsForChunk(int worldSeed, Vector2 chunkOrigin, int chunkSize, Func<Vector2, float> baseHeightSampler)
    {
        EnsureInitialized(worldSeed);

        if (baseHeightSampler == null)
            return;

        float chunkMinX = chunkOrigin.x;
        float chunkMaxX = chunkOrigin.x + chunkSize;
        float chunkMinZ = chunkOrigin.y;
        float chunkMaxZ = chunkOrigin.y + chunkSize;

        for (int i = 0; i < sites.Count; i++)
        {
            VillageSite site = sites[i];
            if (site == null || site.hasFlattenHeight)
                continue;

            float influenceRadius = Mathf.Max(site.reservationRadius, site.flattenRadius + site.flattenBlendRadius);
            if (!IntersectsChunk(site.centerXZ, influenceRadius, chunkMinX, chunkMaxX, chunkMinZ, chunkMaxZ))
                continue;

            site.flattenHeight = baseHeightSampler(site.centerXZ);
            site.hasFlattenHeight = true;
        }
    }

    public static float ApplyVillageFlatten(int worldSeed, Vector2 worldPoint, float baseHeight)
    {
        EnsureInitialized(worldSeed);

        float finalHeight = baseHeight;
        float strongestInfluence = 0f;

        for (int i = 0; i < sites.Count; i++)
        {
            VillageSite site = sites[i];
            if (site == null || !site.hasFlattenHeight)
                continue;

            float distance = Vector2.Distance(worldPoint, site.centerXZ);
            float influence = GetFlattenInfluence(site, distance);
            if (influence <= 0f || influence < strongestInfluence)
                continue;

            finalHeight = Mathf.Lerp(baseHeight, site.flattenHeight, influence);
            strongestInfluence = influence;
        }

        return finalHeight;
    }

    public static bool IsReserved(int worldSeed, Vector3 worldPosition, float extraMargin = 0f)
    {
        EnsureInitialized(worldSeed);

        Vector2 point = new Vector2(worldPosition.x, worldPosition.z);
        for (int i = 0; i < sites.Count; i++)
        {
            VillageSite site = sites[i];
            if (site == null)
                continue;

            float reservation = Mathf.Max(0f, site.reservationRadius + extraMargin);
            if (Vector2.Distance(point, site.centerXZ) <= reservation)
                return true;
        }

        return false;
    }

    static VillageSite CreateSiteFromDefinition(VillageDefinition definition, int worldSeed, int index)
    {
        Vector2 position = GenerateVillagePosition(worldSeed, index);

        return new VillageSite
        {
            siteId = $"{GetVillageKey(definition)}_{index}",
            definition = definition,
            centerXZ = position,
            rotationY = GenerateVillageRotation(worldSeed, index),
            reservationRadius = Mathf.Max(definition != null ? definition.reservationRadius : 26f, 8f),
            flattenRadius = Mathf.Max(definition != null ? definition.flattenRadius : 18f, 6f),
            flattenBlendRadius = Mathf.Max(definition != null ? definition.flattenBlendRadius : 10f, 0f)
        };
    }

    static VillageSite CreateFallbackSite(int worldSeed, int index)
    {
        return new VillageSite
        {
            siteId = $"fallback_village_{index}",
            definition = null,
            centerXZ = GenerateVillagePosition(worldSeed, index),
            rotationY = GenerateVillageRotation(worldSeed, index),
            reservationRadius = 26f,
            flattenRadius = 18f,
            flattenBlendRadius = 10f
        };
    }

    static Vector2 GenerateVillagePosition(int worldSeed, int index)
    {
        unchecked
        {
            System.Random random = new System.Random(worldSeed + (index + 1) * 811);
            float side = random.NextDouble() > 0.5 ? 1f : -1f;
            float x = side * (92f + index * 85f + (float)random.NextDouble() * 24f);
            float z = ((float)random.NextDouble() * 120f) - 60f;
            z += index * 36f * (random.NextDouble() > 0.5 ? 1f : -1f);
            return new Vector2(x, z);
        }
    }

    static float GenerateVillageRotation(int worldSeed, int index)
    {
        unchecked
        {
            System.Random random = new System.Random(worldSeed ^ ((index + 1) * 1597));
            return Mathf.Round((float)random.NextDouble() * 4f) * 90f;
        }
    }

    static float GetFlattenInfluence(VillageSite site, float distance)
    {
        if (site == null)
            return 0f;

        if (distance <= site.flattenRadius)
            return 1f;

        float blendRadius = Mathf.Max(0.001f, site.flattenBlendRadius);
        float outerRadius = site.flattenRadius + blendRadius;
        if (distance >= outerRadius)
            return 0f;

        float t = Mathf.InverseLerp(outerRadius, site.flattenRadius, distance);
        return Mathf.Clamp01(t);
    }

    static bool IntersectsChunk(Vector2 center, float radius, float minX, float maxX, float minZ, float maxZ)
    {
        float closestX = Mathf.Clamp(center.x, minX, maxX);
        float closestZ = Mathf.Clamp(center.y, minZ, maxZ);
        float distanceX = center.x - closestX;
        float distanceZ = center.y - closestZ;
        return distanceX * distanceX + distanceZ * distanceZ <= radius * radius;
    }

    static string GetVillageKey(VillageDefinition definition)
    {
        if (definition == null)
            return "village";

        if (!string.IsNullOrWhiteSpace(definition.villageId))
            return definition.villageId.Trim();

        return definition.name;
    }
}
