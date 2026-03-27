using UnityEngine;

public class EnemyAudio : MonoBehaviour
{
    public AudioSource audioSource;

    [Header("Ambient Sounds")]
    public AudioClip[] ambientClips;

    [Header("Config")]
    public float minDelay = 4f;
    public float maxDelay = 10f;
    public float volume = 0.3f;
    public float pitchVariation = 0.1f;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogError("❌ AudioSource não encontrado em " + gameObject.name);
            return;
        }

        audioSource.spatialBlend = 1f; // 🔥 3D
        audioSource.volume = volume;

        StartCoroutine(SoundLoop());
    }

    System.Collections.IEnumerator SoundLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

            PlayRandomSound();
        }
    }

    void PlayRandomSound()
    {
        if (ambientClips.Length == 0) return;

        AudioClip clip = ambientClips[Random.Range(0, ambientClips.Length)];

        // 🔥 variação de pitch
        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);

        audioSource.PlayOneShot(clip);
    }
}