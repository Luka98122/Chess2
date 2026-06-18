using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.ExceptionServices;
using ChessEngine;
using static ChessEngine.EngineHelpers;
using static ChessEngine.KnightMoveGenerator;


namespace ChessEngine
{
    public struct Move
    {
        public int FromSquare;
        public int ToSquare;
        public int PieceType;

        // Flags for special moves
        public bool IsCapture;
        public bool IsPromotion;
        public int PromotedPieceType;
        public bool IsEnPassant;
        public bool IsCastle;

        public Move(int from, int to, int piece, bool isCapture = false)
        {
            FromSquare = from;
            ToSquare = to;
            PieceType = piece;
            IsCapture = isCapture;

            // Defaults
            IsPromotion = false;
            PromotedPieceType = -1;
            IsEnPassant = false;
            IsCastle = false;
        }
    }

    public struct BoardStateInfo
    {
        public Move MoveMade; // Save the move so we can reverse it!
        public int CapturedPieceType; // Store the exact piece we captured
        public byte CastlingRights;
        public int EnPassantSquare;
        public int HalfMoveClock;
        public int GameType;
        public ulong ZobristKey;
    }

    public class Board
    {
        // Indices 0-5: White (P, N, B, R, Q, K)
        // Indices 6-11: Black (P, N, B, R, Q, K)
        public ulong[] Pieces = new ulong[12];

        public int SideToMove = 0; // 0 for White, 1 for Black
        public byte CastlingRights = 15;
        public int EnPassantSquare = -1;
        public int HalfMoveClock = 0;
        public int GameType = 0; // 0 - Early, 1 - Mid, 2 - End
        // 15 represents binary 1111 (all castling rights intact)
        public static readonly byte[] CastlingRightsMask = new byte[64] {
            13, 15, 15, 15, 12, 15, 15, 14, // Rank 1 (a1=13, e1=12, h1=14)
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15,
            7, 15, 15, 15, 3, 15, 15, 11 // Rank 8 (a8=7, e8=3, h8=11)
        };
        private BoardStateInfo[] _stateHistory = new BoardStateInfo[2048];
        private int _historyPly = 0;

        public ulong ZobristKey;

        public ulong GenerateKey()
        {
            ulong key = 0;

            // 1. XOR all pieces
            for (int p = 0; p < 12; p++)
            {
                ulong bitboard = Pieces[p];
                while (bitboard != 0)
                {
                    int sq = System.Numerics.BitOperations.TrailingZeroCount(bitboard);
                    key ^= Zobrist.Pieces[p, sq];
                    bitboard &= bitboard - 1; // Clear LSB
                }
            }

            // 2. XOR side to move
            if (SideToMove == 1) key ^= Zobrist.SideToMove;

            // 3. XOR castling rights
            key ^= Zobrist.Castling[CastlingRights];

            // 4. XOR en passant square
            if (EnPassantSquare != -1) key ^= Zobrist.EnPassant[EnPassantSquare];

            return key;
        }

