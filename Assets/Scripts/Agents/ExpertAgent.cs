using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ExpertAgent : IPlayerAgent{

    public override PlayerModel Simulate(char[,] maze)
    {
        // Expert: Almost perfect
        float errorRate = 0.01f; // 1% chance (misclick?)
        float backtrackChance = 0.0f;
        int patienceSteps = 300;
        float timePerStep = 0.5f; // Fast

        return RunSimulation(maze, errorRate, backtrackChance, patienceSteps, timePerStep);
    }
}
