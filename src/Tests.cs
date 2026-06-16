using System;
using ChessEngine;
using static ChessEngine.EngineHelpers;

namespace ChessEngine
{
    public delegate int MoveGenerator(Board b, ulong pieces, int color, Span<Move> moves);

    public static class Tests
    {
        public static void allTests()
        {
            Console.WriteLine("=== RUNNING ENGINE TESTS ===\n");

            // --- 1. ROOK TESTS ---
            RunTest("1. Rook (Open Center)", SetupBoard((3, 27)), 0, 3, 27);
            RunTest("2. Rook (Blocked & Captures)", SetupBoard((3, 27), (0, 43), (7, 30)), 0, 3, 27);

            // --- 2. BISHOP TESTS ---
            RunTest("3. Bishop (Open Center)", SetupBoard((2, 27)), 0, 2, 27);
            RunTest("4. Bishop (Blocked & Captures)", SetupBoard((2, 18), (0, 36), (6, 32)), 0, 2, 18);

            // --- 3. QUEEN TESTS ---
            RunTest("5. Queen (Open Center)", SetupBoard((4, 28)), 0, 4, 28);
            RunTest("6. Queen (Crowded Center)", SetupBoard((4, 27), (0, 35), (0, 36), (6, 18), (6, 28)), 0, 4, 27);

            // --- 4. KNIGHT TESTS ---
            RunTest("7. Knight (Center Jump)", SetupBoard((1, 28)), 0, 1, 28);
            RunTest("8. Knight (Edge A1)", SetupBoard((1, 0), (0, 10)), 0, 1, 0);

            // --- 5. PAWN TESTS ---
            RunTest("9. White Pawn (Pushes)", SetupBoard((0, 12)), 0, 0, 12);
            RunTest("10. White Pawn (Captures)", SetupBoard((0, 27), (6, 34), (6, 36)), 0, 0, 27);
            RunTest("11. Black Pawn (Pushes)", SetupBoard((6, 52)), 1, 6, 52);

            RunTest("12. Knight (Pinned to King by Rook - LEGAL MOVES)",
                SetupBoard(
                    (7, 36), // black knight
                    (11, 60), // black king
                    (3, 4)   // white rook
                ),
                1, 7, -1);

            RunTest("13. Kings (Legal moves near enemy pieces)", SetupBoard((11, 1), (3, 56), (5, 17)), 1, 11, -1);

            // --- 6. PAWN PROMOTION TESTS ---
            RunTest("14. White Pawn Promotion (Push)", SetupBoard((0, 52)), 0, 0, 52);
            RunTest("15. Black Pawn Promotion (Capture)", SetupBoard((6, 12), (1, 3)), 1, 6, 12);

            // --- 7. CASTLING TESTS ---
            RunTest("16. White Castling (Kingside & Queenside Open)", 
                SetupBoard((5, 4), (3, 0), (3, 7)), // King e1, Rooks a1 and h1
                0, 5, 4);

            RunTest("17. Black Castling (Kingside & Queenside Open)", 
                SetupBoard((11, 60), (9, 56), (9, 63)), // King e8, Rooks a8 and h8
                1, 11, 60);

            RunTest("18. Castling Through Check (Blocked by Enemy Rook)", 
                SetupBoard(
                    (5, 4),  // White King e1
                    (3, 7),  // White Rook h1
                    (9, 61)  // Black Rook f8 (attacking f1, preventing castling)
                ), 
                0, 5, 4);



            // --- 9. ABSOLUTE PIN TESTS ---
            RunTest("20. Absolute Pin (Pawn Pinned to King - Cannot move)", 
                SetupBoard(
                    (5, 4),   // White King e1
                    (0, 12),  // White Pawn e2
                    (9, 60)   // Black Rook e8
                ), 
                0, 0, 12);

            RunTest("21. Pinned Piece Can Capture Pinner", 
                SetupBoard(
                    (5, 4),   // White King e1
                    (3, 28),  // White Rook e4
                    (10, 60)  // Black Queen e8
                ), 
                0, 3, 28);
        }

        private static Board SetupBoard(params (int pieceType, int square)[] placements)
        {
            Board b = new Board();
            
            // 1. Clear standard array AND wipe all default castling rights
            for (int i = 0; i < 12; i++) b.Pieces[i] = 0UL;
            b.CastlingRights = 0; 
            
            bool hasWhiteKing = false;
            bool hasBlackKing = false;

            foreach (var p in placements)
            {
                b.Pieces[p.pieceType] |= (1UL << p.square);
                if (p.pieceType == 5) hasWhiteKing = true;
                if (p.pieceType == 11) hasBlackKing = true;
            }

            // 2. Dynamically restore Castling Rights based on piece placement
            // If a test places a King and Rook in their home positions, grant the right.
            if ((b.Pieces[5] & (1UL << 4)) != 0) // White King on e1
            {
                if ((b.Pieces[3] & (1UL << 7)) != 0) b.CastlingRights |= 1; // Kingside
                if ((b.Pieces[3] & (1UL << 0)) != 0) b.CastlingRights |= 2; // Queenside
            }
            
            if ((b.Pieces[11] & (1UL << 60)) != 0) // Black King on e8
            {
                if ((b.Pieces[9] & (1UL << 63)) != 0) b.CastlingRights |= 4; // Kingside
                if ((b.Pieces[9] & (1UL << 56)) != 0) b.CastlingRights |= 8; // Queenside
            }

            // 3. SAFETY: Dummy kings to prevent TrailingZeroCount from crashing IsSquareAttacked
            if (!hasWhiteKing) b.Pieces[5] |= (1UL << 0);   // a1
            if (!hasBlackKing) b.Pieces[11] |= (1UL << 63); // h8

            return b;
        }

        private static void RunTest(string title, Board b, int color, int targetPieceType, int targetSquare)
        {
            b.SideToMove = color;
            Span<Move> moves = stackalloc Move[500];
            
            // Extract the bitboard of just the specific piece type we are testing
            ulong pieceBitboard = targetPieceType == -1 ? 0UL : b.Pieces[targetPieceType];
            
            int count = allMoves.GenerateAllLegalMoves(b, moves, color);

            // Convert the Move span back into a raw bitboard for visualization
            ulong attackBitboard = 0UL;
            for (int i = 0; i < count; i++)
            {
                // Only visualize moves originating from our specific test piece 
                if (targetSquare == -1 || moves[i].FromSquare == targetSquare)
                {
                    attackBitboard |= (1UL << moves[i].ToSquare);
                }
            }

            RenderSideBySide(title, b, attackBitboard, count);
            
            // Render formatted moves under the board
            showMoves2(b, moves.Slice(0, count));
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