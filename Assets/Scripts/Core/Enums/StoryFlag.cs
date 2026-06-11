/// <summary>
/// Oyun boyunca gerçekleşmiş hikaye olaylarını temsil eden bayraklar.
/// Choice ile aktif edilenler kalıcıdır. Sistem tarafından yönetilenler runtime'da açılıp kapanabilir.
/// Event açıklamalarını, diyalogları ve seçenekleri koşullu olarak değiştirmek için kullanılır.
/// </summary>
public enum StoryFlag
{
    None = 0,
    //Choice-driven (kalıcı)
    BetrayedAgentsToTheirDeath = 1,
    ToldWomanAgentsWillHandleMe = 2,
    RevealedIntentToAgentsThroughHaste = 3,
    ConfessedOwnDeathToAgents = 4,
    //System-driven (runtime, hardcoded yönetilir)
    SavasAktif = 5, //WarForOilManager savaş başlangıcında set, bitiminde clear
}
