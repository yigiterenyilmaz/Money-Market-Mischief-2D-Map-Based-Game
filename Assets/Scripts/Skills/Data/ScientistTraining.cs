/// <summary>
/// Tek bir bilim adamının eğitim durumunu tutar.
/// Her bilim adamının kendi yatırım eğrisi bağımsız işler.
/// </summary>
public class ScientistTraining
{
    public ScientistData data;            //bilim adamı asset'i (isim, completionLevel vs.)
    public float level;                   //eğitim seviyesi
    public float totalInvested;           //bu bilim adamına toplam yatırılan wealth
    public float startTime;              //eğitimin başladığı an (Time.time)
    public bool isCompleted;             //eğitim tamamlandı mı

    public ScientistTraining(ScientistData data, float startTime)
    {
        this.data = data;
        this.level = 0f;
        this.totalInvested = 0f;
        this.startTime = startTime;
        this.isCompleted = false;
    }
}
