using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ExpertAgent : IPlayerAgent
{
    public override PlayerModel Simulate(char[,] maze)
    {
        // Expert:
        // - Optimal Pathing (BFS/A*)
        // - Almost no errors
        // - High efficiency

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
        
        List<Vector2Int> recordedPath = new List<Vector2Int>();

        // Expert solves it in one go usually
        (int x, int y) current = start;
        recordedPath.Add(new Vector2Int(current.x, current.y));

        List<(int, int)> currentPickups = new List<(int, int)>(allPickups);
        List<(int, int)> currentDropoffs = new List<(int, int)>(allDropoffs);
        bool hasPackage = false;
        
        int steps = 0;
        int maxSteps = 1000; // Safety break

        while (steps < maxSteps)
        {
            steps++;
            totalTime += 0.5f; // Fast thinker

            // Check Objectives
            if (currentPickups.Contains(current)) {
                currentPickups.Remove(current);
                hasPackage = true;
            } else if (currentDropoffs.Contains(current)) {
                currentDropoffs.Remove(current);
                hasPackage = false;
            } else if (current == end && currentPickups.Count == 0 && currentDropoffs.Count == 0) {
                break; // Solved
            }

            // Determine Target
            (int, int) target = end;
            if (!hasPackage && currentPickups.Count > 0) target = GetNearest(current, currentPickups);
            else if (hasPackage && currentDropoffs.Count > 0) target = GetNearest(current, currentDropoffs);

            // Move Logic (Optimal)
            List<(int, int)> path = MazeSimUtils.BFS(maze, current, target);
            
            if (path != null && path.Count > 1)
            {
                current = path[1];
                recordedPath.Add(new Vector2Int(current.x, current.y));
            }
            else
            {
                // Unexpected block? Wait.
                // Expert might wait or check around, but shouldn't happen in valid map
                break; 
            }
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
