using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public struct PieceInfo
{
    public int color;
    public List<int> pieceLocations;
    public Func<List<Move>, List<Move>, Board, int, bool> AddPseudoLegal;

    public PieceInfo(int color, Func<List<Move>, List<Move>, List<int>, int, Board, int, bool> AddPseudoLegal)
    {
        this.color = color;
        var list = new List<int>();
        this.pieceLocations = list;
        this.AddPseudoLegal = (List<Move> moves, List<Move> prio, Board b, int targetSq) => AddPseudoLegal(moves, prio, list, color, b, targetSq);
    }
}
