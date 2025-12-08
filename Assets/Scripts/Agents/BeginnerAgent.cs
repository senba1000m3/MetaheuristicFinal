using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BeginnerAgent : IPlayerAgent
{
    public override PlayerModel Simulate(char[,] maze)
    {
        // Beginner: 
        // - Greedy / Random walk
        // - High chance to get stuck
        // - Low patience -> High Resets
        // - High Backtracks (wandering)

        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);

        // Parse Map
        (int x, int y) start = (-1, -1);
        (int x, int y) end = (-1, -1);
        List<(int, int)> allPickups = new List<(int, int)>();
        List<(int, int)> allDropoffs = new List<(int, int)>();

        for (int y = 0; y < rows; y++) {
            for (int x = 0; x < cols; x++) {
                if (maze[y, x] == 'S') start = (x, y);
                else if (maze[y, x] == 'E') end = (x, y);
                else if (maze[y, x] == 'P') allPickups.Add((x, y));
                else if (maze[y, x] == 'D') allDropoffs.Add((x, y));
            }
        }

        int attempts = 1;
        int backtracks = 0;
        int nearSolves = 0;
        int resets = 0;
        float totalTime = 0f;
        
        int maxResets = 5;
        int patience = 30; // Very low patience

        List<Vector2Int> recordedPath = new List<Vector2Int>();

        // Simulation Loop
        while (resets <= maxResets)
        {
            // Reset State
            (int x, int y) current = start;
            recordedPath.Add(new Vector2Int(current.x, current.y));

            List<(int, int)> currentPickups = new List<(int, int)>(allPickups);
            List<(int, int)> currentDropoffs = new List<(int, int)>(allDropoffs);
            bool hasPackage = false;
            HashSet<(int, int)> visitedThisAttempt = new HashSet<(int, int)>();
            visitedThisAttempt.Add(current);

            int steps = 0;
            bool solved = false;

            while (steps < patience)
            {
                steps++;
                totalTime += 2.0f; // Slow thinker

                // 1. Check Objectives
                if (currentPickups.Contains(current))
                {
                    currentPickups.Remove(current);
                    hasPackage = true;
                    visitedThisAttempt.Clear(); // Reset visited for new segment
                    steps = 0; // Reset patience on progress
                }
                else if (currentDropoffs.Contains(current))
                {
                    currentDropoffs.Remove(current);
                    hasPackage = false;
                    visitedThisAttempt.Clear();
                    steps = 0;
                }
                else if (current == end && currentPickups.Count == 0 && currentDropoffs.Count == 0)
                {
                    solved = true;
                    break;
                }

                // 2. Determine Target (Naive)
                (int, int) target = end;
                if (!hasPackage && currentPickups.Count > 0) target = currentPickups[0]; // Just pick first one
                else if (hasPackage && currentDropoffs.Count > 0) target = currentDropoffs[0];

                // 3. Move Logic (Naive Greedy)
                var neighbors = MazeSimUtils.GetNeighbors(maze, current.x, current.y);
                if (neighbors.Count == 0) break; // Stuck

                // Filter out immediate backtrack if possible
                var validNeighbors = neighbors.Where(n => !visitedThisAttempt.Contains(n)).ToList();
                
                (int, int) next;
                if (validNeighbors.Count > 0)
                {
                    // 70% chance to move towards target, 30% random
                    if (Random.value < 0.7f)
                    {
                        next = validNeighbors.OrderBy(n => MazeSimUtils.Manhattan(n.x, n.y, target.Item1, target.Item2)).First();
                    }
                    else
                    {
                        next = validNeighbors[Random.Range(0, validNeighbors.Count)];
                    }
                }
                else
                {
                    // Forced backtrack
                    next = neighbors[Random.Range(0, neighbors.Count)];
                    backtracks++;
                }

                current = next;
                visitedThisAttempt.Add(current);
                recordedPath.Add(new Vector2Int(current.x, current.y));
            }

            if (solved) break;

            // Failed attempt
            resets++;
            attempts++;
            
            // Check near solve
            if (currentPickups.Count == 0 && currentDropoffs.Count == 0) nearSolves++;
        }

        PlayerModel model = new PlayerModel(attempts, backtracks, nearSolves, resets, totalTime);
        model.pathHistory = recordedPath;
        return model;
    }
}
