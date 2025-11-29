using UnityEngine; 
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class PuzzleController : MonoBehaviour
{
    public PuzzleGenerator generator;
    public ResultPanelController resultPanel;
    public int mapSize = 10;
    public float intervalSeconds = 5f;
    public int T = 100;

    private string savePath;
    private int t = 0;
    private int[] agentDifficulties;
    private MapGenerator mapGenerator;

    IPlayerAgent[] agents;

    void Start()
    {
        savePath = Path.Combine(Application.dataPath, "maze_records.csv");

        agents = new IPlayerAgent[]{
            // new BeginnerAgent(),
            // new NormalAgent(),
            new ExpertAgent()
        };

        Debug.Log("Initializing MapGenerator in PuzzleController...");

        mapGenerator = new MapGenerator();

        Debug.Log($"PuzzleController Start: mapSize={mapSize}, intervalSeconds={intervalSeconds}, T={T}");
        Debug.Log($"Agents count: {agents.Length}");

        agentDifficulties = new int[agents.Length];
        for (int i = 0; i < agentDifficulties.Length; i++){
            agentDifficulties[i] = 5;
        }
        StartCoroutine(GenerateLoop());
    }

    IEnumerator GenerateLoop()
    {
        // 準備初始地圖
        char[][,] currentMaps = new char[agents.Length][,];
        for (int i = 0; i < agents.Length; i++) {
            currentMaps[i] = mapGenerator.GenerateMap(mapSize);
        }

        while (t < T)
        {
            foreach (var obj in GameObject.FindObjectsOfType<GameObject>()) {
                if (obj.name.StartsWith("PuzzleRoot")) {
                    Destroy(obj);
                }
            }

            for (int i = 0; i < agents.Length; i++) {
                generator.GenerateFromCharArray(currentMaps[i], i);

                Debug.Log($"Agent {i} starting simulation...");

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

                GAEngine ga = new GAEngine(model){
                    populationSize = 300,
                    generations = 15,
                    crossoverRate = 0.8f,
                    mapSize = mapSize
                };
                
                char[,] bestMap = ga.Run(agentDifficulties[i]);
                currentMaps[i] = bestMap;
            }

            t++;
            yield return new WaitForSeconds(intervalSeconds); 
        }

        // 保留最後生成的地圖（無統計數據）
        for (int i = 0; i < agents.Length; i++) {
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
        string agentCsvPath = Path.Combine(Application.dataPath, $"maze_records_{tag}.csv");

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
