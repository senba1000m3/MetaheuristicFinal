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

    [Header("GA 適應度分數權重")]
    [Range(0f, 1f)] public float pathLengthWeight = 1f;
    [Range(0f, 1f)] public float cornersWeight = 1f;
    [Range(0f, 1f)] public float emptySpaceWeight = 1f;
    [Range(0f, 1f)] public float pickupsWeight = 1f;
    [Range(0f, 1f)] public float orthogonalPickupsWeight = 1f;

    [Header("使用的 Agent 列表")]
    public List<IPlayerAgent> agents;

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
            foreach (var obj in GameObject.FindObjectsOfType<GameObject>()) {
                if (obj.name.StartsWith("PuzzleRoot")) {
                    Destroy(obj);
                }
            }

            for (int i = 0; i < agents.Count; i++) {
                generator.GenerateFromCharArray(currentMaps[i], i);

                Debug.Log($"Agent {i} starting simulation on map at iteration {t} with difficulty {agentDifficulties[i]}.");

                var stats = agents[i].Simulate(currentMaps[i]);

                PlayerModel model = new PlayerModel(
                    stats.attempts, 
                    stats.backtracks, 
                    stats.nearSolves, 
                    stats.resets, 
                    stats.timeTaken
                );

                SaveMapToCSV(currentMaps[i], model, agents[i].GetType().Name);

                int newDifficulty = model.SuggestDifficulty(agentDifficulties[i]);
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

                char[,] bestMap = ga.Run(agentDifficulties[i], weights);
                stopwatch.Stop();
                Debug.Log($"GAEngine.Run 花費時間: {stopwatch.ElapsedMilliseconds} ms");
                
                if (resultPanel != null)
                {
                    string resultText = $"Iter: {t + 1}/{T}\n" +
                                        $"Diff: {agentDifficulties[i]}, Att: {stats.attempts}\n" +
                                        $"Back: {stats.backtracks}, Near: {stats.nearSolves}\n" +
                                        $"Rst: {stats.resets}\n" +
                                        $"Time: {stats.timeTaken:F1}, GA Time: {stopwatch.ElapsedMilliseconds}ms\n" +
                                        $"Best Fit: {ga.BestFitness:F4}";
                    resultPanel.UpdateResult(i, resultText);
                }

                currentMaps[i] = bestMap;
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


    void SaveMapToCSV(char[,] map, PlayerModel player, string tag)
    {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        // 依 agent 名稱分檔
        string agentCsvPath = Path.Combine(Application.dataPath, $"map_records_{tag}.csv");

        using (StreamWriter writer = new StreamWriter(agentCsvPath, true))
        {
            if (player != null) {
                writer.WriteLine($"#{tag} PlayerData: Attempts={player.attempts}, Backtracks={player.backtracks}, NearSolves={player.nearSolves}, Resets={player.resets}, TimeTaken={player.timeTaken}, SuggestedDifficulty={player.SuggestDifficulty(5)}");
            }
            else {
                writer.WriteLine($"#{tag} PlayerData: (no stats, not played)");
            }

            for (int y = 0; y < rows; y++)
            {
                string line = "";
                for (int x = 0; x < cols; x++)
                {
                    line += map[y, x];
                    if (x < cols - 1) line += ",";
                }
                writer.WriteLine(line);
            }

            writer.WriteLine();
        }
    }
}
