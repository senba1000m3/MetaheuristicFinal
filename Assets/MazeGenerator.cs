using UnityEngine;
using System.Collections.Generic;

public static class MazeGenerator {

    public static char[,] GenerateMaze(int size, int pickups = 3, int dropoffs = 2){

        if(size % 2 == 0) size += 1;   // 必須奇數才能好挖迷宮

        char[,] map = new char[size, size];

        // 初始化全部為牆壁 O
        for(int y = 0; y < size; y++){
            for(int x = 0; x < size; x++){
                map[y, x] = 'O';
            }
        }

        // DFS 挖迷宮 (只針對內部區域，不處理最外圍)
        int startX = 1;
        int startY = 1;
        map[startY, startX] = 'X';
        DFS(map, startX, startY, size);

        // 放置 Start / End
        PlaceStartAndEnd(map, size);

        // 隨機放置 Pickup / Dropoff
        PlaceSpecialPoints(map, size, pickups, 'P');
        PlaceSpecialPoints(map, size, dropoffs, 'D');

        return map;
    }

    private static void DFS(char[,] map, int x, int y, int size){
        int[] dx = { 0, 0, 2, -2 };
        int[] dy = { 2, -2, 0, 0 };

        List<int> dirs = new List<int> { 0, 1, 2, 3 };
        Shuffle(dirs);

        foreach(int dir in dirs){
            int nx = x + dx[dir];
            int ny = y + dy[dir];

            // 只檢查內部格子，不管最外圍
            if(nx > 0 && nx < size - 1 && ny > 0 && ny < size - 1 && map[ny, nx] == 'O'){
                map[ny, nx] = 'X';
                map[y + dy[dir]/2, x + dx[dir]/2] = 'X';
                DFS(map, nx, ny, size);
            }
        }
    }

    private static void Shuffle(List<int> list){
        for(int i = 0; i < list.Count; i++){
            int rand = Random.Range(i, list.Count);
            int tmp = list[i];
            list[i] = list[rand];
            list[rand] = tmp;
        }
    }

    private static void PlaceStartAndEnd(char[,] map, int size){
        // Start = 第一列找到的路徑
        for(int x = 0; x < size; x++){
            if(map[0, x] == 'X'){
                map[0, x] = 'S';
                break;
            }
        }
        // End = 最後列找到的路徑
        for(int x = size - 1; x >= 0; x--){
            if(map[size - 1, x] == 'X'){
                map[size - 1, x] = 'E';
                break;
            }
        }
    }

    private static void PlaceSpecialPoints(char[,] map, int size, int count, char symbol){
        int placed = 0;
        while(placed < count){
            int x = Random.Range(1, size - 1);
            int y = Random.Range(1, size - 1);

            if(map[y, x] == 'X'){ // 只能放在路徑
                map[y, x] = symbol;
                placed++;
            }
        }
    }
}
