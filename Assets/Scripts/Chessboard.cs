using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Reflection.Emit;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

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
    [SerializeField] private GameObject victoryScreen;


    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    //LOGIC
    private Chesspiece[,] chessPieces;
    private Chesspiece currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<Chesspiece>deadWhites = new List<Chesspiece>();
    private List<Chesspiece>deadBlacks = new List<Chesspiece>();
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private bool isWhiteTurn;
    private SpecialMove specialMove;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();

    // Multi logic
    private int playerCount = -1;
    private int currentTeam = -1;
    private bool localGame = true;


    private void Start()
    {
        isWhiteTurn = true;

        GenerateAlltiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();

        RegisterEvents();
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
        if(Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile" , "Hover", "Highlight")))
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
            if(currentHover != hitposition)
            {
                tiles[currentHover.x, currentHover.y].layer = 
                (ContainsValidMove(ref availableMoves, currentHover) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile"));
                currentHover = hitposition;
                tiles[hitposition.x, hitposition.y].layer = LayerMask.NameToLayer("Hover");
            }

            //If we press down on the mouse
            if(Input.GetMouseButtonDown(0))
            {
                if(chessPieces[hitposition.x, hitposition.y] != null)
                {
                    //Is it our Turn?
                    if((chessPieces[hitposition.x, hitposition.y].team == 0 && isWhiteTurn && currentTeam == 0) || 
                    (chessPieces[hitposition.x, hitposition.y].team == 1 && !isWhiteTurn && currentTeam == 1))
                    {
                        currentlyDragging = chessPieces[hitposition.x , hitposition.y];

                        //Get a list of where i can go, highlight tiles as well
                        availableMoves = currentlyDragging.GetAvailableMove(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                        //Get  a list of special moves as well
                        specialMove = currentlyDragging.GetSpecialMove(ref chessPieces, ref moveList, ref availableMoves);

                        PreventCheck();
                        HighlightTiles();
                    }
                }
            }

            //If we are releasing the mouse button
            if(currentlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousePosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);
                bool validMove = MoveTo(currentlyDragging, hitposition.x, hitposition.y);
                if(!validMove)
                    currentlyDragging.SetPosition(GetTileCenter(previousePosition.x , previousePosition.y));
                
                RemoveHighlightTiles();
                currentlyDragging = null;
            }
        
        
        }
        else
        {
            if(currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = 
                (ContainsValidMove(ref availableMoves, currentHover) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile"));
                currentHover = -Vector2Int.one;
            }
            if(currentlyDragging && Input.GetMouseButtonUp(0))
            {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX , currentlyDragging.currentY));
                currentlyDragging = null;
                RemoveHighlightTiles();
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

    //Highlight Tiles
    private void HighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
    }

    private void RemoveHighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        
        availableMoves.Clear();
    }

    // CheckMate
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }

    private void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
    }

    public void OnResetButton()
    {
        // UI
        victoryScreen.transform.GetChild(0).gameObject.SetActive(true);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(true);
        victoryScreen.SetActive(false);

        // Fields reset
        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();

        // Clean up
        for (int x = 0; x <  TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(chessPieces[x, y] != null)
                    Destroy(chessPieces[x, y].gameObject);

                chessPieces[x, y] = null;
            }
        }

        for (int i = 0; i < deadWhites.Count; i++)
            Destroy(deadWhites[i].gameObject);
        
        for (int i = 0; i < deadBlacks.Count; i++)
            Destroy(deadBlacks[i].gameObject);

        deadWhites.Clear();
        deadBlacks.Clear();

        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;
    }

    public void OnExitButton()
    {
        Application.Quit();
    }

    // Special Moves
    private void ProcessSpecialMove()
    {
        if(specialMove == SpecialMove.EnPassant)
        {
            var _newmove = moveList[moveList.Count - 1];
            Chesspiece _myPawn = chessPieces[_newmove[1].x, _newmove[1].y];
            var _targetPawnPosition = moveList[moveList.Count - 2];
            Chesspiece _enemyPawn = chessPieces[_targetPawnPosition[1].x, _targetPawnPosition[1].y];

            if(_myPawn.currentX == _enemyPawn.currentX)
            {
                if(_myPawn.currentY == _enemyPawn.currentY - 1 || _myPawn.currentY == _enemyPawn.currentY + 1)
                {
                    if(_enemyPawn.team == 0)
                    {
                        deadWhites.Add(_enemyPawn);
                        _enemyPawn.SetScale(Vector3.one * deatSize);
                        _enemyPawn.SetPosition( new Vector3(8.5f * tileSize, yOffset, -1.85f * tileSize) - bounds + 
                        new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadWhites.Count);
                    }
                    else
                    {
                        deadBlacks.Add(_enemyPawn);
                        _enemyPawn.SetScale(Vector3.one * deatSize);
                        _enemyPawn.SetPosition( new Vector3(8.5f * tileSize, yOffset, -1.85f * tileSize) - bounds + 
                        new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadBlacks.Count);
                    }
                    chessPieces[_enemyPawn.currentX, _enemyPawn.currentY] = null;
                }
            }
        }

        if(specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] _lastMove = moveList[moveList.Count - 1];
            Chesspiece _targetPawn = chessPieces[_lastMove[1].x, _lastMove[1].y];
            
            if(_targetPawn.type == ChesspieceType.Pawn)
            {
                if(_targetPawn.team == 0 && _lastMove[1].y == 7)
                {
                    Chesspiece _newQueen = SpawnSinglePiece(ChesspieceType.Queen, 0);
                    _newQueen.transform.position = chessPieces[_lastMove[1].x, _lastMove[1].y].transform.position;
                    Destroy(chessPieces[_lastMove[1].x, _lastMove[1].y].gameObject);
                    chessPieces[_lastMove[1].x, _lastMove[1].y] = _newQueen;
                    PositionSinglePiece(_lastMove[1].x, _lastMove[1].y, true);
                }
                if(_targetPawn.team == 1 && _lastMove[1].y == 0)
                {
                    Chesspiece _newQueen = SpawnSinglePiece(ChesspieceType.Queen, 1);
                    _newQueen.transform.position = chessPieces[_lastMove[1].x, _lastMove[1].y].transform.position;
                    Destroy(chessPieces[_lastMove[1].x, _lastMove[1].y].gameObject);
                    chessPieces[_lastMove[1].x, _lastMove[1].y] = _newQueen;
                    PositionSinglePiece(_lastMove[1].x, _lastMove[1].y, true);
                }
            }
        }

        if(specialMove == SpecialMove.Castling)
        {
            Vector2Int[] _lastMove = moveList[moveList.Count - 1];

            //Left Rok
            if(_lastMove[1].x == 2)
            {
                if(_lastMove[1].y == 0) // White Side
                {
                    Chesspiece _rook = chessPieces[0, 0];
                    chessPieces[3, 0] = _rook;
                    PositionSinglePiece(3, 0);
                    chessPieces[0, 0] = null;
                }
                else if(_lastMove[1].y == 7) //Black Side
                {
                    Chesspiece _rook = chessPieces[0,7];
                    chessPieces[3,7] = _rook;
                    PositionSinglePiece(3, 7);
                    chessPieces[0, 7] = null;
                }
            }
            //Right Rook
            else if(_lastMove[1].x == 6)
            {
                if(_lastMove[1].y == 0) // White Side
                {
                    Chesspiece _rook = chessPieces[7, 0];
                    chessPieces[5, 0] = _rook;
                    PositionSinglePiece(5, 0);
                    chessPieces[7, 0] = null;
                }
                else if(_lastMove[1].y == 7) //Black Side
                {
                    Chesspiece _rook = chessPieces[7,7];
                    chessPieces[5,7] = _rook;
                    PositionSinglePiece(5, 7);
                    chessPieces[7, 7] = null;
                }
            }
        }
    }
    private void PreventCheck()
    {
        Chesspiece _targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if(chessPieces[x, y] != null)
                    if(chessPieces[x, y].type == ChesspieceType.King)
                        if(chessPieces[x, y].team == currentlyDragging.team)
                            _targetKing = chessPieces[x, y];
        
        // Since we're sending in ref available moves we will be deleting moves that are putting us in check
        SimulateMoveForSinglePiece(currentlyDragging, ref availableMoves, _targetKing);
    }

    private void SimulateMoveForSinglePiece(Chesspiece cp, ref List<Vector2Int> moves, Chesspiece targetKing)
    {
        // Save the current value, to reset after the function call
        int _actualX = cp.currentX;
        int _actualY = cp.currentY;
        List<Vector2Int> _movesToRemove = new List<Vector2Int>();

        // Going through all the moves, simulate them and check if we're in check
        for (int i = 0; i < moves.Count; i++)
        {
            int _simX = moves[i].x;
            int _simY = moves[i].y;

            Vector2Int _kingPositionThisSim = new Vector2Int(targetKing.currentX, targetKing.currentY);
            // Did we simulate the king's move
            if(cp.type == ChesspieceType.King)
                _kingPositionThisSim = new Vector2Int(_simX, _simY);
            
            // Copy the [,] and not a refrence
            Chesspiece[,] _simulation  = new Chesspiece[TILE_COUNT_X, TILE_COUNT_Y];
            List<Chesspiece> _simAttackingPieces = new List<Chesspiece>();
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if(chessPieces[x, y] != null)
                    {
                        _simulation[x, y] = chessPieces[x, y];
                        if(_simulation[x, y].team != cp.team)
                            _simAttackingPieces.Add(_simulation[x, y]);
                    }
                }
            }

            // Simulate that move
            _simulation[_actualX, _actualY] = null;
            cp.currentX = _simX;
            cp.currentY = _simY;
            _simulation[_simX, _simY] = cp;

            // Did one of the piece got taken down during our simulation ?
            var _deadPiece = _simAttackingPieces.Find(c => c.currentX == _simX && c.currentY == _simY);
            if(_deadPiece != null)
                _simAttackingPieces.Remove(_deadPiece);

            // Get all the simulated attacking pieces moves
            List<Vector2Int> _simMoves = new List<Vector2Int>();
            for (int a = 0; a < _simAttackingPieces.Count; a++)
            {
                var _pieceMoves = _simAttackingPieces[a].GetAvailableMove(ref _simulation, TILE_COUNT_X, TILE_COUNT_Y);
                for (int b = 0; b < _pieceMoves.Count; b++)
                {
                    _simMoves.Add(_pieceMoves[b]);
                }
            }

            // Is the king in trouble ? if so, remove the move
            if(ContainsValidMove(ref _simMoves, _kingPositionThisSim))
            {
                _movesToRemove.Remove(moves[i]);
            }

            // REstore actual cp data
            cp.currentX = _actualX;
            cp.currentY = _actualY;

        }

        // Remove from the current available move list
        for (int i = 0; i < _movesToRemove.Count; i++)
            moves.Remove(_movesToRemove[i]);
    }

    private bool CheckForCheckmate()
    {
        var _lastMove = moveList[moveList.Count -1 ];
        int _targetTeam = (chessPieces[_lastMove[1].x, _lastMove[1].y].team == 0) ? 1 : 0;

        List<Chesspiece> _attackingPieces = new List<Chesspiece>();
        List<Chesspiece> _defendingPieces = new List<Chesspiece>();
        Chesspiece _targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if(chessPieces[x, y] != null)
                {
                    if(chessPieces[x, y].team == _targetTeam)
                    {
                        _defendingPieces.Add(chessPieces[x, y]);
                        if(chessPieces[x, y].type == ChesspieceType.King)
                            _targetKing = chessPieces[x, y];
                    }
                    else
                    {
                        _attackingPieces.Add(chessPieces[x, y]);
                    }

                }

        // Is the king attacked right now?
        List<Vector2Int> _currentAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < _attackingPieces.Count; i++)
        {
            var _pieceMoves = _attackingPieces[i].GetAvailableMove(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            for (int b = 0; b < _pieceMoves.Count; b++)
            {
                _currentAvailableMoves.Add(_pieceMoves[b]);
            }
        }

        // Are we in check right now ?
        if(ContainsValidMove(ref _currentAvailableMoves, new Vector2Int(_targetKing.currentX, _targetKing.currentY)))
        {
            // King is under attack, can we move something to help him ?
            for (int i = 0; i < _defendingPieces.Count; i++)
            {
                List<Vector2Int> _defendingMoves = _defendingPieces[i].GetAvailableMove(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                // Since we're sending ref availableMoves, we will be deleting moves that are putting us in check
                SimulateMoveForSinglePiece(_defendingPieces[i], ref _defendingMoves, _targetKing);

                if(_defendingMoves.Count  != 0)
                    return false;
            }

            return true; // Checkmate Exit
        }

        return false;
    }

    // Operations
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos)
    {
        for (int i = 0; i < moves.Count; i++)
            if(moves[i].x == pos.x && moves[i].y == pos.y)
                return true;

        return false;
    }

    private bool MoveTo(Chesspiece cp, int x, int y)
    {
        if(!ContainsValidMove( ref availableMoves, new Vector2Int(x,y)))
            return false;

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
                if(othercp.type == ChesspieceType.King)
                    CheckMate(1);

                deadWhites.Add(othercp);
                othercp.SetScale(Vector3.one * deatSize);
                othercp.SetPosition( new Vector3(8.5f * tileSize, yOffset, -1.85f * tileSize) - bounds + 
                new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadWhites.Count);
            }
            else
            {
                if(othercp.type == ChesspieceType.King)
                    CheckMate(0);

                deadBlacks.Add(othercp);
                othercp.SetScale(Vector3.one * deatSize);
                othercp.SetPosition( new Vector3(-1 * tileSize, yOffset, 9f * tileSize) - bounds + 
                new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.back * deathSpacing) * deadBlacks.Count);
            }

        }
        chessPieces[x,y] = cp;
        chessPieces[previousePosition.x , previousePosition.y] = null;

        PositionSinglePiece(x, y);

        isWhiteTurn = !isWhiteTurn;
        if (localGame)
            currentTeam = (currentTeam == 0) ? 1 : 0;
        moveList.Add(new Vector2Int[] {previousePosition, new Vector2Int(x, y)});

        ProcessSpecialMove();
        if(CheckForCheckmate())
            CheckMate(cp.team);

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

    #region
    private void RegisterEvents()
    {
        NetUtility.S_WELCOME += OnWelcomeServer;

        NetUtility.C_WELCOME += OnWelcomeClient;
        NetUtility.C_START_GAME += OnStartGameClient;

        GameUI.instance.SetLocalGame += OnSetLocalGame;
    }
    private void UnRegisterEvents()
    {
        NetUtility.S_WELCOME -= OnWelcomeServer;

        NetUtility.C_WELCOME -= OnWelcomeClient;
        NetUtility.C_START_GAME -= OnStartGameClient;

        GameUI.instance.SetLocalGame -= OnSetLocalGame;
    }

    //Server
    private void OnWelcomeServer(NetMessage msg, NetworkConnection cnn)
    {
        // Client has connected, assign a team and return the message back to him
        NetWelcome nw = msg as NetWelcome;

        // Assign a team
        nw.AssignedTeam = ++playerCount;

        // Return back to the client
        Server.instance.SendToClient(cnn, nw);

        // If full, start the game
        if(playerCount == 1)
            Server.instance.Broadcast(new NetStartGame());
    }

    //Client
    private void OnWelcomeClient(NetMessage msg)
    {
        // Receive the connection message
        NetWelcome nw = msg as NetWelcome;

        // Assign the team
        currentTeam = nw.AssignedTeam;

        Debug.Log($"My assigned team is {nw.AssignedTeam}");

        if(localGame & currentTeam == 0)
            Server.instance.Broadcast(new NetStartGame());
    }
    private void OnStartGameClient(NetMessage obj)
    {
        GameUI.instance.ChangeCamera((currentTeam == 0) ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
    }

    //
    private void OnSetLocalGame(bool v)
    {
        localGame = v;
    }
    #endregion

}
