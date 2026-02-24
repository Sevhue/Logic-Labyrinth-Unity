using System.Collections.Generic;

/// <summary>
/// Static configuration of puzzle answer keys for all levels.
/// InteractiveTable reads from here at runtime to auto-configure
/// the correct answer keys based on the current level.
/// 
/// Answer key format:
///   Each GateType[] represents one question.
///   Element 0 → Box1, Element 1 → Box2, etc.
///
/// LEVEL 1 (Tutorial) — single question:
///   Q1: OR OR AND
///
/// LEVEL 2 — five questions:
///   Q1: NOT OR OR AND           (4 boxes)
///   Q2: NOT NOT OR OR AND       (5 boxes)
///   Q3: NOT NOT OR OR AND       (5 boxes)
///   Q4: NOT NOT OR OR AND       (5 boxes)
///   Q5: NOT NOT NOT OR OR AND   (6 boxes)
///
/// LEVEL 3 — single question:
///   Q1: AND AND OR
///
/// LEVEL 4 — five questions:
///   Q1: NOT AND AND OR           (4 boxes)
///   Q2: NOT NOT AND AND OR       (5 boxes)
///   Q3: NOT NOT AND AND OR       (5 boxes)
///   Q4: NOT NOT AND AND OR       (5 boxes)
///   Q5: NOT NOT NOT AND AND OR   (6 boxes)
/// </summary>
public static class AnswerKeyConfig
{
    /// <summary>
    /// Returns the answer keys for the given level number (1-based).
    /// Each inner array is one question's answer key (Box1, Box2, ...).
    /// Single-question levels return an array with 1 element.
    /// </summary>
    public static GateType[][] GetAnswerKeys(int level)
    {
        switch (level)
        {
            case 1:
                return new GateType[][]
                {
                    // Q1: OR OR AND
                    new GateType[] { GateType.OR, GateType.OR, GateType.AND }
                };

            case 2:
                return new GateType[][]
                {
                    // Q1: NOT OR OR AND
                    new GateType[] { GateType.NOT, GateType.OR, GateType.OR, GateType.AND },
                    // Q2: NOT NOT OR OR AND
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.OR, GateType.OR, GateType.AND },
                    // Q3: NOT NOT OR OR AND
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.OR, GateType.OR, GateType.AND },
                    // Q4: NOT NOT OR OR AND
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.OR, GateType.OR, GateType.AND },
                    // Q5: NOT NOT NOT OR OR AND
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.NOT, GateType.OR, GateType.OR, GateType.AND }
                };

            case 3:
                return new GateType[][]
                {
                    // Q1: AND AND OR
                    new GateType[] { GateType.AND, GateType.AND, GateType.OR }
                };

            case 4:
                return new GateType[][]
                {
                    // Q1: NOT AND AND OR
                    new GateType[] { GateType.NOT, GateType.AND, GateType.AND, GateType.OR },
                    // Q2: NOT NOT AND AND OR
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.AND, GateType.AND, GateType.OR },
                    // Q3: NOT NOT AND AND OR
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.AND, GateType.AND, GateType.OR },
                    // Q4: NOT NOT AND AND OR
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.AND, GateType.AND, GateType.OR },
                    // Q5: NOT NOT NOT AND AND OR
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.NOT, GateType.AND, GateType.AND, GateType.OR }
                };

            case 6:
                return new GateType[][]
                {
                    // Q1: (A + B̅)(B̅ + C)(A + C) -> 2 NOT, 3 OR, 2 AND
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.OR, GateType.OR, GateType.OR, GateType.AND, GateType.AND },
                    // Q2: (A·B̅) + (B̅·C) + (A·C) -> 2 NOT, 3 AND, 2 OR
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.AND, GateType.AND, GateType.AND, GateType.OR, GateType.OR },
                    // Q3: (A̅ + B)(B + C̅)(A + C) -> 2 NOT, 3 OR, 2 AND
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.OR, GateType.OR, GateType.OR, GateType.AND, GateType.AND },
                    // Q4: (A̅·B) + (B·C̅) + (A·C) -> 2 NOT, 3 AND, 2 OR
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.AND, GateType.AND, GateType.AND, GateType.OR, GateType.OR },
                    // Q5: (A̅ + C̅)(B + C)(A + B) -> 2 NOT, 3 OR, 2 AND
                    new GateType[] { GateType.NOT, GateType.NOT, GateType.OR, GateType.OR, GateType.OR, GateType.AND, GateType.AND }
                };

            default:
                UnityEngine.Debug.LogWarning($"[AnswerKeyConfig] No answer keys defined for level {level}. Using default (OR OR AND).");
                return new GateType[][]
                {
                    new GateType[] { GateType.OR, GateType.OR, GateType.AND }
                };
        }
    }

    /// <summary>
    /// Returns true if the level has multiple questions (Q1-Q5).
    /// </summary>
    public static bool IsMultiQuestion(int level)
    {
        return GetAnswerKeys(level).Length > 1;
    }
}
