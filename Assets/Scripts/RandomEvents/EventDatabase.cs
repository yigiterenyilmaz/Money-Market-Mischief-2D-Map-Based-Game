using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/EventDatabase")]
public class EventDatabase : ScriptableObject
{
    public List<Event> allEvents;

    public Event GetById(string id)
    {
        foreach (Event evt in allEvents)
        {
            if (evt.id == id)
            {
                return evt;
            }
        }

        Debug.LogError($"Event not found with id: {id}");
        return null;
    }
}
