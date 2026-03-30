using UnityEngine;

public class EnemyCombatAudio : MonoBehaviour
{
    public AudioSource audioSource;

    [Header("Attack Sounds")]
    public AudioClip[] attackClips;

    public float pitchVariation = 0.1f;
    public float volume = 0.5f;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void PlayAttackSound()
    {
        if (attackClips.Length == 0 || audioSource == null) return;

        AudioClip clip = attackClips[Random.Range(0, attackClips.Length)];

        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(clip, volume);
    }
}