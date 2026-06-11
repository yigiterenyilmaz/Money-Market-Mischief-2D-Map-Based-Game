using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/PipeHunt/Database")]
public class PipeHuntDatabase : ScriptableObject
{
    [Header("Borular")]
    public List<PipeType> pipeTypes; //oyunda çıkabilecek boru tipleri
    public int minPipeCount = 5; //minimum boru sayısı
    public int maxPipeCount = 10; //maximum boru sayısı

    [Header("Aletler")]
    public List<HuntTool> tools; //oyuncunun seçebileceği alet tipleri

    [Header("Süre")]
    public float minGameDuration = 15f; //stealth=0 → en kısa süre (saniye)
    public float maxGameDuration = 45f; //stealth=1 → en uzun süre (saniye)

    [Header("Boru Yerleşim")]
    public float minPipeDistance = 0.1f; //borular arası minimum mesafe (normalize 0-1)

    [Header("Süre Aşımı — Şüphe")]
    [Tooltip("Süre %100 aşıldığında toplam eklenecek şüphe miktarı")]
    public float suspicionBase = 50f;

    [Tooltip("Büyüme eğrisi gücü (>1 = yavaş başlar hızlı artar)")]
    public float suspicionExponent = 2f;
}
