using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardBehavior : MonoBehaviour
{
    public enum BoardColorFilter
    {
        Default,
        WhiteKing,
        WhitePawn,
        WhiteKnight,
        WhiteBishop,
        WhiteRook,
        WhiteQueen,
        BlackKing,
        BlackPawn,
        BlackKnight,
        BlackBishop,
        BlackRook,
        BlackQueen,
    }
    private readonly Dictionary<BoardColorFilter, int> filterToId = new Dictionary<BoardColorFilter, int>()
    {
        { BoardColorFilter.WhiteKing,   Piece.White | Piece.King },
        { BoardColorFilter.WhitePawn,   Piece.White | Piece.Pawn },
        { BoardColorFilter.WhiteKnight, Piece.White | Piece.Knight },
        { BoardColorFilter.WhiteBishop, Piece.White | Piece.Bishop },
        { BoardColorFilter.WhiteRook,   Piece.White | Piece.Rook },
        { BoardColorFilter.WhiteQueen,  Piece.White | Piece.Queen },
        { BoardColorFilter.BlackKing,   Piece.Black | Piece.King },
        { BoardColorFilter.BlackPawn,   Piece.Black | Piece.Pawn },
        { BoardColorFilter.BlackKnight, Piece.Black | Piece.Knight },
        { BoardColorFilter.BlackBishop, Piece.Black | Piece.Bishop },
        { BoardColorFilter.BlackRook,   Piece.Black | Piece.Rook },
        { BoardColorFilter.BlackQueen,  Piece.Black | Piece.Queen },
    };

    [Header("Board Color")]
    public BoardColorFilter filter = BoardColorFilter.Default;
    public Gradient colorEvalGradient;
    public Color lightColor = new Color(1, 1, 1);
    public Color darkColor = new Color(0, 0, 0);
    public Color lightHighlightColor = new Color(1, 1, 1);
    public Color darkHighlightColor = new Color(0, 0, 0);

    [Header("Settings")]
    public float squareSize = 1f;

    [Header("Game Object References")]
    public GameController controller;
    public GameObject whitePromotionPrompt;
    public GameObject blackPromotionPrompt;

    [Header("Prefabs")]
    public GameObject square;
    public GameObject piece;

    

    public Sprite[] pieceSprites;

    private SpriteRenderer[] squareObjects = new SpriteRenderer[64];
    private List<GameObject> pieceObjects;
    private Move highlight = new Move(-1, -1);

    // Flag to disable dragging pieces, for when player is in menu
    public bool pauseDrag = false;

    // Start is called before the first frame update
    void Start()
    {
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                var newSq = Instantiate(square, new Vector3((file - 3.5f) * squareSize, (rank - 3.5f) * squareSize, 0), new Quaternion());
                newSq.transform.SetParent(this.transform);
                squareObjects[rank * 8 + file] = newSq.GetComponent<SpriteRenderer>();
            }
        }

        RenderSquares();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void RenderHighlight (Move move)
    {
        if (highlight.origin != -1)
        {
            squareObjects[highlight.origin].color = (highlight.origin + highlight.origin / 8) % 2 == 1 ? lightColor : darkColor;
            squareObjects[highlight.target].color = (highlight.target + highlight.target / 8) % 2 == 1 ? lightColor : darkColor;
        }
        if (move.origin != -1)
        {
            squareObjects[move.origin].color = (move.origin + move.origin / 8) % 2 == 1 ? lightHighlightColor : darkHighlightColor;
            squareObjects[move.target].color = (move.target + move.target / 8) % 2 == 1 ? lightHighlightColor : darkHighlightColor;
        }
        highlight = move;
    }

    public void ChangeFilter (int newFilter)
    {
        filter = (BoardColorFilter) newFilter;
        RenderSquares();
    }

    public void RenderSquares ()
    {
        for(int i = 0; i < squareObjects.Length; i++)
        {
            Color c;
            int marker;
            if (filter == BoardColorFilter.Default)
            {
                c = (i + (i / 8)) % 2 == 1 ? lightColor : darkColor;
                marker = i;
            }
            else
            {
                int pieceType = filterToId[filter];
                int value = Eval.piecePosTable[pieceType][i];
                float gradientSpot = (value + 50) / 100f;
                c = colorEvalGradient.Evaluate(gradientSpot);
                marker = value;
            }
            squareObjects[i].GetComponent<SpriteRenderer>().color = c;
            squareObjects[i].GetComponentInChildren<Text>().text = marker.ToString();
        }
    }

    public void RenderPieces (int[] board)
    {
        if(pieceObjects != null)
        {
            foreach (var piece in pieceObjects) Destroy(piece);
        }
        pieceObjects = new List<GameObject>();

        for (int i = 0; i < board.Length; i++)
        {
            var sprite = pieceSprites[board[i]];
            if(sprite != null)
            {
                var newPiece = Instantiate(piece, new Vector3((i % 8 - 3.5f) * squareSize, (i / 8 - 3.5f) * squareSize, 0), new Quaternion());
                newPiece.GetComponent<SpriteRenderer>().sprite = sprite;
                pieceObjects.Add(newPiece);

                var newPieceScript = newPiece.GetComponent<PieceBehavior>();
                newPieceScript.location = i;
                newPieceScript.homeLocation = newPiece.transform.position;
                newPieceScript.sqSize = this.squareSize;
                newPieceScript.moveTo = (move) => controller.MakeMove(move);
            }
        }
    }

    public void MakePromotionChoice(int file, bool isWhite)
    {
        pauseDrag = true;
        GameObject prompt = isWhite ? whitePromotionPrompt : blackPromotionPrompt;
        var promptPos = prompt.GetComponent<RectTransform>().localPosition;
        promptPos.x = (file - 3.5f) * squareSize;
        promptPos.y = (4f) * squareSize;
        prompt.GetComponent<RectTransform>().localPosition = promptPos;
        prompt.SetActive(true);
    }

    public void HidePrompt(bool isWhite)
    {
        pauseDrag = false;
        GameObject prompt = isWhite ? whitePromotionPrompt : blackPromotionPrompt;
        prompt.SetActive(false);
    }
}
