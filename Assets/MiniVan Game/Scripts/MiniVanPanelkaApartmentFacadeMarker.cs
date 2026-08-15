using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanPanelkaApartmentFacadeSide
    {
        PositiveX,
        PositiveZ
    }

    [DisallowMultipleComponent]
    public sealed class MiniVanPanelkaApartmentFacadeMarker : MonoBehaviour
    {
        [SerializeField] private MiniVanPanelkaApartmentFacadeSide side;

        public MiniVanPanelkaApartmentFacadeSide Side => side;

        public void Configure(MiniVanPanelkaApartmentFacadeSide facadeSide)
        {
            side = facadeSide;
        }
    }
}
