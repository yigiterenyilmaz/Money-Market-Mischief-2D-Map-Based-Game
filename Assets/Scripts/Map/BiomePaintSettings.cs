using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct CityBuildingEntry
{
    public Sprite daySprite;
    [Tooltip("Gece sprite'ı. Boş bırakılabilir.")]
    public Sprite nightSprite;
}

[System.Serializable]
public struct SpecialCityBuilding
{
    [Tooltip("Özel bina sprite'ı (gündüz).")]
    public Sprite daySprite;
    [Tooltip("Özel bina sprite'ı (gece). Boş bırakılabilir.")]
    public Sprite nightSprite;
    [Tooltip("Haritada kaç adet yerleştirilecek.")]
    [Range(1, 10)] public int count;
    [Tooltip("Sadece yoğun bölgeye mi yerleşsin?")]
    public bool denseOnly;
}

[CreateAssetMenu(fileName = "BiomePaintSettings", menuName = "Map/BiomePaintSettings")]
public class BiomePaintSettings : ScriptableObject
{
    [Header("Beach — sandy coastal strip")]
    public Color beachDark  = new Color(0.72f, 0.62f, 0.38f); // wet sand
    public Color beachLight = new Color(0.86f, 0.78f, 0.52f); // dry sand

    [Header("Water")]
    public Color waterDeep    = new Color(0.04f, 0.12f, 0.35f);
    public Color waterShallow = new Color(0.12f, 0.35f, 0.62f);

    [Header("Fog")]
    public Color fogColor = new Color(0.70f, 0.74f, 0.80f);

    [Header("Urban — untouched nature, base green of the country")]
    public Color urbanDark  = new Color(0.18f, 0.42f, 0.14f); // deep natural green
    public Color urbanLight = new Color(0.26f, 0.54f, 0.20f); // slightly lighter green

    [Header("Agricultural — livelier, richer green than urban")]
    public Color agriculturalDark  = new Color(0.20f, 0.52f, 0.12f); // vivid crop green
    public Color agriculturalLight = new Color(0.38f, 0.66f, 0.18f); // bright lush green

    [Header("Cities — pale, concrete-like, washed out urban tone")]
    public Color citiesDark  = new Color(0.52f, 0.52f, 0.48f); // pale grey
    public Color citiesLight = new Color(0.66f, 0.65f, 0.60f); // lighter concrete

    [Header("Industrial — cracked earth, dark, barren")]
    public Color industrialDark   = new Color(0.30f, 0.24f, 0.18f); // dark cracked earth
    public Color industrialLight  = new Color(0.46f, 0.38f, 0.28f); // dry dirt
    public Color industrialCrack  = new Color(0.18f, 0.14f, 0.10f); // deep cracks

    [Header("Sea Rocks — gray stone formations in the ocean")]
    public Color seaRockDark  = new Color(0.38f, 0.38f, 0.40f); // dark gray stone
    public Color seaRockLight = new Color(0.56f, 0.55f, 0.54f); // lighter gray
    public Color seaRockCrack = new Color(0.24f, 0.23f, 0.22f); // deep crevice

    [Header("Decorative Sprites — Urban")]
    public List<Sprite> urbanDecor = new List<Sprite>();

    [Header("Decorative Sprites — Agricultural")]
    public List<Sprite> agriculturalDecor = new List<Sprite>();

    [Header("Decorative Sprites — Cities (Genel Binalar)")]
    public List<CityBuildingEntry> citiesDecor = new List<CityBuildingEntry>();

    [Header("City Special Buildings — Sabit Sayılı Özel Binalar")]
    public List<SpecialCityBuilding> specialCityBuildings = new List<SpecialCityBuilding>();

    [Header("Decorative Sprites — Industrial")]
    public List<Sprite> industrialDecor = new List<Sprite>();
}