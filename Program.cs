using System;
using System.Collections.Generic;
using ChessEngine;

// 1. Make the board and set up the standard starting position
Board board = new Board();
InitializeStartingPosition(board);

Console.WriteLine("=== Initial Board State ===");
RenderBoard(board);

// 2. Play the move e2-e4
// Square Math: 
// A1 = 0, B1 = 1 ... E1 = 4
// E2 = Rank 2 (index 1) * 8 + File E (index 4) = 12
// E4 = Rank 4 (index 3) * 8 + File E (index 4) = 28
// PieceType 0 is White Pawn
Move moveE4 = new Move(from: 12, to: 28, piece: 0);
board.MakeMove(moveE4);

// 4. Render the board state again to show the move
Console.WriteLine("\n=== Board State after e2-e4 ===");
RenderBoard(board);


// --- 3. HELPER FUNCTIONS ---

// Helper to render the board state in the terminal
void RenderBoard(Board b)
{
    // Indices match the bitboard array (0-5 White, 6-11 Black)
    // Uppercase for White, Lowercase for Black
    char[] pieceChars = { 'P', 'N', 'B', 'R', 'Q', 'K', 'p', 'n', 'b', 'r', 'q', 'k' };

    Console.WriteLine("  +-----------------+");
    
    // Loop backwards from Rank 8 down to Rank 1 so the board prints right-side up
    for (int rank = 7; rank >= 0; rank--)
    {
        Console.Write($"{rank + 1} | ");
        
        for (int file = 0; file < 8; file++)
        {
            int square = rank * 8 + file;
            char printChar = '.'; // Default empty square

            // Check all 12 bitboards to see if any piece is occupying this square
            for (int pieceType = 0; pieceType < 12; pieceType++)
            {
                // Bitwise AND to check if the specific square's bit is a 1
                if ((b.Pieces[pieceType] & (1UL << square)) != 0)
                {
                    printChar = pieceChars[pieceType];
                    break;
                }
            }
            Console.Write(printChar + " ");
        }
        Console.WriteLine("|");
    }
    Console.WriteLine("  +-----------------+");
    Console.WriteLine("    a b c d e f g h");
}

// Helper to set the standard 64-bit hexadecimal starting positions
void InitializeStartingPosition(Board b)
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
    }
}


