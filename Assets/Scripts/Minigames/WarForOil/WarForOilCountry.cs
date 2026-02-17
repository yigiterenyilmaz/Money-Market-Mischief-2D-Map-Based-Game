using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/WarForOil/Country")]
public class WarForOilCountry : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;

    public float baseReward; //savaş kazanıldığında taban ödül (supportRatio ile çarpılır)
    [Range(0f, 1f)] public float invasionDifficulty; //işgal zorluğu — savaş kazanma şansını düşürür

    public List<WarForOilEvent> events; //bu ülkeye özel savaş eventleri
}
