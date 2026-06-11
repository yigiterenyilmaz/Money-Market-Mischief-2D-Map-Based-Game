using System;

public static class SkillEvents
{
    public static Action<Skill> OnSkillUnlocked;
    public static Action<Skill> OnSkillBlocked; //bir skill kalıcı olarak kilitlendiğinde
    public static Action<string> OnSkillUnlockRequested;
}
