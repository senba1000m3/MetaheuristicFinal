using UnityEngine; 
using System.Collections;
using System.IO;

public class PuzzleController : MonoBehaviour
{
    public PuzzleGenerator generator;
    public ResultPanelController resultPanel;
    public int mazeSize = 10;
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

        Debug.Log("Initializing MazeGenerator in PuzzleController...");

        mapGenerator = new MapGenerator();

        Debug.Log($"PuzzleController Start: mazeSize={mazeSize}, intervalSeconds={intervalSeconds}, T={T}");
        Debug.Log($"Agents count: {agents.Length}");

        agentDifficulties = new int[agents.Length];
        for (int i = 0; i < agentDifficulties.Length; i++){
            agentDifficulties[i] = 5;
        }
        StartCoroutine(GenerateLoop());
    }

    IEnumerator GenerateLoop()
    {
        while (t < T)
        {
            foreach (var obj in GameObject.FindObjectsOfType<GameObject>()) {
                if (obj.name.StartsWith("PuzzleRoot")) {
                    DestroyImmediate(obj);
                }
            }

            char[][,] mazes = new char[agents.Length][,];
            PlayerModel[] models = new PlayerModel[agents.Length];

            // 每個 agent 用自己的難度生成迷宮並模擬
            for (int i = 0; i < agents.Length; i++) {
                var targets = PlayerModel.MapDifficultyToTargets(agentDifficulties[i]);
                mazes[i] = mapGenerator.GenerateMap(mazeSize, targets);
                Debug.Log($"Agent {i} maze generated (difficulty={agentDifficulties[i]}, targets={string.Join(", ", targets)})");
                models[i] = agents[i].Simulate(mazes[i]);
                Debug.Log($"Simulation done: Attempts={models[i].attempts}, Backtracks={models[i].backtracks}, NearSolves={models[i].nearSolves}, Resets={models[i].resets}, TimeTaken={models[i].timeTaken}");
                if (resultPanel != null) {
                    string result = $"Difficulty: {agentDifficulties[i]}\nAttempts: {models[i].attempts}\nBacktracks: {models[i].backtracks}\nNearSolves: {models[i].nearSolves}\nResets: {models[i].resets}\nTimeTaken: {models[i].timeTaken:F2}";
                    resultPanel.UpdateResult(i, result);
                }
            }

            // 根據每個 agent 的建議調整自己的難度，並生成新迷宮
            for (int i = 0; i < agents.Length; i++) {
                int newDifficulty = models[i].SuggestDifficulty(agentDifficulties[i]);
                Debug.Log($"Agent {i} difficulty changed: {agentDifficulties[i]} -> {newDifficulty}");
                agentDifficulties[i] = newDifficulty;
                GAEngine ga = new GAEngine(models[i]){
                    populationSize = 30,
                    generations = 15,
                    mutationRate = 0.1f,
                    mazeSize = mazeSize
                };
                char[,] bestMaze = ga.Run();
                Debug.Log($"GAEngine run complete for agent {i}");
                generator.GenerateFromCharArray(bestMaze, i);
                SaveMazeToCSV(bestMaze, models[i], agents[i].GetType().Name);
                Debug.Log("Generated maze and saved CSV.");
            }

            t++;
            yield return new WaitForSeconds(intervalSeconds);
        }
    }


    void SaveMazeToCSV(char[,] maze, PlayerModel player, string tag)
    {
        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);

        using (StreamWriter writer = new StreamWriter(savePath, true))
        {
            writer.WriteLine($"#{tag} PlayerData: Attempts={player.attempts}, Backtracks={player.backtracks}, NearSolves={player.nearSolves}, Resets={player.resets}, TimeTaken={player.timeTaken}, SuggestedDifficulty={player.SuggestDifficulty(5)}");

            for (int y = 0; y < rows; y++)
            {
                string line = "";
                for (int x = 0; x < cols; x++)
                {
                    line += maze[y, x];
                    if (x < cols - 1) line += ",";
                }
                writer.WriteLine(line);
            }

            writer.WriteLine();
        }
    }
}
