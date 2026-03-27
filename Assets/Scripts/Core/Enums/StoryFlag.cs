/// <summary>
/// Oyun boyunca gerçekleşmiş hikaye olaylarını temsil eden bayraklar.
/// Bir kez aktif edildikten sonra oyun boyunca kalıcıdır.
/// Event açıklamalarını, diyalogları ve seçenekleri koşullu olarak değiştirmek için kullanılır.
/// </summary>
public enum StoryFlag
{
    None = 0,
    BetrayedAgentsToTheirDeath = 1,
    ToldWomanAgentsWillHandleMe = 2,
}
