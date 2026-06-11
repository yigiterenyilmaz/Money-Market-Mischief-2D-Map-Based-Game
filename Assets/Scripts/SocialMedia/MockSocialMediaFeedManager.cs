using System;
using System.Collections.Generic;
using UnityEngine;

public class MockSocialMediaFeedManager : MonoBehaviour
{
    public static MockSocialMediaFeedManager Instance { get; private set; }

    [Header("Mesajlar")]
    public List<MockSocialMediaPost> posts = new List<MockSocialMediaPost>();

    [Header("Zamanlama")]
    public float minInterval = 2f;
    public float maxInterval = 5f;

    // B kategorisi aktif mi
    bool categoryBActive;

    // gosterilecek mesajlarin indexleri
    List<int> availableIndices = new List<int>();
    int lastShownIndex = -1;
    float timer;
    float currentInterval;

    public static event Action<MockSocialMediaPost> OnNewMockPost;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        RefreshAvailableIndices();
        currentInterval = UnityEngine.Random.Range(minInterval, maxInterval);
        timer = 0f;
    }

    void Update()
    {
        if (availableIndices.Count == 0) return;

        timer += Time.deltaTime;
        if (timer >= currentInterval)
        {
            timer = 0f;
            currentInterval = UnityEngine.Random.Range(minInterval, maxInterval);
            ShowNextPost();
        }
    }

    void ShowNextPost()
    {
        if (availableIndices.Count == 0) return;

        // ayni mesaji ust uste gosterme
        int pick;
        if (availableIndices.Count == 1)
        {
            pick = availableIndices[0];
        }
        else
        {
            do
            {
                pick = availableIndices[UnityEngine.Random.Range(0, availableIndices.Count)];
            } while (pick == lastShownIndex);
        }

        lastShownIndex = pick;
        OnNewMockPost?.Invoke(posts[pick]);
    }

    /// <summary>
    /// B kategorisini aktif eder. Disaridan cagirilacak.
    /// </summary>
    public void ActivateCategoryB()
    {
        if (categoryBActive) return;
        categoryBActive = true;
        RefreshAvailableIndices();
    }

    public bool IsCategoryBActive() => categoryBActive;

    void RefreshAvailableIndices()
    {
        availableIndices.Clear();
        for (int i = 0; i < posts.Count; i++)
        {
            if (posts[i].category == MessageCategory.A)
                availableIndices.Add(i);
            else if (posts[i].category == MessageCategory.B && categoryBActive)
                availableIndices.Add(i);
        }
    }
}
