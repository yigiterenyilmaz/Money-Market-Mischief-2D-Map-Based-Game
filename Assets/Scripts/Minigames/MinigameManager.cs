using System;
using System.Collections.Generic;
using UnityEngine;

public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance { get; private set; }

    private HashSet<MiniGameData> unlockedMinigames = new HashSet<MiniGameData>();
    private Dictionary<MiniGameData, float> cooldownTimers = new Dictionary<MiniGameData, float>();

    public static event Action<MiniGameData> OnMinigameUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        //cooldown sayaçlarını güncelle
        if (cooldownTimers.Count == 0) return;

        List<MiniGameData> expired = null;
        foreach (var kvp in cooldownTimers)
        {
            if (kvp.Value - Time.deltaTime <= 0f)
            {
                if (expired == null) expired = new List<MiniGameData>();
                expired.Add(kvp.Key);
            }
        }

        //süresi dolmuşları sil
        if (expired != null)
        {
            for (int i = 0; i < expired.Count; i++)
                cooldownTimers.Remove(expired[i]);
        }

        //kalanları güncelle
        var keys = new List<MiniGameData>(cooldownTimers.Keys);
        for (int i = 0; i < keys.Count; i++)
            cooldownTimers[keys[i]] -= Time.deltaTime;
    }

    public void UnlockMinigame(MiniGameData minigame)
    {
        if (unlockedMinigames.Contains(minigame))
            return;

        unlockedMinigames.Add(minigame);
        OnMinigameUnlocked?.Invoke(minigame);
    }

    public bool IsMinigameUnlocked(MiniGameData minigame)
    {
        return unlockedMinigames.Contains(minigame);
    }

    /// <summary>
    /// Minigame için cooldown başlatır. Süre MiniGameData.cooldownDuration'dan alınır.
    /// </summary>
    public void StartCooldown(MiniGameData minigame)
    {
        cooldownTimers[minigame] = minigame.cooldownDuration;
    }

    /// <summary>
    /// Minigame cooldown'da mı kontrol eder.
    /// </summary>
    public bool IsOnCooldown(MiniGameData minigame)
    {
        return cooldownTimers.ContainsKey(minigame);
    }

    /// <summary>
    /// Kalan cooldown süresini döner (saniye). Cooldown yoksa 0.
    /// </summary>
    public float GetRemainingCooldown(MiniGameData minigame)
    {
        return cooldownTimers.ContainsKey(minigame) ? cooldownTimers[minigame] : 0f;
    }

    public List<MiniGameData> GetUnlockedMinigames()
    {
        return new List<MiniGameData>(unlockedMinigames);
    }
}
