using System;
using System.Collections.Generic;

[Serializable]
public class PuzzleItem
{
    public int id;
    public string topic;
    public string question;
    public float correctAnswer;
    public float[] answerOptions;
    public string hint;          // ← NEW — how-to-solve guidance for the totem puzzle
}

[Serializable]
public class PuzzleDatabase
{
    public List<PuzzleItem> easy;
    public List<PuzzleItem> medium;
    public List<PuzzleItem> hard;
}
