using System;
using System.Collections.Generic;
using UnityEngine;

public class CountryData : MonoBehaviour
{
    public static CountryData Instance { get; private set; }

    [Header("Map Generator Reference")]
    public MapGenerator mapGenerator;

    private Dictionary<RegionType, float> regionRatios = new Dictionary<RegionType, float>();

    private float corruptionIndex;
    private float educationIndex;
    private float climateFertility;
    private float naturalResourceWealth;

    public static event Action OnCountryDataReady;

    // MapGenerator biome IDs → RegionType
    // 1 = Agricultural
    // 2 = Cities
    // 3 = Industrial
    // 4 = Urban
    private static readonly Dictionary<int, RegionType> biomeToRegion = new Dictionary<int, RegionType>
    {
        { 1, RegionType.Agricultural },
        { 2, RegionType.Cities },
        { 3, RegionType.Industrial },
        { 4, RegionType.Urban }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (mapGenerator != null)
            mapGenerator.OnMapGenerated += HandleMapGenerated;
    }

    private void OnDisable()
    {
        if (mapGenerator != null)
            mapGenerator.OnMapGenerated -= HandleMapGenerated;
    }

    private void HandleMapGenerated()
    {
        PullRegionRatios();
        GenerateCountryProperties();
        LogCountryData();
        OnCountryDataReady?.Invoke();
    }

    private void GenerateCountryProperties()
    {
        corruptionIndex       = GenerateWeightedRandom();
        educationIndex        = GenerateWeightedRandom();
        climateFertility      = GenerateWeightedRandom();
        naturalResourceWealth = GenerateWeightedRandom();
    }

    // %90 chance: 14-86 range, %10 chance: extremes (0-13 or 87-100)
    private float GenerateWeightedRandom()
    {
        float roll = UnityEngine.Random.Range(0f, 1f);
        if (roll < 0.9f)
            return UnityEngine.Random.Range(14f, 87f);
        else
            return UnityEngine.Random.Range(0f, 1f) < 0.5f
                ? UnityEngine.Random.Range(0f, 14f)
                : UnityEngine.Random.Range(87f, 101f);
    }

    private void PullRegionRatios()
    {
        regionRatios[RegionType.Agricultural] = mapGenerator.ForestRatio;
        regionRatios[RegionType.Cities]       = mapGenerator.DesertRatio;
        regionRatios[RegionType.Industrial]   = mapGenerator.MountainRatio;
        regionRatios[RegionType.Urban]        = mapGenerator.PlainsRatio;
    }

    #region Region Getters

    public float GetRegionRatio(RegionType regionType)
        => regionRatios.TryGetValue(regionType, out float ratio) ? ratio : 0f;

    public RegionType GetDominantRegion()
    {
        RegionType dominant = RegionType.Urban;
        float maxRatio = 0f;
        foreach (var pair in regionRatios)
            if (pair.Value > maxRatio) { maxRatio = pair.Value; dominant = pair.Key; }
        return dominant;
    }

    public Dictionary<RegionType, float> GetAllRegionRatios()
        => new Dictionary<RegionType, float>(regionRatios);

    #endregion

    private void LogCountryData()
    {
        Debug.Log("=== ÜLKE VERİSİ ===\n" +
            $"[Bölge Oranları]\n" +
            $"  Sanayi:  %{GetRegionRatio(RegionType.Industrial)   * 100f:F1}\n" +
            $"  Şehir:   %{GetRegionRatio(RegionType.Cities)       * 100f:F1}\n" +
            $"  Tarım:   %{GetRegionRatio(RegionType.Agricultural) * 100f:F1}\n" +
            $"  Doğa:    %{GetRegionRatio(RegionType.Urban)        * 100f:F1}\n" +
            $"[Ülke Özellikleri]\n" +
            $"  Yozlaşma:     {corruptionIndex:F0}\n" +
            $"  Eğitim:       {educationIndex:F0}\n" +
            $"  İklim:        {climateFertility:F0}\n" +
            $"  Doğal Kaynak: {naturalResourceWealth:F0}");
    }

    #region Country Property Getters

    public float CorruptionIndex       => corruptionIndex;
    public float EducationIndex        => educationIndex;
    public float ClimateFertility      => climateFertility;
    public float NaturalResourceWealth => naturalResourceWealth;

    #endregion
}