using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class HumanPlayerController : MonoBehaviour
{
    public char[,] map;
    public PuzzleGenerator generator;
    public int agentIndex;
    
    private Vector2Int currentPos;
    private Vector2Int startPos;
    private Vector2Int endPos;
    
    private bool hasPackage = false;
    private HashSet<Vector2Int> usedPickups = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> usedDropoffs = new HashSet<Vector2Int>();
    
    // Stats
    public int attempts = 1;
    public int backtracks = 0;
    public int nearSolves = 0;
    public int resets = 0;
    public float startTime;
    public bool isLevelComplete = false;
    
    private List<Vector2Int> pathHistory = new List<Vector2Int>();
    private List<Vector2Int> currentAttemptPath = new List<Vector2Int>();
    private Dictionary<Vector2Int, Color> originalTileColors = new Dictionary<Vector2Int, Color>();
    private Dictionary<int, List<(char type, Vector2Int pos)>> stepInteractions = new Dictionary<int, List<(char type, Vector2Int pos)>>();

    private GameObject currentPuzzleRoot;
    [Tooltip("Assign a UI Text component here to display the timer")]
    public Text timerText;

    private Animator animator;
    private GameObject packageIndicator;

    private void Awake()
    {
       if (GameObject.Find("PlayerTimer") != null)
       {
           timerText = GameObject.Find("PlayerTimer").GetComponent<Text>();
       }
       animator = GetComponent<Animator>();
    }

    public void Initialize(char[,] map, PuzzleGenerator generator, int agentIndex)
    {
        this.map = map;
        this.generator = generator;
        this.agentIndex = agentIndex;
        
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);
        
        for (int y = 0; y < rows; y++) {
            for (int x = 0; x < cols; x++) {
                if (map[y, x] == 'S') startPos = new Vector2Int(x, y);
                else if (map[y, x] == 'E') endPos = new Vector2Int(x, y);
            }
        }
        
        // Find the puzzle root if it exists
        string rootName = GetPuzzleRootName(agentIndex);
        currentPuzzleRoot = GameObject.Find(rootName);

        ResetState(true);
        startTime = Time.time;
    }
    
    private string GetPuzzleRootName(int index)
    {
        string[] agentNames = {"Beginner", "Normal", "Expert"};
        return "PuzzleRoot-" + ((index >= 0 && index < agentNames.Length) ? agentNames[index] : index.ToString());
    }
    
    private void ResetState(bool firstTime)
    {
        currentPos = startPos;
        hasPackage = false;
        usedPickups.Clear();
        usedDropoffs.Clear();
        currentAttemptPath.Clear();
        currentAttemptPath.Add(currentPos);
        stepInteractions.Clear();
        
        if (!firstTime) {
            pathHistory.Add(currentPos); // Add reset point to history
        } else {
            pathHistory.Clear();
            pathHistory.Add(currentPos);
        }
        
        UpdatePosition();
        UpdateVisuals();
        
        // Regenerate map visuals on reset
        if (!firstTime)
        {
            if (currentPuzzleRoot != null) Destroy(currentPuzzleRoot);
            generator.GenerateFromCharArray(map, agentIndex);
            string rootName = GetPuzzleRootName(agentIndex);
            currentPuzzleRoot = GameObject.Find(rootName);
        }
        
        originalTileColors.Clear();
        MarkTileVisited(currentPos);
        
        CheckInteraction();

        if (animator != null) animator.SetBool("ifStart", false);
    }

    void Update()
    {
        if (isLevelComplete) return;

        // Update Timer
        if (timerText != null) {
            float t = Time.time - startTime;
            timerText.text = $"{t:F1}s";
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            resets++;
            attempts++;
            
            int remainingP = CountRemaining('P');
            int remainingD = CountRemaining('D');
            if ((remainingP + remainingD) <= 2 && (remainingP + remainingD) > 0) nearSolves++;
            
            ResetState(false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            Debug.Log("Level Skipped!");
            isLevelComplete = true;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (currentAttemptPath.Count > 1)
            {
                // Undo interactions first
                int currentStepIndex = currentAttemptPath.Count - 1;
                if (stepInteractions.ContainsKey(currentStepIndex)) {
                    var interactions = stepInteractions[currentStepIndex];
                    // Reverse order
                    for (int i = interactions.Count - 1; i >= 0; i--) {
                        var (type, pos) = interactions[i];
                        if (type == 'P') {
                            // Undo Pickup
                            hasPackage = false;
                            usedPickups.Remove(pos);
                            // Restore visual
                            float xOffset = agentIndex * (map.GetLength(1) + 2) * generator.cellSize;
                            Vector3 pPos = new Vector3(pos.x * generator.cellSize + xOffset, 0f, -pos.y * generator.cellSize);
                            generator.RestoreObjectAt(agentIndex, pos.x, pos.y, TileType.Pickup, pPos, Quaternion.identity);
                        } else if (type == 'D') {
                            // Undo Dropoff
                            hasPackage = true;
                            usedDropoffs.Remove(pos);
                            // Restore visual
                            float xOffset = agentIndex * (map.GetLength(1) + 2) * generator.cellSize;
                            Vector3 pPos = new Vector3(pos.x * generator.cellSize + xOffset, 0f, -pos.y * generator.cellSize);
                            generator.RestoreObjectAt(agentIndex, pos.x, pos.y, TileType.Dropoff, pPos, Quaternion.identity);
                        }
                    }
                    stepInteractions.Remove(currentStepIndex);
                    UpdateVisuals();
                }

                // Undo move
                Vector2Int current = currentAttemptPath[currentAttemptPath.Count - 1];
                UnmarkTileVisited(current);

                currentAttemptPath.RemoveAt(currentAttemptPath.Count - 1);
                Vector2Int prev = currentAttemptPath[currentAttemptPath.Count - 1];
                currentPos = prev;
                
                // We don't remove from pathHistory, we add the backtrack step
                pathHistory.Add(currentPos); 
                
                backtracks++;
                UpdatePosition();
            }
            return;
        }

        Vector2Int move = Vector2Int.zero;
        // Map mapping: W -> y-1 (North/Z+), S -> y+1 (South/Z-), A -> x-1 (West/X-), D -> x+1 (East/X+)
        if (Input.GetKeyDown(KeyCode.W)) move = new Vector2Int(0, -1);
        else if (Input.GetKeyDown(KeyCode.S)) move = new Vector2Int(0, 1);
        else if (Input.GetKeyDown(KeyCode.A)) move = new Vector2Int(-1, 0);
        else if (Input.GetKeyDown(KeyCode.D)) move = new Vector2Int(1, 0);

        if (move != Vector2Int.zero)
        {
            // Rotate character to face movement direction
            Vector3 lookDir = new Vector3(move.x, 0, -move.y);
            if (lookDir != Vector3.zero) {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
            // Set animator ifStart to false when first move from start
            if (animator != null && currentPos == startPos) animator.SetBool("ifStart", false);

            Vector2Int next = currentPos + move;
            if (IsValidMove(next))
            {
                currentPos = next;
                currentAttemptPath.Add(currentPos);
                pathHistory.Add(currentPos);
                MarkTileVisited(currentPos);
                UpdatePosition();
                CheckInteraction();
                CheckWin();
            }
        }
    }

    private bool IsValidMove(Vector2Int pos)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);
        if (pos.x < 0 || pos.x >= cols || pos.y < 0 || pos.y >= rows) return false;
        
        if (currentAttemptPath.Contains(pos)) return false;

        char c = map[pos.y, pos.x];
        
        return MazeSimUtils.IsWalkable(c);
    }

    private void UpdatePosition()
    {
        float xOffset = agentIndex * (map.GetLength(1) + 2) * generator.cellSize;
        transform.position = new Vector3(currentPos.x * generator.cellSize + xOffset, 0.5f, -currentPos.y * generator.cellSize);
    }

    private void UpdateVisuals()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = hasPackage ? Color.yellow : Color.blue;
        }
        // Handle package indicator
        if (hasPackage)
        {
            if (packageIndicator == null)
            {
                packageIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                packageIndicator.transform.SetParent(transform);
                packageIndicator.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                packageIndicator.transform.localPosition = new Vector3(0, 0.4f, 0.45f);
                var indRenderer = packageIndicator.GetComponent<Renderer>();
                if (indRenderer != null) indRenderer.material.color = Color.green;
            }
        }
        else
        {
            if (packageIndicator != null)
            {
                Destroy(packageIndicator);
                packageIndicator = null;
            }
        }

        // if (animator != null) animator.SetBool("ifStart", !hasPackage);
    }

    private void CheckInteraction()
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);
        
        int[] dx = {0, 0, 1, -1};
        int[] dy = {1, -1, 0, 0};
        
        bool actionTaken = false;
        int currentStepIndex = currentAttemptPath.Count - 1;
        if (!stepInteractions.ContainsKey(currentStepIndex)) {
            stepInteractions[currentStepIndex] = new List<(char, Vector2Int)>();
        }

        // 1. Try Drop
        if (hasPackage) {
            for(int k=0; k<4; k++) {
                int nx = currentPos.x + dx[k];
                int ny = currentPos.y + dy[k];
                if(nx >= 0 && nx < cols && ny >= 0 && ny < rows) {
                    char c = map[ny, nx];
                    Vector2Int target = new Vector2Int(nx, ny);
                    if (c == 'D' && !usedDropoffs.Contains(target)) {
                        hasPackage = false;
                        usedDropoffs.Add(target);
                        generator.RemoveObjectAt(agentIndex, nx, ny);
                        stepInteractions[currentStepIndex].Add(('D', target));
                        actionTaken = true;
                        break; 
                    }
                }
            }
        }

        // 2. Try Pick
        if (!hasPackage) {
            for(int k=0; k<4; k++) {
                int nx = currentPos.x + dx[k];
                int ny = currentPos.y + dy[k];
                if(nx >= 0 && nx < cols && ny >= 0 && ny < rows) {
                    char c = map[ny, nx];
                    Vector2Int target = new Vector2Int(nx, ny);
                    if (c == 'P' && !usedPickups.Contains(target)) {
                        hasPackage = true;
                        usedPickups.Add(target);
                        generator.RemoveObjectAt(agentIndex, nx, ny);
                        stepInteractions[currentStepIndex].Add(('P', target));
                        actionTaken = true;
                        break; 
                    }
                }
            }
        }

        // 3. Try Drop Again
        if (hasPackage) {
            for(int k=0; k<4; k++) {
                int nx = currentPos.x + dx[k];
                int ny = currentPos.y + dy[k];
                if(nx >= 0 && nx < cols && ny >= 0 && ny < rows) {
                    char c = map[ny, nx];
                    Vector2Int target = new Vector2Int(nx, ny);
                    if (c == 'D' && !usedDropoffs.Contains(target)) {
                        hasPackage = false;
                        usedDropoffs.Add(target);
                        generator.RemoveObjectAt(agentIndex, nx, ny);
                        stepInteractions[currentStepIndex].Add(('D', target));
                        actionTaken = true;
                        break;
                    }
                }
            }
        }
        
        if (actionTaken) UpdateVisuals();
    }

    private void CheckWin()
    {
        if (currentPos == endPos && !hasPackage)
        {
            // Check if all tasks done
            int remainingP = CountRemaining('P');
            int remainingD = CountRemaining('D');
            
            if (remainingP == 0 && remainingD == 0)
            {
                isLevelComplete = true;
                Debug.Log("Level Complete!");
                if (animator != null) animator.SetBool("ifStart", true);
            }
        }
    }

    private int CountRemaining(char type)
    {
        int count = 0;
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);
        for(int y=0; y<rows; y++) {
            for(int x=0; x<cols; x++) {
                if (map[y, x] == type) {
                    Vector2Int p = new Vector2Int(x, y);
                    if (type == 'P' && !usedPickups.Contains(p)) count++;
                    if (type == 'D' && !usedDropoffs.Contains(p)) count++;
                }
            }
        }
        return count;
    }
    
    public PlayerModel GetResults()
    {
        float totalTime = Time.time - startTime;
        PlayerModel model = new PlayerModel(attempts, backtracks, nearSolves, resets, totalTime);
        model.pathHistory = new List<Vector2Int>(pathHistory);
        return model;
    }

    private void MarkTileVisited(Vector2Int pos)
    {
        if (generator == null) return;
        
        // Don't overwrite the Start tile color
        if (pos == startPos) return;

        GameObject tile = generator.GetTileObject(agentIndex, pos.x, pos.y);
        if (tile != null)
        {
            Renderer r = tile.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                if (!originalTileColors.ContainsKey(pos))
                {
                    originalTileColors[pos] = r.material.color;
                }
                r.material.color = Color.blue;
            }
        }
    }

    private void UnmarkTileVisited(Vector2Int pos)
    {
        if (originalTileColors.ContainsKey(pos))
        {
            if (generator != null)
            {
                GameObject tile = generator.GetTileObject(agentIndex, pos.x, pos.y);
                if (tile != null)
                {
                    Renderer r = tile.GetComponentInChildren<Renderer>();
                    if (r != null)
                    {
                        r.material.color = originalTileColors[pos];
                    }
                }
            }
            originalTileColors.Remove(pos);
        }
    }
}
