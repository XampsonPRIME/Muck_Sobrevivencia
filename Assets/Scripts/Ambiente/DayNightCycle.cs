using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    public TextMeshProUGUI warningText;
    public Material skyboxMaterial;
    public Light sun;
    public Light moon;
    public float dayDuration = 120f;
    [Range(0f, 23.99f)] public float startHour = 8f;
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI hourText;
    public ParticleSystem stars;

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

        // 🌞🌙 Rotação
        sun.transform.rotation = Quaternion.Euler(sunAngle - 90f, 170f, 0);
        moon.transform.rotation = Quaternion.Euler(sunAngle + 90f, 170f, 0);

        // 💡 LUZ (corrigido)
        sun.intensity = Mathf.Lerp(0.1f, 1.2f, sunIntensity);
        sun.color = Color.Lerp(new Color(1f, 0.5f, 0.3f), Color.white, sunIntensity);

        moon.intensity = Mathf.Lerp(0.2f, 0.5f, moonIntensity);
        moon.color = new Color(0.6f, 0.7f, 1f);

        // 🌍 AMBIENT LIGHT (corrigido - nunca preto!)
        Color dayAmbient = new Color(1f, 0.95f, 0.8f);
        Color nightAmbient = new Color(0.1f, 0.15f, 0.3f);
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, sunIntensity);

        // 🌫️ FOG DINÂMICO (ESSENCIAL)
        Color dayFog = new Color(0.93f, 0.85f, 0.6f);
        Color nightFog = new Color(0.05f, 0.08f, 0.15f);

        RenderSettings.fogColor = Color.Lerp(nightFog, dayFog, sunIntensity);
        RenderSettings.fogDensity = Mathf.Lerp(0.015f, 0.002f, sunIntensity);

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
            MessageSystem.Instance.ShowMessage("Está anoitecendo! Prepare-se para os perigos da noite.");
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
