using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanPanelkaWindowSocket : MonoBehaviour
    {
        [SerializeField] private string roomId;
        [SerializeField] private MiniVanPanelkaApartmentFacadeSide side;
        [SerializeField] private GameObject windowModule;
        [SerializeField] private GameObject solidWallModule;

        public string RoomId => roomId;
        public MiniVanPanelkaApartmentFacadeSide Side => side;
        public GameObject WindowModule => windowModule;
        public GameObject SolidWallModule => solidWallModule;
        public bool IsWindowActive =>
            windowModule != null && windowModule.activeSelf;

        public void Configure(
            string ownerRoomId,
            MiniVanPanelkaApartmentFacadeSide facadeSide,
            GameObject window,
            GameObject solidWall)
        {
            roomId = ownerRoomId ?? string.Empty;
            side = facadeSide;
            windowModule = window;
            solidWallModule = solidWall;
        }

        public void SetWindowActive(bool active)
        {
            if (windowModule != null)
                windowModule.SetActive(active);
            if (solidWallModule != null)
                solidWallModule.SetActive(!active);
        }
    }
}
