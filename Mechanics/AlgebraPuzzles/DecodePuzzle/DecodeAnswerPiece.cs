using UnityEngine;

[System.Serializable]
public class DecodeAnswerPiece
{
    public string value;      
    public DecodePieceType type;     
}

public enum DecodePieceType
{
    Term,       
    Operator,   
    Number,     
    Variable,   
    Hindrance   
}