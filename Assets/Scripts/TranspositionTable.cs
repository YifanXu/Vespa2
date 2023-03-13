using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class TranspositionTable
{
    public enum EntryType
    {
        Exact, // Move is evaluated fully and is the principal variation (acceptable for both sides)
        BetaCutOff, // Move exceeds Beta and is too good for the side moving for its opponent to accept
        Quiscence // Move is searched during a quiscence search
    }

    public class TranspositionEntry
    {
        public ulong hash;
        public int depth;
        public int turn;
        public EntryType type;
        public Eval.Result principalMove;

        public TranspositionEntry(ulong hash, int depth, int turn, EntryType type, Eval.Result principalMove = null)
        {
            this.hash = hash;
            this.depth = depth;
            this.turn = turn;
            this.type = type;
            this.principalMove = principalMove;
        }
    }

    public static ulong[][] zobristKey { get; private set; }
    public static ulong[] castleKeys { get; private set; }
    public static ulong[] epKeys { get; private set; }
    public static ulong colorKey { get; private set; }

    public static TranspositionEntry[] hashTable { get; private set; }

    public static int hashTableOccupancy = 0;

    private const int totalULongCount = 64 * 12 + 16 + 8 + 1;
    public const int tableSize = 1035721;
    public const int minDepth = 0;
    public static bool didInit = false;

    private static readonly Dictionary<int, int> pieceToIdDictionary = new Dictionary<int, int>()
    {
        {Piece.White | Piece.King, 0 },
        {Piece.White | Piece.Queen, 1 },
        {Piece.White | Piece.Bishop, 2 },
        {Piece.White | Piece.Knight, 3 },
        {Piece.White | Piece.Rook, 4 },
        {Piece.White | Piece.Pawn, 5 },
        {Piece.Black | Piece.King, 6 },
        {Piece.Black | Piece.Queen, 7 },
        {Piece.Black | Piece.Bishop, 8 },
        {Piece.Black | Piece.Knight, 9 },
        {Piece.Black | Piece.Rook, 10 },
        {Piece.Black | Piece.Pawn, 11 },
    };
    private static int[] pieceToIdTable;

    public static void Init(int? seed = null)
    {
        // Generate Table
        Random r;
        r = (seed == null) ? new Random() : new Random(seed.Value); 
        byte[] byteBuffer = new byte[totalULongCount * 8];
        r.NextBytes(byteBuffer);

        int bufferIndex = 0;
        zobristKey = new ulong[64][];
        for(int i = 0; i < 64; i++)
        {
            zobristKey[i] = GenerateULongArray(12, byteBuffer, ref bufferIndex);
        }
        castleKeys = GenerateULongArray(16, byteBuffer, ref bufferIndex);
        epKeys = GenerateULongArray(8, byteBuffer, ref bufferIndex);
        colorKey = GenerateULong(byteBuffer, ref bufferIndex);

        //Simplify dictionary
        pieceToIdTable = IntDictionaryToArray<int>(pieceToIdDictionary);

        hashTable = new TranspositionEntry[tableSize];
        didInit = true;
    }

    private static ulong[] GenerateULongArray (int count, byte[] buffer, ref int bufferIndex)
    {
        ulong[] array = new ulong[count];

        for(int i = 0; i < array.Length; i++)
        {
            array[i] = GenerateULong(buffer, ref bufferIndex);
        }

        return array;
    }

    private static ulong GenerateULong (byte[] buffer, ref int index)
    {
        if (index + 8 > buffer.Length) throw new Exception("Ran out of random bytes to use");
        ulong result = ((ulong)buffer[index] << 56)
                 | ((ulong)buffer[index + 1] << 48)
                 | ((ulong)buffer[index + 2] << 40)
                 | ((ulong)buffer[index + 3] << 32)
                 | ((ulong)buffer[index + 4] << 24)
                 | ((ulong)buffer[index + 5] << 16)
                 | ((ulong)buffer[index + 6] << 8)
                 | ((ulong)buffer[index + 7]);
        index += 8;
        return result;
    }

    public static T[] IntDictionaryToArray<T> (Dictionary<int, T> dict)
    {
        int max = -1;
        foreach(var entry in dict)
        {
            if (entry.Key < 0) throw new ArgumentOutOfRangeException("Cannot convert dictionaries with negative integer keys");
            if (entry.Key > max) max = entry.Key;
        };

        T[] array = new T[max + 1];
        foreach(var entry in dict)
        {
            array[entry.Key] = entry.Value;
        }

        return array;
    }

    // Hashing Functions

    /// <summary>
    /// Create a has based on a set board. This should only be used to initialize a hash
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    public static ulong Hash (Board b)
    {
        ulong hash = 0;
        // Encode Squares
        for(int sq = 0; sq < 64; sq++)
        {
            if(b[sq] != 0)
            {
                var pieceId = pieceToIdTable[b[sq]];
                hash ^= zobristKey[sq][pieceId];
            }
        }

        // Encode Castle State
        hash ^= castleKeys[b.castleAvaliability];

        // Encode EP File
        if(b.epSquare != -1)
        {
            hash ^= epKeys[b.epSquare % 8];
        }

        // Encode Side to move
        if (b.colorToMove == Piece.Black)
        {
            hash ^= colorKey;
        }

        return hash;
    }

    public static ulong HashTogglePiece (ulong hash, int piece, int square)
    {
        return hash ^ zobristKey[square][pieceToIdTable[piece]];
    }

    public static ulong HashToggleEPSquare (ulong hash, int epSquare)
    {
        return hash ^ epKeys[epSquare % 8];
    }

    public static ulong HashToggleCastleState (ulong hash, int castleState)
    {
        return hash ^ castleKeys[castleState];
    }

    public static ulong HashToggleColorToMove (ulong hash)
    {
        return hash ^ colorKey;
    }

    // Table Functions

    public static bool CheckForEntry (ulong hash)
    {
        return hashTable[(int)hash % tableSize] != null;
    }

    public static bool AddNewEntry (ulong hash, TranspositionEntry entry)
    {
        if (entry.depth < minDepth) return false;
        int tableId = (int)(hash % tableSize);

        // Do not replace entries that are deeper than new entry
        if(hashTable[tableId] != null && hashTable[tableId].turn + hashTable[tableId].depth > entry.turn + entry.depth)
        {
            return false;
        }

        if (hashTable[tableId] == null) hashTableOccupancy++;

        hashTable[tableId] = entry;
        return true;
    }

    public static bool GetEntry (ulong hash, out TranspositionEntry entry)
    {
        entry = hashTable[hash % tableSize];
        if(entry == null || entry.hash != hash)
        {
            return false;
        }

        return true;
    }

    private static char[] HexDigits = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

    public static string HashToHex (ulong hash, int division = -1)
    {
        StringBuilder s = new StringBuilder();
        for(int i = 0; i < 16; i++)
        {
            if (division != -1 && i % division == 0) s.Append('\n');
            int digit = (int) hash & 15;
            s.Append(HexDigits[digit]);
            hash >>= 4;
        }
        return s.ToString();
    }
}