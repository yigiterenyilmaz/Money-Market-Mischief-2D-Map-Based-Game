using System;
using UnityEngine;

public class ElectionManager : MonoBehaviour
{
    public static ElectionManager Instance { get; private set; }

    [Header("Election Settings")]
    public float electionInterval = 300f; //5 dakika (saniye cinsinden)

    //runtime
    private Party currentRulingParty;
    private float electionTimer = 0f;
    private int electionCount = 0; //kaç seçim yapıldı

    //events
    public static event Action OnElectionStarted; //seçim başladığında
    public static event Action<Party> OnElectionEnded; //seçim bittiğinde, kazanan parti
    public static event Action<Party, Party> OnRulingPartyChanged; //iktidar değiştiğinde (eski, yeni)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        //oyun başında rastgele bir parti iktidarda
        currentRulingParty = (Party)UnityEngine.Random.Range(0, 2);
    }

    private void Update()
    {
        electionTimer += Time.deltaTime;

        if (electionTimer >= electionInterval)
        {
            HoldElection();
            electionTimer = 0f;
        }
    }

    private void HoldElection()
    {
        OnElectionStarted?.Invoke();

        Party previousParty = currentRulingParty;

        //şimdilik rastgele seçim sonucu (ileride detaylandırılacak)
        Party winner = (Party)UnityEngine.Random.Range(0, 2);
        currentRulingParty = winner;
        electionCount++;

        OnElectionEnded?.Invoke(winner);

        if (previousParty != winner)
        {
            OnRulingPartyChanged?.Invoke(previousParty, winner);
        }
    }

    public Party GetRulingParty()
    {
        return currentRulingParty;
    }

    public float GetTimeUntilNextElection()
    {
        return electionInterval - electionTimer;
    }

    public int GetElectionCount()
    {
        return electionCount;
    }

    //ileride kullanılmak üzere - seçim sonucunu etkileme
    public void SetElectionInfluence(Party party, float influence)
    {
        //TODO: implement edilecek
    }
}
