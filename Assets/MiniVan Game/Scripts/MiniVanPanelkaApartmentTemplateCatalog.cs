using System;
using UnityEngine;

namespace MiniVanGame
{
    public enum MiniVanPanelkaApartmentCornerVariant
    {
        Standard,
        CornerLeft,
        CornerRight
    }

    [CreateAssetMenu(
        fileName = "ApartmentTemplateCatalog",
        menuName = "MiniVan Game/Panelka/Apartment Template Catalog")]
    public sealed class MiniVanPanelkaApartmentTemplateCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Range(1, 5)] public int TemplateIndex;
            public GameObject Prefab;
        }

        [SerializeField] private Entry[] templates = Array.Empty<Entry>();
        [SerializeField] private Entry[] cornerLeftTemplates = Array.Empty<Entry>();
        [SerializeField] private Entry[] cornerRightTemplates = Array.Empty<Entry>();
        [SerializeField] private GameObject exteriorOnlyPrefab;

        public int Count => templates != null ? templates.Length : 0;
        public GameObject ExteriorOnlyPrefab => exteriorOnlyPrefab;

        public GameObject GetPrefab(int templateIndex)
        {
            return GetPrefab(
                templateIndex,
                MiniVanPanelkaApartmentCornerVariant.Standard);
        }

        public GameObject GetPrefab(
            int templateIndex,
            MiniVanPanelkaApartmentCornerVariant cornerVariant)
        {
            Entry[] source = cornerVariant ==
                             MiniVanPanelkaApartmentCornerVariant.CornerLeft
                ? cornerLeftTemplates
                : cornerVariant ==
                  MiniVanPanelkaApartmentCornerVariant.CornerRight
                    ? cornerRightTemplates
                    : templates;
            if (source == null)
                return null;

            for (int i = 0; i < source.Length; i++)
            {
                Entry entry = source[i];
                if (entry != null && entry.TemplateIndex == templateIndex)
                    return entry.Prefab;
            }

            return null;
        }

        public void Configure(GameObject[] prefabs)
        {
            Configure(prefabs, null, null);
        }

        public void Configure(
            GameObject[] prefabs,
            GameObject[] cornerLeftPrefabs,
            GameObject[] cornerRightPrefabs)
        {
            templates = BuildEntries(prefabs);
            cornerLeftTemplates = BuildEntries(cornerLeftPrefabs);
            cornerRightTemplates = BuildEntries(cornerRightPrefabs);
        }

        private static Entry[] BuildEntries(GameObject[] prefabs)
        {
            Entry[] entries = new Entry[prefabs != null ? prefabs.Length : 0];
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = new Entry
                {
                    TemplateIndex = i + 1,
                    Prefab = prefabs[i]
                };
            }

            return entries;
        }

        public void SetExteriorOnlyPrefab(GameObject prefab)
        {
            exteriorOnlyPrefab = prefab;
        }
    }
}
