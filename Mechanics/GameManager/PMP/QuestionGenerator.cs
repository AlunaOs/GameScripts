using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System;

public class QuestionGenerator
{
    private List<QuestionTemplate> availableTemplates = new List<QuestionTemplate>();
    private Dictionary<int, List<QuestionTemplate>> difficultyBuckets = new Dictionary<int, List<QuestionTemplate>>();
    private Queue<string> recentQuestions = new Queue<string>();
    private const int HISTORY_SIZE = 3;

    public bool IsLoaded { get; private set; } = false;

    // =========================================================================
    //  TEMPLATE LOADING
    // =========================================================================

    public IEnumerator LoadTemplates()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "QuestionDatas.json");
        UnityWebRequest request = UnityWebRequest.Get(path);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            TemplateList templateDB = JsonUtility.FromJson<TemplateList>(request.downloadHandler.text);
            if (templateDB != null && templateDB.templates != null && templateDB.templates.Count > 0)
            {
                availableTemplates = templateDB.templates;
                OrganizeBuckets();
                IsLoaded = true;
                Debug.Log($"[QuestionGenerator] Loaded {availableTemplates.Count} templates successfully.");
            }
            else
            {
                Debug.LogError("[QuestionGenerator] JsonUtility returned null or empty template list.");
            }
        }
        else
        {
            Debug.LogError($"[QuestionGenerator] Failed to load JSON: {request.error}");
        }
    }

    private void OrganizeBuckets()
    {
        difficultyBuckets.Clear();
        for (int i = 1; i <= 3; i++)
            difficultyBuckets[i] = availableTemplates.Where(t => t.difficulty == i).ToList();
    }

    // =========================================================================
    //  QUESTION SELECTION & GENERATION
    // =========================================================================

    public Question GetQuestion(int level, string category)
    {
        if (!IsLoaded) return CreateFallbackQuestion();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            var template = GetRandomTemplate(level, category);
            if (template == null) return CreateFallbackQuestion();

            Question q = GenerateQuestionFromTemplate(template, level);
            if (q == null || string.IsNullOrEmpty(q.answer)) continue;

            recentQuestions.Enqueue(template.pattern);
            if (recentQuestions.Count > HISTORY_SIZE) recentQuestions.Dequeue();

            return q;
        }
        return CreateFallbackQuestion();
    }

    private QuestionTemplate GetRandomTemplate(int level, string category)
    {
        if (!difficultyBuckets.ContainsKey(level) || difficultyBuckets[level].Count == 0) return null;

        var pool = difficultyBuckets[level].Where(t => t.topic == category).ToList();
        if (pool.Count == 0) pool = difficultyBuckets[level];
        if (pool.Count == 0) return null;

        var available = pool.Where(t => !recentQuestions.Contains(t.pattern)).ToList();
        var finalPool = available.Count > 0 ? available : pool;
        return finalPool[UnityEngine.Random.Range(0, finalPool.Count)];
    }

    private Question GenerateQuestionFromTemplate(QuestionTemplate template, int level)
    {
        if (template == null) return CreateFallbackQuestion();

        int minVal = (level == 1) ? 2 : (level == 2) ? 3 : 1;
        int maxVal = (level == 1) ? 5 : (level == 2) ? 9 : 4;

        int a = UnityEngine.Random.Range(minVal, maxVal);
        int b = UnityEngine.Random.Range(minVal, maxVal);
        int c = UnityEngine.Random.Range(minVal, maxVal);
        int d = UnityEngine.Random.Range(minVal, maxVal);

        // Ensure division templates produce clean integer answers
        if (template.answerPattern == "b/a")
        {
            b = a * UnityEngine.Random.Range(1, maxVal / a + 1);
        }
        else if (template.answerPattern == "(c-b)/a")
        {
            int multiple = UnityEngine.Random.Range(1, 5);
            b = UnityEngine.Random.Range(minVal, maxVal - a);
            c = b + a * multiple;
        }
        else if (template.answerPattern == "(c+b)/a")
        {
            int multiple = UnityEngine.Random.Range(2, 5);
            b = UnityEngine.Random.Range(minVal, maxVal);
            c = a * multiple - b;
            if (c < 1) c = a;
        }
        else if (template.answerPattern == "(a/b)*x")
        {
            b = UnityEngine.Random.Range(2, 5);
            a = b * UnityEngine.Random.Range(1, 4);
        }

        if (template.answerPattern.Contains("a^3") || template.answerPattern.Contains("x^2 - a*a"))
        {
            while (b == a) b = UnityEngine.Random.Range(minVal, maxVal);
        }

        var vars = new Dictionary<string, int>
        {
            { "a", a }, { "b", b }, { "c", c }, { "d", d }
        };

        var extVars = new Dictionary<string, string>
        {
            { "{a_sq}",  (a * a).ToString() },
            { "{two_a}", (2 * a).ToString() }
        };

        string questionText = template.pattern;
        foreach (var kv in extVars)
            questionText = questionText.Replace(kv.Key, kv.Value);

        questionText = ResolveCompoundPlaceholders(questionText, vars);
        questionText = ReplaceSinglePlaceholders(questionText, vars);

        string answerText = BuildAnswer(template, vars, a, b, c, d);

        if (string.IsNullOrEmpty(questionText) || string.IsNullOrEmpty(answerText))
            return CreateFallbackQuestion();

        string explanation = GenerateExplanation(template, vars, answerText);

        // ── NEW: build the hint string with the same token substitution as explanation.
        // The hint panel reads this field — never the explanation — so the answer
        // is never revealed before the player solves the question.
        string hint = GenerateHint(template, vars);

        return new Question
        {
            category = template.topic,
            difficulty = template.difficulty,
            text = questionText,
            answer = answerText,
            hint = hint,               // ← NEW
            explanation = explanation
        };
    }

    // =========================================================================
    //  ANSWER & EXPLANATION BUILDERS
    // =========================================================================

    private string BuildAnswer(QuestionTemplate template, Dictionary<string, int> vars, int a, int b, int c, int d)
    {
        string ap = template.answerPattern;
        bool isSymbolic = ap.Contains("x") || ap.Contains("y") || ap.Contains("^")
                       || ap.Contains("sqrt") || ap.Contains("|") || ap.Contains("or")
                       || ap.Contains("gcd") || ap.Contains("p*") || ap.Contains("(p");
        return isSymbolic
            ? BuildSymbolicAnswer(template, vars, a, b, c, d)
            : BuildNumericAnswer(ap, vars);
    }

    private string BuildNumericAnswer(string answerPattern, Dictionary<string, int> vars)
    {
        string expr = SubstituteVarsInAnswer(answerPattern, vars);
        return EvaluateArithmetic(expr);
    }

    private string BuildSymbolicAnswer(QuestionTemplate template, Dictionary<string, int> vars, int a, int b, int c, int d)
    {
        switch (template.id)
        {
            case 1: return $"n + {a}";
            case 2: return "2x";
            case 3: return $"y - {a}";
            case 4: return $"{a}m";
            case 5: return $"k/{a}";
            case 6: return $"2p + {b}";
            case 7: return "n²";
            case 8: return a.ToString();
            case 9: return $"-{c}";
            case 10:
                {
                    int quad = a - c;
                    int lin = b + 1;
                    return quad == 0 ? $"{lin}x" : $"{quad}x² + {lin}x";
                }
            case 11: return (b * b - a * b + 1).ToString();
            case 12: return $"{a * b}x - {a * c}";
            case 13: return $"{a * c}x" + ToSuperscript(b + d);
            case 14: return $"{a * a}x² - {2 * a * b}x + {b * b}";
            case 15: return $"x² - {a * a}";
            case 16: return $"x² + {a + b}x + {a * b}";
            case 17:
                int quadCoeff = a - c;
                int linearCoeff = a * b;
                return quadCoeff == 0 ? $"{linearCoeff}x" : $"{quadCoeff}x² + {linearCoeff}x";

            default:
                return SubstituteVarsInAnswer(template.answerPattern, vars);
        }
    }

    private string GenerateExplanation(QuestionTemplate template, Dictionary<string, int> vars, string answerText)
    {
        if (string.IsNullOrEmpty(template.explanation)) return "No explanation available.";
        string exp = template.explanation;
        exp = ResolveCompoundPlaceholders(exp, vars);
        exp = ReplaceSinglePlaceholders(exp, vars);
        exp = exp.Replace("[answer]", answerText);
        return exp;
    }

    // ── NEW: build the hint the same way we build the explanation.
    // The hint NEVER contains the answer. It only guides.
    private string GenerateHint(QuestionTemplate template, Dictionary<string, int> vars)
    {
        if (string.IsNullOrEmpty(template.hint)) return "";

        string h = template.hint;
        h = ResolveCompoundPlaceholders(h, vars);   // e.g. {a*b} → 12
        h = ReplaceSinglePlaceholders(h, vars);     // e.g. {a} → 3
        return h;
    }

    // =========================================================================
    //  MATH EVALUATION & STRING PARSING HELPERS
    // =========================================================================

    private string ResolveCompoundPlaceholders(string text, Dictionary<string, int> vars)
    {
        return Regex.Replace(text, @"\{([^}]+)\}", m =>
        {
            string inner = m.Groups[1].Value;
            if (inner.Length == 1 && vars.ContainsKey(inner)) return m.Value;
            string expr = inner;
            expr = Regex.Replace(expr, @"(\d)([a-d])", "$1*$2");
            foreach (var kv in vars)
                expr = Regex.Replace(expr, $@"\b{kv.Key}\b", kv.Value.ToString());
            return EvaluateArithmetic(expr);
        });
    }

    private string ReplaceSinglePlaceholders(string text, Dictionary<string, int> vars)
    {
        foreach (var kv in vars)
            text = text.Replace("{" + kv.Key + "}", kv.Value.ToString());
        return text;
    }

    private string SubstituteVarsInAnswer(string pattern, Dictionary<string, int> vars)
    {
        if (string.IsNullOrEmpty(pattern)) return "";
        foreach (var kvp in vars)
        {
            pattern = pattern.Replace("{" + kvp.Key + "}", kvp.Value.ToString());
        }
        return pattern;
    }

    private string ToSuperscript(int number)
    {
        string str = number.ToString();
        string superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        string result = "";
        foreach (char ch in str)
        {
            if (ch >= '0' && ch <= '9')
                result += superscripts[ch - '0'];
            else
                result += ch;
        }
        return result;
    }

    private string EvaluateArithmetic(string expr)
    {
        expr = expr.Trim();
        try
        {
            var result = new System.Data.DataTable().Compute(expr, null);
            double dv = Convert.ToDouble(result);
            if (Math.Abs(dv - Math.Round(dv)) < 0.0001) return ((int)Math.Round(dv)).ToString();
            return Math.Round(dv, 2).ToString();
        }
        catch { return expr; }
    }

    private string FormatCoeffVar(int coeff, string varName)
    {
        if (coeff == 0) return "0";
        if (coeff == 1) return varName;
        if (coeff == -1) return $"-{varName}";
        return $"{coeff}{varName}";
    }

    private string FormatCoeffVarSigned(int coeff, string varName)
    {
        if (coeff == 0) return "";
        if (coeff > 0) return $"+ {FormatCoeffVar(coeff, varName)}";
        return $"- {FormatCoeffVar(Math.Abs(coeff), varName)}";
    }

    private int GCD(int a, int b)
    {
        while (b != 0) { int t = b; b = a % b; a = t; }
        return Math.Abs(a);
    }

    private string FactorQuadratic(int a, int b, int c)
    {
        int product = a * c;
        for (int i = -Math.Abs(product); i <= Math.Abs(product); i++)
        {
            if (i == 0 || product % i != 0) continue;
            int j = product / i;
            if (i + j != b) continue;
            if (i == 0 || j == 0) continue;
            for (int p1 = 1; p1 <= Math.Abs(a); p1++)
            {
                if (a % p1 != 0) continue;
                int r1 = a / p1;
                for (int q1 = -Math.Abs(c); q1 <= Math.Abs(c); q1++)
                {
                    if (q1 == 0 || c % q1 != 0) continue;
                    int s1 = c / q1;
                    if (p1 * s1 + q1 * r1 == b) return $"({p1}x + {q1})({r1}x + {s1})";
                }
            }
        }
        return null;
    }

    private Question CreateFallbackQuestion()
    {
        Debug.LogWarning("[QuestionGenerator] Using fallback question.");
        return new Question
        {
            category = "Algebra",
            difficulty = 1,
            text = "What is 1 + 1?",
            answer = "2",
            hint = "Add the two numbers together.",
            explanation = "1 + 1 = 2."
        };
    }

    public void ResetHistory()
    {
        recentQuestions.Clear();
    }
}
