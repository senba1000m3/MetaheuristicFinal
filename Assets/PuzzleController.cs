using UnityEngine;
using System.Collections;
using System.IO;

public class PuzzleController : MonoBehaviour
{
    public PuzzleGenerator generator;
    public int mazeSize = 9;
    public float intervalSeconds = 5f; // 每隔幾秒生成一次

    private string savePath;

    void Start()
    {
        savePath = Path.Combine(Application.dataPath, "maze_records.csv");
        StartCoroutine(GenerateLoop());
    }

    IEnumerator GenerateLoop()
    {
        while (true)
        {
            // 假設玩家互動數據，可隨機或從實際遊玩取得
            PlayerModel player = new PlayerModel(
                attempts: Random.Range(1, 5),
                backtracks: Random.Range(0, 3),
                nearSolves: Random.Range(0, 3),
                resets: Random.Range(0, 3),
                timeTaken: Random.Range(5f, 60f)
            );

            // GA 生成迷宮
            GAEngine ga = new GAEngine(player)
            {
                populationSize = 30,
                generations = 15,
                mutationRate = 0.1f,
                mazeSize = mazeSize+2
            };

            char[,] bestMaze = ga.Run();

            // 生成 Prefab
            generator.GenerateFromCharArray(bestMaze);

            // 存 CSV
            SaveMazeToCSV(bestMaze, player);

            Debug.Log("Generated maze and saved CSV.");

            yield return new WaitForSeconds(intervalSeconds);
        }
    }

    void SaveMazeToCSV(char[,] maze, PlayerModel player)
    {
        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);

        using (StreamWriter writer = new StreamWriter(savePath, true)) // append
        {
            writer.WriteLine($"#PlayerData: Attempts={player.attempts}, Backtracks={player.backtracks}, NearSolves={player.nearSolves}, Resets={player.resets}, TimeTaken={player.timeTaken}, SuggestedDifficulty={player.SuggestDifficulty(5)}");

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

            writer.WriteLine(); // 空行分隔紀錄
        }
    }

    char[,] LoadMapFromCSV(string path){
        string[] lines = File.ReadAllLines(path);
        int size = lines.Length;
        char[,] map = new char[size, size];

        for(int y = 0; y < size; y++){
            string[] cells = lines[y].Split(',');
            for(int x = 0; x < size; x++){
                map[y, x] = cells[x][0];
            }
        }

        return map;
    }
}
