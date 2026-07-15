using UnityEngine;

/// <summary>
/// Merkezi harita seed sistemi. Kullanıcı TEK bir seed görür/verir (MapGenerator.seed);
/// her üretim aşaması (ada, boya, yol, dekor, petrol, hazine, fay...) kendi seed'ini bu
/// tek seed'den türetir. Böylece event abone sırası veya araya giren alakasız Random
/// çağrıları değişse bile her aşama kendi içinde deterministik kalır.
///
/// Kullanım: her generator'ın giriş noktasında MapSeed.Apply("faz-adı") çağrılır.
/// Üretim zinciri bitince MapSeed.RandomizeRuntime() ile global Random tekrar
/// nondeterministik hale getirilir — gemi/trafik/tornado gibi runtime sistemler
/// seed'e bağlanmaz.
/// </summary>
public static class MapSeed
{
    /// <summary>Aktif haritanın seed'i. MapGenerator.GenerateMap başında set edilir.</summary>
    public static int CurrentSeed { get; private set; }

    public static void SetSeed(int seed) => CurrentSeed = seed;

    /// <summary>
    /// Faz adından deterministik alt-seed türetir (FNV-1a). string.GetHashCode
    /// kullanılmaz — runtime sürümleri arasında kararlılığı garanti değildir.
    /// </summary>
    public static int Derive(string phase)
    {
        unchecked
        {
            uint hash = 2166136261u ^ (uint)CurrentSeed;
            for (int i = 0; i < phase.Length; i++)
            {
                hash ^= phase[i];
                hash *= 16777619u;
            }
            return (int)hash;
        }
    }

    /// <summary>Global UnityEngine.Random'ı bu fazın türetilmiş seed'ine kilitler.</summary>
    public static void Apply(string phase) => Random.InitState(Derive(phase));

    /// <summary>
    /// Üretim bittikten sonra çağrılır: global Random'ı zamana dayalı bir değerle
    /// yeniden tohumlar ki runtime rastgelelik (gemiler, trafik, olay zamanlamaları)
    /// harita seed'inden bağımsız kalsın.
    /// </summary>
    public static void RandomizeRuntime()
        => Random.InitState(unchecked((int)System.DateTime.Now.Ticks));
}
