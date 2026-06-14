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

        public static void TestRookMagicMoves()
        {
            Console.WriteLine("\n=== RUNNING ROOK MAGIC BITBOARD TEST ===");
            Board b = new Board();
            
            // 1. Clear the board completely (overriding standard setup)
            for(int i = 0; i < 12; i++) 
            {
                b.Pieces[i] = 0UL;
            }

            // 2. Set up a custom scenario
            // White Rook on d4 (Rank 4, File D = index 27)
            b.Pieces[3] |= (1UL << 27); 

            // Friendly White Pawn on d6 (Rank 6, File D = index 43) -> Blocks North
            b.Pieces[0] |= (1UL << 43);

            // Enemy Black Knight on g4 (Rank 4, File G = index 30) -> Blocks East (Capture)
            b.Pieces[7] |= (1UL << 30);

            b.SideToMove = 0; // White's turn

            Console.WriteLine("1. Initial Board Setup (Rook: d4 | Friendly: d6 | Enemy: g4):");
            RenderBoard(b);

            // 3. Generate moves using your Magic Bitboards
            Span<Move> moves = stackalloc Move[218];
            int moveCount = RookMoveGenerator.GetRookMoves(b, b.Pieces[3], 0, moves);

            // 4. Convert the generated Move structs back into a bitboard for visualization
            ulong attackBitboard = 0UL;
            for (int i = 0; i < moveCount; i++)
            {
                attackBitboard |= (1UL << moves[i].ToSquare);
            }

            Console.WriteLine($"\n2. Rook generated {moveCount} valid moves.");
            renderBitboard(attackBitboard, "Valid Rook Targets");
        }

        public static void allTests()
        {
            Console.WriteLine("Tests:");
            Tests.showKnightMoves(27);
            Tests.showKnightMoves(15);

            Tests.TestRookMagicMoves();
        }
    }
}