        public void MakeMove(Move move)
        {
            if (_historyPly >= _stateHistory.Length)
            {
                Array.Resize(ref _stateHistory, _stateHistory.Length * 2);
            }

            ref BoardStateInfo state = ref _stateHistory[_historyPly++];
            state.CastlingRights = CastlingRights;
            state.MoveMade = move;
            state.HalfMoveClock = HalfMoveClock;
            state.EnPassantSquare = EnPassantSquare;
            state.GameType = GameType;
            state.CapturedPieceType = -1;
            state.ZobristKey = this.ZobristKey;

            EnPassantSquare = -1;

            CastlingRights &= CastlingRightsMask[move.FromSquare];
            CastlingRights &= CastlingRightsMask[move.ToSquare];

            if (move.IsEnPassant)
            {
                int capturedPawnSquare = move.ToSquare + (SideToMove == 0 ? -8 : 8);
                int opponentPawnType = SideToMove == 0 ? 6 : 0;
                state.CapturedPieceType = opponentPawnType;
                Pieces[opponentPawnType] &= ~(1UL << capturedPawnSquare);
                HalfMoveClock = 0;
            }
            else if (move.IsCapture)
            {
                int startIndex = SideToMove == 0 ? 6 : 0;
                ulong targetBit = 1UL << move.ToSquare;

                for (int i = startIndex; i <= startIndex + 5; i++)
                {
                    if ((Pieces[i] & targetBit) != 0)
                    {
                        state.CapturedPieceType = i;
                        Pieces[i] &= ~targetBit;
                        break;
                    }
                }
                HalfMoveClock = 0;
            }
            else if (move.PieceType == 0 || move.PieceType == 6)
            {
                HalfMoveClock = 0;
            }
            else
            {
                HalfMoveClock++;
            }

            Pieces[move.PieceType] &= ~(1UL << move.FromSquare);

            if (move.IsPromotion)
            {
                Pieces[move.PromotedPieceType + 6 * SideToMove] |= (1UL << move.ToSquare);
            }
            else
            {
                Pieces[move.PieceType] |= (1UL << move.ToSquare);
            }

            if ((move.PieceType == 0 || move.PieceType == 6) && !move.IsPromotion)
            {
                int fromRank = move.FromSquare / 8;
                int toRank = move.ToSquare / 8;
                if (Math.Abs(toRank - fromRank) == 2)
                {
                    EnPassantSquare = move.FromSquare + (SideToMove == 0 ? 8 : -8);
                }
            }

            if (move.IsCastle)
            {
                int rookType = SideToMove == 0 ? 3 : 9;

                if (move.ToSquare == 6) // White Kingside (g1)
                {
                    Pieces[rookType] &= ~(1UL << 7); // Remove rook from h1
                    Pieces[rookType] |= (1UL << 5);  // Add rook to f1
                }
                else if (move.ToSquare == 2) // White Queenside (c1)
                {
                    Pieces[rookType] &= ~(1UL << 0); // Remove rook from a1
                    Pieces[rookType] |= (1UL << 3);  // Add rook to d1
                }
                else if (move.ToSquare == 62) // Black Kingside (g8)
                {
                    Pieces[rookType] &= ~(1UL << 63); // Remove rook from h8
                    Pieces[rookType] |= (1UL << 61);  // Add rook to f8
                }
                else if (move.ToSquare == 58) // Black Queenside (c8)
                {
                    Pieces[rookType] &= ~(1UL << 56); // Remove rook from a8
                    Pieces[rookType] |= (1UL << 59);  // Add rook to d8
                }
            }

            

            

            int pieceCount = 0;
            for (int i = 0; i < 12; i++)
            {
                pieceCount += BitOperations.PopCount(Pieces[i]);
            }

            if (pieceCount <= 23)
            {
                GameType = 1;
            }


            if (pieceCount<=7)
            {
                GameType = 2;
            }
            
            // 4. Update Turn
            SideToMove = SideToMove == 0 ? 1 : 0;

            this.ZobristKey = GenerateKey();
        }

        public void UnmakeMove()
        {
            ref BoardStateInfo state = ref _stateHistory[--_historyPly];
            Move move = state.MoveMade;

            SideToMove = 1 - SideToMove;
            CastlingRights = state.CastlingRights;
            EnPassantSquare = state.EnPassantSquare;
            HalfMoveClock = state.HalfMoveClock;
            GameType = state.GameType;

            // 3. Reverse the moving piece
            if (move.IsPromotion)
            {
                // Remove the newly promoted piece from the board
                Pieces[move.PromotedPieceType + 6 * SideToMove] &= ~(1UL << move.ToSquare);
            }
            else
            {
                // Remove the standard piece from its destination
                Pieces[move.PieceType] &= ~(1UL << move.ToSquare);
            }

            // Put the original piece back onto its starting square
            Pieces[move.PieceType] |= (1UL << move.FromSquare);

            // 4. Restore the Captured Piece (if there was one)
            if (state.CapturedPieceType != -1)
            {
                if (move.IsEnPassant)
                {
                    int capturedSquare = move.ToSquare + (SideToMove == 0 ? -8 : 8);
                    Pieces[state.CapturedPieceType] |= (1UL << capturedSquare);
                }
                else
                {
                    Pieces[state.CapturedPieceType] |= (1UL << move.ToSquare);
                }
            }

            // 5. Reverse the Castling Rook
            if (move.IsCastle)
            {
                int rookType = SideToMove == 0 ? 3 : 9;

                // Reverse the bitflips we did in MakeMove
                if (move.ToSquare == 6) { Pieces[rookType] &= ~(1UL << 5); Pieces[rookType] |= (1UL << 7); } // g1
                else if (move.ToSquare == 2) { Pieces[rookType] &= ~(1UL << 3); Pieces[rookType] |= (1UL << 0); } // c1
                else if (move.ToSquare == 62) { Pieces[rookType] &= ~(1UL << 61); Pieces[rookType] |= (1UL << 63); } // g8
                else if (move.ToSquare == 58) { Pieces[rookType] &= ~(1UL << 59); Pieces[rookType] |= (1UL << 56); } // c8
            }
            this.ZobristKey = state.ZobristKey;
        }

