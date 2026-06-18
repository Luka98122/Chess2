using System; // LUKA FILE

namespace ChessEngine
{
    public static class EngineHelpers
    {

        public static void init()
        {
            //Console.WriteLine("Finding Rook Magics... This might take a minute.");
            //MagicFinder.GenerateAllRookMagics();
            //Console.WriteLine("Done! Copy the output above.");
            //return;
            InitializeNotationMaps();
            KingMoveGenerator.PreCalculateKingMoves();
            KnightMoveGenerator.PreCalculateKnightMoves();
            BishopMoveGenerator.PreCalculateBishopAttacks();
            RookMoveGenerator.PreCalculateRookAttacks();
        }

        static string SquareToString(int square)
        {
            int file = square % 8;
            int rank = square / 8;

            char fileChar = (char)('a' + file);
            char rankChar = (char)('1' + rank);

            return $"{fileChar}{rankChar}";
        }

        public static void showMoves(Board b, Span<Move> possibleMoves)
        {
            int movesFound = allMoves.GenerateAllLegalMoves(b, possibleMoves, b.SideToMove);

            ulong toSquaresBitboard = 0;

            for (int i = 0; i < movesFound; i++)
            {
                ref Move m = ref possibleMoves[i];

                string moveStr = $"{SquareToString(m.FromSquare)}{SquareToString(m.ToSquare)}";
                // build bitboard of destinations
                toSquaresBitboard |= 1UL << m.ToSquare;
            }

            renderBitboard(toSquaresBitboard);
        }

        public static void showMoves2(Board b, Span<Move> moves)
        {

            Console.WriteLine($"\n=== Moves for {(b.SideToMove == 0 ? "White" : "Black")} ({moves.Length} total) ===");

            for (int i = 0; i < moves.Length; i++)
            {
                Move m = moves[i];

                string from = IndexToNotation[m.FromSquare];
                string to = IndexToNotation[m.ToSquare];

                string output = $"{from} -> {to} |";

                if (m.IsCapture)
                {
                    output += " is_capture";
                }

                if (m.IsEnPassant)
                {
                    output += " is_en_passant";
                }

                if (m.IsCastle)
                {
                    output += " IsCastle";
                }

                if (m.IsPromotion)
                {
                    char[] pieceChars = { 'P', 'N', 'B', 'R', 'Q', 'K', 'p', 'n', 'b', 'r', 'q', 'k' };
                    char promoChar = char.ToUpper(pieceChars[m.PromotedPieceType]);
                    output += $" is_promotion promotionPieceType: {promoChar}";
                }

                Console.WriteLine(output);
            }
            Console.WriteLine("=========================");
        }

        public static Dictionary<int, string> IndexToNotation = new Dictionary<int, string>(); // fun fekt: int.getHashCode() runnuje odmah i samo vrati taj int, ne hashuje ga.
        public static Dictionary<string, int> NotationToIndex = new Dictionary<string, int>();

        // 2. The initialization function
        public static void InitializeNotationMaps()
        {
            for (int rank = 0; rank < 8; rank++)

            {
                for (int file = 0; file < 8; file++)
                {
                    int index = rank * 8 + file;

                    // Character math: 'a' + 0 = 'a', 'a' + 1 = 'b', etc.
                    char fileChar = (char)('a' + file);

                    // Character math: '1' + 0 = '1', '1' + 1 = '2', etc.
                    char rankChar = (char)('1' + rank);

                    string notation = $"{fileChar}{rankChar}";

                    // Populate both dictionaries simultaneously
                    IndexToNotation[index] = notation;
                    NotationToIndex[notation] = index;
                }
            }
        }

