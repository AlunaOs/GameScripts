using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class TopicManager : MonoBehaviour
{
    public enum Subject { Algebra, Geometry }

    [System.Serializable]
    public class Topic
    {
        public string title;
        [TextArea(6, 12)]
        public string content;
    }

    [Header("Active State")]
    public Subject currentSubject = Subject.Algebra;
    private int currentRankIndex = 0; // 0 = Bronze, 1 = Silver, 2 = Gold, 3 = Diamond

    [Header("UI References")]
    public List<TextMeshProUGUI> topicDisplayTexts;
    public ScrollRect scrollRect;

    [Header("Algebra Topics List")]
    public List<Topic> algebraBronzeTopics = new List<Topic>();
    public List<Topic> algebraSilverTopics = new List<Topic>();
    public List<Topic> algebraGoldTopics = new List<Topic>();
    public List<Topic> algebraDiamondTopics = new List<Topic>();

    [Header("Geometry Topics List")]
    public List<Topic> geometryBronzeTopics = new List<Topic>();
    public List<Topic> geometrySilverTopics = new List<Topic>();
    public List<Topic> geometryGoldTopics = new List<Topic>();
    public List<Topic> geometryDiamondTopics = new List<Topic>();

    private void Start()
    {
        InitializeDefaultTopicsIfEmpty();
        ShowBronze();
        
    }

    // -------------------------------------------------------------
    // TOP-LEFT SWITCH BUTTON
    // -------------------------------------------------------------
    public void ToggleSubject()
    {
        currentSubject = (currentSubject == Subject.Algebra) ? Subject.Geometry : Subject.Algebra;
        RefreshCurrentRank();
    }

    private void RefreshCurrentRank()
    {
        switch (currentRankIndex)
        {
            case 0: ShowBronze(); break;
            case 1: ShowSilver(); break;
            case 2: ShowGold(); break;
            case 3: ShowDiamond(); break;
        }
    }

    // -------------------------------------------------------------
    // RANK BUTTON FUNCTIONS
    // -------------------------------------------------------------
    public void ShowBronze()  => DisplayTopicList(0, currentSubject == Subject.Algebra ? algebraBronzeTopics : geometryBronzeTopics);
    public void ShowSilver()  => DisplayTopicList(1, currentSubject == Subject.Algebra ? algebraSilverTopics : geometrySilverTopics);
    public void ShowGold()    => DisplayTopicList(2, currentSubject == Subject.Algebra ? algebraGoldTopics : geometryGoldTopics);
    public void ShowDiamond() => DisplayTopicList(3, currentSubject == Subject.Algebra ? algebraDiamondTopics : geometryDiamondTopics);

    private void DisplayTopicList(int rankIndex, List<Topic> topics)
    {
        currentRankIndex = rankIndex;

        if (topics == null || topics.Count == 0)
        {
            UpdateTopic("No Content Available", "Please add topics in the Inspector for this rank.");
            return;
        }

        // Combines all sub-topics into a single formatted layout for the rank
        string combinedContent = "";
        for (int i = 0; i < topics.Count; i++)
        {
            combinedContent += $"• <b>{topics[i].title}:</b><br>{topics[i].content}";
            if (i < topics.Count - 1) combinedContent += "<br><br>";
        }

        string rankName = GetRankName(rankIndex);
        UpdateTopic($"{currentSubject} - {rankName} Rank", combinedContent);
    }

    private string GetRankName(int index)
    {
        switch (index)
        {
            case 0: return "Bronze";
            case 1: return "Silver";
            case 2: return "Gold";
            case 3: return "Diamond";
            default: return "";
        }
    }

    private void UpdateTopic(string title, string content)
    {
        foreach (TextMeshProUGUI display in topicDisplayTexts)
        {
            if (display != null)
            {
                display.text = $"<b>{title}</b>\n\n{content}";
            }
        }

        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    // Pre-populates baseline content if lists are empty
    private void InitializeDefaultTopicsIfEmpty()
    {
        if (algebraBronzeTopics.Count == 0)
        {
            algebraBronzeTopics.Add(new Topic { title = "Real Numbers", content = "Encompasses all rational and irrational numbers on the continuous number line.<br><i>Example:</i> 1/2 and 3.14159." });
            algebraBronzeTopics.Add(new Topic { title = "Sets & Set Notation", content = "A collection of distinct objects or numbers.<br><i>Example:</i> If A = {1, 2} and B = {2, 3}, A U B = {1, 2, 3}." });
        }

        if (geometryBronzeTopics.Count == 0)
        {
            geometryBronzeTopics.Add(new Topic { title = "Points, Lines, & Planes", content = "The fundamental building blocks of geometry. A point marks a location, a line extends infinitely, and a plane is a flat 2D surface." });
            geometryBronzeTopics.Add(new Topic { title = "Angles & Measurement", content = "Formed by two rays sharing a vertex. Acute (<90°), Right (=90°), Obtuse (>90°), and Straight (=180°)." });
        }
    }
}