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
        
        int maxResets = 5; // Increased from 2
        int basePatience = 150;
        float errorRate = 0f; // Adjusted to 0.1f (balanced)

        List<Vector2Int> recordedPath = new List<Vector2Int>();

        while (resets <= maxResets)
        {
            (int x, int y) current = start;
            recordedPath.Add(new Vector2Int(current.x, current.y));
            HashSet<(int, int)> visitedThisAttempt = new HashSet<(int, int)>();
            visitedThisAttempt.Add(current);

            List<(int, int)> currentPickups = new List<(int, int)>(allPickups);
            List<(int, int)> currentDropoffs = new List<(int, int)>(allDropoffs);
            bool hasPackage = false;
            
            int steps = 0;
            bool solved = false;
            int patience = basePatience + (resets * 50); // Patience increases significantly
            (int, int)? currentTarget = null; // Lock target to prevent looping

            while (steps < patience)
            {
                steps++;
                totalTime += 1.0f; // Normal speed

                // Check Objectives
                bool actionTaken = false;

                // 1. Drop
                if (hasPackage) {
                    int dIndex = currentDropoffs.FindIndex(d => MazeSimUtils.IsAdjacent(current, d));
                    if (dIndex != -1) {
                        currentDropoffs.RemoveAt(dIndex);
                        hasPackage = false;
                        steps = 0; // Reset patience
                        visitedThisAttempt.Clear();
                        backtracks += Random.Range(0, 2);
                        currentTarget = null; // Retarget
                        actionTaken = true;
                    }
                }

                // 2. Pick
                if (!hasPackage) {
                    int pIndex = currentPickups.FindIndex(p => MazeSimUtils.IsAdjacent(current, p));
                    if (pIndex != -1) {
                        currentPickups.RemoveAt(pIndex);
                        hasPackage = true;
                        steps = 0; // Reset patience
                        visitedThisAttempt.Clear();
                        backtracks += Random.Range(0, 2);
                        currentTarget = null; // Retarget
                        actionTaken = true;
                    }
                }

                // 3. Drop again (Pick -> Drop)
                if (hasPackage) {
                    int dIndex = currentDropoffs.FindIndex(d => MazeSimUtils.IsAdjacent(current, d));
                    if (dIndex != -1) {
                        currentDropoffs.RemoveAt(dIndex);
                        hasPackage = false;
                        steps = 0; // Reset patience
                        visitedThisAttempt.Clear();
                        backtracks += Random.Range(0, 2);
                        currentTarget = null; // Retarget
                        actionTaken = true;
                    }
                }

                if (current == end && currentPickups.Count == 0 && currentDropoffs.Count == 0) {
                    solved = true;
                    break;
                }

                // Determine Target
                if (currentTarget == null) {
                    if (!hasPackage && currentPickups.Count > 0) currentTarget = GetNearest(current, currentPickups);
                    else if (hasPackage && currentDropoffs.Count > 0) currentTarget = GetNearest(current, currentDropoffs);
                    else currentTarget = end;
                }
                
                (int, int) target = currentTarget.Value;

                // Adjust target to walkable neighbor if it's not end (End is walkable)
                if (target != end) {
                    target = MazeSimUtils.GetClosestWalkableNeighbor(maze, current, target);
                }

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
                    currentTarget = null; // Unlock target to try finding a new one
                    var neighbors = MazeSimUtils.GetNeighbors(maze, current.x, current.y);
                    if (neighbors.Count > 0) next = neighbors[Random.Range(0, neighbors.Count)];
                }

                if (visitedThisAttempt.Contains(next) && next != current) backtracks++;
                current = next;
                visitedThisAttempt.Add(current);
                recordedPath.Add(new Vector2Int(current.x, current.y));
            }

            if (solved) break;

            resets++;
            attempts++;
            errorRate *= 0.85f; // Learn: Reduce error rate (balanced learning)
            
            int remaining = currentPickups.Count + currentDropoffs.Count;
            if (remaining > 0 && remaining <= 2) nearSolves++;
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
