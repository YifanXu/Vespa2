using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{
    public Camera mainCamera;
    public BoardBehavior boardObject;
    public Text debugOutput;
    public Text moveHistoryOutput;
    public AudioClip moveSound;
    public AudioClip captureSound;

    Board board;
    List<Move> currentMoveList;
    Move promotionMoveHolder;

    // Move History UI Info
    private const int moveDisplayLength = 8;
    private List<string> currentMoveHistory;
    private int initialTurn;
    private bool isStartWhite;

    //UI
    public bool showMoveList = false;
    public bool showAIEval = false;

    // AI
    [Header("AI Settings")]
    public int aiColor = Piece.Black;
    public Thread aiCalculateThread;
    public int aiDepth = 6;
    public int aiMaxDepth = 20;
    public int aiMinTime = 1000;
    private EngineResultSuite? latestResult = null;
    private Eval.EvalDiagnosticCount liveDiagnostics = null;

    private Task<EngineResultSuite> aiTask = null;
    private CancellationTokenSource cancelTokenSource;
    private CancellationToken cancelToken;

    private struct EngineResultSuite
    {
        public TimeSpan timeElapsed;
        public Eval.Result evalResult;
        public Eval.EvalDiagnosticCount diagnostics;
    }

    // Start is called before the first frame update
    void Start()
    {
        MoveGenerator.Init();
        Eval.Init();
        TranspositionTable.Init(1890859);

        board = new Board();
        //board = new Board("r5r1/p7/bpk1p2p/1p1p1ppP/2qP4/2P2Q2/PRP1N1PB/3K3R w - - 0 1");
        //board = new Board("5rk1/pQ3p1p/2p3p1/4brq1/2B5/2P4P/PP3PP1/3R1RK1 w - - 1 22");
        boardObject.RenderPieces(board.squares);

        // Generate Moves
        currentMoveList = MoveGenerator.GetLegalMoves(board);
        UpdateDebugInfo(currentMoveList);

        // Init move history
        initialTurn = board.turn;
        isStartWhite = board.colorToMove == Piece.White;
        currentMoveHistory = new List<string>();

        // Init Cancellation Tokens;
        cancelTokenSource = new CancellationTokenSource();
        cancelToken = cancelTokenSource.Token;
    }

    // Update is called once per frame
    void Update()
    {
        // Check for dragging pieces
        if (!boardObject.pauseDrag && Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);

            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);

            if(hit.collider != null)
            {
                var piece = hit.collider.gameObject.GetComponent<PieceBehavior>();
                if(piece != null)
                {
                    piece.isDragged = true;
                    piece.GetComponent<SpriteRenderer>().sortingLayerName = "SelectedPiece";
                    piece.mainCamera = mainCamera;
                }
            }
        }

        // Check for AI move completion
        if (aiTask != null && !aiTask.IsCanceled && aiTask.IsCompleted)
        {
            latestResult = aiTask.Result;
            aiTask = null;

            if (latestResult.Value.evalResult.line.Count > 0)
            {
                Move moveToMake = latestResult.Value.evalResult.line.Peek();
                currentMoveHistory.Add(moveToMake.ToAlgebraic(board));

                board.MakeMove(moveToMake);

                // Play audio
                this.GetComponent<AudioSource>().clip = board.squares[moveToMake.target] == 0 ? moveSound : captureSound;
                this.GetComponent<AudioSource>().Play();

                // Rerender Pieces
                boardObject.RenderPieces(board.squares);
                boardObject.RenderHighlight(moveToMake);
            }

            // Update Move History UI
            currentMoveList = MoveGenerator.GetLegalMoves(board);
            UpdateDebugInfo(currentMoveList);

            // Ree
            aiTask = Task.Run(AIMakeMove);
        }

        if (aiTask != null && !aiTask.IsCompleted && !aiTask.IsCanceled)
        {
            debugOutput.text = GetString(liveDiagnostics);
        }
    }

    public void UpdateDebugInfo(List<Move> moveList)
    {
        string castleStr = "";
        int castleLetter = 8;
        for(int i = 0; i < 4; i++)
        {
            castleStr += (board.castleAvaliability & castleLetter) != 0 ? "o" : "-";
            castleLetter /= 2;
        }
        string s = $"Turn={board.turn}({(board.colorToMove == Piece.White ? "white" : "black")})\nHistoryStackCount={board.moveHistory.Count}\nCastle={castleStr}\nHalfMove={board.halfMoveClock}\nEP={board.epSquare}\n\n";
        s += $"\"{board.ToString()}\"\n";
        s += $"Hash = {TranspositionTable.HashToHex(board.hash, 16)}\n";
        s += $"TT Table = {TranspositionTable.hashTableOccupancy}/{TranspositionTable.tableSize}\n";

        if (showAIEval)
        {
            // Engine Info
            s += $"Static Eval(Simple) = {Eval.staticEval(board, false)}\n";
            s += $"Static Eval(Normal) = {Eval.staticEval(board)}\n\n";

            // AI Last Justification
            s += "AI Last Move:\n";
            if (latestResult != null) {
                s += $"AI Evaluation = {(float)(latestResult.Value.evalResult.intEval) / 100f}\n";
                s += $"{latestResult.Value.diagnostics.evalCount} Evals ({latestResult.Value.diagnostics.staticEvalcount} leaf)\n";
                s += $"({latestResult.Value.diagnostics.quiscenceCount} QSearch (-{latestResult.Value.diagnostics.quiscenceForceStop}), {latestResult.Value.diagnostics.staticEvalcount} staticEvals)\n";
                s += $"(T1={latestResult.Value.diagnostics.totalTransposeCount}, T2={latestResult.Value.diagnostics.betaTransposeCount} T3={latestResult.Value.diagnostics.secondaryTransposecount})\n";
                s += $"Time Elapsed: {latestResult.Value.timeElapsed.ToString(@"m\:ss\.fff")}\n";
                var line = latestResult.Value.evalResult.line;
                while (line.Count > 0)
                {
                    s += line.Pop().ToString() + "\n";
                }
            }
            s += "\n";
        }

        if (showMoveList)
        {
            s += "\n";
            var newList = MoveGenerator.GetLegalMoves(board, Eval.OrderMoveList);
            foreach (var item in newList)
            {
                s += $"{item.ToAlgebraic(board)}({item.ToString()}{(item.flag == Move.MoveFlag.None ? "" : $"(flag={item.flag}")})\n";
            }
        }

        debugOutput.text = s;
    }

    public void UpdateMoveList ()
    {
        StringBuilder s = new StringBuilder();
        int turn = initialTurn;
        bool isWhite = isStartWhite;
        const string space = " ";

        // Add starting blanks if first move is black for some reason
        if(!isStartWhite)
        {
            s.Append(turn);
            for(int i = 0; i < moveDisplayLength + 1; i++)
            {
                s.Append(space);
            }
        }

        foreach (var move in currentMoveHistory)
        {
            if(isWhite)
            {
                s.Append(turn);
                s.Append(space);
                s.Append(move);

                // Add spaces so black moves align
                for (int j = 0; j < moveDisplayLength - move.Length; j++)
                {
                    s.Append(space);
                }
            }
            else
            {
                s.Append(move);
                s.AppendLine();
                turn++;
            }
            isWhite = !isWhite;
        }

        moveHistoryOutput.text = s.ToString();
    }

    public void ConfirmPromotion(int promotion)
    {
        MakeMove(promotionMoveHolder, promotion);
    }

    public bool MakeMove(Move move, int promotion = 0)
    {
        if(aiTask != null && !aiTask.IsCanceled && !aiTask.IsCompleted)
        {
            // Player is not allowed to move again while AI is thinking
            return false;
        }
        // Move Lookup
        bool foundLegalMove = false;
        foreach(Move legal in currentMoveList)
        {
            if(move.origin == legal.origin && move.target == legal.target)
            {
                if(legal.flag == Move.MoveFlag.Promotion)
                {
                    if (promotion == 0)
                    {
                        // It's a promotion, player has yet to decide
                        promotionMoveHolder = move;
                        boardObject.MakePromotionChoice(move.target % 8, board.colorToMove == Piece.White);
                        return false;
                    }
                    else if(legal.promotion != promotion)
                    {
                        // Current promotion, but not to the current piece. Keep Looking.
                        continue;
                    }
                }
                foundLegalMove = true;
                move = legal;
                break;
            }
        }

        if (!foundLegalMove)
        {
            UnityEngine.Debug.Log($"{move.ToString()} is illegal!");
            return false;
        }

        // Update Move History UI
        currentMoveHistory.Add(move.ToAlgebraic(board));
        UpdateMoveList();

        // Update Debug UI
        currentMoveList = MoveGenerator.GetLegalMoves(board);
        UpdateDebugInfo(currentMoveList);

        // Actually move
        board.MakeMove(move);

        // Play audio
        this.GetComponent<AudioSource>().clip = board.squares[move.target] == 0 ? moveSound : captureSound;
        this.GetComponent<AudioSource>().Play();

        // Rerender Pieces
        boardObject.RenderPieces(board.squares);
        boardObject.RenderHighlight(move);

        if (board.colorToMove == aiColor)
        {
            aiTask = Task.Run(AIMakeMove);
        }
        else
        {
            // Make move list
            currentMoveList = MoveGenerator.GetLegalMoves(board);
            UpdateDebugInfo(currentMoveList);
        }

        return true;
    }

    public void UndoMove ()
    {
        // If the engine is currently making a move, abort the calculation
        if(aiTask != null && !aiTask.IsCompleted)
        {
            cancelTokenSource.Cancel();
        }

        bool undoingAImove = board.colorToMove == aiColor;

        var move = board.Undo();
        currentMoveHistory.RemoveAt(currentMoveHistory.Count - 1);
        if (move != null)
        {
            // If the first move undone is by the AI, or if the move undone now is also by the ai, undo again!
            if(undoingAImove || board.colorToMove == aiColor)
            {
                var newMove = board.Undo();
                currentMoveHistory.RemoveAt(currentMoveHistory.Count - 1);
                if (newMove != null) move = newMove;
            }

            // Make move list
            currentMoveList = MoveGenerator.GetLegalMoves(board);
            UpdateDebugInfo(currentMoveList);
            UpdateMoveList();

            // Rerender Pieces
            boardObject.RenderPieces(board.squares);
            boardObject.RenderHighlight(board.moveHistory.Count == 0 ? new Move(-1, -1) : board.moveHistory.Peek());
        }
        else
        {
            UnityEngine.Debug.Log("No more moves to undo!");
        }
    }

    private EngineResultSuite AIMakeMove ()
    {
        liveDiagnostics = new Eval.EvalDiagnosticCount();
        cancelToken = cancelTokenSource.Token;

        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        Eval.Result result = null;
        for (int i = 1;aiDepth % 2 != 0 || i <= aiDepth * 2 || timer.ElapsedMilliseconds < aiMinTime; i++)
        {
            result = Eval.Evaluate(board, i, liveDiagnostics, cancelToken);
            liveDiagnostics.latestDepth = i;
            liveDiagnostics.latestEval = result.ToAlgebraic(board);
        }

        timer.Stop();
        TimeSpan timeTaken = timer.Elapsed;

        var returnObj = new EngineResultSuite();
        returnObj.diagnostics = liveDiagnostics;
        returnObj.evalResult = result;
        returnObj.timeElapsed = timeTaken;

        return returnObj;
    }

    public string GetString (Eval.EvalDiagnosticCount diagnostics)
    {
        StringBuilder s = new StringBuilder();
        foreach (var field in diagnostics.GetType().GetFields())
        {
            s.AppendLine($"{field.Name}={field.GetValue(diagnostics)}");
        }
        return s.ToString();
    }
}
