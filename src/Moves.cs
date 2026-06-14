using System;
using System.Runtime.InteropServices;
using System.Numerics;

namespace ChessEngine
{
    public static class KnightMoveGenerator
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

        public static int GetKnightMoves(Board b, ulong knights, int color, Span<Move> moves)
        {
            int moveCount = 0;

            // 1. Calculate Friendly and Enemy piece bitboards
            ulong friendlyPieces = color == 0 
                ? (b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5])
                : (b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11]);
                
            ulong enemyPieces = color == 0
                ? (b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11])
                : (b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5]);

            // Knights can jump anywhere, as long as they don't land on a friendly piece
            ulong validSquares = ~friendlyPieces;
            
            // PieceType: White Knight = 1, Black Knight = 7
            int pieceType = color == 0 ? 1 : 7; 

            // 2. Iterate ONLY over the squares that actually contain a knight
            ulong knightsIter = knights;
            while (knightsIter != 0)
            {
                // Find the index (0-63) of the first knight in the bitboard
                int fromSquare = BitOperations.TrailingZeroCount(knightsIter);

                // 3. Look up the precalculated attacks and mask out friendly pieces
                ulong attacks = KnightMoveGenerator.KnightPreCalcs[fromSquare] & validSquares;

                // 4. Iterate over all valid destination squares for this specific knight
                ulong attacksIter = attacks;
                while (attacksIter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(attacksIter);
                    
                    // A move is a capture if the destination square intersects with the enemy piece bitboard
                    bool isCapture = (enemyPieces & (1UL << toSquare)) != 0;

                    // Add the move to the span
                    moves[moveCount++] = new Move(fromSquare, toSquare, pieceType, isCapture);

                    // Clear the destination bit we just processed
                    attacksIter &= attacksIter - 1;
                }

                // Clear the knight bit we just processed so the loop moves to the next knight
                knightsIter &= knightsIter - 1;
            }

            return moveCount;
        }
    }

    public static class PawnMoveGenerator
    {
        // Masks to prevent pawns from wrapping around the board edges during diagonal captures
        private const ulong NotFileA = 0xFEFEFEFEFEFEFEFEUL;
        private const ulong NotFileH = 0x7F7F7F7F7F7F7F7FUL;

        // The optimized signature: Pass the board state, the pawn bitboard, the color, and the target Span.
        public static int GetPawnMoves(Board b, ulong pawns, int color, Span<Move> moves)
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
    public static class allMoves {
        public static int GenerateAllPseudoLegalMoves(Board b, Span<Move> moves)
        {
            int totalMoves = 0;
            int color = b.SideToMove;

            // 1. Generate Pawn Moves
            // Pass the whole span. It returns how many moves were added.
            int pawnCount = PawnMoveGenerator.GetPawnMoves(b, b.Pieces[color == 0 ? 0 : 6], color, moves);
            totalMoves += pawnCount;

            // 2. Generate Knight Moves
            // Slice the span so the knights start writing exactly where the pawns left off!
            int knightCount = KnightMoveGenerator.GetKnightMoves(b, b.Pieces[color == 0 ? 1 : 7], color, moves.Slice(totalMoves));
            totalMoves += knightCount;

            // 3. Generate Sliding Moves (Bishops, Rooks, Queens)
            // int sliderCount = SlidingMoveGenerator.GenerateSlidingMoves(b, color, moves.Slice(totalMoves));
            // totalMoves += sliderCount;

            // 4. Generate King Moves (and Castling)
            // int kingCount = KingMoveGenerator.GenerateKingMoves(b, b.Pieces[color == 0 ? 5 : 11], color, moves.Slice(totalMoves));
            // totalMoves += kingCount;

            return totalMoves;
        }
    }
}