using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ExpertAgent : IPlayerAgent
{
    public override PlayerModel Simulate(char[,] maze)
    {
        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);

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

        int maxResets = 2;
        
        while (resets <= maxResets)
        {
            // Reset State
            (int x, int y) current = start;
            recordedPath.Add(new Vector2Int(current.x, current.y));
            HashSet<(int, int)> visitedThisAttempt = new HashSet<(int, int)>();
            visitedThisAttempt.Add(current);

            List<(int, int)> currentPickups = new List<(int, int)>(allPickups);
            List<(int, int)> currentDropoffs = new List<(int, int)>(allDropoffs);
            bool hasPackage = false;
            
            int steps = 0;
            int maxSteps = 1000; // Safety break
            bool solved = false;
            (int, int)? currentTarget = null; // Lock target to prevent looping

            while (steps < maxSteps)
            {
                steps++;
                totalTime += 0.5f; // Fast thinker

                // Check Objectives
                // Priority: Drop then Pick if carrying. Pick then Drop if not.
                // This allows handling both in one step if adjacent to both.
                
                bool actionTaken = false;

                // 1. Try to Drop if we have a package
                if (hasPackage) {
                    int dIndex = currentDropoffs.FindIndex(d => MazeSimUtils.IsAdjacent(current, d));
                    if (dIndex != -1) {
                        currentDropoffs.RemoveAt(dIndex);
                        hasPackage = false;
                        currentTarget = null; // Retarget
                        // visitedThisAttempt.Clear();
                        // backtracks += Random.Range(0, 1);
                        actionTaken = true;
                    }
                }

                // 2. Try to Pick if we don't have a package (or just dropped one)
                if (!hasPackage) {
                    int pIndex = currentPickups.FindIndex(p => MazeSimUtils.IsAdjacent(current, p));
                    if (pIndex != -1) {
                        currentPickups.RemoveAt(pIndex);
                        hasPackage = true;
                        currentTarget = null; // Retarget
                        // visitedThisAttempt.Clear();
                        // backtracks += Random.Range(0, 1);
                        actionTaken = true;
                    }
                }

                // 3. Try to Drop AGAIN if we just picked one and there is a dropoff nearby
                // This handles the "Pick then Drop" case in a single step
                if (hasPackage) {
                     int dIndex = currentDropoffs.FindIndex(d => MazeSimUtils.IsAdjacent(current, d));
                     if (dIndex != -1) {
                        currentDropoffs.RemoveAt(dIndex);
                        hasPackage = false;
                        currentTarget = null; // Retarget
                        // visitedThisAttempt.Clear();
                        // backtracks += Random.Range(0, 1);
                        actionTaken = true;
                     }
                }

                if (current == end && currentPickups.Count == 0 && currentDropoffs.Count == 0) {
                    solved = true;
                    break; // Solved
                }

                // Determine Target (Locking)
                if (currentTarget == null) {
                    if (!hasPackage && currentPickups.Count > 0) currentTarget = GetSmartNearest(maze, current, currentPickups, end, visitedThisAttempt);
                    else if (hasPackage && currentDropoffs.Count > 0) currentTarget = GetSmartNearest(maze, current, currentDropoffs, end, visitedThisAttempt);
                    else currentTarget = end;
                }

                (int, int) target = currentTarget.Value;

                // Adjust target to walkable neighbor if it's not end (End is walkable)
                if (target != end) {
                    // Find the best reachable neighbor of the target
                    var bestNeighbor = GetBestReachableNeighbor(maze, current, target, visitedThisAttempt);
                    if (bestNeighbor.Item1 != -1) {
                        target = bestNeighbor;
                    } else {
                        // No reachable neighbor found for this target
                        currentTarget = null; // Unlock and try another target
                        continue; // Skip to next loop to pick new target
                    }
                }

                // Move Logic (Optimal)
                List<(int, int)> path = MazeSimUtils.AStar(maze, current, target, visitedThisAttempt, 100f);
                
                if (path != null && path.Count > 1)
                {
                    (int, int) next = path[1];
                    
                    // Only count as backtrack if reversing to the immediately previous tile
                    // This avoids counting loops or crossings as backtracks, which the user considers "not turning back"
                    if (recordedPath.Count >= 2) {
                        Vector2Int prev = recordedPath[recordedPath.Count - 2];
                        if (next.Item1 == prev.x && next.Item2 == prev.y) {
                            backtracks++;
                        }
                    }

                    current = next;
                    visitedThisAttempt.Add(current);
                    recordedPath.Add(new Vector2Int(current.x, current.y));
                }
                else if (path != null && path.Count == 1)
                {
                    // We are already at the target neighbor.
                    // Just wait for the next iteration to handle interaction.
                    continue;
                }
                else
                {
                    // Path blocked or invalid target
                    // If we are truly stuck (no path to any target), we will eventually run out of steps or reset here.
                    // But let's try to unlock target and see if we can find another one (unlikely if GetSmartNearest works, but safe)
                    currentTarget = null;
                    break; 
                }
            }

            if (solved) break;

            resets++;
            attempts++;
            int remaining = currentPickups.Count + currentDropoffs.Count;
            if (remaining > 0 && remaining <= 2) nearSolves++;
        }

        PlayerModel model = new PlayerModel(attempts, backtracks, nearSolves, resets, totalTime);
        model.pathHistory = recordedPath;
        return model;
    }

    private (int, int) GetSmartNearest(char[,] maze, (int x, int y) from, List<(int, int)> targets, (int x, int y) endPoint, HashSet<(int, int)> visited)
    {
        (int, int) best = targets[0];
        float minCost = float.MaxValue;
        
        foreach (var t in targets)
        {
            // Check all neighbors of the target to find the true shortest path
            var neighbors = MazeSimUtils.GetNeighbors(maze, t.Item1, t.Item2);
            foreach (var n in neighbors) {
                var path = MazeSimUtils.AStar(maze, from, n, visited, 100f);
                if (path != null)
                {
                    // Calculate cost including visited penalty
                    float cost = path.Count;
                    // Add extra cost for visited nodes in the path to prefer unvisited paths
                    foreach(var p in path) {
                        if(visited.Contains(p)) cost += 100f; 
                    }

                    if (cost < minCost)
                    {
                        minCost = cost;
                        best = t;
                    }
                    else if (Mathf.Abs(cost - minCost) < 0.01f)
                    {
                        // Tie-breaker: Choose the one closer to the End point
                        float currentBestDist = MazeSimUtils.Manhattan(best.Item1, best.Item2, endPoint.x, endPoint.y);
                        float newDist = MazeSimUtils.Manhattan(t.Item1, t.Item2, endPoint.x, endPoint.y);
                        
                        if (newDist < currentBestDist)
                        {
                            best = t;
                        }
                    }
                }
            }
        }
        
        // If all unreachable (minCost still Max), fallback to Manhattan
        if (minCost == float.MaxValue) {
             return GetNearest(from, targets);
        }
        
        return best;
    }

    private (int, int) GetBestReachableNeighbor(char[,] maze, (int x, int y) from, (int x, int y) targetNode, HashSet<(int, int)> visited) {
        var neighbors = MazeSimUtils.GetNeighbors(maze, targetNode.Item1, targetNode.Item2);
        (int, int) best = (-1, -1);
        float minCost = float.MaxValue;

        foreach(var n in neighbors) {
            var path = MazeSimUtils.AStar(maze, from, n, visited, 100f);
            if(path != null) {
                float cost = path.Count;
                foreach(var p in path) {
                    if(visited.Contains(p)) cost += 100f;
                }

                if (cost < minCost) {
                    minCost = cost;
                    best = n;
                }
            }
        }
        return best;
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
