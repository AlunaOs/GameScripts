using System;
using System.Collections.Generic;

[Serializable]
public enum DifficultyLevel
{
    Easy = 1,
    Medium = 2,
    Hard = 3
}

[Serializable]
public class ScaleQuestion
{
    public string id;
    public string topic;
    public string problemText;
    public string leftPanText;
    public string rightPanText;
    public string correctAnswer;
    public string[] options;
    public string template;

    public int[] coeffs;
}

[Serializable]
public class ScalePuzzleDatasetContainer
{
    public List<ScaleQuestion> easyQuestions;
    public List<ScaleQuestion> mediumQuestions;
    public List<ScaleQuestion> hardQuestions;
}