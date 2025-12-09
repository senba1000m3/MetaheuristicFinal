using UnityEngine; 
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public enum AgentType{
    BeginnerAgent,
    NormalAgent,
    ExpertAgent
}

public class PuzzleController : MonoBehaviour
    
{
    [Header("相關元件")]
    public PuzzleGenerator generator;
    public ResultPanelController resultPanel;

    [Header("初始設定參數")]
    public int mapSize = 10;
    public int T = 100;
    public float intervalSeconds = 5f;
    [Tooltip("初始難度")]
    public int initialDifficulty = 5;
       [Tooltip("是否啟用 GA 與玩家模型調整難度 (若關閉則一直生成相同難度的隨機地圖)")]
    public bool enableAdaptiveGA = true;

    [Tooltip("是否只根據時間調整難度 (忽略嘗試次數、回溯等)")]
    public bool timeOnlyMode = false;

    [Tooltip("是否啟用玩家遊玩模式 (無視 Agents)")]
    public bool playMode = false;
    [Tooltip("是否啟用 GA 視覺化演示 (僅 Expert Agent)")]
    public bool showGAViz = false;
    public AgentType targetAgentNameForViz;
    public GameObject humanPlayerPrefab;
    [Tooltip("指定場景中的 UI Text 物件來顯示計時器")]
    public Text playerTimerText;
    
    [Header("GA 適應度分數權重")]
    [Range(0f, 1f)] public float pathLengthWeight = 1f;
    [Range(0f, 1f)] public float cornersWeight = 1f;
    [Range(0f, 1f)] public float emptySpaceWeight = 1f;
    [Range(0f, 1f)] public float pickupsWeight = 1f;
    [Range(0f, 1f)] public float orthogonalPickupsWeight = 1f;

    [Header("使用的 Agent 列表")]
    public List<IPlayerAgent> agents;

    [Header("Agent Visualization")]
    public GameObject[] agentPrefabs;
    private List<GameObject> activeVisualAgents = new List<GameObject>();

    private string savePath;
    private int t = 0;
    private int[] agentDifficulties;
    private MapGenerator mapGenerator;
    private List<GameObject> gaVizObjects = new List<GameObject>();

    void Start()
    {     
        savePath = Path.Combine(Application.dataPath, "map_records.csv");


        Debug.Log("Initializing MapGenerator in PuzzleController...");

        mapGenerator = new MapGenerator();

        Debug.Log($"PuzzleController Start: mapSize={mapSize}, intervalSeconds={intervalSeconds}, T={T}");
        Debug.Log($"Agents count: {agents.Count}");

        agentDifficulties = new int[agents.Count];
        for (int i = 0; i < agentDifficulties.Length; i++){
            agentDifficulties[i] = initialDifficulty;
        }

        // Clear CSV files at start
        foreach(var agent in agents) {
            string path = Path.Combine(Application.dataPath, $"map_records_{agent.GetType().Name}.csv");
            if(File.Exists(path)) File.Delete(path);
            // Write header
            using (StreamWriter writer = new StreamWriter(path, false)) {
                writer.WriteLine("Iteration,Difficulty,Attempts,Backtracks,NearSolves,Resets,TimeTaken,GATime,BestFitness");
            }
        }

        StartCoroutine(GenerateLoop());
    }

