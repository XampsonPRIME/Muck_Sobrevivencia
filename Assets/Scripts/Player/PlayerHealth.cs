using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Vida")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Estado")]
    public bool isDead = false;

    [Header("Regeneração")]
    public float regenRate = 0.5f; // vida por segundo
    public float regenDelay = 5f; // tempo sem levar dano

    private float lastDamageTime;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isDead) return;

        // 🔥 só regenera se passou o tempo sem dano
        if (Time.time >= lastDamageTime + regenDelay)
        {
            RegenerateHealth();
        }
    }

    void RegenerateHealth()
    {
        if (currentHealth >= maxHealth) return;

        currentHealth += regenRate * Time.deltaTime;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }

    // 🔥 RECEBER DANO
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;

        // 🔥 marca último dano
        lastDamageTime = Time.time;

        Debug.Log("❤️ Vida atual: " + currentHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    // 🔥 NORMALIZADO (pra UI)
    public float GetHealthNormalized()
    {
        return currentHealth / maxHealth;
    }

    // 💀 MORTE
    void Die()
    {
        if (isDead) return;

        isDead = true;

        // 🔥 desativa movimento
        PlayerMovement pm = GetComponent<PlayerMovement>();
        if (pm != null) pm.enabled = false;

        // 🔥 desativa ataque
        PlayerAttack pa = GetComponent<PlayerAttack>();
        if (pa != null) pa.enabled = false;

        // 🔥 inicia animação
        StartCoroutine(DeathEffect());
    }

    // 🎥 ANIMAÇÃO DE MORTE
    IEnumerator DeathEffect()
    {
        Camera camObj = Camera.main;

        if (camObj == null)
        {
            Debug.LogError("❌ Camera não encontrada!");
            yield break;
        }

        Transform cam = camObj.transform;

        float duration = 1.2f;
        float time = 0;

        Vector3 startPos = cam.position;
        Quaternion startRot = cam.rotation;

        // 🔥 posição final fixa
        Vector3 endPos = startPos + new Vector3(0, -0.8f, 0);

        // 🔥 rotação tipo "caiu"
        Quaternion endRot = Quaternion.Euler(90f, cam.eulerAngles.y, 20f);

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;

            cam.position = Vector3.Lerp(startPos, endPos, t);
            cam.rotation = Quaternion.Slerp(startRot, endRot, t);

            yield return null;
        }

        // 🔥 garante final correto
        cam.position = endPos;
        cam.rotation = endRot;


    }
}