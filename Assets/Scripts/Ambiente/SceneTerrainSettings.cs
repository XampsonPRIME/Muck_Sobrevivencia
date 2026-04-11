using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneBiomeOverride
{
    Default,
    ForestOnly,
    DesertOnly,
    SnowOnly
}

[ExecuteAlways]
public class SceneTerrainSettings : MonoBehaviour
{
    const string AmbientParticlesObjectName = "AmbientParticles";



    [Header("Terreno Encantado")]
    public Material enchantedTerrainMaterial;
    public WorldHeightmapData worldHeightmap;
    public RoadMaskData roadMask;
    public SceneBiomeOverride biomeOverride = SceneBiomeOverride.Default;
    [Header("Vegetacao")]
    public float treeDensityMultiplier = 1f;
    public int maxTreesPerChunkOverride = 0;
    public float minTreeDistanceOverride = 0f;
    public float mushroomDensityMultiplier = 1f;
    public bool preferMagicForestTrees;
    [Range(0f, 1f)] public float magicForestTreeChance = 0.75f;
    public GameObject decorativeMushroomSinglePrefab;
    public GameObject decorativeMushroomClusterPrefab;
    [Range(0f, 1f)] public float decorativeMushroomClusterChance = 0.35f;
    public Vector2 decorativeMushroomScaleRange = new Vector2(0.85f, 1.2f);
    public float decorativeMushroomMinDistance = 2.4f;
    public float decorativeMushroomYOffset = 0.03f;
    public Color decorativeMushroomTintA = new Color(0.92f, 0.97f, 0.62f, 1f);
    public Color decorativeMushroomTintB = new Color(0.74f, 0.88f, 0.54f, 1f);
    [Range(0f, 1f)] public float decorativeMushroomTintStrength = 0.6f;

    [Header("Atmosfera")]
    public bool applySceneAtmosphere = true;
    public Color fogColor = new Color(0.24f, 0.32f, 0.34f, 1f);
    public float fogDensity = 0.028f;
    public Color ambientSkyColor = new Color(0.18f, 0.24f, 0.22f, 1f);
    public Color ambientEquatorColor = new Color(0.10f, 0.14f, 0.11f, 1f);
    public Color ambientGroundColor = new Color(0.05f, 0.07f, 0.06f, 1f);
    public Color dayFogColor = new Color(0.48f, 0.67f, 0.58f, 1f);
    public Color nightFogColor = new Color(0.08f, 0.13f, 0.16f, 1f);
    public float dayFogDensity = 0.008f;
    public float nightFogDensity = 0.035f;
    public Color dayAmbientLight = new Color(0.58f, 0.72f, 0.62f, 1f);
    public Color nightAmbientLight = new Color(0.10f, 0.16f, 0.18f, 1f);
    public Color sunDayColor = new Color(0.86f, 0.98f, 0.84f, 1f);
    public Color sunDawnDuskColor = new Color(0.56f, 0.88f, 0.66f, 1f);
    public float minSunIntensity = 0.08f;
    public float maxSunIntensity = 0.92f;
    public Color moonLightColor = new Color(0.46f, 0.78f, 0.72f, 1f);
    public float minMoonIntensity = 0.12f;
    public float maxMoonIntensity = 0.36f;

    [Header("Particulas")]
    public bool enableAmbientParticles;
    public bool ambientParticlesFollowCamera = true;
    public Vector3 ambientParticleOffset = new Vector3(0f, 10f, 0f);
    public Vector3 ambientParticleArea = new Vector3(80f, 28f, 80f);
    [Range(0f, 200f)] public float ambientParticleRate = 22f;
    [Min(1)] public int ambientParticleMaxParticles = 240;
    public Vector2 ambientParticleLifetimeRange = new Vector2(8f, 15f);
    public Vector2 ambientParticleSizeRange = new Vector2(0.12f, 0.3f);
    public Vector2 ambientParticleSpeedRange = new Vector2(0.08f, 0.26f);
    public Color ambientParticleColorA = new Color(0.45f, 1f, 0.78f, 0.18f);
    public Color ambientParticleColorB = new Color(0.80f, 0.55f, 1f, 0.12f);

    ParticleSystem ambientParticles;
    Transform ambientParticlesTransform;

