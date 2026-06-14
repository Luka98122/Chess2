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
    
    public static class RookMoveGenerator
    {
        // 1. Core Arrays for Magic Bitboards
        public static ulong[] RookMasks = new ulong[64];
        public static ulong[][] RookAttacks = new ulong[64][];
        
        // The number of relevant blocker bits for a rook on each square (varies from 7 to 12)
        public static int[] RookRelevantBits = new int[64];

        // You will need to populate this with standard 64-bit Rook Magic Numbers 
        // (easily found on the Chess Programming Wiki).
        
        public static readonly ulong[] RookMagics = new ulong[64] {
            0x0A80011040008060UL, // Square 0
            0x424000E000401000UL, // Square 1
            0x2880088010042000UL, // Square 2
            0x0200060020084011UL, // Square 3
            0x1080040008000281UL, // Square 4
            0x0100040008020100UL, // Square 5
            0xA080208051000200UL, // Square 6
            0x008000428005A100UL, // Square 7
            0x002180008040002CUL, // Square 8
            0xC200404000201000UL, // Square 9
            0x0101002001001044UL, // Square 10
            0x0101000900201000UL, // Square 11
            0x8112800400804801UL, // Square 12
            0x0100800400020080UL, // Square 13
            0x0100808001000200UL, // Square 14
            0x4082000084004916UL, // Square 15
            0x1804848000C00226UL, // Square 16
            0x0002850040010020UL, // Square 17
            0x4010008020001089UL, // Square 18
            0x3010004008040040UL, // Square 19
            0x8603010004100800UL, // Square 20
            0x040008010420C010UL, // Square 21
            0x0400340018820150UL, // Square 22
            0x10400A0010488104UL, // Square 23
            0x0660208180004000UL, // Square 24
            0x034C200880400082UL, // Square 25
            0x4000110100200044UL, // Square 26
            0x8100100500210008UL, // Square 27
            0x0000040080080082UL, // Square 28
            0x7880020080040080UL, // Square 29
            0xA001000101040200UL, // Square 30
            0x0008004200008401UL, // Square 31
            0x0080204000800081UL, // Square 32
            0x149000201240004AUL, // Square 33
            0x0007001041002000UL, // Square 34
            0x6C08001000808008UL, // Square 35
            0x10E0100501000800UL, // Square 36
            0x2400020080800400UL, // Square 37
            0x2000210224001088UL, // Square 38
            0x2000800040800100UL, // Square 39
            0x0280002000414008UL, // Square 40
            0x0030002000404000UL, // Square 41
            0x0529001020010044UL, // Square 42
            0x0001002010010008UL, // Square 43
            0x00B8000901110004UL, // Square 44
            0x0000104004080120UL, // Square 45
            0x0400500208040041UL, // Square 46
            0x0041088044060001UL, // Square 47
            0x11C0004820800480UL, // Square 48
            0x4802002081004200UL, // Square 49
            0x0000201040820200UL, // Square 50
            0x8000100009002100UL, // Square 51
            0x1F04800400080080UL, // Square 52
            0x0020800200040080UL, // Square 53
            0x0A10418208104400UL, // Square 54
            0x0080140100508200UL, // Square 55
            0x8040800010290441UL, // Square 56
            0x2A40400683102501UL, // Square 57
            0x4000A0001B004013UL, // Square 58
            0x0022850020581001UL, // Square 59
            0x004A902800030301UL, // Square 60
            0x400100028804002DUL, // Square 61
            0x4210014800900204UL, // Square 62
            0x0400408044010022UL, // Square 63
        };

        public static int GetRookMoves(Board b, ulong rooks, int color, Span<Move> moves)
        {
            int moveCount = 0;

            // 1. Calculate bitboards
            ulong friendlyPieces = color == 0 
                ? (b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5])
                : (b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11]);
                
            ulong enemyPieces = color == 0
                ? (b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11])
                : (b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5]);

            ulong occupied = friendlyPieces | enemyPieces;
            int pieceType = color == 0 ? 3 : 9; // 3 = White Rook, 9 = Black Rook

            // 2. Iterate over rooks
            ulong rooksIter = rooks;
            while (rooksIter != 0)
            {
                int fromSquare = BitOperations.TrailingZeroCount(rooksIter);

                // --- MAGIC BITBOARD HASHING ---
                // Mask the board to only look at relevant blockers for this square
                ulong blockers = occupied & RookMasks[fromSquare];
                
                // Multiply by the magic number and shift down to get a clean array index
                int magicIndex = (int)((blockers * RookMagics[fromSquare]) >> (64 - RookRelevantBits[fromSquare]));
                
                // Instantly look up the precalculated attack bitboard and mask out friendly pieces
                ulong attacks = RookAttacks[fromSquare][magicIndex] & ~friendlyPieces;

                // 3. Extract moves
                ulong attacksIter = attacks;
                while (attacksIter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(attacksIter);
                    bool isCapture = (enemyPieces & (1UL << toSquare)) != 0;
                    
                    moves[moveCount++] = new Move(fromSquare, toSquare, pieceType, isCapture);
                    attacksIter &= attacksIter - 1;
                }

                rooksIter &= rooksIter - 1;
            }

            return moveCount;
        }

        // --- THE INITIALIZATION (Runs once at startup) ---
        public static void PreCalculateRookAttacks()
        {
            for (int square = 0; square < 64; square++)
            {
                // 1. Get the relevant blocker mask for this square (ignoring board edges)
                RookMasks[square] = CreateRookMask(square);
                RookRelevantBits[square] = BitOperations.PopCount(RookMasks[square]);

                // 2. Allocate memory for this square's specific number of permutations (2^n)
                int permutationCount = 1 << RookRelevantBits[square];
                RookAttacks[square] = new ulong[permutationCount];

                // 3. Generate all possible blocker combinations and calculate naive attacks
                ulong mask = RookMasks[square];
                ulong blockerPattern = 0; // Carry-Rippler trick to iterate sub-masks

                do
                {
                    // Calculate the magic index for this specific blocker pattern
                    int magicIndex = (int)((blockerPattern * RookMagics[square]) >> (64 - RookRelevantBits[square]));
                    
                    // Run standard slow raycasting to find the true attacks for this blocker state
                    RookAttacks[square][magicIndex] = CalculateNaiveRookAttacks(square, blockerPattern);

                    // Iterate to the next sub-mask permutation
                    blockerPattern = (blockerPattern - mask) & mask;
                } 
                while (blockerPattern != 0);
            }
        }

        // Helper: Generates the blocker mask (ignores outer edges because rays stop there anyway)
        public static ulong CreateRookMask(int square)
        {
            ulong mask = 0UL;
            int r = square / 8;
            int f = square % 8;

            for (int i = r + 1; i <= 6; i++) mask |= (1UL << (i * 8 + f)); // North
            for (int i = r - 1; i >= 1; i--) mask |= (1UL << (i * 8 + f)); // South
            for (int i = f + 1; i <= 6; i++) mask |= (1UL << (r * 8 + i)); // East
            for (int i = f - 1; i >= 1; i--) mask |= (1UL << (r * 8 + i)); // West

            return mask;
        }

        // Helper: Slow raycasting used ONLY during startup
        public static ulong CalculateNaiveRookAttacks(int square, ulong blockers)
        {
            ulong attacks = 0UL;
            int r = square / 8;
            int f = square % 8;

            // North
            for (int i = r + 1; i <= 7; i++) { attacks |= (1UL << (i * 8 + f)); if ((blockers & (1UL << (i * 8 + f))) != 0) break; }
            // South
            for (int i = r - 1; i >= 0; i--) { attacks |= (1UL << (i * 8 + f)); if ((blockers & (1UL << (i * 8 + f))) != 0) break; }
            // East
            for (int i = f + 1; i <= 7; i++) { attacks |= (1UL << (r * 8 + i)); if ((blockers & (1UL << (r * 8 + i))) != 0) break; }
            // West
            for (int i = f - 1; i >= 0; i--) { attacks |= (1UL << (r * 8 + i)); if ((blockers & (1UL << (r * 8 + i))) != 0) break; }

            return attacks;
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