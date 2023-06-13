using System.Reflection.Emit;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chessboard : MonoBehaviour
{
    [Header("Art stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deatSize = 0.3f;
    [SerializeField] private float deathSpacing = 5f;
    [SerializeField] private float dragOffset = 5f;


    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    //LOGIC
    private Chesspiece[,] chessPieces;
    private Chesspiece currentlyDragging;
    private List<Chesspiece>deadWhites = new List<Chesspiece>();
    private List<Chesspiece>deadBlacks = new List<Chesspiece>();
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;


    private void Awake()
    {
        GenerateAlltiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();
    }

    private void Update()
    {
        if(!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if(Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile" , "Hover")))
        {
            //Get the indexes of the tile i,ve hit
            Vector2Int hitposition = LookupTileIndex(info.transform.gameObject);

            //If we're hovering a tile after not hovering any tiles
            if(currentHover == -Vector2Int.one)
            {
                currentHover = hitposition;
                tiles[hitposition.x, hitposition.y].layer=LayerMask.NameToLayer("Hover");
            }

            //if we were already hovering a tile, change the previouse one
            if(currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = LayerMask.NameToLayer("Tile");
                currentHover = hitposition;
                tiles[hitposition.x, hitposition.y].layer = LayerMask.NameToLayer("Hover");
            }

            //If we press down on the mouse
            if(Input.GetMouseButtonDown(0))
            {
                if(chessPieces[hitposition.x, hitposition.y] != null)
                {
                    //Is it our Turn?
                    if(true)
                    {
                        currentlyDragging = chessPieces[hitposition.x , hitposition.y];      
                    }
                }
            }

            //If we are releasing the mouse button
            if(currentlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousePosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

                bool validMove = MoveTo(currentlyDragging, hitposition.x, hitposition.y);
                if(!validMove)
                {
                    currentlyDragging.SetPosition(GetTileCenter(previousePosition.x , previousePosition.y));
                    currentlyDragging = null;
                }
                else
                {
                    currentlyDragging = null;
                    
                }
            }
        
        
        }
        else
        {
            if(currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }
            if(currentlyDragging && Input.GetMouseButtonUp(0))
            {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX , currentlyDragging.currentY));
                currentlyDragging = null;
                
            }
        }

        //If we're dragging a piece
        if(currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if(horizontalPlane.Raycast(ray, out distance))
                currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
        }
    }

    

    //Generate the board
    private void GenerateAlltiles(float tilesize, int tilecountX, int tilecountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((tilecountX / 2) * tilesize, 0, (tilecountX / 2) * tilesize) + boardCenter;

        tiles = new GameObject[tilecountX, tilecountY];
        for (int x = 0; x < tilecountX; x++)
            for (int y = 0; y < tilecountY; y++)
                tiles[x, y] = GenerateSingleTile(tilesize, x, y);
    }

    private GameObject GenerateSingleTile(float tilesize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("x : {0} , y : {1}", x, y));
        tileObject.transform.parent = transform;
        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;
        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tilesize, yOffset, y * tilesize) - bounds;
        vertices[1] = new Vector3(x * tilesize, yOffset, (y + 1) * tilesize) - bounds;
        vertices[2] = new Vector3((x + 1) * tilesize, yOffset, y * tilesize) - bounds;
        vertices[3] = new Vector3((x + 1) * tilesize, yOffset, (y + 1) * tilesize) - bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };
        mesh.vertices = vertices;
        mesh.triangles = tris;

        //mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();
        return tileObject;
    }

    // Spawning of the pieces
    private void SpawnAllPieces()
    {
        chessPieces = new Chesspiece[TILE_COUNT_X, TILE_COUNT_Y];

        int whiteTeam = 0, blackTeam = 1;

        //whiteTeam
        chessPieces[0,0] = SpawnSinglePiece(ChesspieceType.Rook, whiteTeam);
        chessPieces[1,0] = SpawnSinglePiece(ChesspieceType.Knight, whiteTeam);
        chessPieces[2,0] = SpawnSinglePiece(ChesspieceType.Bishop, whiteTeam);
        chessPieces[3,0] = SpawnSinglePiece(ChesspieceType.Queen, whiteTeam);
        chessPieces[4,0] = SpawnSinglePiece(ChesspieceType.King, whiteTeam);
        chessPieces[5,0] = SpawnSinglePiece(ChesspieceType.Bishop, whiteTeam);
        chessPieces[6,0] = SpawnSinglePiece(ChesspieceType.Knight, whiteTeam);
        chessPieces[7,0] = SpawnSinglePiece(ChesspieceType.Rook, whiteTeam);
        for (int i = 0; i < TILE_COUNT_X; i++)
            chessPieces[i,1] = SpawnSinglePiece(ChesspieceType.Pawn, whiteTeam);

        //blackTeam
        chessPieces[0,7] = SpawnSinglePiece(ChesspieceType.Rook, blackTeam);
        chessPieces[1,7] = SpawnSinglePiece(ChesspieceType.Knight, blackTeam);
        chessPieces[2,7] = SpawnSinglePiece(ChesspieceType.Bishop, blackTeam);
        chessPieces[3,7] = SpawnSinglePiece(ChesspieceType.Queen, blackTeam);
        chessPieces[4,7] = SpawnSinglePiece(ChesspieceType.King, blackTeam);
        chessPieces[5,7] = SpawnSinglePiece(ChesspieceType.Bishop, blackTeam);
        chessPieces[6,7] = SpawnSinglePiece(ChesspieceType.Knight, blackTeam);
        chessPieces[7,7] = SpawnSinglePiece(ChesspieceType.Rook, blackTeam);
        for (int i = 0; i < TILE_COUNT_X; i++)
            chessPieces[i,6] = SpawnSinglePiece(ChesspieceType.Pawn, blackTeam);

    }

    private Chesspiece SpawnSinglePiece(ChesspieceType type, int team)
    {
        Chesspiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<Chesspiece>();
        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];


        return cp;
    }

    // Positioning
    private void PositionAllPieces()
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if(chessPieces[x,y] != null)
                    PositionSinglePiece(x, y, true);
    }

    private void PositionSinglePiece(int x, int y, bool force = false)
    {
        chessPieces[x,y].currentX = x;
        chessPieces[x,y].currentY = y;
        chessPieces[x,y].SetPosition(GetTileCenter(x, y), force);
    }

    private Vector3 GetTileCenter(int x, int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize/2);
    }

    // Operations

    private bool MoveTo(Chesspiece cp, int x, int y)
    {
        Vector2Int previousePosition = new Vector2Int(cp.currentX, cp.currentY);

        //Is there another piece on the target position ?
        if(chessPieces[x,y] != null)
        {
            Chesspiece othercp = chessPieces[x,y];
            if(cp.team == othercp.team)
                return false;

            //If its the enemy team
            if(othercp.team == 0)
            {
                deadWhites.Add(othercp);
                othercp.SetScale(Vector3.one * deatSize);
                othercp.SetPosition( new Vector3(8.5f * tileSize, yOffset, -1.85f * tileSize) - bounds + 
                new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadWhites.Count);
            }
            else
            {
                deadBlacks.Add(othercp);
                othercp.SetScale(Vector3.one * deatSize);
                othercp.SetPosition( new Vector3(-1 * tileSize, yOffset, 9f * tileSize) - bounds + 
                new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.back * deathSpacing) * deadBlacks.Count);
            }

        }
        chessPieces[x,y] = cp;
        chessPieces[previousePosition.x , previousePosition.y] = null;

        PositionSinglePiece(x, y);
        return true;
    }

    private Vector2Int LookupTileIndex(GameObject hitinfo)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if(tiles[x,y] == hitinfo)
                    return new Vector2Int(x, y);
        
        return Vector2Int.one; //Invalid 
    }

}
