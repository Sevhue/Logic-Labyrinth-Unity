using System;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance;

    private Dictionary<string, PuzzleLevel> puzzleLevels = new Dictionary<string, PuzzleLevel>();
    private Dictionary<string, string> playerPuzzleAssignments = new Dictionary<string, string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePuzzleDatabase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializePuzzleDatabase()
    {
        
        var level1 = new PuzzleLevel
        {
            levelId = "Level1",
            difficulty = Difficulty.Easy,
            category = "BasicGates",
            variants = new PuzzleVariant[]
            {
                new PuzzleVariant
                {
                    variantId = "AND_1",
                    logicExpression = "F = A • B",
                    problemStatement = "Create a circuit where the output is HIGH only when both A AND B are HIGH",
                    availableGates = new GateType[] { GateType.AND },
                    difficulty = Difficulty.Easy,
                    rewardGates = 1,
                    truthTable = new TruthTable(
                        new string[] { "A", "B" },
                        "F",
                        new int[][] {
                            new int[] {0, 0, 0},
                            new int[] {0, 1, 0},
                            new int[] {1, 0, 0},
                            new int[] {1, 1, 1}
                        }
                    )
                },
                new PuzzleVariant
                {
                    variantId = "AND_2",
                    logicExpression = "F = B • C",
                    problemStatement = "Build a circuit that outputs HIGH when both switches B and C are ON",
                    availableGates = new GateType[] { GateType.AND },
                    difficulty = Difficulty.Easy,
                    rewardGates = 1,
                    truthTable = new TruthTable(
                        new string[] { "B", "C" },
                        "F",
                        new int[][] {
                            new int[] {0, 0, 0},
                            new int[] {0, 1, 0},
                            new int[] {1, 0, 0},
                            new int[] {1, 1, 1}
                        }
                    )
                },
                new PuzzleVariant
                {
                    variantId = "AND_3",
                    logicExpression = "F = X • Y",
                    problemStatement = "Design a circuit where the light turns on only when both X AND Y switches are activated",
                    availableGates = new GateType[] { GateType.AND },
                    difficulty = Difficulty.Easy,
                    rewardGates = 1,
                    truthTable = new TruthTable(
                        new string[] { "X", "Y" },
                        "F",
                        new int[][] {
                            new int[] {0, 0, 0},
                            new int[] {0, 1, 0},
                            new int[] {1, 0, 0},
                            new int[] {1, 1, 1}
                        }
                    )
                },
                new PuzzleVariant
                {
                    variantId = "AND_4",
                    logicExpression = "F = P • Q",
                    problemStatement = "Construct a logic circuit that outputs 1 only when both inputs P and Q are 1",
                    availableGates = new GateType[] { GateType.AND },
                    difficulty = Difficulty.Easy,
                    rewardGates = 1,
                    truthTable = new TruthTable(
                        new string[] { "P", "Q" },
                        "F",
                        new int[][] {
                            new int[] {0, 0, 0},
                            new int[] {0, 1, 0},
                            new int[] {1, 0, 0},
                            new int[] {1, 1, 1}
                        }
                    )
                },
                new PuzzleVariant
                {
                    variantId = "AND_5",
                    logicExpression = "F = M • N",
                    problemStatement = "The output should be true only when both M AND N are true simultaneously",
                    availableGates = new GateType[] { GateType.AND },
                    difficulty = Difficulty.Easy,
                    rewardGates = 1,
                    truthTable = new TruthTable(
                        new string[] { "M", "N" },
                        "F",
                        new int[][] {
                            new int[] {0, 0, 0},
                            new int[] {0, 1, 0},
                            new int[] {1, 0, 0},
                            new int[] {1, 1, 1}
                        }
                    )
                }
            }
        };

        
        var level2 = new PuzzleLevel
        {
            levelId = "Level2",
            difficulty = Difficulty.Easy,
            category = "BasicGates",
            variants = new PuzzleVariant[]
            {
                new PuzzleVariant
                {
                    variantId = "OR_NOT_1",
                    logicExpression = "F = A + B'",
                    problemStatement = "Output should be HIGH when A is ON OR B is OFF",
                    availableGates = new GateType[] { GateType.OR, GateType.NOT },
                    difficulty = Difficulty.Easy,
                    rewardGates = 1,
                    truthTable = new TruthTable(
                        new string[] { "A", "B" },
                        "F",
                        new int[][] {
                            new int[] {0, 0, 1},
                            new int[] {0, 1, 0},
                            new int[] {1, 0, 1},
                            new int[] {1, 1, 1}
                        }
                    )
                },
                new PuzzleVariant
                {
                    variantId = "OR_NOT_2",
                    logicExpression = "F = A' + B",
                    problemStatement = "The light should turn on when A is OFF OR B is ON",
                    availableGates = new GateType[] { GateType.OR, GateType.NOT },
                    difficulty = Difficulty.Easy,
                    rewardGates = 1,
                    truthTable = new TruthTable(
                        new string[] { "A", "B" },
                        "F",
                        new int[][] {
                            new int[] {0, 0, 1},
                            new int[] {0, 1, 1},
                            new int[] {1, 0, 0},
                            new int[] {1, 1, 1}
                        }
                    )
                },
                
                new PuzzleVariant
                {
                    variantId = "OR_NOT_3",
                    logicExpression = "F = X + Y'",
                    problemStatement = "Design a circuit that outputs HIGH when X is ON OR Y is OFF",
                    availableGates = new GateType[] { GateType.OR, GateType.NOT },
                    difficulty = Difficulty.Easy,
                    rewardGates = 1,
                    truthTable = new TruthTable(
                        new string[] { "X", "Y" },
                        "F",
                        new int[][] {
                            new int[] {0, 0, 1},
                            new int[] {0, 1, 0},
                            new int[] {1, 0, 1},
                            new int[] {1, 1, 1}
                        }
                    )
                },
                new PuzzleVariant
                {
                    variantId = "OR_NOT_4",
                    logicExpression = "F = P' + Q",
                    problemStatement = "The output should be true when P is OFF OR Q is ON",
                    availableGates = new GateType[] { GateType.OR, GateType.NOT },
                    difficulty = Difficulty.Easy,
                    rewardGates = 1,
                    truthTable = new TruthTable(
                        new string[] { "P", "Q" },
                        "F",
                        new int[][] {
                            new int[] {0, 0, 1},
                            new int[] {0, 1, 1},
                            new int[] {1, 0, 0},
                            new int[] {1, 1, 1}
                        }
                    )
                },
                new PuzzleVariant
                {
                    variantId = "OR_NOT_5",
                    logicExpression = "F = M + N'",
                    problemStatement = "Create a circuit where output is 1 when M is 1 OR N is 0",
                    availableGates = new GateType[] { GateType.OR, GateType.NOT },
                    difficulty = Difficulty.Easy,
                    rewardGates = 1,
                    truthTable = new TruthTable(
                        new string[] { "M", "N" },
                        "F",
                        new int[][] {
                            new int[] {0, 0, 1},
                            new int[] {0, 1, 0},
                            new int[] {1, 0, 1},
                            new int[] {1, 1, 1}
                        }
                    )
                }
            }
        };

        puzzleLevels.Add("Level1", level1);
        puzzleLevels.Add("Level2", level2);
    }

    public PuzzleVariant GetPuzzleForPlayer(string levelId, string playerId)
    {
        
        if (puzzleLevels.ContainsKey(levelId))
        {
            PuzzleLevel level = puzzleLevels[levelId];

            
            PuzzleVariant randomVariant = level.variants[UnityEngine.Random.Range(0, level.variants.Length)];

            Debug.Log($"🎲 RANDOMLY ASSIGNED {playerId} to {levelId} variant: {randomVariant.variantId}");
            Debug.Log($"📊 Total variants available: {level.variants.Length}");

            return randomVariant;
        }

        Debug.LogError($"Level {levelId} not found!");
        return null;
    }

    private PuzzleVariant GetVariantById(string variantId)
    {
        foreach (var level in puzzleLevels.Values)
        {
            foreach (var variant in level.variants)
            {
                if (variant.variantId == variantId)
                    return variant;
            }
        }
        return null;
    }

   
    public void CompletePuzzle(string puzzleId)
    {
        AccountManager.Instance.CompletePuzzle(puzzleId);
        Debug.Log($"Puzzle {puzzleId} marked as completed!");
    }

    public bool IsPuzzleCompleted(string puzzleId)
    {
        return AccountManager.Instance.IsPuzzleCompleted(puzzleId);
    }

   
    public void TestRandomization(string levelId = "Level1")
    {
        Debug.Log($"🔍 TESTING RANDOMIZATION FOR {levelId}:");
        for (int i = 0; i < 5; i++)
        {
            PuzzleVariant testVariant = GetPuzzleForPlayer(levelId, "test_player");
            Debug.Log($"Test {i + 1}: {testVariant.variantId} - {testVariant.problemStatement}");
        }
    }
}