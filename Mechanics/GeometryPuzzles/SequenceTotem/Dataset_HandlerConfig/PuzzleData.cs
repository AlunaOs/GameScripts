using System;
using System.Collections.Generic;

[Serializable]
public class PuzzleItem
{
    public int id;
    public string topic;
    public string question;
    public float correctAnswer; // The target numerical degree (e.g., 55, 70, 180)
    public float[] answerOptions; // Preset pool of choices shown on rotation (e.g., [30, 55, 90, 120, 180])
}

[Serializable]
public class PuzzleDatabase
{
    public List<PuzzleItem> easy;
    public List<PuzzleItem> medium;
    public List<PuzzleItem> hard;
}