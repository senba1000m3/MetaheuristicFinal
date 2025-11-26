using UnityEngine;
using System.Collections.Generic;

public class GAEngine
{
    public int populationSize = 30;
    public int generations = 10;
    public float mutationRate = 0.1f;
    public int mazeSize = 9;

    private List<char[,]> population;
    private PlayerModel playerModel;

    public GAEngine(PlayerModel player)
    {
        playerModel = player;
        population = new List<char[,]>();
    }

    public char[,] Run()
    {
        InitializePopulation();

        char[,] bestMaze = null;
        float bestFit = -1f;

        Dictionary<string, float> targetMetrics = MapDifficultyToTargets(playerModel.SuggestDifficulty(5));

        for (int gen = 0; gen < generations; gen++)
        {
            float maxFit = -1f;
            char[,] bestInGen = null;

            foreach (var maze in population)
            {
                float f = Fitness(maze, targetMetrics);
                if (f > maxFit)
                {
                    maxFit = f;
                    bestInGen = maze;
                }
            }

            if (maxFit > bestFit)
            {
                bestFit = maxFit;
                bestMaze = CloneMaze(bestInGen);
            }

            List<char[,]> nextGen = new List<char[,]>();
            while (nextGen.Count < populationSize)
            {
                char[,] p1 = Select();
                char[,] p2 = Select();
                (char[,], char[,]) children = Crossover(p1, p2);
                Mutate(children.Item1);
                Mutate(children.Item2);
                nextGen.Add(children.Item1);
                nextGen.Add(children.Item2);
            }

            population = nextGen;
        }

        return bestMaze;
    }

    private void InitializePopulation()
    {
        population.Clear();
        for (int i = 0; i < populationSize; i++)
        {
            population.Add(MazeGenerator.GenerateMaze(mazeSize));
        }
    }

    private Dictionary<string, float> MapDifficultyToTargets(int difficulty)
    {
        return new Dictionary<string, float>
        {
            { "Path", Mathf.Lerp(8,50,(difficulty-1)/9f) },
            { "Corners", Mathf.Lerp(0,20,(difficulty-1)/9f) },
            { "Empty", Mathf.Lerp(20,5,(difficulty-1)/9f) },
            { "Pickups", Mathf.Lerp(1,12,(difficulty-1)/9f) },
            { "OrthoPickups", Mathf.Lerp(0,2,(difficulty-1)/9f) }
        };
    }

    private float Fitness(char[,] maze, Dictionary<string, float> targetMetrics)
    {
        float pathLen = CalculatePathLength(maze);
        float corners = CountCorners(maze);
        float empty = CountEmptySpaces(maze);
        float pickups = CountPickups(maze);
        float ortho = CountOrthogonalPickups(maze);

        float score = 0f;
        score += Mathf.Max(0f, targetMetrics["Path"] - Mathf.Abs(targetMetrics["Path"] - pathLen));
        score += Mathf.Max(0f, targetMetrics["Corners"] - Mathf.Abs(targetMetrics["Corners"] - corners));
        score += Mathf.Max(0f, targetMetrics["Empty"] - Mathf.Abs(targetMetrics["Empty"] - empty));
        score += Mathf.Max(0f, targetMetrics["Pickups"] - Mathf.Abs(targetMetrics["Pickups"] - pickups));
        score += Mathf.Max(0f, targetMetrics["OrthoPickups"] - Mathf.Abs(targetMetrics["OrthoPickups"] - ortho));

        return score;
    }

    private char[,] Select()
    {
        return population[Random.Range(0, population.Count)];
    }

    private (char[,], char[,]) Crossover(char[,] p1, char[,] p2)
    {
        int size = p1.GetLength(0);
        char[,] c1 = CloneMaze(p1);
        char[,] c2 = CloneMaze(p2);

        int row = Random.Range(1, size-1);
        for (int y = row; y < size-1; y++)
        {
            for (int x = 1; x < size-1; x++)
            {
                char tmp = c1[y,x];
                c1[y,x] = c2[y,x];
                c2[y,x] = tmp;
            }
        }

        MazeUtils.FillPathBFS(c1);
        MazeUtils.FillPathBFS(c2);

        return (c1, c2);
    }

    private void Mutate(char[,] maze)
    {
        int size = maze.GetLength(0);
        for(int y=1;y<size-1;y++)
        {
            for(int x=1;x<size-1;x++)
            {
                if(Random.value < mutationRate)
                {
                    char[] tiles = { 'X','P','D' };
                    maze[y,x] = tiles[Random.Range(0,tiles.Length)];
                }
            }
        }
    }

    private char[,] CloneMaze(char[,] maze)
    {
        int size = maze.GetLength(0);
        char[,] clone = new char[size,size];
        for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
                clone[y,x] = maze[y,x];
        return clone;
    }

    private float CalculatePathLength(char[,] maze)
    {
        int count=0;
        int size=maze.GetLength(0);
        for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
                if(maze[y,x]=='X'||maze[y,x]=='S'||maze[y,x]=='E') count++;
        return count;
    }

    private float CountCorners(char[,] maze){ return Random.Range(0,10); }
    private float CountEmptySpaces(char[,] maze){ int cnt=0; int size=maze.GetLength(0); for(int y=0;y<size;y++) for(int x=0;x<size;x++) if(maze[y,x]=='#') cnt++; return cnt; }
    private float CountPickups(char[,] maze){ int cnt=0; int size=maze.GetLength(0); for(int y=0;y<size;y++) for(int x=0;x<size;x++) if(maze[y,x]=='P') cnt++; return cnt; }
    private float CountOrthogonalPickups(char[,] maze){ return Random.Range(0,2); }
}
