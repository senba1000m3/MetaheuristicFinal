using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NormalAgent : IPlayerAgent
{
    public override PlayerModel Simulate(char[,] maze)
    {
        // Normal:
        // - Uses BFS but has error rate
        // - Learns from mistakes (error rate decreases on reset)
        // - Moderate patience

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
        
        int maxResets = 2;
        int basePatience = 150;
        float errorRate = 0.05f; // Reduced from 0.15f to 0.05f

        List<Vector2Int> recordedPath = new List<Vector2Int>();

        while (resets <= maxResets)
        {
            (int x, int y) current = start;
            recordedPath.Add(new Vector2Int(current.x, current.y));

            List<(int, int)> currentPickups = new List<(int, int)>(allPickups);
            List<(int, int)> currentDropoffs = new List<(int, int)>(allDropoffs);
            bool hasPackage = false;
            
            int steps = 0;
            bool solved = false;
            int patience = basePatience + (resets * 50); // Patience increases significantly

            while (steps < patience)
            {
                steps++;
                totalTime += 1.0f; // Normal speed

                // Check Objectives
                if (currentPickups.Contains(current)) {
                    currentPickups.Remove(current);
                    hasPackage = true;
                    steps = 0; // Reset patience
                } else if (currentDropoffs.Contains(current)) {
                    currentDropoffs.Remove(current);
                    hasPackage = false;
                    steps = 0;
                } else if (current == end && currentPickups.Count == 0 && currentDropoffs.Count == 0) {
                    solved = true;
                    break;
                }

                // Determine Target
                (int, int) target = end;
                if (!hasPackage && currentPickups.Count > 0) target = GetNearest(current, currentPickups);
                else if (hasPackage && currentDropoffs.Count > 0) target = GetNearest(current, currentDropoffs);

                // Move Logic (BFS with Error)
                List<(int, int)> path = MazeSimUtils.BFS(maze, current, target);
                (int, int) next = current;

                if (path != null && path.Count > 1)
                {
                    (int, int) ideal = path[1];
                    
                    // Only make a mistake occasionally
                    if (Random.value < errorRate)
                    {
                        // Mistake: 50% chance to wait/hesitate, 50% chance to move wrong
                        if (Random.value < 0.5f)
                        {
                            // Hesitate (stay put)
                            next = current;
                        }
                        else
                        {
                            // Wrong move
                            var neighbors = MazeSimUtils.GetNeighbors(maze, current.x, current.y);
                            var wrong = neighbors.Where(n => n != ideal).ToList();
                            if (wrong.Count > 0) {
                                next = wrong[Random.Range(0, wrong.Count)];
                                backtracks++; 
                            } else {
                                next = ideal;
                            }
                        }
                    }
                    else
                    {
                        next = ideal;
                    }
                }
                else
                {
                    // Stuck or no path
                    var neighbors = MazeSimUtils.GetNeighbors(maze, current.x, current.y);
                    if (neighbors.Count > 0) next = neighbors[Random.Range(0, neighbors.Count)];
                }

                current = next;
                recordedPath.Add(new Vector2Int(current.x, current.y));
            }

            if (solved) break;

            resets++;
            attempts++;
            errorRate *= 0.8f; // Learn: Reduce error rate
            
            if (currentPickups.Count == 0 && currentDropoffs.Count == 0) nearSolves++;
        }

        PlayerModel model = new PlayerModel(attempts, backtracks, nearSolves, resets, totalTime);
        model.pathHistory = recordedPath;
        return model;
    }

    private (int, int) GetNearest((int x, int y) from, List<(int, int)> targets)
    {
        (int, int) best = targets[0];
        float minDist = float.MaxValue;
        foreach (var t in targets)
        {
            float d = MazeSimUtils.Manhattan(from.x, from.y, t.Item1, t.Item2);
            if (d < minDist)
            {
                minDist = d;
                best = t;
            }
        }
        return best;
    }
}
