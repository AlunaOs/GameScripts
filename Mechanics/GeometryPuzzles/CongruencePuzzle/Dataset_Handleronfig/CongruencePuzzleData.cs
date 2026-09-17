using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

[Serializable]
public class CongruenceQuestionData
{
    public string questionId;
    public int questionNumber;
    public string difficulty; // Matches "easy", "medium", or "hard" in JSON
    public string questionText;
    public string[] options;
    public int correctAnswerIndex;
    public string hintText;

    // 3D Geometry Vertices
    public SerializableVector3[] triangleA_Vertices;
    public SerializableVector3[] triangleB_Vertices;
}

[Serializable]
public class CongruencePuzzleData
{
    public List<CongruenceQuestionData> questions;
}