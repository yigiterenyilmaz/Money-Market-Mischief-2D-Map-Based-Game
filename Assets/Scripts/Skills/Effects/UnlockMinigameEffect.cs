using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnlockMinigameEffect : SkillEffect
{
    public List<MiniGameData> minigamesToBeUnlocked;

    public override void Apply()
    {
        foreach (MiniGameData minigame in minigamesToBeUnlocked)
        {
            MinigameManager.Instance.UnlockMinigame(minigame);
        }
    }
}
