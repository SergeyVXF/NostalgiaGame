using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance;
    private AudioSource audioSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            audioSource = GetComponent<AudioSource>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (audioSource == null) return;
        audioSource.clip = clip;
        audioSource.Play();
        Debug.Log("[MusicPlayer] Воспроизведение трека: " + (clip != null ? clip.name : "null"));
    }

    public bool IsPlaying()
    {
        bool playing = audioSource != null && audioSource.isPlaying;
        Debug.Log("[MusicPlayer] IsPlaying: " + playing);
        return playing;
    }
} 