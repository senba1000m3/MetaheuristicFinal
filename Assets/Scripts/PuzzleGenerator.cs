using UnityEngine;

public enum TileType{
    Empty,      // #
    Path,       // X
    PathUp,     // ↑
    PathDown,   // ↓
    PathLeft,   // ←
    PathRight,  // →
    Pickup,     // P
    Dropoff,    // D
    Obstacle,   // O
    Start,      // S
    End         // E
}

public class PuzzleGenerator : MonoBehaviour{

    [Header("地圖方塊物件")]
    public GameObject emptyPrefab;
    public GameObject pathPrefab;
    public GameObject pickupPrefab;
    public GameObject dropoffPrefab;
    public GameObject obstaclePrefab;
    public GameObject startPrefab;
    public GameObject endPrefab;

    [Header("Settings")]
    public float cellSize = 1f;

    private Transform puzzleRoot;
    private System.Collections.Generic.Dictionary<string, GameObject> instantiatedObjects = new System.Collections.Generic.Dictionary<string, GameObject>();

    public void ClearCache() {
        instantiatedObjects.Clear();
    }

    public void RemoveObjectAt(int agentIndex, int x, int y){
        string key = $"{agentIndex}_{x}_{y}";
        if(instantiatedObjects.ContainsKey(key)){
            if(instantiatedObjects[key] != null){
                Destroy(instantiatedObjects[key]);
            }
            instantiatedObjects.Remove(key);
        }
    }

    public GameObject GetTileObject(int agentIndex, int x, int y)
    {
        string key = $"{agentIndex}_{x}_{y}";
        if (instantiatedObjects.ContainsKey(key))
        {
            return instantiatedObjects[key];
        }
        return null;
    }

    public void GenerateFromCharArray(char[,] map, int index = 0, bool useAutoOffset = true){
        TileType[,] converted = ConvertCharMap(map);
        Generate(converted, index, useAutoOffset);
    }

    public void Generate(TileType[,] map, int index = 0, bool useAutoOffset = true){
        string[] agentNames = {"Beginner", "Normal", "Expert"};
        string rootName = "PuzzleRoot-" + ((index >= 0 && index < agentNames.Length) ? agentNames[index] : index.ToString());
        
        // Ensure we clean up any existing root with the same name before creating a new one
        GameObject existingRoot = GameObject.Find(rootName);
        if (existingRoot != null)
        {
            DestroyImmediate(existingRoot);
        }

        puzzleRoot = new GameObject(rootName).transform;
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);
        float xOffset = useAutoOffset ? index * (cols+2) * cellSize : 0f;
        
        for(int y = 0; y < rows; y++){
            for(int x = 0; x < cols; x++){
                GameObject prefab;
                TileType type = map[y, x];
                prefab = GetPrefab(type);
                if(prefab == null){
                    Debug.LogWarning($"No prefab for TileType at [{y},{x}]");
                    continue;
                }
                Vector3 pos = new Vector3(x * cellSize + xOffset, 0f, -y * cellSize);
                GameObject instance = Instantiate(prefab, pos, Quaternion.identity, puzzleRoot);
                
                string key = $"{index}_{x}_{y}";
                if (instantiatedObjects.ContainsKey(key)) instantiatedObjects.Remove(key);
                instantiatedObjects[key] = instance;
                
                // Apply rotation based on direction
                if (type == TileType.PathRight) {
                    instance.transform.rotation = Quaternion.FromToRotation(Vector3.left, Vector3.right);
                } else if (type == TileType.PathLeft) {
                    instance.transform.rotation = Quaternion.FromToRotation(Vector3.left, Vector3.left);
                } else if (type == TileType.PathUp) {
                    instance.transform.rotation = Quaternion.FromToRotation(Vector3.left, Vector3.forward);
                } else if (type == TileType.PathDown) {
                    instance.transform.rotation = Quaternion.FromToRotation(Vector3.left, Vector3.back);
                }
            }
        }
    }

    private TileType[,] ConvertCharMap(char[,] map){
        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        TileType[,] result = new TileType[rows, cols];

        for(int y = 0; y < rows; y++){
            for(int x = 0; x < cols; x++){

                switch(map[y, x]){
                    case '#': result[y, x] = TileType.Empty; break;
                    case 'X': result[y, x] = TileType.Path; break;
                    case '↑': result[y, x] = TileType.PathUp; break;
                    case '↓': result[y, x] = TileType.PathDown; break;
                    case '←': result[y, x] = TileType.PathLeft; break;
                    case '→': result[y, x] = TileType.PathRight; break;
                    case 'P': result[y, x] = TileType.Pickup; break;
                    case 'D': result[y, x] = TileType.Dropoff; break;
                    case 'O': result[y, x] = TileType.Obstacle; break;
                    case 'S': result[y, x] = TileType.Start; break;
                    case 'E': result[y, x] = TileType.End; break;
                    default:
                        Debug.LogWarning($"Unknown char '{map[y,x]}' at [{y},{x}] → Default Empty");
                        result[y, x] = TileType.Empty;
                        break;
                }
            }
        }

        return result;
    }

    private GameObject GetPrefab(TileType type){
        switch(type){
            case TileType.Empty: return emptyPrefab;
            case TileType.Path: 
            case TileType.PathUp:
            case TileType.PathDown:
            case TileType.PathLeft:
            case TileType.PathRight:
                return pathPrefab;
            case TileType.Pickup: return pickupPrefab;
            case TileType.Dropoff: return dropoffPrefab;
            case TileType.Obstacle: return obstaclePrefab;
            case TileType.Start: return startPrefab;
            case TileType.End: return endPrefab;
        }
        return null;
    }

    public void ClearPreviousPuzzle(){
        if(puzzleRoot != null){
            DestroyImmediate(puzzleRoot.gameObject);
        }
    }
}
