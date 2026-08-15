using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanPanelkaRoomIdentity : MonoBehaviour
    {
        public string RoomId;
        public Vector3 RoomCenterLocal;
        public Vector3 RoomSizeLocal;
        public string[] DoorEdges = new string[0];

        public void Configure(string roomId, Vector3 roomCenterLocal)
        {
            RoomId = roomId;
            RoomCenterLocal = roomCenterLocal;
        }

        public void Configure(string roomId, Vector3 roomCenterLocal, Vector3 roomSizeLocal,
            string[] doorEdges)
        {
            RoomId = roomId;
            RoomCenterLocal = roomCenterLocal;
            RoomSizeLocal = roomSizeLocal;
            DoorEdges = doorEdges != null ? (string[])doorEdges.Clone() : new string[0];
        }

        public bool HasDoorOnEdge(string edge)
        {
            if (string.IsNullOrEmpty(edge) || DoorEdges == null)
            {
                return false;
            }

            for (int i = 0; i < DoorEdges.Length; i++)
            {
                if (string.Equals(DoorEdges[i], edge, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