        // Helper to set the standard 64-bit hexadecimal starting positions
        public static void InitializeStartingPosition(Board b)
        {
            // --- White Pieces ---
            b.Pieces[0] = 0x000000000000FF00; // Pawns (Rank 2)
            b.Pieces[1] = 0x0000000000000042; // Knights (b1, g1)
            b.Pieces[2] = 0x0000000000000024; // Bishops (c1, f1)
            b.Pieces[3] = 0x0000000000000081; // Rooks (a1, h1)
            b.Pieces[4] = 0x0000000000000008; // Queen (d1)
            b.Pieces[5] = 0x0000000000000010; // King (e1)

            // --- Black Pieces ---
            b.Pieces[6] = 0x00FF000000000000; // Pawns (Rank 7)
            b.Pieces[7] = 0x4200000000000000; // Knights (b8, g8)
            b.Pieces[8] = 0x2400000000000000; // Bishops (c8, f8)
            b.Pieces[9] = 0x8100000000000000; // Rooks (a8, h8)
            b.Pieces[10] = 0x0800000000000000; // Queen (d8)
            b.Pieces[11] = 0x1000000000000000; // King (e8)

            b.SideToMove = 0; // White's turn to move
        }

        public static bool TryLoadFen(Board b, string fen, out string error)
        {
            error = "";

            if (b == null)
            {
                error = "Board is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(fen))
            {
                error = "Paste a FEN first.";
                return false;
            }

            string[] parts = fen.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 4 || parts.Length > 6)
            {
                error = "FEN must have 4 to 6 fields: pieces side castling en-passant halfmove fullmove.";
                return false;
            }

            for (int i = 0; i < 12; i++)
            {
                b.Pieces[i] = 0UL;
            }

            string[] ranks = parts[0].Split('/');

            if (ranks.Length != 8)
            {
                error = "Piece placement must contain 8 ranks.";
                return false;
            }

            for (int fenRank = 0; fenRank < 8; fenRank++)
            {
                int file = 0;

                for (int i = 0; i < ranks[fenRank].Length; i++)
                {
                    char c = ranks[fenRank][i];

                    if (char.IsDigit(c))
                    {
                        int emptySquares = c - '0';

                        if (emptySquares < 1 || emptySquares > 8)
                        {
                            error = "Invalid empty-square number in piece placement.";
                            return false;
                        }

                        file += emptySquares;
                        continue;
                    }

                    int pieceType = FenCharToPieceType(c);

                    if (pieceType == -1)
                    {
                        error = "Invalid piece character: " + c;
                        return false;
                    }

                    if (file >= 8)
                    {
                        error = "Too many squares in one rank.";
                        return false;
                    }

                    int engineRank = 7 - fenRank;
                    int square = engineRank * 8 + file;

                    b.Pieces[pieceType] |= 1UL << square;
                    file++;
                }

                if (file != 8)
                {
                    error = "Each rank must add up to exactly 8 squares.";
                    return false;
                }
            }

            if (CountBits(b.Pieces[5]) != 1 || CountBits(b.Pieces[11]) != 1)
            {
                error = "FEN must contain exactly one white king and one black king.";
                return false;
            }

            if (parts[1] == "w")
            {
                b.SideToMove = 0;
            }
            else if (parts[1] == "b")
            {
                b.SideToMove = 1;
            }
            else
            {
                error = "Side to move must be w or b.";
                return false;
            }

            if (!TryParseCastlingRights(b, parts[2], out error))
            {
                return false;
            }

            b.EnPassantSquare = -1;
            if (parts.Length >= 4 && parts[3] != "-")
            {
                if (parts[3].Length == 2)
                {
                    int file = parts[3][0] - 'a';
                    int rank = parts[3][1] - '1';
                    if (file >= 0 && file < 8 && rank >= 0 && rank < 8)
                    {
                        b.EnPassantSquare = rank * 8 + file;
                    }
                }
            }

            b.HalfMoveClock = 0;

            if (parts.Length >= 5)
            {
                int halfMoveClock;

                if (!int.TryParse(parts[4], out halfMoveClock) || halfMoveClock < 0)
                {
                    error = "Halfmove clock must be 0 or higher.";
                    return false;
                }

                b.HalfMoveClock = halfMoveClock;
            }

            if (parts.Length >= 6)
            {
                int fullMoveNumber;

                if (!int.TryParse(parts[5], out fullMoveNumber) || fullMoveNumber < 1)
                {
                    error = "Fullmove number must be 1 or higher.";
                    return false;
                }

                // Board does not currently store fullmove number, so we only validate it.
            }

            RemoveImpossibleCastlingRights(b);

            return true;
        }