        // Helper method to locate and clear a captured piece
        private void ClearPieceAtTarget(int square, int opponentColor)
        {
            int startIndex = opponentColor == 0 ? 0 : 6;
            int endIndex = startIndex + 5;

            ulong squareMask = ~(1UL << square);

            for (int i = startIndex; i <= endIndex; i++)
            {
                Pieces[i] &= squareMask;
            }
        }

        public bool IsSquareAttacked(int square, int attackerColor)
        {
            if (square < 0 || square > 63) return false;
            ulong friendlyPieces = attackerColor == 0
                ? (Pieces[0] | Pieces[1] | Pieces[2] | Pieces[3] | Pieces[4] | Pieces[5])
                : (Pieces[6] | Pieces[7] | Pieces[8] | Pieces[9] | Pieces[10] | Pieces[11]);

            ulong enemyPieces = attackerColor == 0
                ? (Pieces[6] | Pieces[7] | Pieces[8] | Pieces[9] | Pieces[10] | Pieces[11])
                : (Pieces[0] | Pieces[1] | Pieces[2] | Pieces[3] | Pieces[4] | Pieces[5]);

            ulong occupied = friendlyPieces | enemyPieces;
            ulong squareBB = 1UL << square;

            ulong knights = Pieces[attackerColor == 0 ? 1 : 7];
            if ((KnightMoveGenerator.KnightPreCalcs[square] & knights) != 0) return true;

            ulong kings = Pieces[attackerColor == 0 ? 5 : 11];
            if ((KingMoveGenerator.KingPreCalcs[square] & kings) != 0) return true;

            ulong pawns = Pieces[attackerColor == 0 ? 0 : 6];
            if (attackerColor == 0) // White is attacking
            {
                if ((((squareBB & PawnMoveGenerator.NotFileA) >> 9) & pawns) != 0) return true;
                if ((((squareBB & PawnMoveGenerator.NotFileH) >> 7) & pawns) != 0) return true;
            }
            else // Black is attacking
            {
                if ((((squareBB & PawnMoveGenerator.NotFileA) << 7) & pawns) != 0) return true;
                if ((((squareBB & PawnMoveGenerator.NotFileH) << 9) & pawns) != 0) return true;
            }

            ulong bishopsQueens = Pieces[attackerColor == 0 ? 2 : 8] | Pieces[attackerColor == 0 ? 4 : 10];
            if (bishopsQueens != 0)
            {
                ulong bishopBlockers = occupied & BishopMoveGenerator.BishopMasks[square];
                int bMagicIndex = (int)((bishopBlockers * BishopMoveGenerator.BishopMagics[square]) >> (64 - BishopMoveGenerator.BishopRelevantBits[square]));
                if ((BishopMoveGenerator.BishopAttacks[square][bMagicIndex] & bishopsQueens) != 0) return true;
            }

            ulong rooksQueens = Pieces[attackerColor == 0 ? 3 : 9] | Pieces[attackerColor == 0 ? 4 : 10];
            if (rooksQueens != 0)
            {
                ulong rookBlockers = occupied & RookMoveGenerator.RookMasks[square];
                int rMagicIndex = (int)((rookBlockers * RookMoveGenerator.RookMagics[square]) >> (64 - RookMoveGenerator.RookRelevantBits[square]));
                if ((RookMoveGenerator.RookAttacks[square][rMagicIndex] & rooksQueens) != 0) return true;
            }

            return false;
        }

