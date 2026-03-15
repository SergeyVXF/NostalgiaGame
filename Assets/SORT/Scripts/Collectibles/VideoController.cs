using UnityEngine;
using UnityEngine.Video;

namespace AG.Collectibles
{
    /// <summary>
    /// Автоматически запускает видео при активации объекта
    /// </summary>
    public class VideoController : MonoBehaviour
    {
        [Header("Настройки видео")]
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loopVideo = true;
        [SerializeField] private bool restartOnEnable = true;
        
        private VideoPlayer _videoPlayer;
        
        private void Awake()
        {
            _videoPlayer = GetComponent<VideoPlayer>();
            if (_videoPlayer == null)
            {
                Debug.LogError("[VideoController] VideoPlayer не найден на объекте!");
                return;
            }
            
            // Настраиваем VideoPlayer
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = loopVideo;
        }
        
        private void OnEnable()
        {
            if (!playOnEnable || _videoPlayer == null) return;
            
            if (restartOnEnable)
            {
                _videoPlayer.Stop();
                _videoPlayer.Play();
            }
            else if (!_videoPlayer.isPlaying)
            {
                _videoPlayer.Play();
            }
            
            Debug.Log($"[VideoController] Видео запущено: {_videoPlayer.clip?.name ?? "без клипа"}");
        }
        
        private void OnDisable()
        {
            if (_videoPlayer != null && _videoPlayer.isPlaying)
            {
                _videoPlayer.Stop();
            }
        }
        
        // Публичные методы для ручного управления
        public void PlayVideo()
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Play();
            }
        }
        
        public void StopVideo()
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
            }
        }
        
        public void PauseVideo()
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Pause();
            }
        }
    }
}