        private static int FenCharToPieceType(char c)
        {
            if (c == 'P') return 0;
            if (c == 'N') return 1;
            if (c == 'B') return 2;
            if (c == 'R') return 3;
            if (c == 'Q') return 4;
            if (c == 'K') return 5;

            if (c == 'p') return 6;
            if (c == 'n') return 7;
            if (c == 'b') return 8;
            if (c == 'r') return 9;
            if (c == 'q') return 10;
            if (c == 'k') return 11;

            return -1;
        }

        private static bool TryParseCastlingRights(Board b, string castlingText, out string error)
        {
            error = "";
            b.CastlingRights = 0;

            if (castlingText == "-")
            {
                return true;
            }

            bool seenK = false;
            bool seenQ = false;
            bool seenk = false;
            bool seenq = false;

            for (int i = 0; i < castlingText.Length; i++)
            {
                char c = castlingText[i];

                if (c == 'K')
                {
                    if (seenK)
                    {
                        error = "Duplicate castling right: K.";
                        return false;
                    }

                    seenK = true;
                    b.CastlingRights |= 1;
                }
                else if (c == 'Q')
                {
                    if (seenQ)
                    {
                        error = "Duplicate castling right: Q.";
                        return false;
                    }

                    seenQ = true;
                    b.CastlingRights |= 2;
                }
                else if (c == 'k')
                {
                    if (seenk)
                    {
                        error = "Duplicate castling right: k.";
                        return false;
                    }

                    seenk = true;
                    b.CastlingRights |= 4;
                }
                else if (c == 'q')
                {
                    if (seenq)
                    {
                        error = "Duplicate castling right: q.";
                        return false;
                    }

                    seenq = true;
                    b.CastlingRights |= 8;
                }
                else
                {
                    error = "Castling rights must be KQkq or -.";
                    return false;
                }
            }

            return true;
        }

        private static void RemoveImpossibleCastlingRights(Board b)
        {
            if ((b.Pieces[5] & (1UL << 4)) == 0 || (b.Pieces[3] & (1UL << 7)) == 0)
            {
                b.CastlingRights &= 14; // remove white kingside
            }

            if ((b.Pieces[5] & (1UL << 4)) == 0 || (b.Pieces[3] & (1UL << 0)) == 0)
            {
                b.CastlingRights &= 13; // remove white queenside
            }

            if ((b.Pieces[11] & (1UL << 60)) == 0 || (b.Pieces[9] & (1UL << 63)) == 0)
            {
                b.CastlingRights &= 11; // remove black kingside
            }

            if ((b.Pieces[11] & (1UL << 60)) == 0 || (b.Pieces[9] & (1UL << 56)) == 0)
            {
                b.CastlingRights &= 7; // remove black queenside
            }
        }

        private static int CountBits(ulong value)
        {
            int count = 0;

            while (value != 0)
            {
                value &= value - 1;
                count++;
            }

            return count;
        }


        public static void renderBitboard(ulong board, string title = "Bitboard")
        {

            string displayTitle = $" {title} ";
            int boardWidth = 19; // The exact width of "+-----------------+"

            if (displayTitle.Length >= boardWidth)
            {
                // If the title is too long, just print it aligned with the 2-space margin
                Console.WriteLine($"  {displayTitle}");
            }
            else
            {

                int leftPad = (boardWidth - displayTitle.Length) / 2;
                int rightPad = boardWidth - displayTitle.Length - leftPad;

                Console.WriteLine("  " + new string('=', leftPad) + displayTitle + new string('=', rightPad));
            }

            Console.WriteLine("  +-----------------+");

            for (int rank = 7; rank >= 0; rank--)
            {
                Console.Write($"{rank + 1} | ");

                for (int file = 0; file < 8; file++)
                {
                    int square = rank * 8 + file;

                    if ((board & (1UL << square)) != 0)
                    {
                        Console.Write("1 ");
                    }
                    else
                    {
                        Console.Write(". ");
                    }
                }
                Console.WriteLine("|");
            }
            Console.WriteLine("  +-----------------+");
            Console.WriteLine("    a b c d e f g h");

            // Print the raw value for easy copy-pasting
            Console.WriteLine($"Dec : {board}\n");
        }
    }
}