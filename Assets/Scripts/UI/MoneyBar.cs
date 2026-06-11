using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;


public class MoneyBar : MonoBehaviour
{
    public float money, maxMoney, width, height;
    
    [SerializeField]
    private RectTransform moneyBar;

    public void setMaxMoney(float maxMoneySet)
    {
        maxMoney = maxMoneySet;
    }

    public void setMoney(float moneySet)
    {
        money = moneySet;
        moneyBar.sizeDelta = new Vector2((money/maxMoney)*width, height);
    }
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
