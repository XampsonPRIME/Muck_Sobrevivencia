using UnityEngine;
using UnityEngine.SceneManagement;

public static class DemoWorldProgression
{
    public const float FreshSpawnMinRadiusFromWorldCenter = 900f;
    public const float FreshSpawnMaxRadiusFromWorldCenter = 1350f;
    public const float FreshSpawnMinDistanceFromVillage = 780f;
    public const float FreshSpawnMinDistanceFromLegacyBoss = 820f;
    public const float FreshSpawnHeight = 20f;
    public const float FinalBossUnlockDistanceFromSpawn = 1800f;
    public const float FinalBossMinDistanceFromSpawn = 1900f;
    public const float FinalBossPreferredDistanceFromPlayer = 155f;
    public const float SecondaryBossMinDistanceFromPlayer = 115f;
    public const float VillageDistanceFromFreshSpawn = 1250f;
    public const float BossDistanceFromFreshSpawn = 2700f;
    public const float VillageLateralSpread = 220f;
    public const float BossLateralSpread = 360f;
    public const float PortalDistanceFromBoss = 18f;
    public const float FinalBossLandmarkRevealDistance = 520f;
    public const float FinalBossActivationDistance = 360f;
    public const float VillageHeight = 10.17f;
    public const float BossHeight = 11.9787f;
    public static readonly Vector3 VillageAnchor = new Vector3(119.97627f, 0f, -10f);
    public static readonly Vector3 LegacyBossAnchor = new Vector3(-181.58424f, 0f, 113.626854f);
    public static readonly Vector3 LegacyPlayerStartAnchor = new Vector3(100f, 0f, 100f);
    public static readonly Vector3 FallbackFreshSpawn = new Vector3(900f, FreshSpawnHeight, -650f);

    static bool fallbackSeedInitialized;
    static int fallbackSeed;
    static int cachedSpawnSeed;
    static bool hasCachedSpawn;
    static Vector3 cachedSpawn;

    public static Vector3 ResolveFreshPlayerSpawn()
    {
        int seed = ResolveWorldSeed();
        if (hasCachedSpawn && cachedSpawnSeed == seed)
            return cachedSpawn;

        cachedSpawnSeed = seed;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            int saltOffset = attempt * 97;
            float angle = HashToUnit(seed, 0x51504157 + saltOffset) * Mathf.PI * 2f;
            float radius = Mathf.Lerp(
                FreshSpawnMinRadiusFromWorldCenter,
                FreshSpawnMaxRadiusFromWorldCenter,
                HashToUnit(seed, 0x51504158 + saltOffset));

            Vector3 candidate = new Vector3(
                Mathf.Cos(angle) * radius,
                FreshSpawnHeight,
                Mathf.Sin(angle) * radius);

            if (IsValidFreshSpawnCandidate(candidate))
            {
                cachedSpawn = candidate;
                hasCachedSpawn = true;
                return cachedSpawn;
            }
        }

