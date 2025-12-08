using UnityEngine; 
using System.Collections;
using System.Collections.Generic;
using System.IO;

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

                    nextMap = ga.Run(agentDifficulties[i], weights);
                    stopwatch.Stop();
                    gaTime = stopwatch.ElapsedMilliseconds;
                    bestFitness = ga.BestFitness;
                    Debug.Log($"GAEngine.Run 花費時間: {gaTime} ms");
                }
                else
                {
                    // 不使用 GA，直接生成相同難度的新地圖
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
                                        $"Back: {model.backtracks}, Near: {model.nearSolves}\n" +
                                        $"Rst: {model.resets}\n" +
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

    // MoveAgent moved to IPlayerAgent
}
