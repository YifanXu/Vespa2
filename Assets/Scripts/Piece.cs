using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public static class Piece
{
    public const int None = 0;
    public const int King = 1;
    public const int Pawn = 2;
    public const int Knight = 3;
    public const int Bishop = 4;
    public const int Rook = 5;
    public const int Queen = 6;

    public const int White = 0;
    public const int Black = 8;

    public static readonly IDictionary<int, char> pieceToLetter = new Dictionary<int, char>()
    {
        {None, '?' },
        {Pawn, 'P' },
        {King, 'K' },
        {Knight, 'N' },
        {Bishop, 'B' },
        {Rook, 'R' },
        {Queen, 'Q' }
    };

    public static bool IsColor (int piece, int color)
    {
        return piece / 8 == color / 8;
    }
}
