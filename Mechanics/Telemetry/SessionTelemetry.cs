using System;
using System.Collections.Generic;

[Serializable]
public class AttemptData
{
    public string topic;
    public string tier;
    public int attempt_no;
    public bool correct;
    public float response_time_ms;
    public bool hint_used;
}

[Serializable]
public class SessionSummary
{
    public string final_rank;
    public int xp_total;
    public float accuracy_easy;
    public float accuracy_medium;
    public float accuracy_hard;
    public int mean_response_time_ms;
    public int hint_uses_total;
    public int difficulty_changes;
    public int session_duration_sec;
}

[Serializable]
public class SessionTelemetry
{
    public string session_code;
    public string date;
    public string dungeon;
    public List<AttemptData> attempts = new List<AttemptData>();
    public SessionSummary summary = new SessionSummary();
}