using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class Board
{
    private static Dictionary<char, int> pieceFromSymbol = new Dictionary<char, int>()
    {
        {'k', Piece.King },
        {'p', Piece.Pawn },
        {'n', Piece.Knight },
        {'b', Piece.Bishop },
        {'r', Piece.Rook },
        {'q', Piece.Queen }
    };

    private static Dictionary<int, char> symbolFromPieces = new Dictionary<int, char>()
    {
        { Piece.King,  'k'},
        { Piece.Pawn,  'p'},
        { Piece.Knight,'n'},
        { Piece.Bishop,'b'},
        { Piece.Rook,  'r'},
        { Piece.Queen, 'q'}
    };

    // Masks to check castle state
    private const int whiteKingAval = 8; //1000
    private const int whiteQueenAval = 4; //0100
    private const int blackKingAval = 2; //0010
    private const int blackQueenAval = 1; //0001

    // Board State Information
    public int[] squares;
    public int colorToMove;
    public int castleAvaliability;
    public int epSquare;
    public int halfMoveClock;
    public int turn;

    // Board hash
    public ulong hash;

    // History Trackers (For Undoing)
    public Stack<Move> moveHistory = new Stack<Move>();
    public Stack<int> captureHistory = new Stack<int>();
    public Stack<int> castleAvalHistory = new Stack<int>();
    public Stack<int> epSquareHistory = new Stack<int>();
    public Stack<int> halfMoveClockHistory = new Stack<int>();
    public Stack<ulong> hashHistory = new Stack<ulong>();

    // Look up of all pieces
    public Dictionary<int, PieceInfo> whiteLookup = new Dictionary<int, PieceInfo>()
    {
        {Piece.White | Piece.Knight, new PieceInfo(Piece.White, MoveGenerator.AddKnightMoves) },
        {Piece.White | Piece.Bishop, new PieceInfo(Piece.White, MoveGenerator.AddBishopMoves) },
        {Piece.White | Piece.Pawn, new PieceInfo(Piece.White, MoveGenerator.AddPawnMoves) },
        {Piece.White | Piece.Rook, new PieceInfo(Piece.White, MoveGenerator.AddRookMoves) },
        {Piece.White | Piece.Queen, new PieceInfo(Piece.White, MoveGenerator.AddQueenMoves) },
        {Piece.White | Piece.King, new PieceInfo(Piece.White, MoveGenerator.AddKingMoves) }
    };

    public Dictionary<int, PieceInfo> blackLookup = new Dictionary<int, PieceInfo>()
    {
        {Piece.Black | Piece.Knight, new PieceInfo(Piece.Black, MoveGenerator.AddKnightMoves) },
        {Piece.Black | Piece.Bishop, new PieceInfo(Piece.Black, MoveGenerator.AddBishopMoves) },
        {Piece.Black | Piece.Pawn, new PieceInfo(Piece.Black, MoveGenerator.AddPawnMoves) },
        {Piece.Black | Piece.Rook, new PieceInfo(Piece.Black, MoveGenerator.AddRookMoves) },
        {Piece.Black | Piece.Queen, new PieceInfo(Piece.Black, MoveGenerator.AddQueenMoves) },
        {Piece.Black | Piece.King, new PieceInfo(Piece.Black, MoveGenerator.AddKingMoves) },
    };

    public Board(): this("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")
    {
        
    }

    public Board(string fen)
    {
        squares = new int[64];
        int file = 0, rank = 7;
        string[] fenInfo = fen.Split(' ');
        string fenBoard = fenInfo[0];
        
        foreach(char symbol in fenBoard)
        {
            if(symbol == '/')
            {
                file = 0;
                rank--;
            }
            else
            {
                if(char.IsDigit(symbol))
                {
                    file += (int) char.GetNumericValue(symbol);
                }
                else
                {
                    int pieceColor = char.IsUpper(symbol) ? Piece.White : Piece.Black;
                    int pieceType = pieceFromSymbol[char.ToLower(symbol)];
                    int pieceId = pieceType | pieceColor;
                    squares[rank * 8 + file] = pieceId;
                    if(pieceColor == Piece.White)
                    {
                        whiteLookup[pieceId].pieceLocations.Add(rank * 8 + file);
                    }
                    else
                    {
                        blackLookup[pieceId].pieceLocations.Add(rank * 8 + file);
                    }
                    file++;
                }
            }
        }

        // Color
        this.colorToMove = fenInfo[1] == "w" ? Piece.White : Piece.Black;

        // Castle Avaliability
        string castleAvStr = fenInfo[2];
        string[] castleLetters = new string[] { "q", "k", "Q", "K" };
        int mask = 1;
        foreach(string c in castleLetters)
        {
            if(castleAvStr.Contains(c))
            {
                castleAvaliability += mask;
            }
            mask *= 2;
        }

        // En Passant Square
        string epStr = fenInfo[3];
        if (epStr != "-")
        {
            int epFile = epStr[0] - 'a';
            int epRank = epStr[1] - 1;
            epSquare = epFile + epRank * 8;
        }
        else epSquare = -1;

        // Move Counters
        halfMoveClock = int.Parse(fenInfo[4]);
        turn = int.Parse(fenInfo[5]);

        // Initiate Hash
        hash = TranspositionTable.Hash(this);
    }

    public int this[int id]
    {
        get
        {
            return this.squares[id];
        }
        set
        {
            this.squares[id] = value;
        }
    }

    public int this[int rank, int file]
    {
        get
        {
            return this.squares[rank * 8 + file];
        }
        set
        {
            this.squares[rank * 8 + file] = value;
        }
    }

    public int GetWhiteKing ()
    {
        return whiteLookup[Piece.White | Piece.King].pieceLocations[0];
    }

    public int GetBlackKing()
    {
        return blackLookup[Piece.Black | Piece.King].pieceLocations[0];
    }

    public void MakeMove (Move move)
    {
        // Used to update halfmove clock
        bool isCaptureOrPawnMove = false;

        // Push history
        epSquareHistory.Push(epSquare);
        halfMoveClockHistory.Push(halfMoveClock);
        castleAvalHistory.Push(castleAvaliability);
        moveHistory.Push(move);
        hashHistory.Push(hash);

        // Remove old EP File Data
        if(epSquare != -1)
        {
            this.hash = TranspositionTable.HashToggleEPSquare(hash, epSquare);
        }

        // Used to add to capture History
        int capture = 0;

        var isWhite = colorToMove == Piece.White;
        var friendlyTable = isWhite ? whiteLookup : blackLookup;
        var enemyTable = isWhite ? blackLookup : whiteLookup;

        int movingPiece = squares[move.origin];
        int targetPiece = squares[move.target];
        if (targetPiece != 0)
        {
            // Is Capture, need to update lookup
            isCaptureOrPawnMove = true;
            enemyTable[targetPiece].pieceLocations.Remove(move.target);
            capture = targetPiece;
        }

        // Update the lookup table of the piece that is moving
        if(!friendlyTable.ContainsKey(movingPiece))
        {
            Debug.Log("Illegal!");
        }
        var movingPieceInfo = friendlyTable[movingPiece].pieceLocations;
        movingPieceInfo.Remove(move.origin);
        if(move.flag != Move.MoveFlag.Promotion) movingPieceInfo.Add(move.target);

        // Update Squares
        squares[move.target] = squares[move.origin];
        squares[move.origin] = 0;

        // Update Hash
        this.hash = TranspositionTable.HashTogglePiece(this.hash, movingPiece, move.origin);
        if (move.flag != Move.MoveFlag.Promotion) this.hash = TranspositionTable.HashTogglePiece(this.hash, movingPiece, move.target);
        if (targetPiece != 0) this.hash = TranspositionTable.HashTogglePiece(this.hash, movingPiece, move.target);

        // Remove Castle Rights if rook is captured
        if(targetPiece % 8 == Piece.Rook)
        {
            if (!isWhite)
            {
                if ((castleAvaliability & whiteKingAval) != 0 && move.target == 7) castleAvaliability ^= whiteKingAval;
                if ((castleAvaliability & whiteQueenAval) != 0 && move.target == 0) castleAvaliability ^= whiteQueenAval;
            }
            else
            {
                if ((castleAvaliability & blackKingAval) != 0 && move.target == 63) castleAvaliability ^= blackKingAval;
                if ((castleAvaliability & blackQueenAval) != 0 && move.target == 56) castleAvaliability ^= blackQueenAval;
            }
        }

        if(movingPiece == (colorToMove | Piece.King))
        {
            // Update Castle Avaliablility (If King Moves, Disqualify both)
            if (isWhite)
            {
                if ((castleAvaliability & whiteKingAval) != 0) castleAvaliability ^= whiteKingAval;
                if ((castleAvaliability & whiteQueenAval) != 0) castleAvaliability ^= whiteQueenAval;
            }
            else
            {
                if ((castleAvaliability & blackKingAval) != 0) castleAvaliability ^= blackKingAval;
                if ((castleAvaliability & blackQueenAval) != 0) castleAvaliability ^= blackQueenAval;
            }
        }
        else if(movingPiece == (colorToMove | Piece.Rook))
        {
            // Update Castle Avliability If Rook
            if(isWhite)
            {
                if ((castleAvaliability & whiteKingAval) != 0 && move.origin == 7) castleAvaliability ^= whiteKingAval;
                if ((castleAvaliability & whiteQueenAval) != 0 && move.origin == 0) castleAvaliability ^= whiteQueenAval;
            }
            else
            {
                if ((castleAvaliability & blackKingAval) != 0 && move.origin == 63) castleAvaliability ^= blackKingAval;
                if ((castleAvaliability & blackQueenAval) != 0 && move.origin == 56) castleAvaliability ^= blackQueenAval;
            }
        }
        else if(movingPiece == (colorToMove | Piece.Pawn))
        {
            // Update Halfmove if pawn
            isCaptureOrPawnMove = true;
        }

        // Make special case moves
        switch(move.flag)
        {
            case Move.MoveFlag.Promotion:
                int newPiece = move.promotion;
                squares[move.target] = newPiece;
                friendlyTable[newPiece].pieceLocations.Add(move.target);

                // Update Hash Table to new piece
                this.hash = TranspositionTable.HashTogglePiece(this.hash, newPiece, move.target);
                break;

            case Move.MoveFlag.KingCastle:
                int kingRookOrigin = isWhite ? 7 : 63;
                int kingRookTarget = isWhite ? 5 : 61;

                // Update Rook Table
                var kingRookId = colorToMove | Piece.Rook;
                var kingRookInfo = friendlyTable[kingRookId].pieceLocations;
                kingRookInfo.Remove(kingRookOrigin);
                kingRookInfo.Add(kingRookTarget);

                // Update Squares
                squares[kingRookTarget] = squares[kingRookOrigin];
                squares[kingRookOrigin] = 0;

                // Update Castle Avaliability
                hash = TranspositionTable.HashToggleCastleState(this.hash, castleAvaliability);
                if (isWhite)
                {
                    if ((castleAvaliability & whiteKingAval) != 0) castleAvaliability ^= whiteKingAval;
                    if ((castleAvaliability & whiteQueenAval) != 0) castleAvaliability ^= whiteQueenAval;
                }
                else
                {
                    if ((castleAvaliability & blackKingAval) != 0) castleAvaliability ^= blackKingAval;
                    if ((castleAvaliability & blackQueenAval) != 0) castleAvaliability ^= blackQueenAval;
                }
                hash = TranspositionTable.HashToggleCastleState(this.hash, castleAvaliability);

                // Update Hash
                hash = TranspositionTable.HashTogglePiece(this.hash, kingRookId, kingRookOrigin);
                hash = TranspositionTable.HashTogglePiece(this.hash, kingRookId, kingRookTarget);
                break;

            case Move.MoveFlag.QueenCastle:
                int queenRookOrigin = isWhite ? 0 : 56;
                int queenRookTarget = isWhite ? 3 : 59;

                // Update Rook Table
                var queenRookId = colorToMove | Piece.Rook;
                var queenRookInfo = friendlyTable[colorToMove | Piece.Rook].pieceLocations;
                queenRookInfo.Remove(queenRookOrigin);
                queenRookInfo.Add(queenRookTarget);

                // Update Squares
                squares[queenRookTarget] = squares[queenRookOrigin];
                squares[queenRookOrigin] = 0;

                // Update Castle Avaliability
                hash = TranspositionTable.HashToggleCastleState(this.hash, castleAvaliability);
                if (isWhite)
                {
                    if ((castleAvaliability & whiteKingAval) != 0) castleAvaliability ^= whiteKingAval;
                    if ((castleAvaliability & whiteQueenAval) != 0) castleAvaliability ^= whiteQueenAval;
                }
                else
                {
                    if ((castleAvaliability & blackKingAval) != 0) castleAvaliability ^= blackKingAval;
                    if ((castleAvaliability & blackQueenAval) != 0) castleAvaliability ^= blackQueenAval;
                }
                hash = TranspositionTable.HashToggleCastleState(this.hash, castleAvaliability);

                // Update Hash
                hash = TranspositionTable.HashTogglePiece(this.hash, queenRookId, queenRookOrigin);
                hash = TranspositionTable.HashTogglePiece(this.hash, queenRookId, queenRookTarget);
                break;

            case Move.MoveFlag.EP:
                int epPawnDelta = isWhite ? -8 : 8;
                int targetSq = move.target + epPawnDelta;
                int epPawn = squares[targetSq];

                squares[targetSq] = 0;
                enemyTable[epPawn].pieceLocations.Remove(targetSq);
                hash = TranspositionTable.HashTogglePiece(this.hash, epPawn, targetSq);
                break;
        }

        // Add Capture History
        captureHistory.Push(capture);

        // Update EP Flag
        if (move.flag == Move.MoveFlag.Double)
        {
            // Update to EP square
            int epPawnDelta = isWhite ? -8 : 8;
            epSquare = move.target + epPawnDelta;
            hash = TranspositionTable.HashToggleEPSquare(hash, epSquare);
        }
        else
        {
            epSquare = -1;
        }

        // Update Clock
        halfMoveClock = isCaptureOrPawnMove ? 0 : halfMoveClock + 1;
        if (colorToMove == Piece.Black)
        {
            turn++;
            colorToMove = Piece.White;
        }
        else
        {
            colorToMove = Piece.Black;
        }

        hash = TranspositionTable.HashToggleColorToMove(hash);
    }

    public Move? Undo ()
    {
        if (moveHistory.Count == 0) return null;

        Move move = moveHistory.Pop();
        bool isWhite;

        // Restore Counters
        epSquare = epSquareHistory.Pop();
        halfMoveClock = halfMoveClockHistory.Pop();
        castleAvaliability = castleAvalHistory.Pop();
        hash = hashHistory.Pop();

        // Restore move and turn counter
        if (colorToMove == Piece.White) {
            turn--;
            colorToMove = Piece.Black;
            isWhite = false;
        }
        else
        {
            colorToMove = Piece.White;
            isWhite = true;
        }

        var friendlyTable = isWhite ? whiteLookup : blackLookup;
        var enemyTable = isWhite ? blackLookup : whiteLookup;
        var movingPiece = squares[move.target];
        var captured = captureHistory.Pop();

        // Move piece back to original location
        squares[move.origin] = movingPiece;
        squares[move.target] = captured;

        // Update Captured Piece Table
        if(captured != 0) enemyTable[captured].pieceLocations.Add(move.target);

        if(!friendlyTable.ContainsKey(movingPiece) || !friendlyTable[movingPiece].pieceLocations.Contains(move.target))
        {
            Debug.Log("Help!");
        }
        // Update Moving Piece Table
        friendlyTable[movingPiece].pieceLocations.Remove(move.target);
        if (move.flag != Move.MoveFlag.Promotion) friendlyTable[movingPiece].pieceLocations.Add(move.origin);

        // Handle Special Flagged moves
        switch (move.flag)
        {
            case Move.MoveFlag.Promotion:
                // Need to convert the moving piece back into a pawn
                int pawnId = colorToMove | Piece.Pawn;
                squares[move.origin] = pawnId;
                friendlyTable[pawnId].pieceLocations.Add(move.origin);
                break;

            case Move.MoveFlag.KingCastle:
                // Need to return rook to correct place
                int kingRookOrigin = isWhite ? 7 : 63;
                int kingRookTarget = isWhite ? 5 : 61;

                // Update Rook Table
                var kingRookInfo = friendlyTable[colorToMove | Piece.Rook].pieceLocations;
                kingRookInfo.Remove(kingRookTarget);
                kingRookInfo.Add(kingRookOrigin);

                // Update Squares
                squares[kingRookTarget] = 0;
                squares[kingRookOrigin] = colorToMove | Piece.Rook;
                break;

            case Move.MoveFlag.QueenCastle:
                int queenRookOrigin = isWhite ? 0 : 56;
                int queenRookTarget = isWhite ? 3 : 59;

                // Update Rook Table
                var queenRookInfo = friendlyTable[colorToMove | Piece.Rook].pieceLocations;
                queenRookInfo.Remove(queenRookTarget);
                queenRookInfo.Add(queenRookOrigin);

                // Update Squares
                squares[queenRookTarget] = 0;
                squares[queenRookOrigin] = colorToMove | Piece.Rook;
                break;

            case Move.MoveFlag.EP:
                // Need to restore pawn
                int epPawnDelta = isWhite ? -8 : 8;
                int targetSq = move.target + epPawnDelta;
                int enemyPawnId = (isWhite ? Piece.Black : Piece.White) | Piece.Pawn;

                squares[targetSq] = enemyPawnId;
                enemyTable[enemyPawnId].pieceLocations.Add(targetSq);

                break;
        }

        return move;
    }

    // To fen
    public override string ToString()
    {
        StringBuilder s = new StringBuilder();
        for (int rank = 7; rank >= 0; rank--)
        {
            int emptySpace = 0;
            for (int file = 0; file < 8; file++)
            {
                int piece = squares[rank * 8 + file];
                if (piece == 0)
                {
                    emptySpace++;
                }
                else
                {
                    if (emptySpace != 0)
                    {
                        s.Append(emptySpace);
                        emptySpace = 0;
                    }
                    string pieceChar = symbolFromPieces[piece % 8].ToString();
                    if (piece / 8 == Piece.White / 8)
                    {
                        pieceChar = pieceChar.ToUpper();
                    }
                    s.Append(pieceChar);
                }
            }
            if (emptySpace != 0)
            {
                s.Append(emptySpace);
            }
            if (rank != 0) s.Append('/');
        }

        s.Append(" ");

        // Color To Move
        s.Append(colorToMove == Piece.White ? "w" : "b");

        s.Append(" ");

        // Castle Avaliability
        if(castleAvaliability == 0) s.Append("-");
        else
        {
            if ((castleAvaliability & whiteKingAval) != 0) s.Append("K");
            if ((castleAvaliability & whiteQueenAval) != 0) s.Append("Q");
            if ((castleAvaliability & blackKingAval) != 0) s.Append("k");
            if ((castleAvaliability & blackQueenAval) != 0) s.Append("q");
        }

        s.Append(" ");

        // EP Square
        if (epSquare == -1) s.Append("-");
        else
        {
            s.Append(Move.sqToStr(epSquare));
        }

        // Move Counters
        s.Append($" {halfMoveClock} {turn}");

        return s.ToString();
    }
}
