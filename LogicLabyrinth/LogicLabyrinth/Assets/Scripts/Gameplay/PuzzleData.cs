using System;
using System.Collections.Generic;

[System.Serializable]
public enum GateType
{
    AND,
    OR,
    NOT,
    WIRE
}

[System.Serializable]
public enum Difficulty
{
    Easy,
    Medium,
    Hard
}

[System.Serializable]
public class TruthTable
{
    public string[] inputLabels;
    public string outputLabel;
    public int[][] combinations; // [input1, input2, ..., output]

    public TruthTable(string[] inputs, string output, int[][] combos)
    {
        inputLabels = inputs;
        outputLabel = output;
        combinations = combos;
    }
}

[System.Serializable]
public class PuzzleVariant
{
    public string variantId;
    public string logicExpression;
    public string problemStatement;
    public TruthTable truthTable;
    public GateType[] availableGates;
    public Difficulty difficulty;
    public int rewardGates;
}

[System.Serializable]
public class PuzzleLevel
{
    public string levelId;
    public Difficulty difficulty;
    public PuzzleVariant[] variants;
    public string category;
}