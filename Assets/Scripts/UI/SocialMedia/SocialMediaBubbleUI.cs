using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SocialMediaBubbleUI : MonoBehaviour
{
    [Header("Kaynak")]
    [Tooltip("Tik: mock feed (MockSocialMediaFeedManager). Bos: gercek feed (SocialMediaManager).")]
    public bool useMock = false;

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
    public float slideSpeed = 5f;

    List<RectTransform> activeBubbles = new List<RectTransform>();
    List<float> targetYPositions = new List<float>();
    // her balonun scaled yuksekligi (overflow dahil, gorsel hesap icin)
    List<float> bubbleScaledHeights = new List<float>();

    void OnEnable()
    {
        if (useMock)
            MockSocialMediaFeedManager.OnNewMockPost += HandleMockPost;
        else
            SocialMediaManager.OnNewPost += HandleRealPost;
    }

    void OnDisable()
    {
        if (useMock)
            MockSocialMediaFeedManager.OnNewMockPost -= HandleMockPost;
        else
            SocialMediaManager.OnNewPost -= HandleRealPost;
    }

    void HandleMockPost(MockSocialMediaPost post)
    {
        HandleNewPost(post.authorName, post.content);
    }

    void HandleRealPost(SocialMediaPost post)
    {
        HandleNewPost(post.authorName, post.content);
    }

    void Update()
    {
        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            if (activeBubbles[i] == null)
            {
                activeBubbles.RemoveAt(i);
                targetYPositions.RemoveAt(i);
                bubbleScaledHeights.RemoveAt(i);
                continue;
            }

            Vector2 pos = activeBubbles[i].anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, targetYPositions[i], Time.deltaTime * slideSpeed);
            activeBubbles[i].anchoredPosition = pos;
        }
    }

    void HandleNewPost(string authorName, string content)
    {
        if (bubblePrefab == null || bubbleContainer == null) return;

        GameObject bubbleObj = Instantiate(bubblePrefab, bubbleContainer);
        RectTransform bubbleRect = bubbleObj.GetComponent<RectTransform>();

        // anchor'i alt-ortaya cek
        bubbleRect.anchorMin = new Vector2(0.5f, 0f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0f);

        // scale hesapla
        float containerWidth = bubbleContainer.rect.width;
        float targetWidth = containerWidth - panelLeftMargin - panelRightMargin;
        float originalWidth = bubbleRect.sizeDelta.x;
        float scaleFactor = targetWidth / originalWidth;

        // text'leri yaz (scale oncesi, orijinal olcekte)
        TMP_Text contentText = bubbleObj.transform.Find("ContentText")?.GetComponent<TMP_Text>();
        TMP_Text authorText = bubbleObj.transform.Find("AuthorText")?.GetComponent<TMP_Text>();

        if (contentText != null)
            contentText.text = content;
        if (authorText != null)
            authorText.text = authorName;

        // balon boyutuna DOKUNMA — content tasarsa, content'i yukari dogru tasir
        float extraHeight = 0f;
        if (contentText != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);
            contentText.ForceMeshUpdate();

            float contentAvailableHeight = contentText.rectTransform.rect.height;
            float contentPreferredHeight = contentText.preferredHeight;

            if (contentPreferredHeight > contentAvailableHeight)
            {
                extraHeight = contentPreferredHeight - contentAvailableHeight;
                // content text'in rect'ini yukari dogru genislet
                // overflow mode'u overflow olarak birak — text gorunsun
                contentText.overflowMode = TextOverflowModes.Overflow;
                // content rect'ini yukari buyut (anchoredPosition yukari kaydir)
                contentText.rectTransform.anchoredPosition += new Vector2(0, extraHeight * 0.5f);
                contentText.rectTransform.sizeDelta += new Vector2(0, extraHeight);
            }
        }

        // scale uygula
        bubbleRect.localScale = new Vector3(scaleFactor, scaleFactor, 1f);

        // balon orijinal scaled yuksekligi (balon boyutu degismedi)
        float baseScaledHeight = bubbleRect.sizeDelta.y * scaleFactor;
        // content tasmasi dahil toplam alan (stacking icin)
        float extraScaled = extraHeight * scaleFactor;
        float totalScaledHeight = baseScaledHeight + extraScaled;

        // mevcut balonlarin hedef pozisyonlarini yukari kaydir
        float shiftAmount = totalScaledHeight + spacingBetweenBubbles;
        for (int i = 0; i < targetYPositions.Count; i++)
        {
            targetYPositions[i] += shiftAmount;
        }

        // pozisyon: balonun alt kenari margin'de
        // ekstra content yukari dogru tastigindan, balonun pozisyonu ayni
        float xOffset = (panelLeftMargin - panelRightMargin) / 2f;
        float startY = panelBottomMargin + baseScaledHeight * 0.5f;

        bubbleRect.anchoredPosition = new Vector2(xOffset, startY);
        activeBubbles.Add(bubbleRect);
        targetYPositions.Add(startY);
        bubbleScaledHeights.Add(totalScaledHeight);

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
                bubbleScaledHeights.RemoveAt(i);
                continue;
            }

            float bubbleTop = targetYPositions[i] + bubbleScaledHeights[i] * 0.5f;
            if (bubbleTop > maxTop)
            {
                Destroy(activeBubbles[i].gameObject);
                activeBubbles.RemoveAt(i);
                targetYPositions.RemoveAt(i);
                bubbleScaledHeights.RemoveAt(i);
            }
        }
    }
}
