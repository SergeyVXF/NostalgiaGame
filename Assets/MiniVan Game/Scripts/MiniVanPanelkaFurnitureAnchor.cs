using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanPanelkaFurnitureAnchor : MonoBehaviour
    {
        public const string BackWallId = "BACK_WALL_ID";
        public const string FrontRoomId = "FRONT_ROOM_ID";

        [SerializeField] private string placementId = BackWallId;
        [SerializeField] private Transform backWallAnchor;
        [SerializeField] private Transform frontRoomAnchor;

        public string PlacementId
        {
            get { return placementId; }
        }

        public Transform BackWallAnchor
        {
            get { return backWallAnchor; }
        }

        public Transform FrontRoomAnchor
        {
            get { return frontRoomAnchor; }
        }

        public void Configure(Transform backAnchor, Transform frontAnchor)
        {
            placementId = BackWallId;
            backWallAnchor = backAnchor;
            frontRoomAnchor = frontAnchor;
        }
    }
}