    IEnumerator GenerateLoop()
    {
        if (playMode)
        {
            yield return StartCoroutine(PlayModeLoop());
            yield break;
        }

        // 準備初始地圖
        char[][,] currentMaps = new char[agents.Count][,];
        for (int i = 0; i < agents.Count; i++) {
            // Use initial difficulty to generate map
            var targets = PlayerModel.MapDifficultyToTargets(agentDifficulties[i]);
            int pickups = (int)Mathf.Round(targets["Pickups"]);
            int empty = (int)Mathf.Round(targets["EmptySpace"]);
            currentMaps[i] = mapGenerator.GenerateMap(mapSize, pickups, empty);
        }

        if (resultPanel != null)
        {
            resultPanel.UpdateGAInfo($"GA Settings: Pop: 300, Gen: 10, Size: {mapSize} Cross: 0.80, Mut: 0.30");
        }

        while (t < T)
        {
            // Clear old visual agents
            foreach(var agent in activeVisualAgents) {
                if(agent != null) Destroy(agent);
            }
            activeVisualAgents.Clear();

            foreach (var obj in GameObject.FindObjectsOfType<GameObject>()) {
                if (obj.name.StartsWith("PuzzleRoot")) {
                    Destroy(obj);
                }
            }
            generator.ClearCache();

            for (int i = 0; i < agents.Count; i++) {
                generator.GenerateFromCharArray(currentMaps[i], i);

                Debug.Log($"Agent {i} starting simulation on map at iteration {t} with difficulty {agentDifficulties[i]}.");

                PlayerModel model = agents[i].Simulate(currentMaps[i]);

                char[,] nextMap;
                float gaTime = 0f;
                float bestFitness = 0f;

                if (enableAdaptiveGA)
                {
                    int newDifficulty = model.SuggestDifficulty(agentDifficulties[i], timeOnlyMode);
                    Debug.Log($"Agent {i} difficulty changed: {agentDifficulties[i]} -> {newDifficulty}");
                    agentDifficulties[i] = newDifficulty;

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    // Define mutation rates for different agents
                    float mRate = 0.3f; // Default
                    if (agents[i] is BeginnerAgent) mRate = 0.01f;
                    else if (agents[i] is NormalAgent) mRate = 0.1f;
                    else if (agents[i] is ExpertAgent) mRate = 0.3f;

                    GAEngine ga = new GAEngine(model){
                        populationSize = 300,
                        generations = 10,
                        crossoverRate = 0.8f,
                        mutationRate = mRate,
                        mapSize = mapSize
                    };

                    var weights = new Dictionary<string, float> {
                        { "PathLength", pathLengthWeight },
                        { "Corners", cornersWeight },
                        { "EmptySpace", emptySpaceWeight },
                        { "Pickups", pickupsWeight },
                        { "OrthogonalPickups", orthogonalPickupsWeight }
                    };

                    if (showGAViz && agents[i].GetType().Name == targetAgentNameForViz.ToString())
                    {
                        ga.Initialize(agentDifficulties[i]);
                        for (int gen = 0; gen < ga.generations; gen++)
                        {
                            ga.Step(weights);
                            // Visualize the top specimens from the current generation (sorted by fitness)
                            VisualizeGAPopulation(ga.TopSpecimens);
                            yield return new WaitForSeconds(0.1f);
                        }
                        nextMap = ga.GetBestMap();
                        bestFitness = ga.BestFitness;
                    }
                    else
                    {
                        nextMap = ga.Run(agentDifficulties[i], weights);
                        bestFitness = ga.BestFitness;
                    }

                    stopwatch.Stop();
                    gaTime = stopwatch.ElapsedMilliseconds;
                    // bestFitness = ga.BestFitness;
                    Debug.Log($"GAEngine.Run 花費時間: {gaTime} ms");
                }
                else
                {
                    var targets = PlayerModel.MapDifficultyToTargets(agentDifficulties[i]);
                    MapGenerator mapGen = new MapGenerator();
                    nextMap = mapGen.GenerateMap(mapSize, (int)Mathf.Round(targets["Pickups"]), (int)Mathf.Round(targets["EmptySpace"]));
                    gaTime = 0f;
                    bestFitness = 0f;
                }
                
                if (resultPanel != null)
                {
                    string resultText = $"Iter: {t + 1}/{T}\n" +
                                        $"Diff: {agentDifficulties[i]}, Att: {model.attempts}\n" +
                                        $"Back: {model.backtracks}, Rst: {model.resets}\n" +
                                        $"Time: {model.timeTaken:F1}, GA Time: {gaTime}ms\n" +
                                        $"Best Fit: {bestFitness:F4}";
                    resultPanel.UpdateResult(i, resultText);
                }

                SaveMapToCSV(currentMaps[i], model, agents[i].GetType().Name, t+1, agentDifficulties[i], gaTime / 1000.0f, bestFitness);

                // Visualization
                if (agentPrefabs != null && i < agentPrefabs.Length && agentPrefabs[i] != null)
                {
                    float xOffset = i * (currentMaps[i].GetLength(1) + 2) * generator.cellSize;
                    GameObject visualAgent = Instantiate(agentPrefabs[i]);
                    activeVisualAgents.Add(visualAgent);
                    StartCoroutine(agents[i].MoveAgent(visualAgent, model.pathHistory, xOffset, 7f, currentMaps[i], i, generator));
                }

                currentMaps[i] = nextMap;
            }

            t++;
            yield return new WaitForSeconds(intervalSeconds); 
        }

        // 保留最後生成的地圖（無統計數據）
        foreach (var obj in GameObject.FindObjectsOfType<GameObject>()) {
            if (obj.name.StartsWith("PuzzleRoot")) {
                Destroy(obj);
            }
        }

        for (int i = 0; i < agents.Count; i++) {
            generator.GenerateFromCharArray(currentMaps[i], i);
            SaveMapToCSV(currentMaps[i], null, agents[i].GetType().Name);
            Debug.Log($"Final map generated and saved for agent {i} (no stats).");
        }
    }


