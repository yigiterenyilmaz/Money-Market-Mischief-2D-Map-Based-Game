using UnityEngine;

/// <summary>
/// Tek bir borunun runtime verisi. Manager tarafından oluşturulur, UI tarafından okunur.
/// </summary>
[System.Serializable]
public class PipeInstance
{
    public int id;
    public PipeType pipeType;
    public Vector2 position; //normalize koordinat (0-1 arası x ve y)
    public int remainingDurability; //kalan dayanıklılık
    public bool isBurst; //patladı mı
    public float burstTime; //patladığı an (Time.unscaledTime)

    public PipeInstance(int id, PipeType pipeType, Vector2 position)
    {
        this.id = id;
        this.pipeType = pipeType;
        this.position = position;
        this.remainingDurability = pipeType.durability;
        this.isBurst = false;
        this.burstTime = 0f;
    }
}
