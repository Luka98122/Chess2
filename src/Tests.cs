using System;
using System.Collections.Generic;
using ChessEngine;
using static ChessEngine.EngineHelpers;
using static ChessEngine.KnightMoveGenerator;

namespace ChessEngine
{
    public static class Tests
    {
        public static void showKnightMoves(int pos)
        {
            //pos is 0 to 63 inclusive
            renderBitboard(KnightPreCalcs[pos], "Knight @ "+IndexToNotation[pos]);
        }

        public static void allTests()
        {
            Console.WriteLine("Tests:");
            Tests.showKnightMoves(27);
            Tests.showKnightMoves(15);
        }
    }
}