    void VisualizeGAPopulation(List<char[,]> population)
    {
        // Clear previous visualization
        foreach (var obj in gaVizObjects)
        {
            if (obj != null) Destroy(obj);
        }
        gaVizObjects.Clear();

        // Settings for grid layout
        int showCount = Mathf.Min(population.Count, 25); // Only show top 25
        int cols = 5; // 5x5 grid
        float scale = 0.6f; // Larger scale since we show fewer
        float gap = 1f; // Gap between maps
        float mapWorldSize = mapSize * generator.cellSize * scale;
        
        // Start position offset (to the right of the main maps)
        // Assuming main maps take up some space, let's put this far to the right
        Vector3 startOrigin = new Vector3(60, 0, 0); 

        for (int j = 0; j < showCount; j++)
        {
            int vizIndex = 10000 + j; // Unique index to avoid conflict with main maps
            
            // Generate the map using the existing generator
            // Note: This might be slow for 300 maps. 
            // We rely on generator.GenerateFromCharArray creating a GameObject named "PuzzleRoot-{vizIndex}"
            generator.GenerateFromCharArray(population[j], vizIndex, false);
            
            GameObject root = GameObject.Find($"PuzzleRoot-{vizIndex}");
            if (root != null)
            {
                root.transform.localScale = new Vector3(scale, scale, scale);
                
                int row = j / cols;
                int col = j % cols;
                
                // Arrange in a grid
                Vector3 pos = startOrigin + new Vector3(col * (mapWorldSize + gap), 0, -row * (mapWorldSize + gap));
                root.transform.position = pos;
                
                gaVizObjects.Add(root);
            }
        }
    }

    void SaveMapToCSV(char[,] map, PlayerModel player, string tag, int iteration = -1, int difficulty = -1, float gaTime = 0f, float bestFit = 0f)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        // 依 agent 名稱分檔
        string agentCsvPath = Path.Combine(Application.dataPath, $"map_records_{tag}.csv");

