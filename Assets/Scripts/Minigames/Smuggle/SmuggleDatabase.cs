using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SmuggleDatabase", menuName = "Minigames/Smuggle/Database")]
public class SmuggleDatabase : ScriptableObject
{
    public List<SmuggleRoutePack> routePacks = new List<SmuggleRoutePack>();
    public List<SmuggleCourier> couriers = new List<SmuggleCourier>();
    public List<SmuggleEvent> events = new List<SmuggleEvent>();
}

