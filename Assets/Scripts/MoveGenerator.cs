using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class MoveGenerator
{
    private struct moveDelta
    {
        public readonly int rankDelta;
        public readonly int fileDelta;
        
        public moveDelta (int rankDelta, int fileDelta)
        {
            this.rankDelta = rankDelta;
            this.fileDelta = fileDelta;
        }
    }

    private struct KnightMoveInfo
    {
        public readonly int offset;
        public readonly int minRank;
        public readonly int maxRank;
        public readonly int minFile;
        public readonly int maxFile;

        public KnightMoveInfo(int offset, int minRank, int maxRank, int minFile, int maxFile)
        {
            this.offset = offset;
            this.minRank = minRank;
            this.maxRank = maxRank;
            this.minFile = minFile;
            this.maxFile = maxFile;
        }
    }

    // Sliding Tables
    private static readonly int[] directionOffset = new int[] { 8, -8, -1, 1, 7, -7, 9, -9 };
    private static int[][] distanceToEdge;

    // Knight Tables
    private static readonly moveDelta[] knightMoves = new moveDelta[]
    {
        new moveDelta(1, 2),
        new moveDelta(-1, 2),
        new moveDelta(1, -2),
        new moveDelta(-1, -2),
        new moveDelta(2, 1),
        new moveDelta(-2, 1),
        new moveDelta(2, -1),
        new moveDelta(-2, -1),
    };
    private static KnightMoveInfo[] knightOffset;

    // Pawn Promotion
    private static readonly int[] promotionList = new int[]
    {
        Piece.Queen,
        Piece.Knight,
        Piece.Bishop,
        Piece.Rook,
    };

    public static void Init()
    {
        calculateKnightOffsets();
        calculateDistanceToEdge();
    }

    private static void calculateDistanceToEdge()
    {
        distanceToEdge = new int[64][];

        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                int sqIndex = rank * 8 + file;

                distanceToEdge[sqIndex] = new int[] {
                    7 - rank, //north
                    rank, //south
                    file, //west
                    7 - file, //east
                    Mathf.Min(7 - rank, file), //NW
                    Mathf.Min(rank, 7 - file), //SE
                    Mathf.Min(7 - rank, 7 - file), //NE
                    Mathf.Min(rank, file), //SW
                };
            }
        }
    }

    private static void calculateKnightOffsets ()
    {
        knightOffset = new KnightMoveInfo[knightMoves.Length];
        for(int i = 0; i < knightMoves.Length; i++)
        {
            var move = knightMoves[i];
            knightOffset[i] = new KnightMoveInfo(
                move.rankDelta * 8 + move.fileDelta,
                -move.rankDelta,
                7 - move.rankDelta,
                -move.fileDelta,
                7 - move.fileDelta
            );
        }
    }

    public static List<Move> GetPseudoLegalMoves(Board b, Action<Board, List<Move>> orderingMethod = null, Move? principal = null)
    {
        List<Move> normal = new List<Move>();
        List<Move> prio = new List<Move>();
        Dictionary<int, PieceInfo> pieceList = (b.colorToMove == Piece.White) ? b.whiteLookup : b.blackLookup;

        foreach(var pair in pieceList)
        {
            pair.Value.AddPseudoLegal(normal, prio, b, -1);
        }

        if (orderingMethod != null)
        {
            // Set principal to always be first
            if (principal != null)
            {
                bool found = false;
                for(int i = 0; i < prio.Count; i++)
                {
                    if (prio[i].Equals(principal))
                    {
                        found = true;
                        prio.RemoveAt(i);
                        break;
                    }
                }
                if (!found)
                {
                    for(int i = 0; i < normal.Count; i++)
                    {
                        if(normal[i].Equals(principal))
                        {
                            found = true;
                            normal.RemoveAt(i);
                            break;
                        }
                    }
                }
                if (!found) principal = null; // Probably a hash collision
            }
            orderingMethod(b, normal);
            orderingMethod(b, prio);
        }
        List<Move> moves = new List<Move>(prio.Count + normal.Count + (principal == null ? 0 : 1));
        if (principal != null) moves.Add(principal.Value);
        moves.AddRange(prio);
        moves.AddRange(normal);
        return moves;
    }

    public static bool isInCheck (Board b)
    {
        List<Move> normal = new List<Move>();
        List<Move> prio = new List<Move>();
        Dictionary<int, PieceInfo> pieceList = (b.colorToMove == Piece.Black) ? b.whiteLookup : b.blackLookup;
        int friendlyKing = (b.colorToMove == Piece.White) ? b.GetWhiteKing() : b.GetBlackKing();

        foreach (var pair in pieceList)
        {
            if (pair.Value.AddPseudoLegal(normal, prio, b, friendlyKing)) return true;
        }

        return false;
    }

    public static bool SearchForAttack (Board b, int target, bool currentColor = true)
    {
        List<Move> normal = new List<Move>();
        List<Move> prio = new List<Move>();
        Dictionary<int, PieceInfo> pieceList = ((b.colorToMove == Piece.White) == currentColor) ? b.whiteLookup : b.blackLookup;

        foreach (var pair in pieceList)
        {
            if (pair.Value.AddPseudoLegal(normal, prio, b, target)) return true;
        }

        return false;
    }

    public static List<Move> GetLegalMoves(Board b, Action<Board, List<Move>> orderingMethod = null, Move? principalMove = null)
    {
        List<Move> pseudos = GetPseudoLegalMoves(b, orderingMethod, principalMove);
        List<Move> legals = new List<Move>(pseudos.Count);
        foreach(var move in pseudos)
        {
            b.MakeMove(move);
            int opponentKing = (b.colorToMove == Piece.White) ? b.GetBlackKing() : b.GetWhiteKing();
            if (!SearchForAttack(b, opponentKing)) legals.Add(move);
            b.Undo();
        }
        return legals;
    }

    public static bool AddKnightMoves (List<Move> moves, List<Move> prioMoves, List<int> pieces, int pieceColor, Board b, int targetSq = -1)
    {
        foreach(int knight in pieces)
        {
            foreach(var move in knightOffset)
            {
                int target = knight + move.offset;
                int rank = knight / 8;
                int file = knight % 8;

                // Move is only legal if it is on the board
                if (rank >= move.minRank
                    && rank <= move.maxRank
                    && file >= move.minFile
                    && file <= move.maxFile)
                {
                    if (target == targetSq)
                    {
                        return true;
                    }

                    int targetPiece = b[target];
                    if(targetPiece == 0)
                    {
                        if(targetSq == -1) moves.Add(new Move(knight, target, b.colorToMove == Piece.White));
                    }
                    else if(!Piece.IsColor(targetPiece, pieceColor))
                    {
                        if (targetSq == -1) prioMoves.Add(new Move(knight, target, b.colorToMove == Piece.White));
                    }
                }
            }
        }
        return false;
    }

    private static bool AddSlidingMoves (List<Move> moves, List<Move> prioMoves, List<int> pieces, int pieceColor, Board b, int startIndex, int stopIndex, bool isKing = false, int targetSq = -1)
    {
        foreach (int piece in pieces)
        {
            for (int i = startIndex; i < stopIndex; i++)
            {
                int sqDelta = directionOffset[i];
                int dst = isKing? Mathf.Min(1, distanceToEdge[piece][i]) : distanceToEdge[piece][i];
                for (int j = 1; j <= dst; j++)
                {
                    int target = piece + sqDelta * j;

                    if(target == targetSq)
                    {
                        return true;
                    }

                    int targetPiece = b[target];
                    if(targetPiece != 0)
                    {
                        if(!Piece.IsColor(targetPiece, pieceColor))
                        {
                            prioMoves.Add(new Move(piece, target, b.colorToMove == Piece.White));
                        }
                        break;
                    }
                    else
                    {
                        moves.Add(new Move(piece, target, b.colorToMove == Piece.White));
                    }
                }
            }
        }
        return false;
    }

    public static bool AddRookMoves(List<Move> moves, List<Move> prioMoves, List<int> pieces, int pieceColor, Board b, int targetSq = -1)
    {
        return AddSlidingMoves(moves, prioMoves, pieces, pieceColor, b, 0, 4, false, targetSq);
    }

    public static bool AddBishopMoves(List<Move> moves, List<Move> prioMoves, List<int> pieces, int pieceColor, Board b, int targetSq = -1)
    {
        return AddSlidingMoves(moves, prioMoves, pieces, pieceColor, b, 4, 8, false, targetSq);
    }

    public static bool AddQueenMoves(List<Move> moves, List<Move> prioMoves, List<int> pieces, int pieceColor, Board b, int targetSq = -1)
    {
        return AddSlidingMoves(moves, prioMoves, pieces, pieceColor, b, 0, 8, false, targetSq);
    }

    public static bool AddKingMoves(List<Move> moves, List<Move> prioMoves, List<int> pieces, int pieceColor, Board b, int targetSq = -1)
    {
        bool isWhite = pieceColor == Piece.White;
        // Normal King Moves
        if (AddSlidingMoves(moves, prioMoves, pieces, pieceColor, b, 0, 8, true, targetSq)) return true;
        else if (targetSq != -1) return false;

        // Castling
        int kingPos = pieces[0];

        //Castle Flags
        bool kingsideAval = (b.castleAvaliability & (isWhite ? 8: 2)) != 0;
        bool queensideAval = (b.castleAvaliability & (isWhite ? 4: 1)) != 0;

        //Kingside Castling
        if (kingsideAval)
        {
            bool pathClear = true;
            for(int i = 1; i <= 2; i++)
            {
                if(b[kingPos + i] != 0)
                {
                    pathClear = false;
                    break;
                }
            }
            if (pathClear)
            {
                // The color moving should not be in check
                // In addition to a clear path, Move Must not move through check (aka, white should be able to legally play Kf1 and black Kf8)
                int kingSquare = isWhite ? 4 : 60;
                int transitSquare = isWhite ? 5 : 61;
                if (!SearchForAttack(b, kingSquare, false) && !SearchForAttack(b, transitSquare, false))
                {
                    prioMoves.Add(new Move(kingPos, kingPos + 2, b.colorToMove == Piece.White, Move.MoveFlag.KingCastle));
                }
            }
        }

        //Queenside Castling
        if (queensideAval)
        {
            bool pathClear = true;
            for (int i = 1; i <= 3; i++)
            {
                if (b[kingPos - i] != 0)
                {
                    pathClear = false;
                    break;
                }
            }
            if (pathClear)
            {
                // The color moving should not be in check
                // In addition to a clear path, Move Must not move through check (aka, white should be able to legally play Kf1 and black Kf8)
                int kingSquare = isWhite ? 4 : 60;
                int transitSquare = isWhite ? 3 : 59;
                if (!SearchForAttack(b, kingSquare, false) && !SearchForAttack(b, transitSquare, false))
                {
                    prioMoves.Add(new Move(kingPos, kingPos - 2, b.colorToMove == Piece.White, Move.MoveFlag.QueenCastle));
                }
            }
        }

        return false;
    }

    private static bool AddPawnCaptures(List<Move> prioMoves, int pawn, int pieceColor, Board b, int captureDelta, int proRank, int targetSq = -1)
    {
        int target = pawn + captureDelta;
        int targetPiece = b[target];

        // if the targetSquare is same as requested, return immediately
        if (target == targetSq) return true; 
        
        // If the pawn is capturing towards the En Passant Square
        if (targetPiece == 0 && b.epSquare == target)
        {
            prioMoves.Add(new Move(pawn, target, b.colorToMove == Piece.White, Move.MoveFlag.EP));
        }

        // If there is a piece on the capturing square and belongs to the opponent
        else if (targetPiece != 0 && !Piece.IsColor(targetPiece, pieceColor))
        {
            if (pawn / 8 == proRank)
            {
                foreach (int promotion in promotionList)
                {
                    prioMoves.Add(new Move(pawn, target, b.colorToMove == Piece.White, Move.MoveFlag.Promotion, pieceColor | promotion));
                }
            }
            else prioMoves.Add(new Move(pawn, target, b.colorToMove == Piece.White));
        }

        return false;
    }

    public static bool AddPawnMoves(List<Move> moves, List<Move> prioMoves, List<int> pieces, int pieceColor, Board b, int targetSq = -1)
    {
        if (pieceColor == Piece.White) {
            foreach (int pawn in pieces)
            {
                //Captures & EP ALPHA
                bool captureFind = false;
                if (pawn % 8 >= 1) captureFind = captureFind || AddPawnCaptures(prioMoves, pawn, pieceColor, b, 7, 6, targetSq);
                if (pawn % 8 <= 6) captureFind = captureFind || AddPawnCaptures(prioMoves, pawn, pieceColor, b, 9, 6, targetSq);
                if (captureFind) return true;

                // Moving forward
                int target = pawn + 8;
                if(b[target] == 0)
                {
                    if(pawn / 8 == 6)
                    {
                        foreach(int promotion in promotionList)
                        {
                            prioMoves.Add(new Move(pawn, target, b.colorToMove == Piece.White, Move.MoveFlag.Promotion, pieceColor | promotion));
                        }
                    }
                    else moves.Add(new Move(pawn, target, b.colorToMove == Piece.White));

                    // Double Pawn Move
                    if(pawn / 8 == 1)
                    {
                        target += 8;
                        if(b[target] == 0) moves.Add(new Move(pawn, target, b.colorToMove == Piece.White, Move.MoveFlag.Double));
                    }
                }
            }
        }
        else
        {
            foreach (int pawn in pieces)
            {
                //Captures & EP ALPHA
                bool captureFind = false;
                if (pawn % 8 >= 1) captureFind = captureFind || AddPawnCaptures(prioMoves, pawn, pieceColor, b, -9, 1, targetSq);
                if (pawn % 8 <= 6) captureFind = captureFind || AddPawnCaptures(prioMoves, pawn, pieceColor, b, -7, 1, targetSq);
                if (captureFind) return true;

                // Moving forward
                int target = pawn - 8;
                if (b[target] == 0)
                {
                    if (pawn / 8 == 1)
                    {
                        foreach (int promotion in promotionList)
                        {
                            prioMoves.Add(new Move(pawn, target, b.colorToMove == Piece.White, Move.MoveFlag.Promotion, pieceColor | promotion));
                        }
                    }
                    else moves.Add(new Move(pawn, target, b.colorToMove == Piece.White));

                    // Double Pawn Move
                    if (pawn / 8 == 6)
                    {
                        target -= 8;
                        if (b[target] == 0) moves.Add(new Move(pawn, target, b.colorToMove == Piece.White, Move.MoveFlag.Double));
                    }
                }
            }
        }

        return false;
    }
}