        using (StreamWriter writer = new StreamWriter(agentCsvPath, true))
        {
            if (player != null) {
                // Write structured data line for analysis
                writer.WriteLine($"{iteration},{difficulty},{player.attempts},{player.backtracks},{player.nearSolves},{player.resets},{player.timeTaken},{gaTime},{bestFit}");
                
                writer.WriteLine("# Map Data Below");
                for (int y = 0; y < rows; y++)
                {
                    string line = "# ";
                    for (int x = 0; x < cols; x++)
                    {
                        line += map[y, x];
                        if (x < cols - 1) line += ",";
                    }
                    writer.WriteLine(line);
                }
            }
            else {
                writer.WriteLine($"# Final Map (No Stats)");
                for (int y = 0; y < rows; y++)
                {
                    string line = "# ";
                    for (int x = 0; x < cols; x++)
                    {
                        line += map[y, x];
                        if (x < cols - 1) line += ",";
                    }
                    writer.WriteLine(line);
                }
            }
            writer.WriteLine();
        }
    }

    IEnumerator PlayModeLoop()
    {
        // Initial map
        var targets = PlayerModel.MapDifficultyToTargets(initialDifficulty);
        int pickups = (int)Mathf.Round(targets["Pickups"]);
        int empty = (int)Mathf.Round(targets["EmptySpace"]);
        char[,] currentMap = mapGenerator.GenerateMap(mapSize, pickups, empty);
        int currentDifficulty = initialDifficulty;

        if (resultPanel != null)
        {
            resultPanel.UpdateGAInfo($"Play Mode Active. Size: {mapSize}");
        }

        while (t < T)
        {
            // Cleanup
            foreach (var obj in GameObject.FindObjectsOfType<GameObject>()) {
                if (obj.name.StartsWith("PuzzleRoot") || obj.name.Contains("HumanPlayer")) {
                    Destroy(obj);
                }
            }
            generator.ClearCache();
            
            // Generate Visuals
            // Use index 3 for Human Player to match the convention used in HumanPlayerController
            generator.GenerateFromCharArray(currentMap, 3);
            
            // Spawn Player
            GameObject playerObj;
            if (humanPlayerPrefab != null) {
                playerObj = Instantiate(humanPlayerPrefab);
            } else {
                playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerObj.name = "HumanPlayer";
                playerObj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                // Add a simple material if possible, or just rely on default
            }
            
            HumanPlayerController controller = playerObj.GetComponent<HumanPlayerController>();
            if (controller == null) controller = playerObj.AddComponent<HumanPlayerController>();
            
            // Assign timer text from controller
            controller.timerText = playerTimerText;

            // Initialize with index 3
            controller.Initialize(currentMap, generator, 3);
            
            Debug.Log($"Level {t+1} Started. Difficulty: {currentDifficulty}");

            // Wait for completion
            yield return new WaitUntil(() => controller.isLevelComplete);
            
            // Get Results
            PlayerModel model = controller.GetResults();
            
            // Calculate Next Map
            char[,] nextMap;
            float gaTime = 0f;
            float bestFitness = 0f;

            if (enableAdaptiveGA)
            {
                int newDifficulty = model.SuggestDifficulty(currentDifficulty, timeOnlyMode);
                Debug.Log($"Human Player difficulty changed: {currentDifficulty} -> {newDifficulty}");
                currentDifficulty = newDifficulty;

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                GAEngine ga = new GAEngine(model){
                    populationSize = 300,
                    generations = 10,
                    crossoverRate = 0.8f,
                    mapSize = mapSize
                };

                var weights = new Dictionary<string, float> {
                    { "PathLength", pathLengthWeight },
                    { "Corners", cornersWeight },
                    { "EmptySpace", emptySpaceWeight },
                    { "Pickups", pickupsWeight },
                    { "OrthogonalPickups", orthogonalPickupsWeight }
                };

                nextMap = ga.Run(currentDifficulty, weights);
                stopwatch.Stop();
                gaTime = stopwatch.ElapsedMilliseconds;
                bestFitness = ga.BestFitness;
                Debug.Log($"GAEngine.Run 花費時間: {gaTime} ms");
            }
            else
            {
                var t = PlayerModel.MapDifficultyToTargets(currentDifficulty);
                MapGenerator mapGen = new MapGenerator();
                nextMap = mapGen.GenerateMap(mapSize, (int)Mathf.Round(t["Pickups"]), (int)Mathf.Round(t["EmptySpace"]));
            }
            
            // Update UI
            if (resultPanel != null)
            {
                string resultText = $"Iter: {t + 1}/{T}\n" +
                                    $"Diff: {currentDifficulty}, Att: {model.attempts}\n" +
                                    $"Back: {model.backtracks}, Near: {model.nearSolves}\n" +
                                    $"Rst: {model.resets}\n" +
                                    $"Time: {model.timeTaken:F1}, GA Time: {gaTime}ms\n" +
                                    $"Best Fit: {bestFitness:F4}";
                resultPanel.UpdateResult(3, resultText); // Use slot 3 for Player
            }
            
            SaveMapToCSV(currentMap, model, "HumanPlayer", t+1, currentDifficulty, gaTime / 1000.0f, bestFitness);
            
            currentMap = nextMap;
            t++;
            
            // Small delay before next level
            yield return new WaitForSeconds(1f);
        }
    }
}
