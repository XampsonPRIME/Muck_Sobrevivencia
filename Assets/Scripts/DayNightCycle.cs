using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DayNightCycle : MonoBehaviour
{
    public TextMeshProUGUI warningText;
    public Material skyboxMaterial;
    public Light sun;
    public Light moon;
    public float dayDuration = 120f;
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI hourText;
    public ParticleSystem stars;

    public Image darkOverlay;

    float overlayAlpha = 0f;
    float targetAlpha = 0f;
    public float overlaySpeed = 1f;

    int currentDay = 1;
    float warningTimer = 0f;
    float timeOfDay;

    float fadeDuration = 1f;
    float displayDuration = 2f;

    float fadeTimer = 0f;
    bool isFading = false;

    void Start()
    {
        if (sun == null)
            sun = GameObject.Find("Sun").GetComponent<Light>();

        if (moon == null)
            moon = GameObject.Find("Moon").GetComponent<Light>();
    }

    bool warnedNight = false;

    void Update()
    {
        timeOfDay += Time.deltaTime / dayDuration;

        if (timeOfDay >= 1f)
        {
            timeOfDay = 0f;
            currentDay++;
        }

        float sunAngle = timeOfDay * 360f;

        float sunIntensity = Mathf.Clamp01(Mathf.Sin(timeOfDay * Mathf.PI));
        float moonIntensity = Mathf.Clamp01(Mathf.Sin((timeOfDay - 0.5f) * Mathf.PI));

        // Rotação
        sun.transform.rotation = Quaternion.Euler(sunAngle - 90f, 170f, 0);
        moon.transform.rotation = Quaternion.Euler(sunAngle + 90f, 170f, 0);

        // Luz
        sun.intensity = sunIntensity;
        moon.intensity = moonIntensity * 0.3f;

        RenderSettings.ambientLight = Color.Lerp(Color.black, Color.white, sunIntensity);

        // ⭐ estrelas
        float starVisibility = 1f - sunIntensity;
        var emission = stars.emission;
        emission.rateOverTime = starVisibility * 1000f;

        // quanto mais noite, mais escuro
        targetAlpha = Mathf.Lerp(0f, 0.7f, 1f - sunIntensity);

        // suavizar transição
        overlayAlpha = Mathf.Lerp(overlayAlpha, targetAlpha, Time.deltaTime * overlaySpeed);

        // aplicar
        Color overlayColor = darkOverlay.color;
        overlayColor.a = overlayAlpha;
        darkOverlay.color = overlayColor;

        // ⏰ horário
        float totalHours = timeOfDay * 24f;
        int hours = Mathf.FloorToInt(totalHours);
        int minutes = Mathf.FloorToInt((totalHours - hours) * 60f);

        dayText.text = "Dia " + currentDay;
        hourText.text = string.Format("{0:00}:{1:00}", hours, minutes);

        // 🌙 AVISO DE NOITE (CORRIGIDO)
        if (hours >= 18 && hours < 19 && !warnedNight)
        {
            ShowWarning("Está anoitecendo...");
            warnedNight = true;
        }

        if (hours >= 6 && hours < 7)
        {
            warnedNight = false;
        }

        // 🎬 FADE DO TEXTO
        if (isFading)
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
}