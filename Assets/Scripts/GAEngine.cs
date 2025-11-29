using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GAEngine
{
    // 這邊變數會以 PuzzleController 裡的設定為主。
    public int populationSize = 30;
    public int generations = 10;
    public int mapSize = 10;
    public float crossoverRate = 0.8f;
    public float mutationRate = 0.1f;

    private List<char[,]> population;
    private PlayerModel playerModel;
    private MapGenerator mapGenerator;

    public GAEngine(PlayerModel player)
    {
        mapGenerator = new MapGenerator();
        playerModel = player;
        population = new List<char[,]>();
    }

    public char[,] Run(int difficulty)
    {
        InitializePopulation();

        char[,] bestMap = null;
        float bestFit = -1f;

        Dictionary<string, float> targetMetrics = PlayerModel.MapDifficultyToTargets(difficulty);

        for (int gen = 0; gen < generations; gen++){
            float maxFit = -1f;
            char[,] bestInGen = null;

            foreach (var map in population)
            {
                float f = Fitness(map, targetMetrics);
                if (f > maxFit)
                {
                    maxFit = f;
                    bestInGen = map;
                }
            }

            if (maxFit > bestFit)
            {
                bestFit = maxFit;
                bestMap = CloneMap(bestInGen);
            }

            List<char[,]> nextGen = new List<char[,]>();
            int crossoverCount = (int)(populationSize * crossoverRate);

            List<int> indices = Enumerable.Range(0, population.Count).ToList();

            // 交錯
            List<char[,]> crossoverParents = new List<char[,]>();

            for (int i = 0; i < crossoverCount; i++){
                int idx = indices[Random.Range(0, indices.Count)];
                crossoverParents.Add(population[idx]);
                indices.Remove(idx);
            }

            for (int i = 0; i < crossoverParents.Count; i += 2){
                var (c1, c2) = Crossover(crossoverParents[i], crossoverParents[i + 1]);
                nextGen.Add(c1);
                nextGen.Add(c2);
            }

            foreach (int idx in indices){
                char[,] clone = CloneMap(population[idx]);
                nextGen.Add(clone);
            }

            // 突變
            foreach (var child in nextGen){
                Mutate(child);
            }

            population = nextGen;
        }

        return bestMap;
    }

    private void InitializePopulation()
    {
        population.Clear();
        for (int i = 0; i < populationSize; i++)
        {
            population.Add(mapGenerator.GenerateMap(mapSize));
        }
    }

    private float Fitness(char[,] map, Dictionary<string, float> targetMetrics)
    {
        float pathLen = CalculatePathLength(map);
        float corners = CountCorners(map);
        float empty = CountEmptySpaces(map);
        float pickups = CountPickups(map);
        float ortho = CountOrthogonalPickups(map);

        float score = 0f;
        score += Mathf.Max(0f, targetMetrics["PathLength"] - Mathf.Abs(targetMetrics["PathLength"] - pathLen));
        score += Mathf.Max(0f, targetMetrics["Corners"] - Mathf.Abs(targetMetrics["Corners"] - corners));
        score += Mathf.Max(0f, targetMetrics["EmptySpace"] - Mathf.Abs(targetMetrics["EmptySpace"] - empty));
        score += Mathf.Max(0f, targetMetrics["Pickups"] - Mathf.Abs(targetMetrics["Pickups"] - pickups));
        score += Mathf.Max(0f, targetMetrics["OrthogonalPickups"] - Mathf.Abs(targetMetrics["OrthogonalPickups"] - ortho));

        return score;
    }

    private (char[,], char[,]) Crossover(char[,] p1, char[,] p2)
    {
        int size = p1.GetLength(0);
        char[,] c1 = CloneMap(p1);
        char[,] c2 = CloneMap(p2);

        int row = Random.Range(1, size-1); // 排除最邊邊兩行
        for (int y = row; y < size-1; y++)
        {
            for (int x = 1; x < size-1; x++)
            {
                char tmp = c1[y,x];
                c1[y,x] = c2[y,x];
                c2[y,x] = tmp;
            }
        }

        // BFS 確保路徑連通
        // BFS.FillPathBFS(c1);
        // BFS.FillPathBFS(c2);

        return (c1, c2);
    }

    private void Mutate(char[,] map)
    {
        int size = map.GetLength(0);
        for(int y=1;y<size-1;y++)
        {
            for(int x=1;x<size-1;x++)
            {
                if(Random.value < mutationRate)
                {
                    char[] tiles = {'X', 'P', 'D'};
                    map[y,x] = tiles[Random.Range(0,tiles.Length)];
                }
            }
        }
    }

    private char[,] CloneMap(char[,] map)
    {
        int size = map.GetLength(0);
        char[,] clone = new char[size,size];
        for(int y=0;y<size;y++)
        {
            for(int x=0;x<size;x++)
            {
                clone[y,x] = map[y,x];
            }
        }
        return clone;
    }

    // 繼續各種參數
    private float CalculatePathLength(char[,] map)
    {
        int cnt=0;
        int size=map.GetLength(0);
        
        for(int y=0;y<size;y++){
            for(int x=0;x<size;x++){
                if(map[y,x]=='X'){
                    cnt++;
                }
            }
        }
        return cnt;
    }

    private float CountCorners(char[,] map){ 
        return Random.Range(0,10);
    }

    private float CountEmptySpaces(char[,] map){ 
        int cnt=0;
        int size=map.GetLength(0);

        for(int y=0;y<size;y++){
            for(int x=0;x<size;x++){
                if(map[y,x]=='#'){
                    cnt++;
                }
            }
        }
        return cnt;
    }
    private float CountPickups(char[,] map){
        int cnt=0;
        int size=map.GetLength(0);

        for(int y=0;y<size;y++){
            for(int x=0;x<size;x++){
                if(map[y,x]=='P'){
                    cnt++;
                }
            } 
        }
        return cnt;
    }

    private float CountOrthogonalPickups(char[,] map){
        return Random.Range(0,2);
    }
}
