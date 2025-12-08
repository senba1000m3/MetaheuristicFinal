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
        
        int maxResets = 10; // Increased from 5
        int patience = 30; // Very low patience

        List<Vector2Int> recordedPath = new List<Vector2Int>();
        HashSet<(int, int)> tabooPositions = new HashSet<(int, int)>(); // Memory of stuck positions

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
                bool actionTaken = false;

                // 1. Drop
                if (hasPackage) {
                    int dIndex = currentDropoffs.FindIndex(d => MazeSimUtils.IsAdjacent(current, d));
                    if (dIndex != -1) {
                        currentDropoffs.RemoveAt(dIndex);
                        hasPackage = false;
                        visitedThisAttempt.Clear();
                        steps = 0;
                        actionTaken = true;
                    }
                }

                // 2. Pick
                if (!hasPackage) {
                    int pIndex = currentPickups.FindIndex(p => MazeSimUtils.IsAdjacent(current, p));
                    if (pIndex != -1) {
                        currentPickups.RemoveAt(pIndex);
                        hasPackage = true;
                        visitedThisAttempt.Clear();
                        steps = 0;
                        actionTaken = true;
                    }
                }

                // 3. Drop again
                if (hasPackage) {
                    int dIndex = currentDropoffs.FindIndex(d => MazeSimUtils.IsAdjacent(current, d));
                    if (dIndex != -1) {
                        currentDropoffs.RemoveAt(dIndex);
                        hasPackage = false;
                        visitedThisAttempt.Clear();
                        steps = 0;
                        actionTaken = true;
                    }
                }

                if (current == end)
                {
                    if (currentPickups.Count == 0 && currentDropoffs.Count == 0)
                    {
                        solved = true;
                        break;
                    }
                    else
                    {
                        // Prematurely reached end -> Reset
                        break;
                    }
                }

                // 2. Determine Target (Naive)
                (int, int) target = end;
                if (!hasPackage && currentPickups.Count > 0) target = currentPickups[0]; // Just pick first one
                else if (hasPackage && currentDropoffs.Count > 0) target = currentDropoffs[0];

                // 3. Move Logic (Naive Greedy)
                var neighbors = MazeSimUtils.GetNeighbors(maze, current.x, current.y);
                
                // Filter out taboo positions (Memory)
                var safeNeighbors = neighbors.Where(n => !tabooPositions.Contains(n)).ToList();
                // If all neighbors are taboo, we are forced to use them (or stuck)
                if (safeNeighbors.Count > 0) neighbors = safeNeighbors;

                if (neighbors.Count == 0) {
                    // Stuck completely
                    tabooPositions.Add(current); // Remember this bad spot
                    break; 
                }

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
            int remaining = currentPickups.Count + currentDropoffs.Count;
            if (remaining > 0 && remaining <= 2) nearSolves++;
        }

        PlayerModel model = new PlayerModel(attempts, backtracks, nearSolves, resets, totalTime);
        model.pathHistory = recordedPath;
        return model;
    }
}
