using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    public class TestSuite
    {

        private int MoveGenTest (Board board, int depth)
        {
            if (depth == 0) return 1;
            int totalPositions = 0;
            List<Move> moves = MoveGenerator.GetLegalMoves(board);
            foreach(Move move in moves)
            {
                board.MakeMove(move);
                totalPositions += MoveGenTest(board, depth - 1);
                board.Undo();
            }
            return totalPositions;
        }

        private void MoveGenBulkTest (Board b, int[] expected)
        {
            MoveGenerator.Init();
            for (int i = 0; i < expected.Length; i++)
            {
                int result = MoveGenTest(b, i);
                Assert.AreEqual(expected[i], result);
            }
        }

        [UnityTest]
        public IEnumerator MoveGenTest()
        {
            Board b = new Board();
            MoveGenBulkTest(b, new int[] { 1, 20, 400, 8902, 197281, 4865609 });
            
            return null;
        }

        [UnityTest]
        public IEnumerator Pos5MoveGenTest()
        {
            Board b = new Board("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8");
            MoveGenBulkTest(b, new int[] { 1, 44, 1486, 62379, 2103487 });

            return null;
        }

        [UnityTest]
        public IEnumerator HashTest ()
        {
            MoveGenerator.Init();
            TranspositionTable.Init();

            Board b = new Board("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8");
            ulong hash = b.hash;
            var moves = MoveGenerator.GetLegalMoves(b);
            foreach(Move move in moves)
            {
                b.MakeMove(move);
                Assert.AreNotEqual(hash, b.hash);
                b.Undo();
                Assert.AreEqual(hash, b.hash);
            }

            return null;
        }
    }
}
