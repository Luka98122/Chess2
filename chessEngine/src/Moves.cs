using System;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

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
        public const ulong NotFileA = 0xFEFEFEFEFEFEFEFEUL;
        public const ulong NotFileH = 0x7F7F7F7F7F7F7F7FUL;

        private static int AddPawnMove(Span<Move> moves, int moveCount, int from, int to, int piece, bool isCapture)
        {
            bool isPromotion = to >= 56 || to <= 7;

            if (isPromotion)
            {
                moves[moveCount++] = new Move(from, to, piece, isCapture) { IsPromotion = true, PromotedPieceType = 4 };
                moves[moveCount++] = new Move(from, to, piece, isCapture) { IsPromotion = true, PromotedPieceType = 3 };
                moves[moveCount++] = new Move(from, to, piece, isCapture) { IsPromotion = true, PromotedPieceType = 2 };
                moves[moveCount++] = new Move(from, to, piece, isCapture) { IsPromotion = true, PromotedPieceType = 1 };
            }
            else
            {
                moves[moveCount++] = new Move(from, to, piece, isCapture);
            }
            return moveCount;
        }

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
                    moveCount = AddPawnMove(moves, moveCount, toSquare - 8, toSquare, 0, false);
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
                    moveCount = AddPawnMove(moves, moveCount, toSquare - 7, toSquare, 0, true);
                    iter &= iter - 1;
                }

                // --- Captures Right (Towards H-File) ---
                ulong capturesRight = ((pawns & NotFileH) << 9) & blackPieces;
                iter = capturesRight;
                while (iter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(iter);
                    moveCount = AddPawnMove(moves, moveCount, toSquare - 9, toSquare, 0, true);
                    iter &= iter - 1;
                }

                // --- En Passant ---
                if (b.EnPassantSquare != -1)
                {
                    int epSq = b.EnPassantSquare;
                    int fromLeft = epSq - 7;
                    int fromRight = epSq - 9;
                    if (fromLeft >= 0 && (pawns & (1UL << fromLeft)) != 0)
                        moves[moveCount++] = new Move(fromLeft, epSq, 0, true) { IsEnPassant = true };
                    if (fromRight >= 0 && (pawns & (1UL << fromRight)) != 0)
                        moves[moveCount++] = new Move(fromRight, epSq, 0, true) { IsEnPassant = true };
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
                    moveCount = AddPawnMove(moves, moveCount, toSquare + 8, toSquare, 6, false);
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
                    moveCount = AddPawnMove(moves, moveCount, toSquare + 9, toSquare, 6, true);
                    iter &= iter - 1;
                }

                // --- Captures Right (From Black's perspective, down towards H-File) ---
                ulong capturesRight = ((pawns & NotFileH) >> 7) & whitePieces;
                iter = capturesRight;
                while (iter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(iter);
                    moveCount = AddPawnMove(moves, moveCount, toSquare + 7, toSquare, 6, true);
                    iter &= iter - 1;
                }

                // --- En Passant ---
                if (b.EnPassantSquare != -1)
                {
                    int epSq = b.EnPassantSquare;
                    int fromRight = epSq + 7;
                    int fromLeft = epSq + 9;
                    if (fromRight < 64 && (pawns & (1UL << fromRight)) != 0)
                        moves[moveCount++] = new Move(fromRight, epSq, 6, true) { IsEnPassant = true };
                    if (fromLeft < 64 && (pawns & (1UL << fromLeft)) != 0)
                        moves[moveCount++] = new Move(fromLeft, epSq, 6, true) { IsEnPassant = true };
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


        public static int[] RookRelevantBits = new int[64];

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

            ulong rooksIter = rooks;
            while (rooksIter != 0)
            {
                int fromSquare = BitOperations.TrailingZeroCount(rooksIter);

                // Relevant blocker maska
                ulong blockers = occupied & RookMasks[fromSquare];

                // blockers je jedinstven bitboard layout za relevant blocker mask
                int magicIndex = (int)((blockers * RookMagics[fromSquare]) >> (64 - RookRelevantBits[fromSquare]));

                // O(1) provera i invalidiranje napadanje svojih figura
                ulong attacks = RookAttacks[fromSquare][magicIndex] & ~friendlyPieces;

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

        public static void PreCalculateRookAttacks()
        {
            for (int square = 0; square < 64; square++)
            {
                // 1. Get the relevant blocker mask for this square (ignoring board edges)
                RookMasks[square] = CreateRookMask(square);
                RookRelevantBits[square] = BitOperations.PopCount(RookMasks[square]);

                // Alociranje memorije O(2^n)w
                int permutationCount = 1 << RookRelevantBits[square];
                RookAttacks[square] = new ulong[permutationCount];


                ulong mask = RookMasks[square];
                ulong blockerPattern = 0; // Carry-Rippler

                do
                {
                    // Magican index u koji cemo svrstati keshiran rez
                    int magicIndex = (int)((blockerPattern * RookMagics[square]) >> (64 - RookRelevantBits[square]));

                    // Naivno izracunamo moguce poteze i keshiramo
                    RookAttacks[square][magicIndex] = CalculateNaiveRookAttacks(square, blockerPattern);

                    // Carry-rippler trick (efikasno dodje do sledece podmaske)
                    blockerPattern = (blockerPattern - mask) & mask;
                }
                while (blockerPattern != 0);
            }
        }

        public static ulong CreateRookMask(int square)
        {
            ulong mask = 0UL;
            int r = square / 8;
            int f = square % 8;

            // North
            for (int i = r + 1; i <= 6; i++)
            {
                int targetSquare = i * 8 + f;
                mask |= (1UL << targetSquare);
            }

            // South
            for (int i = r - 1; i >= 1; i--)
            {
                int targetSquare = i * 8 + f;
                mask |= (1UL << targetSquare);
            }

            // East
            for (int i = f + 1; i <= 6; i++)
            {
                int targetSquare = r * 8 + i;
                mask |= (1UL << targetSquare);
            }

            // West
            for (int i = f - 1; i >= 1; i--)
            {
                int targetSquare = r * 8 + i;
                mask |= (1UL << targetSquare);
            }

            return mask;
        }

        public static ulong CalculateNaiveRookAttacks(int square, ulong blockers)
        {
            ulong attacks = 0UL;
            int r = square / 8;
            int f = square % 8;

            // North
            for (int i = r + 1; i <= 7; i++)
            {
                int targetSquare = i * 8 + f;
                ulong squareMask = 1UL << targetSquare;

                attacks |= squareMask;

                // Ako se poklapa bar jedan blocker onda prestajemo
                if ((blockers & squareMask) != 0)
                {
                    break;
                }
            }

            // South
            for (int i = r - 1; i >= 0; i--)
            {
                int targetSquare = i * 8 + f;
                ulong squareMask = 1UL << targetSquare;

                attacks |= squareMask;

                if ((blockers & squareMask) != 0)
                {
                    break;
                }
            }

            // East
            for (int i = f + 1; i <= 7; i++)
            {
                int targetSquare = r * 8 + i;
                ulong squareMask = 1UL << targetSquare;

                attacks |= squareMask;

                if ((blockers & squareMask) != 0)
                {
                    break;
                }
            }

            // West
            for (int i = f - 1; i >= 0; i--)
            {
                int targetSquare = r * 8 + i;
                ulong squareMask = 1UL << targetSquare;

                attacks |= squareMask;

                if ((blockers & squareMask) != 0)
                {
                    break;
                }
            }

            return attacks;
        }
    }

    public static class BishopMoveGenerator
    {
        public static ulong[] BishopMasks = new ulong[64];
        public static ulong[][] BishopAttacks = new ulong[64][];
        public static int[] BishopRelevantBits = new int[64];


        public static readonly ulong[] BishopMagics = new ulong[64] {
            0x800202680E008200UL, // Square 0
            0x1010020801102004UL, // Square 1
            0x0008024400280804UL, // Square 2
            0x0008204042221012UL, // Square 3
            0x485414A002000000UL, // Square 4
            0x4002120220200031UL, // Square 5
            0x0000821010460485UL, // Square 6
            0x1002020042080400UL, // Square 7
            0x0108315006881040UL, // Square 8
            0x0000206809004090UL, // Square 9
            0x1080100408444002UL, // Square 10
            0x2000424081028018UL, // Square 11
            0x8200011040440022UL, // Square 12
            0xC000020804840468UL, // Square 13
            0x0020009401201000UL, // Square 14
            0x8028010400820804UL, // Square 15
            0x8010604420088100UL, // Square 16
            0x0204200228021412UL, // Square 17
            0x0084489002428100UL, // Square 18
            0x0001000820420105UL, // Square 19
            0x0009000811400400UL, // Square 20
            0x0108101208021800UL, // Square 21
            0x0022000041100800UL, // Square 22
            0x2002810146280700UL, // Square 23
            0x2120088044880888UL, // Square 24
            0x0410030028020410UL, // Square 25
            0x4004020681081100UL, // Square 26
            0x0801041008020020UL, // Square 27
            0x000200218A008042UL, // Square 28
            0x000C010068110480UL, // Square 29
            0x5000840000941420UL, // Square 30
            0x0090810809210841UL, // Square 31
            0x4201200800200821UL, // Square 32
            0x0018480400028409UL, // Square 33
            0x0008280400A04102UL, // Square 34
            0x0004020080080080UL, // Square 35
            0x2040048200010104UL, // Square 36
            0x0010208020420200UL, // Square 37
            0x4008110043290800UL, // Square 38
            0x08080A1040008042UL, // Square 39
            0x1404020804004121UL, // Square 40
            0x2485080914801004UL, // Square 41
            0x2050208020801000UL, // Square 42
            0x1080010280800800UL, // Square 43
            0x000004310C001200UL, // Square 44
            0x0802081001004024UL, // Square 45
            0x0002324242010400UL, // Square 46
            0x8C821400488A0200UL, // Square 47
            0x2501041221040110UL, // Square 48
            0x1000240402080020UL, // Square 49
            0x00A0090041100480UL, // Square 50
            0x01A4240108480301UL, // Square 51
            0x0000054022820000UL, // Square 52
            0x2902400204011000UL, // Square 53
            0x0010841048822080UL, // Square 54
            0x0009280800802008UL, // Square 55
            0x0006004228012800UL, // Square 56
            0x4001120090880848UL, // Square 57
            0x0000802100415000UL, // Square 58
            0x98C001C080208800UL, // Square 59
            0x0000201011220220UL, // Square 60
            0x0860001910658200UL, // Square 61
            0x8024100290010220UL, // Square 62
            0x0308820082040100UL, // Square 63
        };

        public static int GetBishopMoves(Board b, ulong bishops, int color, Span<Move> moves)
        {
            int moveCount = 0;

            ulong friendlyPieces = color == 0
                ? (b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5])
                : (b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11]);

            ulong enemyPieces = color == 0
                ? (b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11])
                : (b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5]);

            ulong occupied = friendlyPieces | enemyPieces;
            int pieceType = color == 0 ? 2 : 8; // 2 = White Bishop, 8 = Black Bishop

            ulong bishopsIter = bishops;
            while (bishopsIter != 0)
            {
                int fromSquare = BitOperations.TrailingZeroCount(bishopsIter);

                ulong blockers = occupied & BishopMasks[fromSquare];
                int magicIndex = (int)((blockers * BishopMagics[fromSquare]) >> (64 - BishopRelevantBits[fromSquare]));
                ulong attacks = BishopAttacks[fromSquare][magicIndex] & ~friendlyPieces;

                ulong attacksIter = attacks;
                while (attacksIter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(attacksIter);
                    bool isCapture = (enemyPieces & (1UL << toSquare)) != 0;

                    moves[moveCount++] = new Move(fromSquare, toSquare, pieceType, isCapture);
                    attacksIter &= attacksIter - 1;
                }

                bishopsIter &= bishopsIter - 1;
            }

            return moveCount;
        }

        public static void PreCalculateBishopAttacks()
        {
            for (int square = 0; square < 64; square++)
            {
                BishopMasks[square] = CreateBishopMask(square);
                BishopRelevantBits[square] = BitOperations.PopCount(BishopMasks[square]);

                int permutationCount = 1 << BishopRelevantBits[square];
                BishopAttacks[square] = new ulong[permutationCount];

                ulong mask = BishopMasks[square];
                ulong blockerPattern = 0;

                do
                {
                    int magicIndex = (int)((blockerPattern * BishopMagics[square]) >> (64 - BishopRelevantBits[square]));
                    BishopAttacks[square][magicIndex] = CalculateNaiveBishopAttacks(square, blockerPattern);
                    blockerPattern = (blockerPattern - mask) & mask;
                }
                while (blockerPattern != 0);
            }
        }

        public static ulong CreateBishopMask(int square)
        {
            ulong mask = 0UL;
            int r = square / 8;
            int f = square % 8;

            // North-East (NE)
            for (int i = r + 1, j = f + 1; i <= 6 && j <= 6; i++, j++)
            {
                int targetSquare = i * 8 + j;
                mask |= (1UL << targetSquare);
            }

            // North-West (NW)
            for (int i = r + 1, j = f - 1; i <= 6 && j >= 1; i++, j--)
            {
                int targetSquare = i * 8 + j;
                mask |= (1UL << targetSquare);
            }

            // South-East (SE)
            for (int i = r - 1, j = f + 1; i >= 1 && j <= 6; i--, j++)
            {
                int targetSquare = i * 8 + j;
                mask |= (1UL << targetSquare);
            }

            // South-West (SW)
            for (int i = r - 1, j = f - 1; i >= 1 && j >= 1; i--, j--)
            {
                int targetSquare = i * 8 + j;
                mask |= (1UL << targetSquare);
            }

            return mask;
        }

        public static ulong CalculateNaiveBishopAttacks(int square, ulong blockers)
        {
            ulong attacks = 0UL;
            int r = square / 8;
            int f = square % 8;

            // North-East (NE)
            for (int i = r + 1, j = f + 1; i <= 7 && j <= 7; i++, j++)
            {
                int targetSquare = i * 8 + j;
                ulong squareMask = 1UL << targetSquare;

                attacks |= squareMask;

                if ((blockers & squareMask) != 0)
                {
                    break;
                }
            }

            // North-West (NW)
            for (int i = r + 1, j = f - 1; i <= 7 && j >= 0; i++, j--)
            {
                int targetSquare = i * 8 + j;
                ulong squareMask = 1UL << targetSquare;

                attacks |= squareMask;

                if ((blockers & squareMask) != 0)
                {
                    break;
                }
            }

            // South-East (SE)
            for (int i = r - 1, j = f + 1; i >= 0 && j <= 7; i--, j++)
            {
                int targetSquare = i * 8 + j;
                ulong squareMask = 1UL << targetSquare;

                attacks |= squareMask;

                if ((blockers & squareMask) != 0)
                {
                    break;
                }
            }

            // South-West (SW)
            for (int i = r - 1, j = f - 1; i >= 0 && j >= 0; i--, j--)
            {
                int targetSquare = i * 8 + j;
                ulong squareMask = 1UL << targetSquare;

                attacks |= squareMask;

                if ((blockers & squareMask) != 0)
                {
                    break;
                }
            }

            return attacks;
        }
    }

    public static class QueenMoveGenerator
    {
        public static int GetQueenMoves(Board b, ulong queens, int color, Span<Move> moves)
        {
            int moveCount = 0;

            ulong friendlyPieces = color == 0
                ? (b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5])
                : (b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11]);

            ulong enemyPieces = color == 0
                ? (b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11])
                : (b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5]);

            ulong occupied = friendlyPieces | enemyPieces;
            int pieceType = color == 0 ? 4 : 10; // 4 = White Queen, 10 = Black Queen

            ulong queensIter = queens;
            while (queensIter != 0)
            {
                int fromSquare = BitOperations.TrailingZeroCount(queensIter);

                // Look up Rook Attacks
                ulong rookBlockers = occupied & RookMoveGenerator.RookMasks[fromSquare];
                int rookMagicIndex = (int)((rookBlockers * RookMoveGenerator.RookMagics[fromSquare]) >> (64 - RookMoveGenerator.RookRelevantBits[fromSquare]));
                ulong rookAttacks = RookMoveGenerator.RookAttacks[fromSquare][rookMagicIndex];

                // Look up Bishop Attacks
                ulong bishopBlockers = occupied & BishopMoveGenerator.BishopMasks[fromSquare];
                int bishopMagicIndex = (int)((bishopBlockers * BishopMoveGenerator.BishopMagics[fromSquare]) >> (64 - BishopMoveGenerator.BishopRelevantBits[fromSquare]));
                ulong bishopAttacks = BishopMoveGenerator.BishopAttacks[fromSquare][bishopMagicIndex];

                // OR them together
                ulong attacks = (rookAttacks | bishopAttacks) & ~friendlyPieces;

                ulong attacksIter = attacks;
                while (attacksIter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(attacksIter);
                    bool isCapture = (enemyPieces & (1UL << toSquare)) != 0;

                    moves[moveCount++] = new Move(fromSquare, toSquare, pieceType, isCapture);
                    attacksIter &= attacksIter - 1;
                }

                queensIter &= queensIter - 1;
            }

            return moveCount;
        }
    }

    public static class KingMoveGenerator
    {
        public static Dictionary<int, ulong> KingPreCalcs = new Dictionary<int, ulong>();
        public static void PreCalculateKingMoves()
        {
            int[] dy = { -1, -1, -1, 0, 1, 1, 1, 0 };
            int[] dx = { -1, 0, 1, 1, 1, 0, -1, -1 };

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

                    KingPreCalcs[pos] = res;
                }
            }
        }

        public static int GetKingMoves(Board b, ulong kings, int color, Span<Move> moves)
        {
            int moveCount = 0;

            ulong friendlyPieces = color == 0
                ? (b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5])
                : (b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11]);

            ulong enemyPieces = color == 0
                ? (b.Pieces[6] | b.Pieces[7] | b.Pieces[8] | b.Pieces[9] | b.Pieces[10] | b.Pieces[11])
                : (b.Pieces[0] | b.Pieces[1] | b.Pieces[2] | b.Pieces[3] | b.Pieces[4] | b.Pieces[5]);

            ulong occupied = friendlyPieces | enemyPieces;
            ulong validSquares = ~friendlyPieces;

            int pieceType = color == 0 ? 5 : 11;
            int opponentColor = color == 0 ? 1 : 0;

            ulong kingsIter = kings;
            while (kingsIter != 0)
            {
                int fromSquare = BitOperations.TrailingZeroCount(kingsIter);

                ulong attacks = KingPreCalcs[fromSquare] & validSquares;

                ulong attacksIter = attacks;
                while (attacksIter != 0)
                {
                    int toSquare = BitOperations.TrailingZeroCount(attacksIter);
                    bool isCapture = (enemyPieces & (1UL << toSquare)) != 0;

                    moves[moveCount++] = new Move(fromSquare, toSquare, pieceType, isCapture);
                    attacksIter &= attacksIter - 1;
                }

                kingsIter &= kingsIter - 1;
            }

            // Castling Moves
            if (color == 0) // White
            {
                // Kingside (Bit 0 is set)
                if ((b.CastlingRights & 1) != 0)
                {
                    // Check if f1 (5) and g1 (6) are completely empty
                    if ((occupied & ((1UL << 5) | (1UL << 6))) == 0)
                    {
                        // Check if e1 (4), f1 (5), or g1 (6) are under attack
                        if (!b.IsSquareAttacked(4, 1) && !b.IsSquareAttacked(5, 1) && !b.IsSquareAttacked(6, 1))
                        {
                            moves[moveCount++] = new Move(4, 6, pieceType) { IsCastle = true };
                        }
                    }
                }

                // Queenside (Bit 1 is set)
                if ((b.CastlingRights & 2) != 0)
                {
                    // Check if b1 (1), c1 (2), and d1 (3) are completely empty
                    if ((occupied & ((1UL << 1) | (1UL << 2) | (1UL << 3))) == 0)
                    {
                        // Check if e1 (4), d1 (3), or c1 (2) are under attack (b1 doesn't matter for attacks per chess rules)
                        if (!b.IsSquareAttacked(4, 1) && !b.IsSquareAttacked(3, 1) && !b.IsSquareAttacked(2, 1))
                        {
                            moves[moveCount++] = new Move(4, 2, pieceType) { IsCastle = true };
                        }
                    }
                }
            }
            else // Black
            {
                // Kingside (Bit 2 is set)
                if ((b.CastlingRights & 4) != 0)
                {
                    // Check if f8 (61) and g8 (62) are completely empty
                    if ((occupied & ((1UL << 61) | (1UL << 62))) == 0)
                    {
                        // Check if e8 (60), f8 (61), or g8 (62) are under attack
                        if (!b.IsSquareAttacked(60, 0) && !b.IsSquareAttacked(61, 0) && !b.IsSquareAttacked(62, 0))
                        {
                            moves[moveCount++] = new Move(60, 62, pieceType) { IsCastle = true };
                        }
                    }
                }

                // Queenside (Bit 3 is set)
                if ((b.CastlingRights & 8) != 0)
                {
                    // Check if b8 (57), c8 (58), and d8 (59) are completely empty
                    if ((occupied & ((1UL << 57) | (1UL << 58) | (1UL << 59))) == 0)
                    {
                        // Check if e8 (60), d8 (59), or c8 (58) are under attack
                        if (!b.IsSquareAttacked(60, 0) && !b.IsSquareAttacked(59, 0) && !b.IsSquareAttacked(58, 0))
                        {
                            moves[moveCount++] = new Move(60, 58, pieceType) { IsCastle = true };
                        }
                    }
                }
            }

            return moveCount;
        }
    }

    public static class allMoves
    {
        public static int GenerateAllPseudoLegalMoves(Board b, Span<Move> moves, int col)
        {
            int totalMoves = 0;
            int color = col;

            // 1. Generate Pawn Moves
            int pawnCount = PawnMoveGenerator.GetPawnMoves(b, b.Pieces[color == 0 ? 0 : 6], color, moves);
            totalMoves += pawnCount;

            // 2. Generate Knight Moves
            int knightCount = KnightMoveGenerator.GetKnightMoves(b, b.Pieces[color == 0 ? 1 : 7], color, moves.Slice(totalMoves));
            totalMoves += knightCount;

            // 3. Generate Rook Moves
            int rookCount = RookMoveGenerator.GetRookMoves(b, b.Pieces[color == 0 ? 3 : 9], color, moves.Slice(totalMoves));
            totalMoves += rookCount;

            // 4. Generate Bishop Moves
            int bishopCount = BishopMoveGenerator.GetBishopMoves(b, b.Pieces[color == 0 ? 2 : 8], color, moves.Slice(totalMoves));
            totalMoves += bishopCount;

            // 5. Generate Queen Moves
            int queenCount = QueenMoveGenerator.GetQueenMoves(b, b.Pieces[color == 0 ? 4 : 10], color, moves.Slice(totalMoves));
            totalMoves += queenCount;

            int kingMoves = KingMoveGenerator.GetKingMoves(b, b.Pieces[color == 0 ? 5 : 11], color, moves.Slice(totalMoves));
            totalMoves += kingMoves;
            return totalMoves;
        }

        public static int GenerateAllLegalMoves(Board b, Span<Move> moves, int col)
        {
            int generatedMoves = GenerateAllPseudoLegalMoves(b, moves, col);
            int ind = 0;

            int kingPieceType = col == 0 ? 5 : 11;
            int attackerCol = 1 - col;

            for (int i = 0; i < generatedMoves; i++)
            {
                b.MakeMove(moves[i]);

                int kingSquare = BitOperations.TrailingZeroCount(b.Pieces[kingPieceType]);
                bool isValid = !b.IsSquareAttacked(kingSquare, attackerCol);

                b.UnmakeMove();

                if (isValid)
                {
                    moves[ind++] = moves[i];
                }
            }

            return ind;
        }
    }
}