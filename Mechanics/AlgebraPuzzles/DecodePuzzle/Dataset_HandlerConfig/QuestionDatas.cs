using System.Collections.Generic;

[System.Serializable]
public class Question
{
    public string category;
    public int difficulty;
    public string text;
    public string answer;
    public string explanation;
}

[System.Serializable]
public class QuestionTemplate
{
    public int id;
    public string topic;
    public int difficulty;
    public string pattern;
    public string answerPattern;
    public string hint;
    public int ddaLevel;
    public string explanation;
}

[System.Serializable]
public class TemplateList
{
    public List<QuestionTemplate> templates;
}