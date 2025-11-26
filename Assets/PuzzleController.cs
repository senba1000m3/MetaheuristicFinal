using UnityEngine;
using System.IO;

public class PuzzleController : MonoBehaviour{

    public PuzzleGenerator generator;
    public int size = 5;

    private string savePath;

    void Start(){
        savePath = Path.Combine(Application.dataPath, "random_map.csv");

        char[,] randomMap = GenerateRandomMap(size);
        SaveMapToCSV(randomMap, savePath);

        char[,] loadedMap = LoadMapFromCSV(savePath);

        generator.GenerateFromCharArray(loadedMap);
    }

    char[,] GenerateRandomMap(int size){
        char[,] map = new char[size, size];

        char[] tiles = { '#', 'X', 'P', 'D', 'O', 'S', 'E' };

        for(int y = 0; y < size; y++){
            for(int x = 0; x < size; x++){
                map[y, x] = tiles[Random.Range(0, tiles.Length)];
            }
        }

        return map;
    }

    void SaveMapToCSV(char[,] map, string path){
        int size = map.GetLength(0);

        using(StreamWriter writer = new StreamWriter(path)){
            for(int y = 0; y < size; y++){
                string line = "";
                for(int x = 0; x < size; x++){
                    line += map[y, x];
                    if(x < size - 1) line += ",";
                }
                writer.WriteLine(line);
            }
        }

        Debug.Log("Map saved to: " + path);
    }

    char[,] LoadMapFromCSV(string path){
        string[] lines = File.ReadAllLines(path);
        int size = lines.Length;
        char[,] map = new char[size, size];

        for(int y = 0; y < size; y++){
            string[] cells = lines[y].Split(',');

            for(int x = 0; x < size; x++){
                map[y, x] = cells[x][0];
            }
        }

        Debug.Log("Map loaded from CSV.");

        return map;
    }
}
