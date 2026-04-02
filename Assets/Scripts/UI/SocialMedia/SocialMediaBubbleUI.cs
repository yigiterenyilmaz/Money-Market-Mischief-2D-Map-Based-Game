using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SocialMediaBubbleUI : MonoBehaviour
{
    [Header("Referanslar")]
    public RectTransform bubbleContainer;
    public GameObject bubblePrefab;

    [Header("Panel Alani")]
    public float panelTopMargin = 0f;
    public float panelBottomMargin = 0f;
    public float panelLeftMargin = 0f;
    public float panelRightMargin = 0f;

    [Header("Balon Ayarlari")]
    public float spacingBetweenBubbles = 8f;
    public float slideSpeed = 5f; // smooth kayma hizi

    // her balonun hedef y pozisyonunu tutar
    List<RectTransform> activeBubbles = new List<RectTransform>();
    List<float> targetYPositions = new List<float>();

    void OnEnable()
    {
        MockSocialMediaFeedManager.OnNewMockPost += HandleNewPost;
    }

    void OnDisable()
    {
        MockSocialMediaFeedManager.OnNewMockPost -= HandleNewPost;
    }

    void Update()
    {
        // balonlari hedef pozisyonlarina dogru smooth kaydir
        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            if (activeBubbles[i] == null)
            {
                activeBubbles.RemoveAt(i);
                targetYPositions.RemoveAt(i);
                continue;
            }

            Vector2 pos = activeBubbles[i].anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, targetYPositions[i], Time.deltaTime * slideSpeed);
            activeBubbles[i].anchoredPosition = pos;
        }
    }

    void HandleNewPost(MockSocialMediaPost post)
    {
        if (bubblePrefab == null || bubbleContainer == null) return;

        GameObject bubbleObj = Instantiate(bubblePrefab, bubbleContainer);
        RectTransform bubbleRect = bubbleObj.GetComponent<RectTransform>();

        // hedef genislik
        float containerWidth = bubbleContainer.rect.width;
        float targetWidth = containerWidth - panelLeftMargin - panelRightMargin;

        // orijinal prefab boyutunu oku, scale faktorunu hesapla
        float originalWidth = bubbleRect.sizeDelta.x;
        float scaleFactor = targetWidth / originalWidth;

        // anchor'i alt-ortaya cek (container icindeki konum icin, ic duzeni etkilemez)
        bubbleRect.anchorMin = new Vector2(0.5f, 0f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0f);

        // localScale ile butunuyle buyut
        bubbleRect.localScale = new Vector3(scaleFactor, scaleFactor, 1f);

        // scaled yukseklik
        float scaledHeight = bubbleRect.sizeDelta.y * scaleFactor;

        // text iceriklerini yaz
        TMP_Text contentText = bubbleObj.transform.Find("ContentText")?.GetComponent<TMP_Text>();
        TMP_Text authorText = bubbleObj.transform.Find("AuthorText")?.GetComponent<TMP_Text>();

        if (contentText != null)
            contentText.text = post.content;
        if (authorText != null)
            authorText.text = post.authorName;

        // mevcut balonlarin hedef pozisyonlarini yukari kaydir
        float shiftAmount = scaledHeight + spacingBetweenBubbles;
        for (int i = 0; i < targetYPositions.Count; i++)
        {
            targetYPositions[i] += shiftAmount;
        }

        // yeni balonu panelin altina yerlestir (bu anlik, smooth degil)
        float xOffset = (panelLeftMargin - panelRightMargin) / 2f;
        float startY = panelBottomMargin + scaledHeight * 0.5f;
        bubbleRect.anchoredPosition = new Vector2(xOffset, startY);

        activeBubbles.Add(bubbleRect);
        targetYPositions.Add(startY);

        RemoveOverflowBubbles();
    }

    void RemoveOverflowBubbles()
    {
        float maxTop = bubbleContainer.rect.height - panelTopMargin;

        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            if (activeBubbles[i] == null)
            {
                activeBubbles.RemoveAt(i);
                targetYPositions.RemoveAt(i);
                continue;
            }

            // hedef pozisyona gore kontrol et (gercek pozisyon henuz oraya ulasmamis olabilir)
            float scaledHeight = activeBubbles[i].sizeDelta.y * activeBubbles[i].localScale.y;
            float bubbleTop = targetYPositions[i] + scaledHeight * 0.5f;
            if (bubbleTop > maxTop)
            {
                Destroy(activeBubbles[i].gameObject);
                activeBubbles.RemoveAt(i);
                targetYPositions.RemoveAt(i);
            }
        }
    }
}
