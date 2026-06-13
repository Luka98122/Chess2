using System;
using System.Runtime.InteropServices;
using System.Numerics;

namespace ChessEngine
{
    public static class Knight_Moves
    {
        public static Dictionary<int, ulong> KnightPreCalcs = new Dictionary<int, ulong>();
        public static void PreCalculateKnightMoves()
        {
            int[] dy = { -2, -2, -1, -1, 1, 1, 2, 2 };
            int[] dx = { -1, 1, -2, 2, -2, 2, -1, 1 };

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int pos = y * 8 + x;
                    ulong res = 0UL;

                    for (int i = 0; i < 8; i++)
                    {
                        int targetY = y + dy[i];
                        int targetX = x + dx[i];

                        if (targetY >= 0 && targetY < 8 && targetX >= 0 && targetX < 8)
                        {
                            int targetPos = targetY * 8 + targetX;
                            res |= 1UL << targetPos; //flipuje tu poziciju bitboarda na keca
                        }
                    }

                    KnightPreCalcs[pos] = res;
                }
            }
        }
    }

    public static class PawnMoveGenerator
    {
        // Masks to prevent pawns from wrapping around the board edges during diagonal captures
        private const ulong NotFileA = 0xFEFEFEFEFEFEFEFEUL;
        private const ulong NotFileH = 0x7F7F7F7F7F7F7F7FUL;

        // The optimized signature: Pass the board state, the pawn bitboard, the color, and the target Span.
        public static int GeneratePawnMoves(Board b, ulong pawns, int color, Span<Move> moves)
        {
            int moveCount = 0;

            // 1. Calculate Occupied and Empty square bitboards
            ulong whitePieces = b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5];
            ulong blackPieces = b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11];
            ulong empty = ~(whitePieces | blackPieces);

            if (color == 0) // White (PieceType 0)
            {
                // --- Single Pushes ---
                ulong singlePushes = (pawns << 8) & empty;
                ulong iter = singlePushes;
                while (iter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(iter);
                    moves[moveCount++] = new Move(toSquare - 8, toSquare, 0);
                    iter &= iter - 1; // Clears the least significant '1' bit
                }

                // --- Double Pushes ---
                // Mask with Rank 3 (0x0000000000FF0000) to ensure only pawns that just pushed from Rank 2 can push again
                ulong doublePushes = ((singlePushes & 0x0000000000FF0000UL) << 8) & empty;
                iter = doublePushes;
                while (iter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(iter);
                    moves[moveCount++] = new Move(toSquare - 16, toSquare, 0);
                    iter &= iter - 1;
                }

                // --- Captures Left (Towards A-File) ---
                ulong capturesLeft = ((pawns & NotFileA) << 7) & blackPieces;
                iter = capturesLeft;
                while (iter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(iter);
                    moves[moveCount++] = new Move(toSquare - 7, toSquare, 0, true);
                    iter &= iter - 1;
                }

                // --- Captures Right (Towards H-File) ---
                ulong capturesRight = ((pawns & NotFileH) << 9) & blackPieces;
                iter = capturesRight;
                while (iter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(iter);
                    moves[moveCount++] = new Move(toSquare - 9, toSquare, 0, true);
                    iter &= iter - 1;
                }
            }
            else // Black (PieceType 6)
            {
                // --- Single Pushes ---
                ulong singlePushes = (pawns >> 8) & empty;
                ulong iter = singlePushes;
                while (iter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(iter);
                    moves[moveCount++] = new Move(toSquare + 8, toSquare, 6);
                    iter &= iter - 1;
                }

                // --- Double Pushes ---
                // Mask with Rank 6 (0x0000FF0000000000)
                ulong doublePushes = ((singlePushes & 0x0000FF0000000000UL) >> 8) & empty;
                iter = doublePushes;
                while (iter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(iter);
                    moves[moveCount++] = new Move(toSquare + 16, toSquare, 6);
                    iter &= iter - 1;
                }

                // --- Captures Left (From Black's perspective, down towards A-File) ---
                ulong capturesLeft = ((pawns & NotFileA) >> 9) & whitePieces;
                iter = capturesLeft;
                while (iter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(iter);
                    moves[moveCount++] = new Move(toSquare + 9, toSquare, 6, true);
                    iter &= iter - 1;
                }

                // --- Captures Right (From Black's perspective, down towards H-File) ---
                ulong capturesRight = ((pawns & NotFileH) >> 7) & whitePieces;
                iter = capturesRight;
                while (iter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(iter);
                    moves[moveCount++] = new Move(toSquare + 7, toSquare, 6, true);
                    iter &= iter - 1;
                }
            }

            // Return the total number of moves injected into the Span
            return moveCount;
        }
    }
}