        cachedSpawn = FallbackFreshSpawn;
        hasCachedSpawn = true;
        return cachedSpawn;
    }

    public static Quaternion ResolveFreshPlayerRotation()
    {
        Vector3 spawn = ResolveFreshPlayerSpawn();
        Vector3 lookTarget = ResolveVillagePosition();
        Vector3 lookDirection = new Vector3(lookTarget.x - spawn.x, 0f, lookTarget.z - spawn.z);
        if (lookDirection.sqrMagnitude < 0.001f)
            lookDirection = Vector3.forward;

        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    public static Vector3 ResolveVillagePosition()
    {
        Vector3 spawn = ResolveFreshPlayerSpawn();
        Vector2 direction = ResolveProgressDirection();
        Vector2 lateral = new Vector2(-direction.y, direction.x);
        float offset = Mathf.Lerp(-VillageLateralSpread, VillageLateralSpread, HashToUnit(ResolveWorldSeed(), 0x56494C41));
        return new Vector3(
            spawn.x + direction.x * VillageDistanceFromFreshSpawn + lateral.x * offset,
            VillageHeight,
            spawn.z + direction.y * VillageDistanceFromFreshSpawn + lateral.y * offset);
    }

    public static Vector3 ResolveBossLandmarkPosition()
    {
        Vector3 spawn = ResolveFreshPlayerSpawn();
        Vector2 direction = ResolveProgressDirection();
        Vector2 lateral = new Vector2(-direction.y, direction.x);
        float offset = Mathf.Lerp(-BossLateralSpread, BossLateralSpread, HashToUnit(ResolveWorldSeed(), 0x424F5353));
        return new Vector3(
            spawn.x + direction.x * BossDistanceFromFreshSpawn + lateral.x * offset,
            BossHeight,
            spawn.z + direction.y * BossDistanceFromFreshSpawn + lateral.y * offset);
    }

    public static Vector3 ResolvePortalLandmarkPosition()
    {
        Vector3 boss = ResolveBossLandmarkPosition();
        Vector2 direction = ResolveProgressDirection();
        Vector2 lateral = new Vector2(-direction.y, direction.x);
        return new Vector3(
            boss.x + lateral.x * PortalDistanceFromBoss,
            BossHeight,
            boss.z + lateral.y * PortalDistanceFromBoss);
    }

    public static void ApplyLandmarkLayout(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        MoveRoot(scene, "Village", ResolveVillagePosition(), Quaternion.identity);
        MoveRoot(scene, "BossSpawnPoint", ResolveBossLandmarkPosition(), ResolveLookAtFreshSpawnRotation(ResolveBossLandmarkPosition()));
        MoveRoot(scene, "PortalForestEnchanted", ResolvePortalLandmarkPosition(), ResolveLookAtFreshSpawnRotation(ResolvePortalLandmarkPosition()));
        SetRootActive(scene, "PortalForestEnchanted", false);
    }

    public static void UpdateLandmarkVisibility(Scene scene, Vector3 playerPosition)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        SetRootActive(scene, "PortalForestEnchanted", ShouldRevealFinalBossLandmark(playerPosition));
    }

    public static bool ShouldRevealFinalBossLandmark(Vector3 playerPosition)
    {
        return HorizontalDistance(playerPosition, ResolveBossLandmarkPosition()) <= FinalBossLandmarkRevealDistance;
    }

    public static bool ShouldActivateFinalBossLandmark(Vector3 playerPosition)
    {
        return HasReachedFinalBossRegion(playerPosition) &&
               HorizontalDistance(playerPosition, ResolveBossLandmarkPosition()) <= FinalBossActivationDistance;
    }

    public static bool HasReachedFinalBossRegion(Vector3 playerPosition)
    {
        return HorizontalDistance(playerPosition, ResolveFreshPlayerSpawn()) >= FinalBossUnlockDistanceFromSpawn;
    }

    public static bool IsFarEnoughForFinalBoss(Vector3 candidatePosition)
    {
        return IsFarEnoughForFinalBoss(candidatePosition, FinalBossMinDistanceFromSpawn);
    }

    public static bool IsFarEnoughForFinalBoss(Vector3 candidatePosition, float minDistanceFromSpawn)
    {
        return HorizontalDistance(candidatePosition, ResolveFreshPlayerSpawn()) >= Mathf.Max(0f, minDistanceFromSpawn);
    }

    public static bool IsNearKnownLegacyStart(Vector3 position, float villageRadius, float bossRadius, float playerStartRadius)
    {
        return HorizontalDistance(position, VillageAnchor) <= Mathf.Max(0f, villageRadius) ||
               HorizontalDistance(position, LegacyBossAnchor) <= Mathf.Max(0f, bossRadius) ||
               HorizontalDistance(position, LegacyPlayerStartAnchor) <= Mathf.Max(0f, playerStartRadius);
    }

    public static bool IsNearCurrentProgressLandmarks(Vector3 position, float villageRadius, float bossRadius)
    {
        return HorizontalDistance(position, ResolveVillagePosition()) <= Mathf.Max(0f, villageRadius) ||
               HorizontalDistance(position, ResolveBossLandmarkPosition()) <= Mathf.Max(0f, bossRadius);
    }

    public static float ResolveArenaSearchAngle(Vector3 playerPosition, int salt)
    {
        int playerChunkX = Mathf.RoundToInt(playerPosition.x / 50f);
        int playerChunkZ = Mathf.RoundToInt(playerPosition.z / 50f);
        return HashToUnit(ResolveWorldSeed(), salt, playerChunkX, playerChunkZ) * 360f;
    }

    public static int ResolveWorldSeed()
    {
        LanMultiplayerManager manager = LanMultiplayerManager.Instance ?? Object.FindFirstObjectByType<LanMultiplayerManager>();
        if (manager != null && manager.WorldSeed != 0)
            return manager.WorldSeed;

        if (!fallbackSeedInitialized)
        {
            fallbackSeed = System.Environment.TickCount;
            fallbackSeedInitialized = true;
        }

        return fallbackSeed;
    }

    static Vector2 ResolveProgressDirection()
    {
        float angle = HashToUnit(ResolveWorldSeed(), 0x5452494C) * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }

    static Quaternion ResolveLookAtFreshSpawnRotation(Vector3 position)
    {
        Vector3 spawn = ResolveFreshPlayerSpawn();
        Vector3 lookDirection = new Vector3(spawn.x - position.x, 0f, spawn.z - position.z);
        if (lookDirection.sqrMagnitude < 0.001f)
            lookDirection = Vector3.forward;

        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    static void MoveRoot(Scene scene, string rootName, Vector3 position, Quaternion rotation)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name != rootName)
                continue;

            root.transform.SetPositionAndRotation(position, rotation);
            if (rootName == "Village")
            {
                VillageCraftingSetup village = root.GetComponent<VillageCraftingSetup>();
                if (village != null)
                    village.RequestGroundAlign();
            }

            return;
        }
    }

    static void SetRootActive(Scene scene, string rootName, bool active)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name != rootName)
                continue;

            if (root.activeSelf != active)
                root.SetActive(active);
            return;
        }
    }

    static bool IsValidFreshSpawnCandidate(Vector3 candidate)
    {
        return HorizontalDistance(candidate, VillageAnchor) >= FreshSpawnMinDistanceFromVillage &&
               HorizontalDistance(candidate, LegacyBossAnchor) >= FreshSpawnMinDistanceFromLegacyBoss;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector2 planarA = new Vector2(a.x, a.z);
        Vector2 planarB = new Vector2(b.x, b.z);
        return Vector2.Distance(planarA, planarB);
    }

    static float HashToUnit(int seed, int salt)
    {
        return HashToUnit(seed, salt, 0, 0);
    }

    static float HashToUnit(int seed, int salt, int x, int z)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + seed;
            hash = hash * 31 + salt;
            hash = hash * 31 + x;
            hash = hash * 31 + z;
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            return (hash & 0x7fffffff) / (float)int.MaxValue;
        }
    }
}
