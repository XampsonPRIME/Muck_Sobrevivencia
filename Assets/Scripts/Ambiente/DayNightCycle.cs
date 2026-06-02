using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System;

public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }
    public static event Action<int> DayStarted;

    public TextMeshProUGUI warningText;
    public Material skyboxMaterial;
    public Light sun;
    public Light moon;
    public float dayDuration = 120f;
    [Range(0f, 23.99f)] public float startHour = 8f;
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI hourText;
    public ParticleSystem stars;
    ParticleSystem enchantedForestDust;
    ParticleSystem enchantedForestFireflies;

    public Image darkOverlay;

    float overlayAlpha = 0f;
    float targetAlpha = 0f;
    public float overlaySpeed = 1f;

    int currentDay = 1;
    float timeOfDay;

    float fadeDuration = 1f;
    float displayDuration = 2f;

    float fadeTimer = 0f;
    bool isFading = false;

    bool warnedNight = false;

    public int CurrentHour
    {
        get
        {
            float totalHours = timeOfDay * 24f;
            return Mathf.FloorToInt(totalHours);
        }
    }

    public int CurrentMinute
    {
        get
        {
            float totalHours = timeOfDay * 24f;
            int hours = Mathf.FloorToInt(totalHours);
            return Mathf.FloorToInt((totalHours - hours) * 60f);
        }
    }

    public int CurrentDay => currentDay;

    public float CurrentNormalizedTime => timeOfDay;

    public string CurrentTimeFormatted => string.Format("{0:00}:{1:00}", CurrentHour, CurrentMinute);

    public bool IsNight => CurrentHour >= 18 || CurrentHour < 6;

    void Start()
    {
        Instance = this;

        if (sun == null)
            sun = GameObject.Find("Sun").GetComponent<Light>();

        if (moon == null)
            moon = GameObject.Find("Moon").GetComponent<Light>();

        // 🌫️ garante que o fog está ativo
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;

        if (IsEnchantedForestScene())
        {
            EnsureEnchantedForestDust();
            EnsureEnchantedForestFireflies();
        }

        timeOfDay = Mathf.Repeat(startHour / 24f, 1f);
        ApplyCycleVisuals();
    }

    void Update()
    {
        timeOfDay += Time.deltaTime / dayDuration;

        if (timeOfDay >= 1f)
        {
            timeOfDay -= 1f;
            currentDay++;
            DayStarted?.Invoke(currentDay);
        }

        ApplyCycleVisuals();
    }

    void ApplyCycleVisuals()
    {
        if (sun == null || moon == null)
            return;

        float sunAngle = timeOfDay * 360f;

        float sunIntensity = Mathf.Clamp01(Mathf.Sin(timeOfDay * Mathf.PI));
        float moonIntensity = Mathf.Clamp01(Mathf.Sin((timeOfDay - 0.5f) * Mathf.PI));
        bool enchantedForest = IsEnchantedForestScene();

        // 🌞🌙 Rotação
        sun.transform.rotation = Quaternion.Euler(sunAngle - 90f, 170f, 0);
        moon.transform.rotation = Quaternion.Euler(sunAngle + 90f, 170f, 0);

        // 💡 LUZ (corrigido)
        Color dawnSunColor = enchantedForest ? new Color(0.86f, 0.62f, 0.95f) : new Color(1f, 0.5f, 0.3f);
        Color daySunColor = enchantedForest ? new Color(0.86f, 1f, 0.78f) : Color.white;
        sun.intensity = enchantedForest
            ? Mathf.Lerp(0.18f, 0.92f, sunIntensity)
            : Mathf.Lerp(0.1f, 1.2f, sunIntensity);
        sun.color = Color.Lerp(dawnSunColor, daySunColor, sunIntensity);

        moon.intensity = enchantedForest
            ? Mathf.Lerp(0.3f, 0.72f, moonIntensity)
            : Mathf.Lerp(0.2f, 0.5f, moonIntensity);
        moon.color = enchantedForest ? new Color(0.64f, 0.78f, 1f) : new Color(0.6f, 0.7f, 1f);

        // 🌍 AMBIENT LIGHT (corrigido - nunca preto!)
        Color dayAmbient = enchantedForest ? new Color(0.64f, 0.84f, 0.7f) : new Color(1f, 0.95f, 0.8f);
        Color nightAmbient = enchantedForest ? new Color(0.11f, 0.18f, 0.24f) : new Color(0.1f, 0.15f, 0.3f);
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, sunIntensity);

        // 🌫️ FOG DINÂMICO (ESSENCIAL)
        Color dayFog = enchantedForest ? new Color(0.58f, 0.8f, 0.72f) : new Color(0.93f, 0.85f, 0.6f);
        Color nightFog = enchantedForest ? new Color(0.08f, 0.14f, 0.18f) : new Color(0.05f, 0.08f, 0.15f);

        RenderSettings.fogColor = Color.Lerp(nightFog, dayFog, sunIntensity);
        RenderSettings.fogDensity = enchantedForest
            ? Mathf.Lerp(0.02f, 0.0065f, sunIntensity)
            : Mathf.Lerp(0.015f, 0.002f, sunIntensity);

        // 🌌 Skybox exposição (opcional mas MUITO bom)
        if (skyboxMaterial != null)
        {
            float exposure = Mathf.Lerp(0.3f, 1.3f, sunIntensity);
            skyboxMaterial.SetFloat("_Exposure", exposure);
        }

        // ⭐ estrelas
        if (stars != null)
        {
            float starVisibility = 1f - sunIntensity;
            var emission = stars.emission;
            emission.rateOverTime = starVisibility * 1000f;
        }

        if (enchantedForestDust != null)
        {
            var emission = enchantedForestDust.emission;
            emission.rateOverTime = Mathf.Lerp(16f, 28f, 1f - sunIntensity * 0.35f);

            var main = enchantedForestDust.main;
            Color dayDust = new Color(0.95f, 0.45f, 0.95f, 0.42f);
            Color nightDust = new Color(0.45f, 0.9f, 1f, 0.55f);
            main.startColor = Color.Lerp(dayDust, nightDust, 1f - sunIntensity);
        }

        if (enchantedForestFireflies != null)
        {
            var emission = enchantedForestFireflies.emission;
            emission.rateOverTime = Mathf.Lerp(10f, 24f, 1f - sunIntensity * 0.4f);

            var main = enchantedForestFireflies.main;
            Color dayGlow = new Color(1f, 0.78f, 0.98f, 0.38f);
            Color nightGlow = new Color(0.5f, 1f, 0.92f, 0.68f);
            main.startColor = Color.Lerp(dayGlow, nightGlow, 1f - sunIntensity);
        }

        // 🌑 overlay escuro
        if (darkOverlay != null)
        {
            targetAlpha = Mathf.Lerp(0f, 0.7f, 1f - sunIntensity);
            overlayAlpha = Mathf.Lerp(overlayAlpha, targetAlpha, Time.deltaTime * overlaySpeed);

            Color overlayColor = darkOverlay.color;
            overlayColor.a = overlayAlpha;
            darkOverlay.color = overlayColor;
        }

        // ⏰ horário
        float totalHours = timeOfDay * 24f;
        int hours = Mathf.FloorToInt(totalHours);
        int minutes = Mathf.FloorToInt((totalHours - hours) * 60f);

        if (dayText != null)
            dayText.text = "Dia " + currentDay;

        if (hourText != null)
            hourText.text = string.Format("{0:00}:{1:00}", hours, minutes);

        // 🌙 aviso de noite
        if (hours >= 18 && hours < 19 && !warnedNight)
        {
            MessageSystem.Instance?.ShowMessage("Está anoitecendo! Prepare-se para os perigos da noite.");
            warnedNight = true;
        }

        if (hours >= 6 && hours < 7)
        {
            warnedNight = false;
        }

        // 🎬 fade texto
        if (isFading && warningText != null)
        {
            fadeTimer += Time.deltaTime;

            Color color = warningText.color;

            if (fadeTimer < fadeDuration)
            {
                color.a = fadeTimer / fadeDuration;
            }
            else if (fadeTimer < fadeDuration + displayDuration)
            {
                color.a = 1f;
            }
            else if (fadeTimer < fadeDuration * 2 + displayDuration)
            {
                float t = (fadeTimer - fadeDuration - displayDuration) / fadeDuration;
                color.a = 1f - t;
            }
            else
            {
                color.a = 0f;
                isFading = false;
            }

            warningText.color = color;
        }
    }

    bool IsEnchantedForestScene()
    {
        string worldSceneName = SceneWorldDataResolver.ResolveWorldSceneName(gameObject.scene);
        return string.Equals(worldSceneName, "EnchantedForest", System.StringComparison.Ordinal);
    }

    void EnsureEnchantedForestDust()
    {
        Transform existing = transform.Find("EnchantedForestDust");
        if (existing != null)
        {
            enchantedForestDust = existing.GetComponent<ParticleSystem>();
            return;
        }

        GameObject dustObject = new GameObject("EnchantedForestDust");
        dustObject.transform.SetParent(transform, false);
        dustObject.transform.localPosition = new Vector3(0f, 12f, 0f);
        dustObject.transform.localRotation = Quaternion.identity;

        enchantedForestDust = dustObject.AddComponent<ParticleSystem>();
        var main = enchantedForestDust.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.maxParticles = 500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.9f, 0.5f, 0.95f, 0.4f);

        var emission = enchantedForestDust.emission;
        emission.rateOverTime = 22f;

        var shape = enchantedForestDust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(220f, 28f, 220f);

        var noise = enchantedForestDust.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.18f;
        noise.scrollSpeed = 0.1f;

        var velocityOverLifetime = enchantedForestDust.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        var colorOverLifetime = enchantedForestDust.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.5f, 0.95f), 0f),
                new GradientColorKey(new Color(0.55f, 0.95f, 1f), 0.55f),
                new GradientColorKey(new Color(0.95f, 1f, 0.75f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.45f, 0.15f),
                new GradientAlphaKey(0.3f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var renderer = enchantedForestDust.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;

        enchantedForestDust.Play();
    }

    void EnsureEnchantedForestFireflies()
    {
        Transform existing = transform.Find("EnchantedForestFireflies");
        if (existing != null)
        {
            enchantedForestFireflies = existing.GetComponent<ParticleSystem>();
            return;
        }

        GameObject firefliesObject = new GameObject("EnchantedForestFireflies");
        firefliesObject.transform.SetParent(transform, false);
        firefliesObject.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        firefliesObject.transform.localRotation = Quaternion.identity;

        enchantedForestFireflies = firefliesObject.AddComponent<ParticleSystem>();
        var main = enchantedForestFireflies.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.maxParticles = 320;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(1f, 0.78f, 0.98f, 0.45f);

        var emission = enchantedForestFireflies.emission;
        emission.rateOverTime = 14f;

        var shape = enchantedForestFireflies.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(180f, 8f, 180f);

        var noise = enchantedForestFireflies.noise;
        noise.enabled = true;
        noise.strength = 0.28f;
        noise.frequency = 0.32f;
        noise.scrollSpeed = 0.14f;

        var velocityOverLifetime = enchantedForestFireflies.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);

        var colorOverLifetime = enchantedForestFireflies.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.72f, 0.94f), 0f),
                new GradientColorKey(new Color(0.58f, 1f, 0.92f), 0.5f),
                new GradientColorKey(new Color(0.98f, 0.98f, 0.72f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.2f),
                new GradientAlphaKey(0.6f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = enchantedForestFireflies.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.25f, 1f),
            new Keyframe(0.75f, 1f),
            new Keyframe(1f, 0.2f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = enchantedForestFireflies.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;

        enchantedForestFireflies.Play();
    }

    void ShowWarning(string message)
    {
        warningText.text = message;
        fadeTimer = 0f;
        isFading = true;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void LoadState(int savedDay, float savedNormalizedTime)
    {
        currentDay = Mathf.Max(1, savedDay);
        timeOfDay = Mathf.Repeat(savedNormalizedTime, 1f);
        warnedNight = IsNight;
        ApplyCycleVisuals();
    }
}
