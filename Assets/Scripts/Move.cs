using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public struct Move
{
    public enum MoveFlag
    {
        None,
        Promotion,
        KingCastle,
        QueenCastle,
        EP,
        Double,
    }
    public readonly int origin;
    public readonly int target;
    public readonly int promotion;
    public readonly MoveFlag flag;
    public int priorityGuess;

    public bool isWhiteMove;

    public Move (int origin, int target, bool isWhiteMove, MoveFlag flag = MoveFlag.None, int promotion = 0)
    {
        this.origin = origin;
        this.target = target;
        this.promotion = promotion;
        this.flag = flag;
        this.priorityGuess = 0;
        this.isWhiteMove = isWhiteMove;
    }

    /// <summary>
    /// Duplicates a move to set priority guess
    /// </summary>
    /// <param name="move">Move to Duplicate</param>
    public Move (Move move, int priority)
    {
        this.origin = move.origin;
        this.target = move.target;
        this.promotion = move.promotion;
        this.flag = move.flag;
        this.priorityGuess = priority;
        this.isWhiteMove = move.isWhiteMove;
    }

    public static int CompareMoveOrdering (Move a, Move b)
    {
        return b.priorityGuess - a.priorityGuess;
    }

    public override int GetHashCode()
    {
        return origin << 10 + target << 4 + promotion;
    }

    public override bool Equals(object obj)
    {
        if (!(obj is Move)) return base.Equals(obj);
        Move other = (Move) obj;
        return this.origin == other.origin && this.target == other.target && this.promotion == other.promotion && this.flag == other.flag;
    }

    public override string ToString()
    {
        return $"[{origin}=>{target}]({(this.isWhiteMove ? 'W' : 'B')})";
    }

    private static char fileToChr (int file)
    {
        return (char)(file + 'a');
    }

    public static string sqToStr (int id)
    {
        int rank = id / 8;
        int file = id % 8;
        return $"{fileToChr(file)}{rank + 1}";
    }

    public string ToSimpleAlgebraic(Board b, string distinguishment = "")
    {
        int originPiece = b[origin] % 8;
        if(flag == MoveFlag.KingCastle)
        {
            return "O-O";
        }
        else if (flag == MoveFlag.QueenCastle)
        {
            return "O-O-O";
        }
        else if (originPiece == Piece.Pawn)
        {
            char promoPiece = flag == MoveFlag.Promotion ? Piece.pieceToLetter[promotion] : ' ';
            if (b[target] == 0 && flag != MoveFlag.EP)
            {
                return $"{sqToStr(target)}{(flag == MoveFlag.Promotion ? "=" + promoPiece : "")}";
            }
            else
            {
                char originFile = fileToChr(origin % 8);
                return $"{originFile}x{sqToStr(target)}{(flag == MoveFlag.Promotion ? "=" + promoPiece : "")}";
            }
        }
        else
        {
            if(originPiece < 1 || originPiece == 2 || originPiece > 6)
            {
                Debug.Log("No");
            }
            char piece = Piece.pieceToLetter[originPiece];
            return $"{piece}{distinguishment}{(b[target] != 0 ? "x" : "")}{sqToStr(target)}";
        }
    }

    // Add distinguishing flags (such as Red1 instead of Rd1) but slower. Use for display and PGN only.
    public string ToComplexAlgebraic (Board b)
    {
        int originPiece = b[origin] % 8;

        if (originPiece == Piece.Pawn)
        {
            // Pawns never need distinguishing, since they are already specified by their file and always move 1 rank per move
            return ToSimpleAlgebraic(b);
        }

        // Geenerate all legal moves
        var competitors = MoveGenerator.GetLegalMoves(b);

        for (var i = competitors.Count - 1; i >= 0; i--)
        {
            // For the move to be considered, it must be 
            // 1. moving same piece
            // 2. going to the same square
            if (competitors[i].target != target || b[competitors[i].origin] != b[origin])
            {
                competitors.RemoveAt(i);
            }
        }

        if (competitors.Count <= 1) {
            return ToSimpleAlgebraic(b);
        }

        // Check distinguishment need
        bool rankOnlyOK = true;
        bool fileOnlyOK = true;

        foreach(var cMove in competitors)
        {
            // Since the same move also fall into "competitors" it needs to be skipped
            if (cMove.origin == origin)
            {
                continue;
            }

            // On the same rank?
            if (cMove.origin / 8 == origin / 8)
            {
                rankOnlyOK = false;
            }

            // Same file?
            if (cMove.origin % 8 == origin % 8)
            {
                fileOnlyOK = false;
            }
        }

        if (fileOnlyOK)
        {
            return ToSimpleAlgebraic(b, fileToChr(origin % 8).ToString());
        }
        else if (rankOnlyOK)
        {
            return ToSimpleAlgebraic(b, (origin / 8 + 1).ToString());
        }
        else
        {
            return ToSimpleAlgebraic(b, sqToStr(origin));
        }
    }
}
