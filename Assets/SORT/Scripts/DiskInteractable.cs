using UnityEngine;
using TMPro;
using System.Collections;

public class DiskInteractable : MonoBehaviour
{
    public string diskName = "Название";
    public AudioClip audioClip;
    public TMP_Text labelText;
    public GameObject iconObject; // Иконка над диском
    public KeyCode interactKey = KeyCode.E;
    [HideInInspector] public DiskSpawner spawner;
    public float rotationSpeed = 90f; // градусов в секунду

    private Transform player;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool isPlayerNear = false;
    private bool isPlaying = false;

    void Start()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        if (labelText != null)
        {
            labelText.text = diskName;
            labelText.gameObject.SetActive(false);
        }
        if (iconObject != null)
            iconObject.SetActive(false);
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        if (!isPlaying && gameObject.activeSelf)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
        if (isPlaying) return;
        if (player == null) return;
        if (isPlayerNear && Input.GetKeyDown(interactKey))
        {
            if (spawner != null && MusicPlayer.Instance != null && audioClip != null)
            {
                MusicPlayer.Instance.PlayMusic(audioClip);
                spawner.RemoveDisk(gameObject); // Диск исчезает сразу!
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            if (labelText != null) labelText.gameObject.SetActive(true);
            if (iconObject != null) iconObject.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            if (labelText != null) labelText.gameObject.SetActive(false);
            if (iconObject != null) iconObject.SetActive(false);
        }
    }

    IEnumerator PlayAudioAndRespawn()
    {
        Debug.Log("[DiskInteractable] Корутина запущена для " + gameObject.name);
        isPlaying = true;
        if (labelText != null) labelText.gameObject.SetActive(false);
        if (iconObject != null) iconObject.SetActive(false);

        // Диск исчезает сразу!
        Debug.Log("[DiskInteractable] Диск скрыт сразу после нажатия E для " + gameObject.name);
        gameObject.SetActive(false);

        if (audioClip != null && MusicPlayer.Instance != null)
        {
            MusicPlayer.Instance.PlayMusic(audioClip);
            while (MusicPlayer.Instance.IsPlaying())
                yield return null;
            Debug.Log("[DiskInteractable] Музыка закончилась для " + gameObject.name);
        }
        if (spawner != null)
        {
            Debug.Log("[DiskInteractable] Появляется новый диск");
            spawner.SpawnDisk();
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(true);
        }
        isPlaying = false;
    }
} 