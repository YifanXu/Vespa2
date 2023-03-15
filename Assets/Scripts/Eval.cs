using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class Eval
{
    public class Result
    {
        public int intEval;
        public Stack<Move> line;

        public Result (int eval, bool assignLine = false) {
            this.intEval = eval;
            if (assignLine) line = new Stack<Move>();
        }

        public Result (Result copy)
        {
            this.intEval = copy.intEval;

            // Duplicate stack
            var arr = new Move[copy.line.Count];
            copy.line.CopyTo(arr, 0);
            Array.Reverse(arr);
            this.line = new Stack<Move>(arr);
        }

        public static bool operator > (Result a, Result b)
        {
            return a.intEval > b.intEval;
        }

        public static bool operator < (Result a, Result b)
        {
            return a.intEval < b.intEval;
        }

        public static bool operator > (Result a, int b)
        {
            return a.intEval > b;
        }

        public static bool operator < (Result a, int b)
        {
            return a.intEval < b;
        }

        public static bool operator > (int a, Result b)
        {
            return a > b.intEval;
        }

        public static bool operator < (int a, Result b)
        {
            return a < b.intEval;
        }

        public override string ToString()
        {
            return $"({(intEval > 0 ? "+" : "")}{intEval}){(line.Count == 0 ? "-" : line.Peek().ToString())}";
        }

        public string ToAlgebraic(Board b)
        {
            return $"({(intEval > 0 ? "+" : "")}{intEval}){(line.Count == 0 ? "-" : line.Peek().ToSimpleAlgebraic(b))}";
        }
    }

    public class EvalDiagnosticCount
    {
        public int evalCount = 0;
        public int staticEvalcount = 0;
        public int totalTransposeCount = 0;
        public int betaTransposeCount = 0;
        public int secondaryTransposecount = 0;
        public int leafCount = 0;
        public int quiscenceCount = 0;
        public int quiscenceForceStop = 0;
        public int abPruning = 0;
        public int latestDepth = 0;
        public string latestEval = "-";
    }

    public static readonly Dictionary<int, int> pieceValueTable = new Dictionary<int, int>()
    {
        {Piece.King, 1000 },
        {Piece.Queen, 900 },
        {Piece.Bishop, 330 },
        {Piece.Knight, 300 },
        {Piece.Rook, 500 },
        {Piece.Pawn, 100 }
    };

    private static bool useTT = true;
    private static bool useQS = true;

    private const int endgameMaterial = 16;
    private const int quiscenceSafetyMaterial = 0;
    private const int quiscenceMaxDepth = 4;
    public static Dictionary<int, int[]> piecePosTable;

    private static readonly int[] knightTable =
    {
        -50,-40,-30,-30,-30,-30,-40,-50,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -30,  0, 10, 15, 15, 10,  0,-30,
        -30,  5, 15, 15, 15, 15,  5,-30,
        -30,  0, 15, 15, 15, 15,  0,-30,
        -30,  5,  5, 10, 10,  5,  5,-30,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -50,-40,-30,-30,-30,-30,-40,-50,
    };
    private static readonly int[] bishopTable =
    {
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5, 10, 10,  5,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10, 10,  0,  0,  0,  0, 10,-10,
        -20,-10,-10,-10,-10,-10,-10,-20,
    };
    private static readonly int[] rookTable =
    {
          0,  0,  0,  0,  0,  0,  0,  0,
          5, 10, 10, 10, 10, 10, 10,  5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  2,  2,  0,  0, -5,
          0,  0,  2,  5,  5,  2,  0,  0
    };
    private static readonly int[] queenTable =
{
        -20,-10,-10, -5, -5,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5,  5,  5,  5,  0,-10,
         -5,  0,  5, 10, 10,  5,  0, -5,
          0,  0,  5, 10, 10,  5,  0, -5,
        -10,  5,  5,  5,  5,  5,  0,-10,
        -10,  0,  5,  0,  0,  0,  0,-10,
        -20,-10,-10, -5, -5,-10,-10,-20
    };
    private static readonly int[] pawnTable =
    {
         0,  0,  0,  0,  0,  0,  0,  0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 40, 40, 20, 10, 10,
         5,  5, 20, 35, 35, 20,  5,  5,
         0,  0, 10, 30, 30, 10,  0,  0,
         5, -5,-10,  0,  0,-10, -5,  5,
         5, 10, 10,-20,-20, 10, 10,  5,
         0,  0,  0,  0,  0,  0,  0,  0
    };
    
    private static readonly int[] kingTable =
{
        -50,-60,-80,-90,-90,-80,-60,-50,
        -50,-60,-80,-90,-90,-80,-60,-50,
        -50,-60,-80,-90,-90,-80,-60,-50,
        -50,-50,-60,-70,-70,-60,-50,-50,
        -40,-40,-40,-50,-50,-40,-40,-40,
        -10,-20,-20,-20,-20,-20,-20,-10,
         20, 20,-10,-10,-10,-10, 20, 20,
         20, 30, 10,  0,  0, 10, 30, 20
    };
    private static readonly int[] kingEndTable =
    {
        -50,-40,-30,-20,-20,-30,-40,-50,
        -30,-20,-10,  0,  0,-10,-20,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-30,  0,  0,  0,  0,-30,-30,
        -50,-30,-30,-30,-30,-30,-30,-50
    };

    private const int posInfinity = int.MaxValue;
    private const int negInfinity = int.MinValue;

    public static void Init (bool qs, bool tt)
    {
        useQS = qs;
        useTT = tt;
        // Generate piecePosTable
        piecePosTable = new Dictionary<int, int[]>()
        {
            {Piece.White | Piece.King,   invertTable(kingTable,   true, false)},
            {Piece.White | Piece.None,   invertTable(kingEndTable,true, false)},
            {Piece.White | Piece.Queen,  invertTable(queenTable,  true, false)},
            {Piece.White | Piece.Rook,   invertTable(rookTable,   true, false)},
            {Piece.White | Piece.Knight, invertTable(knightTable, true, false)},
            {Piece.White | Piece.Bishop, invertTable(bishopTable, true, false)},
            {Piece.White | Piece.Pawn,   invertTable(pawnTable,   true, false)},
            {Piece.Black | Piece.King,   invertTable(kingTable,   false, true)},
            {Piece.Black | Piece.None,   invertTable(kingEndTable,false, true)},
            {Piece.Black | Piece.Queen,  invertTable(queenTable,  false, true)},
            {Piece.Black | Piece.Rook,   invertTable(rookTable,   false, true)},
            {Piece.Black | Piece.Knight, invertTable(knightTable, false, true)},
            {Piece.Black | Piece.Bishop, invertTable(bishopTable, false, true)},
            {Piece.Black | Piece.Pawn,   invertTable(pawnTable,   false, true)},
        };
    }

    private static int[] invertTable (int[] table, bool invertPosition, bool invertValues)
    {
        int[] output = new int[64];
        for(int i = 0; i < output.Length; i++)
        {
            int target = invertPosition ? ((7 - (i / 8)) * 8 + i % 8) : i;
            output[i] = invertValues ? (-table[target]) : table[target];
        }
        return output;
    }

    public static Result Evaluate (Board b, int depthRemain, EvalDiagnosticCount diagnostics, CancellationToken ct)
    {
        return InnerEval(b, depthRemain, b.colorToMove == Piece.White, negInfinity, posInfinity, diagnostics, ct);
    }

    private static Result InnerEval (Board b, int depthRemain, bool maximizing, int alpha, int beta, EvalDiagnosticCount diagnostics, CancellationToken ct)
    {
        diagnostics.evalCount++;
        if(ct != null && ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

        // Try to search the transposition table
        Move? principal = null;
        if(useTT && TranspositionTable.GetEntry(b.hash, out var entry))
        {
            // The transposition was searched sufficiently (yay!)
            if (entry.depth >= depthRemain)
            {
                // If the principal move is fully evaluated, we can return the result
                if (entry.type == TranspositionTable.EntryType.Exact)
                {
                    // The entry is fully evaluated and usable!
                    diagnostics.totalTransposeCount++;
                    return new Result(entry.principalMove);
                }
                // If the principal move had a beta cutoff, and causes a beta cutoff here as well, no need to evaluate further
                if (entry.type == TranspositionTable.EntryType.BetaCutOff && ((maximizing && entry.principalMove.intEval >= beta) || (!maximizing && entry.principalMove.intEval <= alpha)))
                {
                    diagnostics.betaTransposeCount++;
                    return new Result(entry.principalMove);
                }
            }

            // The transposition was not searched sufficiently, but we can still steal the principal move    
            if (entry.principalMove.line.Count > 0)
            {
                diagnostics.secondaryTransposecount++;
                principal = entry.principalMove.line.Peek();
            }
        }

        if (depthRemain == 0)
        {
            diagnostics.leafCount++;
            if (useQS)
            {
                return QuiscenceSearch(b, quiscenceMaxDepth, maximizing, alpha, beta, diagnostics, ct);
            }
            int eval = staticEval(b);
            Result newResult = new Result(eval, true);
            if (newResult.line.Count > 20)
            {
                Debug.Log("Wow!");
            }
            TranspositionTable.AddNewEntry(b.hash, new TranspositionTable.TranspositionEntry(
                b.hash,
                0,
                b.turn,
                TranspositionTable.EntryType.Exact,
                new Result(newResult)));
            return newResult;
        }

        List<Move> moves = MoveGenerator.GetLegalMoves(b, Eval.OrderMoveList, principal);
        if (moves.Count == 0)
        {
            if (MoveGenerator.isInCheck(b))
            {
                // Checkmate
                return maximizing ? new Result(negInfinity, true) : new Result(posInfinity, true);
            }

            // Stalemate
            else return new Result(0, true);
        }

        if (maximizing)
        {
            Result maxEval = null;
            bool pruned = false;

            foreach(Move move in moves)
            {
                b.MakeMove(move);
                var newEval = InnerEval(b, depthRemain - 1, false, alpha, beta, diagnostics, ct);

                if (maxEval == null || newEval > maxEval)
                {
                    maxEval = newEval;
                    maxEval.line.Push(move);

                    if(newEval > alpha)
                    {
                        alpha = newEval.intEval;
                        if(beta <= alpha)
                        {
                            pruned = true;
                            b.Undo();
                            diagnostics.abPruning++;
                            break;
                        }
                    }
                }
                b.Undo();
            }
            
            TranspositionTable.AddNewEntry(b.hash, new TranspositionTable.TranspositionEntry(
                b.hash,
                depthRemain,
                b.turn,
                pruned ? TranspositionTable.EntryType.BetaCutOff : TranspositionTable.EntryType.Exact,
                new Result(maxEval)
                ));
            return maxEval;
        }
        else
        {
            Result minEval = null;
            bool pruned = false;

            foreach (Move move in moves)
            {
                b.MakeMove(move);
                var newEval = InnerEval(b, depthRemain - 1, true, alpha, beta, diagnostics, ct);
                if (minEval == null || newEval < minEval)
                {
                    minEval = newEval;
                    minEval.line.Push(move);

                    if (newEval < beta)
                    {
                        beta = newEval.intEval;
                        if (beta <= alpha)
                        {
                            pruned = true;
                            b.Undo();
                            diagnostics.abPruning++;
                            break;
                        }
                    }
                }
                b.Undo();
            }

            TranspositionTable.AddNewEntry(b.hash, new TranspositionTable.TranspositionEntry(
                b.hash,
                depthRemain,
                b.turn,
                pruned ? TranspositionTable.EntryType.BetaCutOff : TranspositionTable.EntryType.Exact,
                new Result(minEval)
                ));

            return minEval;
        }
    }

    public static Result QuiscenceSearch(Board b, int maxDepth, bool maximizing, int alpha, int beta, EvalDiagnosticCount diagnostics, CancellationToken ct)
    {
        diagnostics.quiscenceCount++;

        if (ct != null && ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

        bool setEval = false;
        int baseEval = negInfinity;

        // Look for transposition
        if (useTT && TranspositionTable.GetEntry(b.hash, out var entry) && entry.hash == b.hash)
        {
            // The move must not be a static evaluation (which was the whole point)
            if (entry.depth != 0)
            {
                // (Almost) any type is entry is sufficent
                if (entry.type != TranspositionTable.EntryType.BetaCutOff)
                {
                    diagnostics.totalTransposeCount++;
                    return entry.principalMove;
                }
                // If the move caused a beta cutoff, check for the evaluation here
                else if ((maximizing && entry.principalMove.intEval >= beta) || (!maximizing && entry.principalMove.intEval <= alpha))
                {
                    diagnostics.betaTransposeCount++;
                    return entry.principalMove;
                }

            }
            else
            {
                // At least we can use the eval
                setEval = true;
                baseEval = entry.principalMove.intEval;
            }
        }

        diagnostics.staticEvalcount++;
        if (!setEval) baseEval = staticEval(b);

        if ((maximizing && baseEval >= beta) || (!maximizing && baseEval <= alpha))
        {
            // Beta cutoff: move is already too good, no need to look for ways to improve
            return new Result(baseEval, true);
        }

        // Quiscience is looking too deep, force stop
        if (maxDepth == 0)
        {
            diagnostics.quiscenceForceStop++;
            return new Result(baseEval, true);
        }

        // Minimum Required material gain needed to raise alpha (else the search is pruned)
        int delta;

        // Set base acceptance for side moving
        if (maximizing)
        {
            if (baseEval > alpha) {
                alpha = baseEval;
                delta = 0;
            }
            else
            {
                delta = alpha - baseEval - quiscenceSafetyMaterial;
            }
        }
        else
        {
            
            if (baseEval < beta)
            {
                beta = baseEval;
                delta = 0;
            }
            else
            {
                delta = baseEval - beta - quiscenceSafetyMaterial;
            }
        }

        // If delta is far too big, just give up
        if (delta > 1000) return new Result(baseEval, true);

        List<Move> moves = MoveGenerator.GetLegalMoves(b, OrderMoveListQuiscence);

        Result topEval = null;
        Move? optimal = null;

        if (maximizing)
        {
            foreach (Move move in moves)
            {
                if (move.priorityGuess >= delta)
                {
                    b.MakeMove(move);
                    // Move may sufficient improve result to improve alpha
                    Result r = QuiscenceSearch(b, maxDepth - 1, !maximizing, alpha, beta, diagnostics, ct);
                    if (r == null) continue;
                    if (topEval == null || r > topEval)
                    {
                        topEval = r;
                        optimal = move;
                        if (r > alpha)
                        {
                            alpha = r.intEval;
                            delta = alpha - baseEval - quiscenceSafetyMaterial;
                            if (alpha >= beta)
                            {
                                b.Undo();
                                break;
                            }
                        }
                    }
                    b.Undo();
                }
            }

            //if (topEval == null)
            //{
            //    topEval = new Result(baseEval, true);
            //    TranspositionTable.AddNewEntry(b.hash, new TranspositionTable.TranspositionEntry(
            //        b.hash,
            //        0,
            //        TranspositionTable.EntryType.Exact,
            //        topEval));
            //}
            //else
            //{

            //    // Add transposition
            //    TranspositionTable.AddNewEntry(b.hash, new TranspositionTable.TranspositionEntry(
            //            b.hash,
            //            -1,
            //            reachBeta ? TranspositionTable.EntryType.BetaCutOff : TranspositionTable.EntryType.Quiscence,
            //            topEval));
            //}

            if (optimal.HasValue) topEval.line.Push(optimal.Value);
            return topEval == null ? new Result(baseEval, true) : topEval;
        }
        else
        {
            foreach (Move move in moves)
            {
                if (move.priorityGuess >= delta)
                {
                    b.MakeMove(move);
                    // Move may sufficient improve result to improve alpha
                    Result r = QuiscenceSearch(b, maxDepth - 1, !maximizing, alpha, beta, diagnostics, ct);
                    if (r == null) continue;
                    if (topEval == null || r < topEval)
                    {
                        topEval = r;
                        optimal = move;
                        if (r < beta)
                        {
                            beta = r.intEval;
                            delta = baseEval - beta - quiscenceSafetyMaterial;
                            if (alpha >= beta)
                            {
                                b.Undo();
                                break;
                            }
                        }
                    }
                    b.Undo();
                }
            }

            //if (topEval == null)
            //{
            //    topEval = new Result(baseEval, true);
            //    TranspositionTable.AddNewEntry(b.hash, new TranspositionTable.TranspositionEntry(
            //        b.hash,
            //        0,
            //        TranspositionTable.EntryType.Exact,
            //        topEval));
            //}
            //else
            //{
            //    // Add transposition
            //    TranspositionTable.AddNewEntry(b.hash, new TranspositionTable.TranspositionEntry(
            //            b.hash,
            //            -1,
            //            reachAlpha ? TranspositionTable.EntryType.BetaCutOff : TranspositionTable.EntryType.Quiscence,
            //            topEval));
            //}

            if (optimal.HasValue) topEval.line.Push(optimal.Value);
            return topEval == null ? new Result(baseEval, true) : topEval;
        }
    } 

    public static int staticEval (Board b, bool usePosition = true)
    {
        int materialTotal = 0;
        int eval = 0;
        int whiteKingPos = 0;
        int blackKingPos = 0;


        foreach(var pair in b.whiteLookup)
        {
            int pieceRaw = pair.Key % 8;
            int material = pieceValueTable[pieceRaw];
            if(pieceRaw == Piece.King)
            {
                whiteKingPos = pair.Value.pieceLocations[0];
            }
            else
            {
                foreach(var location in pair.Value.pieceLocations)
                {
                    eval += material;
                    materialTotal += material;
                    if(usePosition) eval += piecePosTable[pair.Key][location];
                }
            }
        }
        foreach (var pair in b.blackLookup)
        {
            int pieceRaw = pair.Key % 8;
            int material = pieceValueTable[pieceRaw];
            if (pieceRaw == Piece.King)
            {
                blackKingPos = pair.Value.pieceLocations[0];
            }
            else
            {
                foreach (var location in pair.Value.pieceLocations)
                {
                    eval -= material;
                    materialTotal += material;
                    if (usePosition) eval += piecePosTable[pair.Key][location];
                }
            }
        }

        if (usePosition)
        {
            if (materialTotal >= endgameMaterial)
            {
                eval += piecePosTable[Piece.White | Piece.King][whiteKingPos];
                eval += piecePosTable[Piece.Black | Piece.King][blackKingPos];
            }
            else
            {
                eval += piecePosTable[Piece.White | Piece.None][whiteKingPos];
                eval += piecePosTable[Piece.Black | Piece.None][blackKingPos];
            }
        }

        return eval;
    }

    public static void OrderMoveList (Board b, List<Move> moveList)
    {
        return;
        for(int i = 0; i < moveList.Count; i++)
        {
            moveList[i] = new Move(moveList[i], EvaluateGuess(b, moveList[i]));
        }
        moveList.Sort(Move.CompareMoveOrdering);
    }

    public static void OrderMoveListQuiscence (Board b, List<Move> moveList)
    {
        for(int i = moveList.Count - 1; i >= 0; i--)
        {
            Move move = moveList[i];
            int materialGain = 0;
            int targetPiece = b[move.target];
            if(b[move.target] != 0)
            {
                // Is a capture, add material gain
                materialGain += pieceValueTable[targetPiece % 8];
            }
            if(move.flag == Move.MoveFlag.Promotion)
            {
                materialGain += pieceValueTable[move.promotion % 8] - pieceValueTable[Piece.Pawn];
            }
            if (materialGain == 0)
            {
                // No need to evaluate moves that do not result in material gain
                moveList.RemoveAt(i);
            }
            else moveList[i] = new Move(move, materialGain);
        }
        //moveList.Sort(Move.CompareMoveOrdering);
    }

    public static int EvaluateGuess (Board b, Move move)
    {
        int movingPiece = b[move.origin];
        int targetPiece = b[move.target];
        int priorityCounter = 0;
        bool isWhite = b.colorToMove == Piece.White;

        // If move is a capture, highly increase the priority by the order of the value difference
        if (targetPiece != 0) {
            // Assign a high value to king to discourage capturing with king
            int movingPieceValue = (movingPiece % 8) == Piece.King ? 1500 : pieceValueTable[movingPiece % 8];
            priorityCounter += 1000 + pieceValueTable[targetPiece % 8] - movingPieceValue;
        }

        if (movingPiece == Piece.Pawn)
        {
            if (move.flag == Move.MoveFlag.Promotion)
            {
                // Really prioritize promotions, plus the value of the promoted piece (prioritize queen promotions)
                priorityCounter += 1000 + pieceValueTable[move.promotion % 8];
            }
            else
            {
                // Promote moves that move pawns up
                priorityCounter += isWhite ? (move.target - move.origin) : (move.origin - move.target);
            }
        }
        else
        {
            // Promote moves that move pieces to better eval squares then where they are at
            var posTable = piecePosTable[movingPiece];
            priorityCounter += (posTable[move.target] - posTable[move.origin]) * 10;
        }

        return priorityCounter;
    }
}