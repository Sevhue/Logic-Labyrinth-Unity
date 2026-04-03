using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PuzzleUIManager : MonoBehaviour
{
    public static PuzzleUIManager Instance;

    [Header("UI Panels")]
    public GameObject puzzlePanel;

    [Header("Problem Display")]
    public TextMeshProUGUI problemText;
    public TextMeshProUGUI expressionText;

    [Header("Truth Table")]
    public Transform truthTableContent;
    public GameObject truthTableRowPrefab;

    [Header("Gate Buttons")]
    public Button andGateButton;
    public Button orGateButton;
    public Button notGateButton;

    [Header("Control Buttons")]
    public Button checkButton;
    public Button resetButton;
    public Button closeButton;

    [Header("Workspace")]
    public RectTransform workspaceArea;
    public GameObject gatePrefab;

    [Header("Result Message")]
    public GameObject resultMessagePanel;
    public TextMeshProUGUI resultMessageText;

    private PuzzleVariant currentPuzzle;
    private List<GameObject> placedGates = new List<GameObject>();
    private GameObject player;
    private List<MonoBehaviour> disabledScripts = new List<MonoBehaviour>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        
        if (puzzlePanel != null)
            puzzlePanel.SetActive(false);

        if (resultMessagePanel != null)
            resultMessagePanel.SetActive(false);

        
        if (checkButton != null) checkButton.onClick.AddListener(CheckSolution);
        if (resetButton != null) resetButton.onClick.AddListener(ResetWorkspace);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePuzzle);

        
        if (andGateButton != null) andGateButton.onClick.AddListener(() => CreateDraggableGate(GateType.AND));
        if (orGateButton != null) orGateButton.onClick.AddListener(() => CreateDraggableGate(GateType.OR));
        if (notGateButton != null) notGateButton.onClick.AddListener(() => CreateDraggableGate(GateType.NOT));

        
        player = GameObject.FindGameObjectWithTag("Player");
    }

    public void OpenPuzzle(PuzzleVariant puzzle)
    {
        currentPuzzle = puzzle;

        
        if (problemText != null) problemText.text = puzzle.problemStatement;
        if (expressionText != null) expressionText.text = $"Expression: {puzzle.logicExpression}";

        
        if (truthTableContent != null && truthTableRowPrefab != null)
            PopulateTruthTable(puzzle.truthTable);

        
        if (puzzlePanel != null)
            puzzlePanel.SetActive(true);

        
        SetUIMode(true);

        
        ResetWorkspace();

        Debug.Log($"Puzzle UI opened: {puzzle.variantId}");
    }

    private void SetUIMode(bool enableUI)
    {
        
        Cursor.visible = enableUI;
        Cursor.lockState = enableUI ? CursorLockMode.None : CursorLockMode.Locked;

        Debug.Log($"=== SETTING UI MODE: {enableUI} ===");

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("❌ PLAYER NOT FOUND WITH TAG 'Player'!");
                
                player = GameObject.Find("Player");
                if (player == null)
                {
                    Debug.LogError("❌ NO GAMEOBJECT CALLED 'Player' EITHER!");
                    return;
                }
                else
                {
                    Debug.Log("✅ Found player by name 'Player'");
                }
            }
            else
            {
                Debug.Log("✅ Found player by tag 'Player'");
            }
        }

        Debug.Log($"Player object: {player.name}");

        
        Component[] allComponents = player.GetComponents<Component>();
        Debug.Log($"=== COMPONENTS ON PLAYER ===");
        foreach (Component comp in allComponents)
        {
            Debug.Log($"{comp.GetType().Name} - Enabled: {(comp is Behaviour behaviour ? behaviour.enabled : "N/A")}");
        }

        if (enableUI)
        {
            
            MonoBehaviour[] scripts = player.GetComponents<MonoBehaviour>();
            disabledScripts.Clear();

            Debug.Log($"=== DISABLING SCRIPTS ===");
            foreach (MonoBehaviour script in scripts)
            {
                if (script != null && script.enabled && script != this)
                {
                    disabledScripts.Add(script);
                    script.enabled = false;
                    Debug.Log($"❌ DISABLED: {script.GetType().Name}");
                }
            }

            
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                Debug.Log($"❌ DISABLED: CharacterController");
            }

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                Debug.Log($"❌ FROZE: Rigidbody (isKinematic = true)");
            }
        }
        else
        {
            Debug.Log($"=== RE-ENABLING SCRIPTS ===");
            
            foreach (MonoBehaviour script in disabledScripts)
            {
                if (script != null)
                {
                    script.enabled = true;
                    Debug.Log($"✅ RE-ENABLED: {script.GetType().Name}");
                }
            }
            disabledScripts.Clear();

            
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = true;
                Debug.Log($"✅ RE-ENABLED: CharacterController");
            }

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                Debug.Log($"✅ UNFROZE: Rigidbody (isKinematic = false)");
            }
        }

        Debug.Log($"=== UI MODE COMPLETE ===");
    }

    private void PopulateTruthTable(TruthTable truthTable)
    {
        
        foreach (Transform child in truthTableContent)
        {
            Destroy(child.gameObject);
        }

        
        GameObject headerRow = Instantiate(truthTableRowPrefab, truthTableContent);
        TextMeshProUGUI headerText = headerRow.GetComponentInChildren<TextMeshProUGUI>();

        string header = "";
        foreach (string input in truthTable.inputLabels)
        {
            header += input + " ";
        }
        header += "| " + truthTable.outputLabel;
        headerText.text = header;
        headerText.fontStyle = FontStyles.Bold;

        
        foreach (var combo in truthTable.combinations)
        {
            GameObject row = Instantiate(truthTableRowPrefab, truthTableContent);
            TextMeshProUGUI rowText = row.GetComponentInChildren<TextMeshProUGUI>();

            string rowString = "";
            for (int i = 0; i < truthTable.inputLabels.Length; i++)
            {
                rowString += combo[i] + " ";
            }
            rowString += "| " + combo[truthTable.inputLabels.Length];
            rowText.text = rowString;
        }
    }

    private void CreateDraggableGate(GateType gateType)
    {
        if (currentPuzzle == null || gatePrefab == null || workspaceArea == null) return;

        
        bool gateAvailable = false;
        foreach (var availableGate in currentPuzzle.availableGates)
        {
            if (availableGate == gateType)
            {
                gateAvailable = true;
                break;
            }
        }

        if (!gateAvailable)
        {
            Debug.LogWarning($"Gate {gateType} not available in this puzzle!");
            ShowResultMessage($"Gate {gateType} not available in this puzzle!", Color.yellow);
            return;
        }

        
        GameObject newGate = Instantiate(gatePrefab, workspaceArea);
        DraggableGate draggable = newGate.GetComponent<DraggableGate>();
        if (draggable != null)
        {
            draggable.Initialize(gateType);
        }

        placedGates.Add(newGate);
        Debug.Log($"Created {gateType} gate in workspace");
    }

    private void CheckSolution()
    {
        
        Debug.Log("Checking solution...");

        
        ShowResultMessage("Solution checking not implemented yet!", Color.yellow);
    }

    private void ResetWorkspace()
    {
       
        foreach (GameObject gate in placedGates)
        {
            if (gate != null) Destroy(gate);
        }
        placedGates.Clear();

        Debug.Log("Workspace reset");
    }

    
    public void ClosePuzzle()
    {
        if (puzzlePanel != null) puzzlePanel.SetActive(false);
        if (resultMessagePanel != null) resultMessagePanel.SetActive(false);
        ResetWorkspace();

        
        SetUIMode(false);

        Debug.Log("Puzzle closed");
    }

    private void ShowResultMessage(string message, Color color)
    {
        if (resultMessagePanel != null && resultMessageText != null)
        {
            resultMessageText.text = message;
            resultMessageText.color = color;
            resultMessagePanel.SetActive(true);

            
            Invoke("HideResultMessage", 3f);
        }
        else
        {
            Debug.Log($"Result: {message}");
        }
    }

    private void HideResultMessage()
    {
        if (resultMessagePanel != null)
            resultMessagePanel.SetActive(false);
    }
}