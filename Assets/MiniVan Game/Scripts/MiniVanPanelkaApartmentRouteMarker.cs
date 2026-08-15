using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanPanelkaApartmentRouteRole
    {
        Inaccessible,
        MainRoute,
        KeySource,
        TransferArrival
    }

    [DisallowMultipleComponent]
    public sealed class MiniVanPanelkaApartmentRouteMarker : MonoBehaviour
    {
        public int FloorNumber;
        public int ApartmentNumber;
        [Range(0, 3)] public int ApartmentSlot;
        public MiniVanPanelkaApartmentRouteRole Role;
        public bool PlayerCanEnter => Role != MiniVanPanelkaApartmentRouteRole.Inaccessible;
        public bool RequiresVisit => Role == MiniVanPanelkaApartmentRouteRole.MainRoute ||
                                     Role == MiniVanPanelkaApartmentRouteRole.KeySource ||
                                     Role == MiniVanPanelkaApartmentRouteRole.TransferArrival;

        public void Configure(
            int floorNumber,
            int apartmentNumber,
            int apartmentSlot,
            MiniVanPanelkaApartmentRouteRole role)
        {
            FloorNumber = floorNumber;
            ApartmentNumber = apartmentNumber;
            ApartmentSlot = apartmentSlot;
            Role = role;
        }
    }
}
