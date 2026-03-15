using UnityEngine;

public class DiskSpawner : MonoBehaviour
{
    public GameObject diskPrefab;
    public Transform spawnPoint;

    private GameObject currentDisk;

    void Start()
    {
        SpawnDisk();
    }

    void Update()
    {
        if (currentDisk == null && MusicPlayer.Instance != null && !MusicPlayer.Instance.IsPlaying())
        {
            SpawnDisk();
        }
    }

    public void SpawnDisk()
    {
        if (currentDisk != null) Destroy(currentDisk);
        currentDisk = Instantiate(diskPrefab, spawnPoint.position, spawnPoint.rotation);
        currentDisk.SetActive(true);
        var interact = currentDisk.GetComponent<DiskInteractable>();
        if (interact != null)
        {
            interact.spawner = this;
        }
    }

    public void RemoveDisk(GameObject disk)
    {
        if (currentDisk == disk)
        {
            Destroy(currentDisk);
            currentDisk = null;
        }
    }
} 