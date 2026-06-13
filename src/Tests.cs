using System;
using System.Collections.Generic;
using ChessEngine;
using static ChessEngine.EngineHelpers;
using static ChessEngine.Knight_Moves;

namespace ChessEngine
{
    public static class Tests
    {
        public static void showKnightMoves(int pos)
        {
            //pos is 0 to 63 inclusive
            renderBitboard(KnightPreCalcs[pos], "Knight @ "+IndexToNotation[pos]);

        }
    }
}