        public int GetBoardState()
        {
            Span<Move> moves = stackalloc Move[218]; // mnogo brze od heap memorije
            int legalMoveCount = allMoves.GenerateAllLegalMoves(this, moves, this.SideToMove);

            if (legalMoveCount > 0)
            {
                if (this.HalfMoveClock >= 100) return 2; // stalemate (pat)
                return -1; // normal
            }

            int kingPieceType = 5 + this.SideToMove * 6;
            int kingSquare = BitOperations.TrailingZeroCount(this.Pieces[kingPieceType]);
            int attackerColor = 1 - this.SideToMove;

            if (this.IsSquareAttacked(kingSquare, attackerColor))
            {
                return attackerColor; // attackerColor wins
            }
            return 2; // Stalemate (pat)
        }

        private int GetCheapestAttackerValue(int sq, int attackerColor, ulong occupied)
        {
            int pOffset = attackerColor == 0 ? 0 : 6;
            ulong targetBit = 1UL << sq;

            // 1. Pawns (100)
            // Reverse pawn attacks: If a White pawn attacks `sq`, the pawn must be at sq-7 or sq-9.
            ulong pawns = Pieces[pOffset + 0];
            ulong pawnAttackers = attackerColor == 0
                ? ((targetBit >> 7) & PawnMoveGenerator.NotFileA) | ((targetBit >> 9) & PawnMoveGenerator.NotFileH)
                : ((targetBit << 7) & PawnMoveGenerator.NotFileH) | ((targetBit << 9) & PawnMoveGenerator.NotFileA);
            if ((pawns & pawnAttackers) != 0) return 100;

            // 2. Knights (300)
            if ((KnightMoveGenerator.KnightPreCalcs[sq] & Pieces[pOffset + 1]) != 0) return 300;

            // 3. Bishops (300)
            ulong bBlockers = occupied & BishopMoveGenerator.BishopMasks[sq];
            int bMagic = (int)((bBlockers * BishopMoveGenerator.BishopMagics[sq]) >> (64 - BishopMoveGenerator.BishopRelevantBits[sq]));
            ulong bAttacks = BishopMoveGenerator.BishopAttacks[sq][bMagic];
            if ((bAttacks & Pieces[pOffset + 2]) != 0) return 300;

            // 4. Rooks (500)
            ulong rBlockers = occupied & RookMoveGenerator.RookMasks[sq];
            int rMagic = (int)((rBlockers * RookMoveGenerator.RookMagics[sq]) >> (64 - RookMoveGenerator.RookRelevantBits[sq]));
            ulong rAttacks = RookMoveGenerator.RookAttacks[sq][rMagic];
            if ((rAttacks & Pieces[pOffset + 3]) != 0) return 500;

            // 5. Queens (900) - Queens share Rook and Bishop attack rays
            if (((bAttacks | rAttacks) & Pieces[pOffset + 4]) != 0) return 900;

            // 6. Kings (6767)
            if ((KingMoveGenerator.KingPreCalcs[sq] & Pieces[pOffset + 5]) != 0) return 6767;

            return 99999; // 99999 means the square is not attacked
        }

