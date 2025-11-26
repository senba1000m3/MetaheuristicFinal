using UnityEngine;

public enum TileType{
    Empty,      // #
    Path,       // X
    Pickup,     // P
    Dropoff,    // D
    Obstacle,   // O
    Start,      // S
    End         // E
}

public class PuzzleGenerator : MonoBehaviour{

    [Header("Assign Prefabs Here")]
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

    public void GenerateFromCharArray(char[,] map){
        TileType[,] converted = ConvertCharMap(map);
        Generate(converted);
    }

    public void Generate(TileType[,] map){

        ClearPreviousPuzzle();

        puzzleRoot = new GameObject("PuzzleRoot").transform;

        int rows = map.GetLength(0);
        int cols = map.GetLength(1);

        int newRows = rows + 2;
        int newCols = cols + 2;

        for(int y = 0; y < newRows; y++){
            for(int x = 0; x < newCols; x++){
                GameObject prefab;
                if (y == 0 || y == newRows - 1 || x == 0 || x == newCols - 1) {
                    prefab = obstaclePrefab;
                } else {
                    TileType type = map[y - 1, x - 1];
                    prefab = GetPrefab(type);
                }

                if(prefab == null){
                    Debug.LogWarning($"No prefab for TileType at [{y},{x}]");
                    continue;
                }

                Vector3 pos = new Vector3(x * cellSize, 0f, -y * cellSize);
                Instantiate(prefab, pos, Quaternion.identity, puzzleRoot);
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
            case TileType.Path: return pathPrefab;
            case TileType.Pickup: return pickupPrefab;
            case TileType.Dropoff: return dropoffPrefab;
            case TileType.Obstacle: return obstaclePrefab;
            case TileType.Start: return startPrefab;
            case TileType.End: return endPrefab;
        }
        return null;
    }

    private void ClearPreviousPuzzle(){
        if(puzzleRoot != null){
            DestroyImmediate(puzzleRoot.gameObject);
        }
    }
}
