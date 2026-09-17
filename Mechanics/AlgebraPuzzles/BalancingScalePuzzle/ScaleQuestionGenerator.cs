using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The PMP (Procedural Math Puzzle) engine for the scale-balance puzzle.
///
/// Owns the static question bank loaded from ScalePuzzleData.json and knows
/// how to:
///   (a) hand out the next question for a given DifficultyLevel, and
///   (b) procedurally re-roll the numeric variables of a "templated"
///       question — instead of repeating the exact same static numbers —
///       whenever ScaleDDAController.EvaluateAnswer reports the player is
///       struggling.
///
/// Plain C# class owned by ScalePuzzleManager by composition, same pattern
/// as ScaleDDAController.
/// </summary>
public class ScaleQuestionGenerator
{
    private ScalePuzzleDatasetContainer dataset;
    private readonly Queue<string> recentIds = new Queue<string>();
    private const int HISTORY_SIZE = 3;
    private readonly System.Random rng = new System.Random();

    public bool IsLoaded => dataset != null;

    /// <summary>Parse the ScalePuzzleData.json text (e.g. from a TextAsset) into the question bank.</summary>
    public void LoadFromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[ScaleQuestionGenerator] No JSON supplied — dataset not loaded.");
            return;
        }

        dataset = JsonUtility.FromJson<ScalePuzzleDatasetContainer>(json);
        if (dataset == null)
            Debug.LogError("[ScaleQuestionGenerator] Failed to parse ScalePuzzleData.json.");
    }

    private List<ScaleQuestion> BucketFor(DifficultyLevel level)
    {
        if (dataset == null) return null;
        switch (level)
        {
            case DifficultyLevel.Easy: return dataset.easyQuestions;
            case DifficultyLevel.Medium: return dataset.mediumQuestions;
            default: return dataset.hardQuestions;
        }
    }

    /// <summary>
    /// Returns the next question to show the player for the given difficulty.
    /// If <paramref name="preferEasierVariant"/> is true (the DDA controller
    /// detected struggling on the previous answer) and the picked question
    /// has a regenerable "template", its variables are re-rolled with
    /// smaller/friendlier numbers instead of returning the exact same static
    /// question again. Non-templated questions (systems, absolute value,
    /// quadratics, etc.) are always served as-is from the dataset.
    /// </summary>
    public ScaleQuestion GetNextQuestion(DifficultyLevel level, bool preferEasierVariant)
    {
        var pool = BucketFor(level);
        if (pool == null || pool.Count == 0) return CreateFallback();

        // Avoid immediately repeating the last few question ids, same idea as
        // GameManager's recentQuestions history buffer.
        var candidates = pool.Where(q => !recentIds.Contains(q.id)).ToList();
        if (candidates.Count == 0) candidates = pool;

        var picked = candidates[rng.Next(candidates.Count)];

        recentIds.Enqueue(picked.id);
        while (recentIds.Count > HISTORY_SIZE) recentIds.Dequeue();

        if (!string.IsNullOrEmpty(picked.template))
        {
            var variant = RegenerateVariables(picked, preferEasierVariant);
            if (variant != null) return variant;
        }

        return picked;
    }

    // ── PMP variable regeneration ───────────────────────────────────────────
    // Each case picks fresh integers for the same algebraic pattern the
    // original dataset question used, then rebuilds problem text, pan
    // labels, the correct answer, and a small set of plausible distractors.
    private ScaleQuestion RegenerateVariables(ScaleQuestion baseQ, bool makeEasier)
    {
        int lo = 2;
        int hi = makeEasier ? 6 : 9;
        int aMax = makeEasier ? 4 : 6;

        int NextInt(int min, int max) => rng.Next(min, max + 1);
        int NonZero(int min, int max)
        {
            int v = NextInt(min, max);
            return v == 0 ? 1 : v;
        }

        try
        {
            switch (baseQ.template)
            {
                case "x_plus_b_eq_c":
                {
                    int x = NonZero(-hi, hi);
                    int b = NextInt(lo, hi);
                    int c = x + b;
                    return Build(baseQ, $"Solve: x + {b} = {c}", $"x + {b}", $"{c}",
                        $"x = {x}", new[] { $"x = {c}", $"x = {c + b}", $"x = {-x}" });
                }
                case "x_minus_b_eq_c":
                {
                    int x = NonZero(-hi, hi);
                    int b = NextInt(lo, hi);
                    int c = x - b;
                    return Build(baseQ, $"Solve: x - {b} = {c}", $"x - {b}", $"{c}",
                        $"x = {x}", new[] { $"x = {c}", $"x = {c - b}", $"x = {-x}" });
                }
                case "ax_eq_c":
                {
                    int a = NextInt(2, aMax);
                    int x = NonZero(-hi, hi);
                    int c = a * x;
                    return Build(baseQ, $"Solve: {a}x = {c}", $"{a}x", $"{c}",
                        $"x = {x}", new[] { $"x = {c}", $"x = {a * c}", $"x = {x + a}" });
                }
                case "x_div_a_eq_c":
                {
                    int a = NextInt(2, aMax);
                    int c = NextInt(lo, hi);
                    int x = a * c;
                    return Build(baseQ, $"Solve: x/{a} = {c}", $"x/{a}", $"{c}",
                        $"x = {x}", new[] { $"x = {c}", $"x = {c + a}", $"x = {x - a}" });
                }
                case "ax_plus_b_eq_c":
                {
                    int a = NextInt(2, aMax);
                    int x = NextInt(1, hi);
                    int b = NextInt(lo, hi);
                    int c = a * x + b;
                    return Build(baseQ, $"Solve: {a}x + {b} = {c}", $"{a}x + {b}", $"{c}",
                        $"x = {x}", new[] { $"x = {c - b}", $"x = {x + b}", $"x = {-x}" });
                }
                case "ax_minus_b_eq_c":
                {
                    int a = NextInt(2, aMax);
                    int x = NextInt(1, hi);
                    int b = NextInt(lo, hi);
                    int c = a * x - b;
                    return Build(baseQ, $"Solve: {a}x - {b} = {c}", $"{a}x - {b}", $"{c}",
                        $"x = {x}", new[] { $"x = {c + b}", $"x = {x - b}", $"x = {-x}" });
                }
                case "x_div_a_plus_b_eq_c":
                {
                    int a = NextInt(2, aMax);
                    int k = NextInt(1, hi); // x = a * k, kept whole-number
                    int b = NextInt(lo, hi);
                    int x = a * k;
                    int c = k + b;
                    return Build(baseQ, $"Solve: x/{a} + {b} = {c}", $"x/{a} + {b}", $"{c}",
                        $"x = {x}", new[] { $"x = {c}", $"x = {c - b}", $"x = {x - b}" });
                }
                case "x_plus_b_gt_c":
                {
                    int boundary = NextInt(1, hi);
                    int b = NextInt(lo, hi);
                    int c = boundary + b;
                    return Build(baseQ, $"Solve the inequality: x + {b} > {c}", $"x + {b}", $"> {c}",
                        $"x > {boundary}", new[] { $"x > {c}", $"x < {boundary}", $"x > {b}" });
                }
                default:
                    return null; // unrecognized template — fall back to the static question
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ScaleQuestionGenerator] Variable regeneration failed for {baseQ.id}: {e.Message}");
            return null;
        }
    }

    private ScaleQuestion Build(ScaleQuestion baseQ, string problemText, string leftPan, string rightPan,
        string correct, string[] wrongCandidates)
    {
        var options = new List<string> { correct };
        foreach (var w in wrongCandidates)
            if (!options.Contains(w)) options.Add(w);

        Shuffle(options);

        return new ScaleQuestion
        {
            id = baseQ.id + "_pmp",
            problemText = problemText,
            leftPanText = leftPan,
            rightPanText = rightPan,
            correctAnswer = correct,
            options = options.ToArray(),
            template = baseQ.template,
            coeffs = baseQ.coeffs
        };
    }

    private void Shuffle(List<string> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private ScaleQuestion CreateFallback()
    {
        Debug.LogWarning("[ScaleQuestionGenerator] Using fallback question — dataset missing or empty.");
        return new ScaleQuestion
        {
            id = "FALLBACK",
            problemText = "Solve: x + 1 = 2",
            leftPanText = "x + 1",
            rightPanText = "2",
            correctAnswer = "x = 1",
            options = new[] { "x = 1", "x = 2", "x = 0", "x = -1" }
        };
    }
}