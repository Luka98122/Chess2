using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Reflection.PortableExecutable;
using ChessEngine;
using static ChessEngine.EngineHelpers;
using static ChessEngine.KnightMoveGenerator;
//MagicFinder.GenerateAllBishopMagics();
init();


// 1. Make the board and set up the standard starting position
Board board = new Board();
InitializeStartingPosition(board);

Console.WriteLine("=== Initial Board State ===");
RenderBoard(board);
// possibleMoves je span koji se passuje u skoro sve
Span<Move> possibleMoves = new Move[500];
showMoves2(board, possibleMoves);

Move moveE4 = new Move(from: 12, to: 28, piece: 0);
board.MakeMove(moveE4);
showMoves2(board, possibleMoves);

Move moveC5 = new Move(from: 50, to: 34, piece: 6);
board.MakeMove(moveC5);
showMoves2(board, possibleMoves);
RenderBoard(board);

// 3. Render the board state again to show the move
Console.WriteLine("\n=== Board State after e2-e4 ===");
RenderBoard(board);

AnalyzePosition(board);

Tests.allTests();



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

    public struct BoardSnapshot
    {
        // Holds a complete copy of the board's piece arrays
        public ulong[] Pieces;
        
        // Game state variables
        public int SideToMove;
        public byte CastlingRights;
        public int EnPassantSquare;
        public int HalfMoveClock;

        public BoardSnapshot(ulong[] currentPieces, int side, byte castling, int ep, int halfMove)
        {
            Pieces = (ulong[])currentPieces.Clone();
            
            SideToMove = side;
            CastlingRights = castling;
            EnPassantSquare = ep;
            HalfMoveClock = halfMove;
        }
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

        // The history stack holds the FULL board snapshot
        private Stack<BoardSnapshot> _history = new Stack<BoardSnapshot>();

        public void MakeMove(Move move)
        {
            // 1. Take a snapshot of the ENTIRE current board and push it to history
            _history.Push(new BoardSnapshot(Pieces, SideToMove, CastlingRights, EnPassantSquare, HalfMoveClock));

            // 2. Apply the move
            // Remove piece from source square by clearing the bit
            Pieces[move.PieceType] &= ~(1UL << move.FromSquare); 
            // Add piece to target square by setting the bit
            Pieces[move.PieceType] |= (1UL << move.ToSquare);

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

            // 3. Handle Captures & 50-Move Rule
            if (move.IsCapture)
            {
                ClearPieceAtTarget(move.ToSquare, SideToMove == 0 ? 1 : 0);
                HalfMoveClock = 0; // Captures reset the 50-move rule clock
            }
            else if (move.PieceType == 0 || move.PieceType == 6) // Pawn move
            {
                HalfMoveClock = 0; // Pawn pushes also reset the clock
            }
            else
            {
                HalfMoveClock++; // Normal moves increment the clock
            }

            if (move.IsPromotion)
            {
                Pieces[0+6*SideToMove] &= ~(1UL << move.ToSquare);
                Pieces[move.PromotedPieceType + 6 * SideToMove] |= (1UL << move.ToSquare);
            }

            // 4. Update Turn
            SideToMove = SideToMove == 0 ? 1 : 0;
        }

        public void UnmakeMove()
        {
            // 1. Pop the complete snapshot from the history stack
            BoardSnapshot previousState = _history.Pop();

            // 2. Overwrite the current arrays and variables with the snapshot data
            // Array.Copy overwrites the existing array without allocating new memory
            Array.Copy(previousState.Pieces, Pieces, 12);
            
            SideToMove = previousState.SideToMove;
            CastlingRights = previousState.CastlingRights;
            EnPassantSquare = previousState.EnPassantSquare;
            HalfMoveClock = previousState.HalfMoveClock;
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
            if (square<0 || square>63) return false;
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

            if (this.IsSquareAttacked(kingSquare, attackerColor)){
                return attackerColor; // attackerColor wins
            }
            return 2; // Stalemate (pat)
        }
    
        public int GetBoardEval()
        {
            //-1000 crni dominatuje
            //+1000 beli dominatuje

            // Checkmate eval
            int state = this.GetBoardState();
            if (state == 2) return 0; // Stalemate
            if (state == 0) return 100000; // Beli (0) je uradio checkmate na crnog 
            if (state == 1) return -100000; // Crni (1) je uradio checkmate na belog

            // Piece eval
            List<int> vals = new List<int> { 100, 300, 300, 500, 900, 6767, -100, -300, -300, -500, -900, -6767 };
            int score = 0;
            for (int i = 0; i < 12; i++)
            {
                score += BitOperations.PopCount(this.Pieces[i]) * vals[i];
                // PopCount = # of 1s in binary rep of a number (bitboard)
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
            if (isWInCheck)
            {
                score += 50; // Configurable, but putting in check is weighted to 50cp (half a pawn)
            }


            return score;
        }
    }
}


