using UnityEngine;

[System.Serializable]
public class StatCondition
{
    //istenen stat condition'ının sağlanıp sağlanmadığını ölçen class. IsMet
    //istenen durumlar sağlanıyorsa true döner. sadece statlara bakar.
    public StatType statType;
    public ComparisonType comparison;
    public float value;

    public bool IsMet()
    {
        float currentValue = GameStatManager.Instance.GetStat(statType);

        return comparison switch
        {
            ComparisonType.GreaterThan => currentValue > value,
            ComparisonType.LessThan => currentValue < value,
            ComparisonType.GreaterOrEqual => currentValue >= value,
            ComparisonType.LessOrEqual => currentValue <= value,
            ComparisonType.Equals => Mathf.Approximately(currentValue, value),
            _ => false
        };
    }
}
