using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public abstract class IPlayerAgent : MonoBehaviour
{
    public abstract PlayerModel Simulate(char[,] maze);

    protected PlayerModel RunSimulation(char[,] maze, float errorRate, float backtrackChance, int patienceSteps, float timePerStep)
    {
        int rows = maze.GetLength(0);
        int cols = maze.GetLength(1);

        // 1. Parse Map
        (int x, int y) start = (-1, -1);
        (int x, int y) end = (-1, -1);
        List<(int, int)> pickups = new List<(int, int)>();
        List<(int, int)> dropoffs = new List<(int, int)>();

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                char c = maze[y, x];
                if (c == 'S') start = (x, y);
                else if (c == 'E') end = (x, y);
                else if (c == 'P') pickups.Add((x, y));
                else if (c == 'D') dropoffs.Add((x, y));
            }
        }

        // Stats
        int attempts = 1;
        int backtracks = 0;
        int nearSolves = 0; // Count how many objectives completed before fail
        int resets = 0;
        int totalSteps = 0;

        // Simulation State
        (int x, int y) currentPos = start;
        bool hasPackage = false;
        HashSet<(int, int)> visitedInSegment = new HashSet<(int, int)>();
        
        // Objectives
        int objectivesCompleted = 0;
        int totalObjectives = pickups.Count + dropoffs.Count + 1; // +1 for End

        int currentAttemptSteps = 0;

        while (true)
        {
            currentAttemptSteps++;
            totalSteps++;

            // Check Patience / Stuck
            if (currentAttemptSteps > patienceSteps)
            {
                resets++;
                attempts++;
                currentAttemptSteps = 0;
                currentPos = start;
                hasPackage = false;
                // Reset objectives? In a real game, a reset means starting over.
                // So we should reset the pickups/dropoffs lists.
                // For simplicity, let's just say we failed this attempt and continue or break?
                // If we reset, we basically start a new "game".
                // Let's re-parse to reset lists.
                pickups.Clear();
                dropoffs.Clear();
                for (int y = 0; y < rows; y++) {
                    for (int x = 0; x < cols; x++) {
                        if (maze[y, x] == 'P') pickups.Add((x, y));
                        else if (maze[y, x] == 'D') dropoffs.Add((x, y));
                    }
                }
                objectivesCompleted = 0;
                visitedInSegment.Clear();
                
                // If too many resets, give up
                if (resets > 5) break;
                continue;
            }

            // Determine Target
            (int x, int y) target = end;
            if (!hasPackage && pickups.Count > 0)
            {
                target = GetNearest(currentPos, pickups);
            }
            else if (hasPackage && dropoffs.Count > 0)
            {
                target = GetNearest(currentPos, dropoffs);
            }
            else
            {
                target = end;
            }

            // Check if reached target
            if (currentPos == target)
            {
                visitedInSegment.Clear(); // New segment
                objectivesCompleted++;
                
                if (target == end && pickups.Count == 0 && dropoffs.Count == 0)
                {
                    // Victory!
                    break;
                }

                // Logic for P/D
                if (pickups.Contains(currentPos))
                {
                    pickups.Remove(currentPos);
                    hasPackage = true;
                }
                else if (dropoffs.Contains(currentPos))
                {
                    dropoffs.Remove(currentPos);
                    hasPackage = false;
                }
                continue;
            }

            // Move Logic
            // 1. Get Ideal Path
            List<(int, int)> path = MazeSimUtils.BFS(maze, currentPos, target);
            
            (int, int) nextStep = currentPos;

            if (path == null || path.Count < 2)
            {
                // No path? Stuck.
                // Random walk or wait (count as step)
                var neighbors = MazeSimUtils.GetNeighbors(maze, currentPos.x, currentPos.y);
                if (neighbors.Count > 0) nextStep = neighbors[Random.Range(0, neighbors.Count)];
            }
            else
            {
                (int, int) idealStep = path[1]; // 0 is current

                // Decide based on Error Rate
                if (Random.value < errorRate)
                {
                    // Make a mistake
                    var neighbors = MazeSimUtils.GetNeighbors(maze, currentPos.x, currentPos.y);
                    // Filter out the ideal one to ensure it's a "mistake" if possible
                    var wrongChoices = neighbors.Where(n => n != idealStep).ToList();
                    
                    if (wrongChoices.Count > 0)
                    {
                        nextStep = wrongChoices[Random.Range(0, wrongChoices.Count)];
                    }
                    else
                    {
                        // Forced to take ideal (dead end or tunnel)
                        nextStep = idealStep;
                    }
                }
                else
                {
                    nextStep = idealStep;
                }
            }

            // Check Backtrack
            if (visitedInSegment.Contains(nextStep))
            {
                backtracks++;
            }
            visitedInSegment.Add(currentPos);
            currentPos = nextStep;
        }

        // Calculate Near Solves (if we didn't finish but did some work)
        // Here we assume if we broke out of loop due to resets, we check progress
        if (pickups.Count > 0 || dropoffs.Count > 0 || currentPos != end)
        {
            // Failed
            if (objectivesCompleted > totalObjectives * 0.5f) nearSolves = 1;
        }
        else
        {
            // Success
            nearSolves = 1; // Or treat as full solve? PlayerModel definition of nearSolves is ambiguous.
            // Usually nearSolves means "Almost solved but failed".
            // If solved, nearSolves = 0? Or maybe we don't care.
            // Let's say nearSolves is for failures.
            nearSolves = 0;
        }

        return new PlayerModel(attempts, backtracks, nearSolves, resets, totalSteps * timePerStep);
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
 