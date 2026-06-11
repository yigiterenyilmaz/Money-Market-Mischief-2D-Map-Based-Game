using UnityEngine;
using UnityEngine.UI;

public class SkillButtonUI : MonoBehaviour
{
    public Skill skill;
    public Button button;

    private void Start()
    {
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        SkillEvents.OnSkillUnlockRequested?.Invoke(skill.id);
    }
}
