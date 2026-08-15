using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanPanelkaApartmentTemplate : MonoBehaviour
    {
        [SerializeField, Range(1, 5)] private int templateIndex = 1;
        [SerializeField] private string templateId = "APARTMENT_TEMPLATE_01";
        [SerializeField] private string sourceCorner = "NE";
        [SerializeField] private Vector2 footprint = new Vector2(8.8f, 9f);
        [SerializeField] private Transform contentRoot;
        [SerializeField] private Transform entrySocket;
        [SerializeField] private Transform routeHoleSocket;
        [SerializeField] private Transform balconySocket;
        [SerializeField] private Transform pipeSocket;
        [SerializeField] private Transform keySocket;

        public int TemplateIndex => templateIndex;
        public string TemplateId => templateId;
        public string SourceCorner => sourceCorner;
        public Vector2 Footprint => footprint;
        public Transform ContentRoot => contentRoot;
        public Transform EntrySocket => entrySocket;
        public Transform RouteHoleSocket => routeHoleSocket;
        public Transform BalconySocket => balconySocket;
        public Transform PipeSocket => pipeSocket;
        public Transform KeySocket => keySocket;

        public void Configure(
            int index,
            string id,
            string corner,
            Vector2 size,
            Transform content,
            Transform entry,
            Transform routeHole,
            Transform balcony,
            Transform pipe,
            Transform key)
        {
            templateIndex = Mathf.Clamp(index, 1, 5);
            templateId = string.IsNullOrWhiteSpace(id)
                ? "APARTMENT_TEMPLATE_" + templateIndex.ToString("00")
                : id;
            sourceCorner = string.IsNullOrWhiteSpace(corner) ? "NE" : corner;
            footprint = size;
            contentRoot = content;
            entrySocket = entry;
            routeHoleSocket = routeHole;
            balconySocket = balcony;
            pipeSocket = pipe;
            keySocket = key;
        }

        public Quaternion GetRotation(string targetCorner)
        {
            return Quaternion.Euler(
                0f,
                GetCornerYaw(targetCorner) - GetCornerYaw(sourceCorner),
                0f);
        }

        public Vector2 GetOrientedFootprint(string targetCorner)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(
                GetCornerYaw(sourceCorner), GetCornerYaw(targetCorner)));
            return Mathf.Approximately(delta, 90f)
                ? new Vector2(footprint.y, footprint.x)
                : footprint;
        }

        private static float GetCornerYaw(string corner)
        {
            switch (corner)
            {
                case "NW": return -90f;
                case "SW": return 180f;
                case "SE": return 90f;
                default: return 0f;
            }
        }
    }
}
