using UnityEngine;

[System.Serializable]
public class CircuitQuestionData
{
    public string expression;
    public int requiredAnd;
    public int requiredOr;
    public int requiredNot;
}

/// <summary>
/// Expression + required gate metadata used by puzzle UI.
/// Overbar notation is represented using combining overline (e.g. A̅, B̅, C̅).
/// </summary>
public static class LevelExpressionConfig
{
    public static CircuitQuestionData[] GetQuestions(int level)
    {
        switch (level)
        {
            case 6:
                return new CircuitQuestionData[]
                {
                    new CircuitQuestionData
                    {
                        expression = "(A + B\u0305)(B\u0305 + C)(A + C)",
                        requiredAnd = 2,
                        requiredOr = 3,
                        requiredNot = 2
                    },
                    new CircuitQuestionData
                    {
                        expression = "(A \u00b7 B\u0305) + (B\u0305 \u00b7 C) + (A \u00b7 C)",
                        requiredAnd = 3,
                        requiredOr = 2,
                        requiredNot = 2
                    },
                    new CircuitQuestionData
                    {
                        expression = "(A\u0305 + B)(B + C\u0305)(A + C)",
                        requiredAnd = 2,
                        requiredOr = 3,
                        requiredNot = 2
                    },
                    new CircuitQuestionData
                    {
                        expression = "(A\u0305 \u00b7 B) + (B \u00b7 C\u0305) + (A \u00b7 C)",
                        requiredAnd = 3,
                        requiredOr = 2,
                        requiredNot = 2
                    },
                    new CircuitQuestionData
                    {
                        expression = "(A\u0305 + C\u0305)(B + C)(A + B)",
                        requiredAnd = 2,
                        requiredOr = 3,
                        requiredNot = 2
                    }
                };
            default:
                return new CircuitQuestionData[]
                {
                    new CircuitQuestionData
                    {
                        expression = string.Empty,
                        requiredAnd = 0,
                        requiredOr = 0,
                        requiredNot = 0
                    }
                };
        }
    }

    public static CircuitQuestionData GetQuestion(int level, int questionIndex)
    {
        CircuitQuestionData[] all = GetQuestions(level);
        if (all == null || all.Length == 0) return null;

        if (questionIndex < 0 || questionIndex >= all.Length)
            return all[0];

        return all[questionIndex];
    }
}
