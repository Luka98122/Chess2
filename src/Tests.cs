using System;
using ChessEngine;
using static ChessEngine.EngineHelpers;

namespace ChessEngine
{
    // Define a delegate so we can pass different move generator functions to our test runner dynamically
    public delegate int MoveGenerator(Board b, ulong pieces, int color, Span<Move> moves);

    public static class Tests
    {
        public static void allTests()
        {
            Console.WriteLine("=== RUNNING ENGINE TESTS ===\n");

            // --- 1. ROOK TESTS ---
            // d4 (index 27)
            RunTest("1. Rook (Open Center)", SetupBoard((3, 27)), 0, 3, 27);
            // d4 with friendly Pawn on d6 (index 43) and enemy Knight on g4 (index 30)
            RunTest("2. Rook (Blocked & Captures)", SetupBoard((3, 27), (0, 43), (7, 30)), 0, 3, 27);

            // --- 2. BISHOP TESTS ---
            // d4 (index 27)
            RunTest("3. Bishop (Open Center)", SetupBoard((2, 27)), 0, 2, 27);
            // c3 (index 18) with friendly Pawn on e5 (index 36) and enemy Pawn on a5 (index 32)
            RunTest("4. Bishop (Blocked & Captures)", SetupBoard((2, 18), (0, 36), (6, 32)), 0, 2, 18);

            // --- 3. QUEEN TESTS ---
            // e4 (index 28)
            RunTest("5. Queen (Open Center)", SetupBoard((4, 28)), 0, 4, 28);
            // d4 (index 27) completely boxed in by friendly and enemy pieces
            RunTest("6. Queen (Crowded Center)", SetupBoard((4, 27), (0, 35), (0, 36), (6, 18), (6, 28)), 0, 4, 27);

            // --- 4. KNIGHT TESTS ---
            // e4 (index 28)
            RunTest("7. Knight (Center Jump)", SetupBoard((1, 28)), 0, 1, 28);
            // a1 (index 0) to test edge boundary constraints
            RunTest("8. Knight (Edge A1)", SetupBoard((1, 0), (0, 10)), 0, 1, 0);

            // --- 5. PAWN TESTS ---
            // White Pawn on e2 (index 12) checking single and double pushes
            RunTest("9. White Pawn (Pushes)", SetupBoard((0, 12)), 0, 0, 12);
            // White Pawn on d4 (index 27) with Black pawns on c5 (34) and e5 (36)
            RunTest("10. White Pawn (Captures)", SetupBoard((0, 27), (6, 34), (6, 36)), 0, 0, 27);
            // Black Pawn on e7 (index 52) checking downward pushes
            RunTest("11. Black Pawn (Pushes)", SetupBoard((6, 52)), 1, 6, 52);


            // Can knight reveal check
            RunTest("12. Knight (Pinned to King by Rook - LEGAL MOVES)",
            SetupBoard(
                    (7, 36), // knight
                    (11, 60), // king
                    (3, 4)   // rook
                ),
                1, // black
                7, // knight
                -1 // Show all legal moves
            );

            RunTest("13. Kings (Type shi) - Legal", SetupBoard((11, 1), (3, 56), (5, 17)), 1, 11, -1);
        }

        // Helper to quickly spawn a board with specific pieces
        // Format: (PieceType index, Square index)
        private static Board SetupBoard(params (int pieceType, int square)[] placements)
        {
            Board b = new Board();
            // Clear standard array
            for(int i = 0; i < 12; i++) b.Pieces[i] = 0UL;
            
            foreach (var p in placements)
            {
                b.Pieces[p.pieceType] |= (1UL << p.square);
            }
            return b;
        }

        private static void RunTest(string title, Board b, int color, int targetPieceType, int targetSquare)
        {
            b.SideToMove = color;
            Span<Move> moves = stackalloc Move[500];
            
            // Extract the bitboard of just the specific piece type we are testing
            ulong pieceBitboard = b.Pieces[targetPieceType];
            
            int count = allMoves.GenerateAllLegalMoves(b, moves, color);

            // Convert the Move span back into a raw bitboard for visualization
            ulong attackBitboard = 0UL;
            for (int i = 0; i < count; i++)
            {
                // Only visualize moves originating from our specific test piece 
                if (moves[i].FromSquare == targetSquare || targetSquare==-1)
                {
                    attackBitboard |= (1UL << moves[i].ToSquare);
                }
            }

            RenderSideBySide(title, b, attackBitboard, count);
        }

        private static void RenderSideBySide(string title, Board b, ulong attacks, int moveCount)
        {
            char[] pieceChars = { 'P', 'N', 'B', 'R', 'Q', 'K', 'p', 'n', 'b', 'r', 'q', 'k' };
            
            Console.WriteLine($"\n--- {title} ---");
            Console.WriteLine($"Generated {moveCount} valid moves.");
            Console.WriteLine("  Board State              Attack Map");
            Console.WriteLine("  +-----------------+      +-----------------+");

            for (int rank = 7; rank >= 0; rank--)
            {
                // 1. Build the Left Board string (Current State)
                string left = $"{rank + 1} | ";
                for (int file = 0; file < 8; file++)
                {
                    int square = rank * 8 + file;
                    char printChar = '.';
                    
                    for (int pt = 0; pt < 12; pt++)
                    {
                        if ((b.Pieces[pt] & (1UL << square)) != 0)
                        {
                            printChar = pieceChars[pt];
                            break;
                        }
                    }
                    left += printChar + " ";
                }
                left += "|";

                // 2. Build the Right Board string (Attack bitboard mapping)
                string right = $"{rank + 1} | ";
                for (int file = 0; file < 8; file++)
                {
                    int square = rank * 8 + file;
                    if ((attacks & (1UL << square)) != 0)
                    {
                        right += "x "; // using 'x' for targeting to visually separate from '.'
                    }
                    else
                    {
                        right += ". ";
                    }
                }
                right += "|";

                // 3. Print them side-by-side with spacing
                Console.WriteLine(left + "    " + right);
            }
            
            Console.WriteLine("  +-----------------+      +-----------------+");
            Console.WriteLine("    a b c d e f g h          a b c d e f g h");
        }
    }
}