using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BeginnerAgent : IPlayerAgent{

    public override PlayerModel Simulate(char[,] maze)
    {
        // Beginner: High error rate, gets lost easily, gives up quickly
        float errorRate = 0.25f; // 25% chance to make a wrong turn
        float backtrackChance = 0.0f; 
        int patienceSteps = 60; // Gives up if a segment takes too long
        float timePerStep = 2.0f; // Slow thinker

        return RunSimulation(maze, errorRate, backtrackChance, patienceSteps, timePerStep);
    }
}
