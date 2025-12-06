using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GAEngine
{
    public int populationSize = 30; // 建議設為 30~50 即可，300 對 BFS 負擔太重
    public int generations = 10;
    public int mapSize = 10;
    public float crossoverRate = 0.8f;
    public float mutationRate = 0.05f; //稍微提高突變率

    private List<char[,]> population;
    private PlayerModel playerModel;
    private MapGenerator mapGenerator;
    private Dictionary<string, float> targetMetrics;

    public GAEngine(PlayerModel player)
    {
        mapGenerator = new MapGenerator(); // 確保這裡是一個新的實例
        playerModel = player;
        population = new List<char[,]>();
    }

    public char[,] Run(int difficulty, Dictionary<string, float> _weights = null)
    {
        // 取得該難度下的目標數值
        targetMetrics = PlayerModel.MapDifficultyToTargets(difficulty);

        InitializePopulation();

        char[,] bestMap = null;
        float bestFit = -1f;

        for (int gen = 0; gen < generations; gen++)
        {
            float maxFit = -1f;
            char[,] bestInGen = null;

            // --- 評估適應度 ---
            for(int i = 0; i < population.Count; i++)
            {
                // 注意：Fitness 內部會執行 BFSRepairMap 修改 population[i] 的內容
                float f = Fitness(population[i], targetMetrics, _weights);
                
                if (f > maxFit)
                {
                    maxFit = f;
                    bestInGen = population[i];
                }
            }

            // Debug.Log($"GA Gen {gen}: Best Score = {maxFit:F4}");

            if (maxFit > bestFit && bestInGen != null)
            {
                bestFit = maxFit;
                bestMap = CloneMap(bestInGen);
            }

            // --- 產生下一代 ---
            List<char[,]> nextGen = new List<char[,]>();
            
            // 菁英保留策略 (Elitism)：保留這一代最好的直接進入下一代
            if(bestInGen != null) nextGen.Add(CloneMap(bestInGen));

            // 輪盤選擇法或隨機選擇進行交配
            while (nextGen.Count < populationSize)
            {
                char[,] p1 = population[Random.Range(0, population.Count)];
                char[,] p2 = population[Random.Range(0, population.Count)];

                if (Random.value < crossoverRate)
                {
                    var (c1, c2) = Crossover(p1, p2);
                    Mutate(c1); // 先交配再突變
                    Mutate(c2);
                    nextGen.Add(c1);
                    if(nextGen.Count < populationSize) nextGen.Add(c2);
                }
                else
                {
                    char[,] c1 = CloneMap(p1);
                    Mutate(c1);
                    nextGen.Add(c1);
                }
            }
            population = nextGen;
        }

        // 最終保險：確保回傳的地圖是可解的
        if (bestMap != null)
        {
            // 雖然 Fitness 跑過，但為了保險再修一次
            if (mapGenerator.BFSRepairMap(bestMap, mapSize))
            {
                return bestMap;
            }
        }

        Debug.LogWarning("GA failed to find a valid map. Generating fallback.");
        int pickups = (int)targetMetrics["Pickups"];
        int empty = (int)Mathf.Round(targetMetrics["EmptySpace"]);
        return mapGenerator.GenerateMap(mapSize, (int)Mathf.Round(targetMetrics["Pickups"]), (int)Mathf.Round(targetMetrics["EmptySpace"]));
    }

    private void InitializePopulation()
    {
        population.Clear();
        int pickups = (int)targetMetrics["Pickups"];
        int empty = (int)targetMetrics["EmptySpace"];

        for (int i = 0; i < populationSize; i++)
        {
            // 初始地圖由 Generator 產生，保證初始是合法的
            population.Add(mapGenerator.GenerateMap(mapSize, (int)Mathf.Round(targetMetrics["Pickups"]), (int)Mathf.Round(targetMetrics["EmptySpace"])));
        }
    }

    // === 核心修改：Fitness 包含路徑修復 ===
    private float Fitness(char[,] map, Dictionary<string, float> targetMetrics, Dictionary<string, float> _weights)
    {
        // 1. 強制修復路徑 (這是 Hard Constraint)
        // 這裡會重新畫 'X'。如果起點被圍死或無解，回傳 false。
        bool isSolvable = mapGenerator.BFSRepairMap(map, mapSize);

        if (!isSolvable)
        {
            return 0f; // 無解地圖，直接淘汰
        }

        // 2. 計算特徵 (此時 map 上的 'X' 已經是最新的有效路徑)
        float pathLen = CalculatePathLength(map);
        float corners = CountCorners(map);
        float empty = CountEmptySpaces(map);
        float pickups = CountPickups(map);
        float ortho = CountOrthogonalPickups(map);

        // 3. 準備權重
        var weights = new Dictionary<string, float> {
            { "PathLength", 1 }, { "Corners", 1 }, { "EmptySpace", 1 },
            { "Pickups", 1 }, { "OrthogonalPickups", 1 }
        };
        if (_weights != null) weights = _weights;

        // 正規化權重
        float weightSum = weights.Values.Sum();
        
        // 4. 計算分數 (使用歸一化公式：分數越高越好，滿分 1.0)
        // 公式： Weight * ( 1 / (1 + |Target - Actual|) )
        float score = 0f;

        score += weights["PathLength"] * (1f / (1f + Mathf.Abs(targetMetrics["PathLength"] - pathLen)));
        score += weights["Corners"] * (1f / (1f + Mathf.Abs(targetMetrics["Corners"] - corners)));
        score += weights["EmptySpace"] * (1f / (1f + Mathf.Abs(targetMetrics["EmptySpace"] - empty)));
        score += weights["Pickups"] * (1f / (1f + Mathf.Abs(targetMetrics["Pickups"] - pickups)));
        score += weights["OrthogonalPickups"] * (1f / (1f + Mathf.Abs(targetMetrics["OrthogonalPickups"] - ortho)));

        return score / weightSum; // 將總分縮放到 0~1 之間方便觀察
    }

    private (char[,], char[,]) Crossover(char[,] p1, char[,] p2)
    {
        int size = p1.GetLength(0);
        char[,] c1 = new char[size, size];
        char[,] c2 = new char[size, size];

        // 單點交配 (Single Point Crossover based on Rows)
        int splitRow = Random.Range(1, size - 1);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (y < splitRow)
                {
                    c1[y, x] = p1[y, x];
                    c2[y, x] = p2[y, x];
                }
                else
                {
                    c1[y, x] = p2[y, x];
                    c2[y, x] = p1[y, x];
                }
            }
        }
        // 注意：交配後路徑肯定斷了，但下一輪 Fitness 會呼叫 BFSRepairMap 重接
        return (c1, c2);
    }

    private void Mutate(char[,] map)
    {
        int size = map.GetLength(0);
        // 只在內部區域突變，保留最外圈圍牆
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                if (Random.value < mutationRate)
                {
                    char current = map[y, x];
                    
                    // 保護 Start 和 End 不被突變掉
                    if (current == 'S' || current == 'E') continue;

                    // 突變選項：空地、障礙、Pickup、Dropoff
                    // === 重點：絕對不要生成 'X' (路徑)，路徑由 BFS 生成 ===
                    char[] tiles = { '#', '#', '#', 'O', 'P', 'D' }; 
                    
                    map[y, x] = tiles[Random.Range(0, tiles.Length)];
                }
            }
        }
        
        // 簡單的平衡機制：如果 P 和 D 數量落差太大，隨機修正
        // 這邊可以保留你原本的邏輯，或者交給 Fitness 去評分淘汰
    }

    private char[,] CloneMap(char[,] map)
    {
        int size = map.GetLength(0);
        char[,] clone = new char[size, size];
        System.Array.Copy(map, clone, map.Length);
        return clone;
    }

    // --- 真實的參數計算邏輯 ---

    private float CalculatePathLength(char[,] map)
    {
        int cnt = 0;
        foreach (char c in map) if (c == 'X') cnt++;
        return cnt;
    }

    private float CountCorners(char[,] map)
    {
        int corners = 0;
        int size = map.GetLength(0);
        
        // 掃描每一個路徑點 'X'
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                if (map[y, x] == 'X')
                {
                    // 檢查上下左右是否有路徑 ('X', 'S', 'E' 都算路徑的一部分)
                    bool u = IsPath(map[y - 1, x]);
                    bool d = IsPath(map[y + 1, x]);
                    bool l = IsPath(map[y, x - 1]);
                    bool r = IsPath(map[y, x + 1]);

                    // 如果是轉角，通常是有兩個相鄰的連接點，且不是直線 (上+下 或 左+右)
                    int connections = (u ? 1 : 0) + (d ? 1 : 0) + (l ? 1 : 0) + (r ? 1 : 0);

                    if (connections == 2)
                    {
                        if (!(u && d) && !(l && r)) // 不是垂直直線 且 不是水平直線
                        {
                            corners++;
                        }
                    }
                }
            }
        }
        return corners;
    }

    private bool IsPath(char c) => c == 'X' || c == 'S' || c == 'E';

    private float CountEmptySpaces(char[,] map)
    {
        int cnt = 0;
        foreach (char c in map) if (c == '#') cnt++;
        return cnt;
    }

    private float CountPickups(char[,] map)
    {
        int cnt = 0;
        foreach (char c in map) if (c == 'P') cnt++;
        return cnt;
    }

    // 計算「正交對齊」的 Pickup 數量
    // 定義：如果 Pickup 與任意一個 Dropoff 在同一行或同一列，視為正交對齊（路徑比較簡單）
    private float CountOrthogonalPickups(char[,] map)
    {
        int cnt = 0;
        int size = map.GetLength(0);
        List<(int y, int x)> pickups = new List<(int, int)>();
        List<(int y, int x)> dropoffs = new List<(int, int)>();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (map[y, x] == 'P') pickups.Add((y, x));
                if (map[y, x] == 'D') dropoffs.Add((y, x));
            }
        }

        foreach (var p in pickups)
        {
            bool isAligned = false;
            foreach (var d in dropoffs)
            {
                // 如果 X 相同 或 Y 相同，則為正交對齊
                if (p.x == d.x || p.y == d.y)
                {
                    isAligned = true;
                    break;
                }
            }
            if (isAligned) cnt++;
        }
        return cnt;
    }
}