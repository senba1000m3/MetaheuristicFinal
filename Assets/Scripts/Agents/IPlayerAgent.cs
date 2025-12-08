using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public abstract class IPlayerAgent : MonoBehaviour
{
    public abstract PlayerModel Simulate(char[,] maze);

    public IEnumerator MoveAgent(GameObject agent, List<Vector2Int> path, float xOffset, float duration, char[,] map, int agentIndex, PuzzleGenerator generator)
    {
        if (path == null || path.Count == 0) yield break;

        // Ensure duration is positive
        if (duration <= 0) duration = 1f;

        float timePerStep = duration / path.Count;
        
        // Initial pos
        Vector2Int start = path[0];
        // Assuming map is on X-Z plane, Y is up. 
        // MapGenerator uses: new Vector3(x * cellSize + xOffset, 0f, -y * cellSize);
        // We place agent slightly above (0.5f)
        agent.transform.position = new Vector3(start.x * generator.cellSize + xOffset, 0.5f, -start.y * generator.cellSize);

        bool hasPackage = false;
        HashSet<Vector2Int> usedPickups = new HashSet<Vector2Int>();
        HashSet<Vector2Int> usedDropoffs = new HashSet<Vector2Int>();

        // Check initial position interaction
        CheckInteraction(agent, start, map, agentIndex, ref hasPackage, usedPickups, usedDropoffs, generator);

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector2Int p1 = path[i];
            Vector2Int p2 = path[i+1];
            
            Vector3 startPos = new Vector3(p1.x * generator.cellSize + xOffset, 0.5f, -p1.y * generator.cellSize);
            Vector3 endPos = new Vector3(p2.x * generator.cellSize + xOffset, 0.5f, -p2.y * generator.cellSize);
            
            // Check for teleport (reset)
            if (Vector2Int.Distance(p1, p2) > 1.5f) 
            {
                // Teleport
                agent.transform.position = endPos;
                // Reset package state on reset? 
                // Usually reset means failed attempt, so state resets.
                hasPackage = false; 
                // But we don't respawn objects in visualization... 
                // So maybe just keep going.
                // Reset color
                var renderer = agent.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.white;

                yield return null;
                CheckInteraction(agent, p2, map, agentIndex, ref hasPackage, usedPickups, usedDropoffs, generator);
                continue;
            }

            float elapsed = 0f;
            while(elapsed < timePerStep)
            {
                if (agent == null) yield break; // Safety check
                agent.transform.position = Vector3.Lerp(startPos, endPos, elapsed / timePerStep);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (agent != null) agent.transform.position = endPos;

            // Check interaction at new position
            CheckInteraction(agent, p2, map, agentIndex, ref hasPackage, usedPickups, usedDropoffs, generator);
        }
    }

    protected void CheckInteraction(GameObject agent, Vector2Int pos, char[,] map, int agentIndex, ref bool hasPackage, HashSet<Vector2Int> usedPickups, HashSet<Vector2Int> usedDropoffs, PuzzleGenerator generator) {
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);
        
        int[] dx = {0, 0, 1, -1};
        int[] dy = {1, -1, 0, 0};
        
        // Priority: Drop then Pick. If Picked, try Drop again.
        
        // 1. Try Drop
        if (hasPackage) {
            for(int k=0; k<4; k++) {
                int nx = pos.x + dx[k];
                int ny = pos.y + dy[k];
                if(nx >= 0 && nx < cols && ny >= 0 && ny < rows) {
                    char c = map[ny, nx];
                    Vector2Int target = new Vector2Int(nx, ny);
                    if (c == 'D' && !usedDropoffs.Contains(target)) {
                        hasPackage = false;
                        usedDropoffs.Add(target);
                        generator.RemoveObjectAt(agentIndex, nx, ny);
                        var renderer = agent.GetComponent<Renderer>();
                        if (renderer != null) renderer.material.color = Color.white;
                        break; // Only one drop per step? Yes.
                    }
                }
            }
        }

        // 2. Try Pick
        if (!hasPackage) {
            for(int k=0; k<4; k++) {
                int nx = pos.x + dx[k];
                int ny = pos.y + dy[k];
                if(nx >= 0 && nx < cols && ny >= 0 && ny < rows) {
                    char c = map[ny, nx];
                    Vector2Int target = new Vector2Int(nx, ny);
                    if (c == 'P' && !usedPickups.Contains(target)) {
                        hasPackage = true;
                        usedPickups.Add(target);
                        generator.RemoveObjectAt(agentIndex, nx, ny);
                        var renderer = agent.GetComponent<Renderer>();
                        if (renderer != null) renderer.material.color = Color.yellow;
                        break; // Only one pick per step
                    }
                }
            }
        }

        // 3. Try Drop Again (Pick -> Drop)
        if (hasPackage) {
            for(int k=0; k<4; k++) {
                int nx = pos.x + dx[k];
                int ny = pos.y + dy[k];
                if(nx >= 0 && nx < cols && ny >= 0 && ny < rows) {
                    char c = map[ny, nx];
                    Vector2Int target = new Vector2Int(nx, ny);
                    if (c == 'D' && !usedDropoffs.Contains(target)) {
                        hasPackage = false;
                        usedDropoffs.Add(target);
                        generator.RemoveObjectAt(agentIndex, nx, ny);
                        var renderer = agent.GetComponent<Renderer>();
                        if (renderer != null) renderer.material.color = Color.white;
                        break;
                    }
                }
            }
        }
    }
}
 