    void OnEnable()
    {
        ApplyAtmosphere();
        RefreshAmbientParticles();
    }

    void OnValidate()
    {
        ApplyAtmosphere();
        RefreshAmbientParticles();
    }

    void Update()
    {
        if (!enableAmbientParticles)
            return;

        if (!IsSceneContextActive())
            return;

        RefreshAmbientParticles();
    }

    void ApplyAtmosphere()
    {
        if (!applySceneAtmosphere)
            return;

        if (!IsSceneContextActive())
            return;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = Mathf.Max(0f, fogDensity);
        RenderSettings.ambientSkyColor = ambientSkyColor;
        RenderSettings.ambientEquatorColor = ambientEquatorColor;
        RenderSettings.ambientGroundColor = ambientGroundColor;
    }

    void RefreshAmbientParticles()
    {
        if (!enableAmbientParticles)
        {
            DisableAmbientParticles();
            return;
        }

        if (!IsSceneContextActive())
            return;

        EnsureAmbientParticles();
        ConfigureAmbientParticles();
        UpdateAmbientParticleTransform();

        if (ambientParticles != null && !ambientParticles.isPlaying)
            ambientParticles.Play();
    }

    void DisableAmbientParticles()
    {
        if (ambientParticles == null)
            return;

        ambientParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (ambientParticles.gameObject.activeSelf)
            ambientParticles.gameObject.SetActive(false);
    }

    void EnsureAmbientParticles()
    {
        if (ambientParticles != null)
        {
            ambientParticlesTransform = ambientParticles.transform;
            if (!ambientParticles.gameObject.activeSelf)
                ambientParticles.gameObject.SetActive(true);
            return;
        }

        Transform child = transform.Find(AmbientParticlesObjectName);
        if (child == null)
        {
            GameObject childObject = new GameObject(AmbientParticlesObjectName);
            child = childObject.transform;
            child.SetParent(transform, false);
        }

        ambientParticlesTransform = child;
        ambientParticles = child.GetComponent<ParticleSystem>();
        if (ambientParticles == null)
            ambientParticles = child.gameObject.AddComponent<ParticleSystem>();

        if (child.GetComponent<ParticleSystemRenderer>() == null)
            child.gameObject.AddComponent<ParticleSystemRenderer>();

        child.gameObject.SetActive(true);
    }

    void ConfigureAmbientParticles()
    {
        if (ambientParticles == null)
            return;

        var main = ambientParticles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = Mathf.Max(1, ambientParticleMaxParticles);
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.1f, ambientParticleLifetimeRange.x),
            Mathf.Max(ambientParticleLifetimeRange.x, ambientParticleLifetimeRange.y)
        );
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0f, ambientParticleSpeedRange.x),
            Mathf.Max(ambientParticleSpeedRange.x, ambientParticleSpeedRange.y)
        );
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.01f, ambientParticleSizeRange.x),
            Mathf.Max(ambientParticleSizeRange.x, ambientParticleSizeRange.y)
        );
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor = new ParticleSystem.MinMaxGradient(ambientParticleColorA, ambientParticleColorB);

        var emission = ambientParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = ambientParticleRate;

        var shape = ambientParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = ambientParticleArea;

        var noise = ambientParticles.noise;
        noise.enabled = true;
        noise.strength = 0.24f;
        noise.frequency = 0.08f;
        noise.scrollSpeed = 0.05f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var colorOverLifetime = ambientParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient alphaGradient = new Gradient();
        alphaGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.18f),
                new GradientAlphaKey(0.75f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGradient);

        var rotationOverLifetime = ambientParticles.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        var renderer = ambientParticles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    void UpdateAmbientParticleTransform()
    {
        if (ambientParticlesTransform == null)
            return;

        Transform anchor = transform;
        if (ambientParticlesFollowCamera && Camera.main != null)
            anchor = Camera.main.transform;

        ambientParticlesTransform.position = anchor.position + ambientParticleOffset;
        ambientParticlesTransform.rotation = Quaternion.identity;
    }

    bool IsSceneContextActive()
    {
        if (!gameObject.scene.IsValid())
            return false;

        if (!Application.isPlaying)
            return SceneManager.GetActiveScene() == gameObject.scene;

        return SceneTerrainContext.GetGameplayWorldScene() == gameObject.scene;
    }
}
