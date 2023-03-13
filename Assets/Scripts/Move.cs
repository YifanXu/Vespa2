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

    public Move (int origin, int target, MoveFlag flag = MoveFlag.None, int promotion = 0)
    {
        this.origin = origin;
        this.target = target;
        this.promotion = promotion;
        this.flag = flag;
        this.priorityGuess = 0;
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
        return $"[{origin}=>{target}]";
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

    public string ToAlgebraic(Board b)
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
            return $"{piece}{(b[target] != 0 ? "x" : "")}{sqToStr(target)}";
        }
    }
}