        public static readonly int[] vals = new int[] { 100, 300, 300, 500, 900, 6767, -100, -300, -300, -500, -900, -6767 }; 
        public int GetBoardEval(bool includeHangingPieces = true)
        {

            int score = 0;
            for (int pt = 0; pt < 12; pt++)
            {
                ulong bitboard = this.Pieces[pt];
                bool isBlack = pt > 5;

                // Grab absolute material value from your Board.vals array
                int materialValue = Math.Abs(vals[pt]);

                while (bitboard != 0)
                {
                    // Isolate the index of the first '1' bit
                    int sq = System.Numerics.BitOperations.TrailingZeroCount(bitboard);

                    // Get the positional bonus from our tables
                    int positionalBonus = PST.GetScore(pt, sq);

                    // Combine material and positional value
                    int pieceScore = materialValue + positionalBonus;

                    // Add for White, subtract for Black
                    if (isBlack)
                    {
                        score -= pieceScore;
                    }
                    else
                    {
                        score += pieceScore;
                    }

                    // Clear the least significant set bit (fastest way to loop through pieces)
                    bitboard &= bitboard - 1;
                }
            }

            // Check eval:

            int wKingSquare = BitOperations.TrailingZeroCount(this.Pieces[5]);
            bool isWInCheck = this.IsSquareAttacked(wKingSquare, 1); //black attacking white king
            if (isWInCheck)
            {
                score -= 50; // Configurable, but putting in check is weighted to 50cp (half a pawn)
            }

            int bKingSquare = BitOperations.TrailingZeroCount(this.Pieces[11]);
            bool isBInCheck = this.IsSquareAttacked(bKingSquare, 0); //white attacking black king
            if (isBInCheck)
            {
                score += 50; // Configurable, but putting in check is weighted to 50cp (half a pawn)
            }

            if (includeHangingPieces)
            {
                ulong occupied = this.Pieces[0] | this.Pieces[1] | this.Pieces[2] | this.Pieces[3] | this.Pieces[4] | this.Pieces[5] |
                                 this.Pieces[6] | this.Pieces[7] | this.Pieces[8] | this.Pieces[9] | this.Pieces[10] | this.Pieces[11];

                // 1. Evaluate hanging pieces for White (Indices 0-4: Pawn to Queen)
                for (int i = 0; i < 5; i++)
                {
                    ulong piecesIter = this.Pieces[i];
                    while (piecesIter != 0)
                    {
                        int sq = BitOperations.TrailingZeroCount(piecesIter);

                        int cheapestAttacker = GetCheapestAttackerValue(sq, 1, occupied); // 1 = Black

                        if (cheapestAttacker != 99999) // If it is attacked at all
                        {
                            bool isDefended = this.IsSquareAttacked(sq, 0); // 0 = White
                            int pieceValue = vals[i]; // E.g., 900 for Queen

                            // It is a bad position if it's completely undefended, 
                            // OR if the attacker is worth less than the victim (e.g., Pawn attacking defended Queen)
                            if (!isDefended || cheapestAttacker < pieceValue)
                            {
                                score -= pieceValue / 2;
                            }
                        }
                        piecesIter &= piecesIter - 1;
                    }
                }

                // 2. Evaluate hanging pieces for Black (Indices 6-10: Pawn to Queen)
                for (int i = 6; i < 11; i++)
                {
                    ulong piecesIter = this.Pieces[i];
                    while (piecesIter != 0)
                    {
                        int sq = BitOperations.TrailingZeroCount(piecesIter);

                        int cheapestAttacker = GetCheapestAttackerValue(sq, 0, occupied); // 0 = White

                        if (cheapestAttacker != 99999)
                        {
                            bool isDefended = this.IsSquareAttacked(sq, 1); // 1 = Black
                            int pieceValue = Math.Abs(vals[i]); // Black values are negative, so get the absolute value for comparison

                            if (!isDefended || cheapestAttacker < pieceValue)
                            {
                                score -= vals[i] / 2; // Penalize Black (subtracting negative adds to White's score)
                            }
                        }
                        piecesIter &= piecesIter - 1;
                    }
                }
            }

            return score;
        }

        public Board Clone()
        {
            Board copy = new Board();

            copy.Pieces = new ulong[this.Pieces.Length];
            Array.Copy(this.Pieces, copy.Pieces, this.Pieces.Length);

            copy.SideToMove = this.SideToMove;
            copy.CastlingRights = this.CastlingRights;
            copy.EnPassantSquare = this.EnPassantSquare;
            copy.HalfMoveClock = this.HalfMoveClock;
            copy.GameType = this.GameType;
            copy.ZobristKey = this.ZobristKey;
            copy._historyPly = this._historyPly;
            Array.Copy(this._stateHistory, copy._stateHistory, this._stateHistory.Length);

            return copy;
        }
    }
}

