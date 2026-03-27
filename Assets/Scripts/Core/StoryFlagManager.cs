using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerçekleşmiş hikaye olaylarını takip eden singleton.
/// Bir kez aktif edilen bayrak oyun boyunca kalıcıdır.
/// </summary>
public class StoryFlagManager : MonoBehaviour
{
    public static StoryFlagManager Instance { get; private set; }

    private HashSet<StoryFlag> activeFlags = new HashSet<StoryFlag>();

    public static event Action<StoryFlag> OnStoryFlagSet;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Bir hikaye bayrağını aktif eder. Zaten aktifse bir şey yapmaz.
    /// </summary>
    public void SetFlag(StoryFlag flag)
    {
        if (flag == StoryFlag.None) return;
        if (activeFlags.Add(flag))
        {
            OnStoryFlagSet?.Invoke(flag);
        }
    }

    /// <summary>
    /// Birden fazla bayrağı aynı anda aktif eder.
    /// </summary>
    public void SetFlags(List<StoryFlag> flags)
    {
        if (flags == null) return;
        for (int i = 0; i < flags.Count; i++)
        {
            SetFlag(flags[i]);
        }
    }

    /// <summary>
    /// Bayrak aktif mi kontrol eder.
    /// </summary>
    public bool HasFlag(StoryFlag flag)
    {
        if (flag == StoryFlag.None) return true;
        return activeFlags.Contains(flag);
    }

    /// <summary>
    /// Tüm aktif bayrakları döner.
    /// </summary>
    public HashSet<StoryFlag> GetActiveFlags()
    {
        return activeFlags;
    }
}
