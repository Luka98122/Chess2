using System;
using System.Diagnostics;
using System.Numerics;

namespace ChessEngine
{
    public static class Zobrist
    {
        public static readonly ulong[,] Pieces = new ulong[12, 64];
        public static readonly ulong[] Castling = new ulong[16];
        public static readonly ulong[] EnPassant = new ulong[64];
        public static readonly ulong SideToMove;

        static Zobrist()
        {
            // Using a fixed seed for reproducibility
            Random rnd = new Random(1337);
            byte[] buffer = new byte[8];

            ulong NextRandom()
            {
                rnd.NextBytes(buffer);
                return BitConverter.ToUInt64(buffer, 0);
            }

            for (int p = 0; p < 12; p++)
                for (int sq = 0; sq < 64; sq++)
                    Pieces[p, sq] = NextRandom();

            for (int i = 0; i < 16; i++)
                Castling[i] = NextRandom();

            for (int i = 0; i < 64; i++)
                EnPassant[i] = NextRandom();

            SideToMove = NextRandom();
        }
    }
    public struct TTEntry
    {
        public ulong Key;
        public int Depth;
        public int Score;
        public int Flag; // 0 = Exact, 1 = Alpha (Upper bound), 2 = Beta (Lower bound)
    }

    public static class TT
    {
        // 0x400000 is ~4.1 million entries (adjust based on desired memory usage)
        private const int Size = 0x400000;
        public static TTEntry[] Entries = new TTEntry[Size];
        public static int CacheHits = 0;
        public static void Store(ulong key, int depth, int score, int flag)
        {
            int index = (int)(key % Size);
            ref TTEntry existing = ref Entries[index];
            if (existing.Key != key || depth >= existing.Depth)
            {
                existing = new TTEntry { Key = key, Depth = depth, Score = score, Flag = flag };
            }
        }

        public static bool TryProbe(ulong key, int depth, int alpha, int beta, out int score)
        {
            score = 0;
            TTEntry entry = Entries[key % Size];

            // Ensure no collision and that the cached depth is sufficient
            if (entry.Key == key && entry.Depth >= depth)
            {
                bool isHit = false;

                if (entry.Flag == 0) { score = entry.Score; isHit = true; } // Exact
                else if (entry.Flag == 1 && entry.Score <= alpha) { score = alpha; isHit = true; } // Upper bound
                else if (entry.Flag == 2 && entry.Score >= beta) { score = beta; isHit = true; }   // Lower bound

                // 2. If we found a valid hit, increment and check the milestone
                if (isHit)
                {
                    CacheHits++;
                    if (CacheHits % 10000 == 0)
                    {
                        Debug.WriteLine($"[DEBUG] Zobrist Cache Hits: {CacheHits}");
                    }
                    return true;
                }
            }
            return false;
        }
    }
    public static class Bot
    {
        // TODO: Fix 
        private const int Infinity = 2000000;
        public static Dictionary<(Board board, int depth, int a, int b, int c, int sideToMove), int> cache = new();
        // Simple inline MVV-LVA calculation
        public static int ScoreMove(Move m)
        {
            int score = 0;

            if (m.IsPromotion)
            {
                // Pushing a pawn to promotion is almost always a top-tier move
                score += 9000;
            }

            if (m.IsCapture)
            {
                // LVA: Subtracting the attacker's value means a Pawn (100) 
                // scores highly (900), while a Queen (900) scores lower (100).
                score += 1000 - Math.Abs(Board.vals[m.PieceType]);
            }

            return score;
        }

        private static int QuiescenceSearch(Board b, int alpha, int beta) //shallow check at the end of depth
        {
            // 1. "Stand Pat" Evaluation
            // If our position is already good enough without making any captures, 
            // we can establish a baseline score.
            int standPat = b.GetBoardEval(includeHangingPieces: false);
            standPat = b.SideToMove == 0 ? standPat : -standPat;

            // Fail-hard beta cutoff
            if (standPat >= beta)
            {
                return beta;
            }

            // Update alpha if standing pat is better than our current alpha
            if (standPat > alpha)
            {
                alpha = standPat;
            }

            // 2. Generate Moves
            Span<Move> moves = stackalloc Move[218];
            int moveCount = allMoves.GenerateAllLegalMoves(b, moves, b.SideToMove);

            // --- MOVE ORDERING START ---
            // Pre-score all moves to avoid O(N^2) function calls
            Span<int> moveScores = stackalloc int[moveCount];
            for (int i = 0; i < moveCount; i++)
            {
                moveScores[i] = ScoreMove(moves[i]);
            }

            // Selection sort based on pre-calculated scores
            for (int i = 0; i < moveCount - 1; i++)
            {
                int bestIndex = i;
                for (int j = i + 1; j < moveCount; j++)
                {
                    if (moveScores[j] > moveScores[bestIndex])
                    {
                        bestIndex = j;
                    }
                }

                if (bestIndex != i)
                {
                    // Swap moves
                    Move tempMove = moves[i];
                    moves[i] = moves[bestIndex];
                    moves[bestIndex] = tempMove;

                    // Swap scores to keep the arrays synchronized
                    int tempScore = moveScores[i];
                    moveScores[i] = moveScores[bestIndex];
                    moveScores[bestIndex] = tempScore;
                }
            }
            // --- MOVE ORDERING END ---

            // 3. Search ONLY Captures
            for (int i = 0; i < moveCount; i++)
            {
                // Skip quiet moves. In a fully optimized engine, you would write a separate 
                // GenerateCaptureMoves method to avoid generating quiet moves entirely.
                if (!moves[i].IsCapture) continue;

                b.MakeMove(moves[i]);

                // Recursively call QS instead of regular Search
                int score = -QuiescenceSearch(b, -beta, -alpha);

                b.UnmakeMove();

                if (score >= beta)
                {
                    return beta; // Opponent has a refutation, prune this branch
                }
                if (score > alpha)
                {
                    alpha = score; // We found a better capture sequence
                }
            }

            return alpha;
        }

        public static Move Think(Board b, int targetDepth, int topX)
        {
            Span<Move> moves = stackalloc Move[218];
            int moveCount = allMoves.GenerateAllLegalMoves(b, moves, b.SideToMove);
            if (moveCount == 0) return default;

            // --- MOVE ORDERING START ---
            // Pre-score all moves to avoid O(N^2) function calls
            Span<int> moveScores = stackalloc int[moveCount];
            for (int i = 0; i < moveCount; i++)
            {
                moveScores[i] = ScoreMove(moves[i]);
            }

            // Selection sort based on pre-calculated scores
            for (int i = 0; i < moveCount - 1; i++)
            {
                int bestIndex = i;
                for (int j = i + 1; j < moveCount; j++)
                {
                    if (moveScores[j] > moveScores[bestIndex])
                    {
                        bestIndex = j;
                    }
                }

                if (bestIndex != i)
                {
                    // Swap moves
                    Move tempMove = moves[i];
                    moves[i] = moves[bestIndex];
                    moves[bestIndex] = tempMove;

                    // Swap scores to keep the arrays synchronized
                    int tempScore = moveScores[i];
                    moveScores[i] = moveScores[bestIndex];
                    moveScores[bestIndex] = tempScore;
                }
            }
            // --- MOVE ORDERING END ---

            Move bestMoveThisTurn = moves[0];

            // Iterative Deepening Loop
            for (int currentDepth = 1; currentDepth <= targetDepth; currentDepth++)
            {
                int alpha = -Infinity;
                int beta = Infinity;
                int bestScore = -Infinity;
                Move bestMoveThisDepth = moves[0];

                for (int i = 0; i < moveCount; i++)
                {
                    b.MakeMove(moves[i]);
                    int score = -Search(b, currentDepth - 1, -beta, -alpha);
                    b.UnmakeMove();

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMoveThisDepth = moves[i];
                    }
                    if (score > alpha)
                    {
                        alpha = score;
                    }
                }

                bestMoveThisTurn = bestMoveThisDepth;
                // Optional: Debug.WriteLine($"Depth {currentDepth} finished. Best move: {bestMoveThisTurn.FromSquare} -> {bestMoveThisTurn.ToSquare}");
            }

            return bestMoveThisTurn;
        }

        public static int Search(Board b, int depth, int alpha, int beta)
        {
            int originalAlpha = alpha;

            // 1. Probe Transposition Table
            if (TT.TryProbe(b.ZobristKey, depth, alpha, beta, out int ttScore))
            {
                return ttScore;
            }

            // 2. Base Case
            if (depth <= 0)
            {
                return QuiescenceSearch(b, alpha, beta);
            }

            Span<Move> moves = stackalloc Move[256];
            int moveCount = allMoves.GenerateAllLegalMoves(b, moves, b.SideToMove);

            // 3. Terminal Node Handling
            if (moveCount == 0)
            {
                int kingPieceType = b.SideToMove == 0 ? 5 : 11;
                int kingSquare = BitOperations.TrailingZeroCount(b.Pieces[kingPieceType]);
                bool inCheck = b.IsSquareAttacked(kingSquare, 1 - b.SideToMove);

                if (inCheck)
                {
                    return -30000 - depth;
                }
                return 0; // Stalemate
            }

            // --- MOVE ORDERING START ---
            // Pre-score all moves to avoid O(N^2) function calls
            Span<int> moveScores = stackalloc int[moveCount];
            for (int i = 0; i < moveCount; i++)
            {
                moveScores[i] = ScoreMove(moves[i]);
            }

            // Selection sort based on pre-calculated scores
            for (int i = 0; i < moveCount - 1; i++)
            {
                int bestIndex = i;
                for (int j = i + 1; j < moveCount; j++)
                {
                    if (moveScores[j] > moveScores[bestIndex])
                    {
                        bestIndex = j;
                    }
                }

                if (bestIndex != i)
                {
                    // Swap moves
                    Move tempMove = moves[i];
                    moves[i] = moves[bestIndex];
                    moves[bestIndex] = tempMove;

                    // Swap scores to keep the arrays synchronized
                    int tempScore = moveScores[i];
                    moveScores[i] = moveScores[bestIndex];
                    moveScores[bestIndex] = tempScore;
                }
            }
            // --- MOVE ORDERING END ---

            int bestScore = -int.MaxValue;

            // 4. Negamax Loop
            for (int i = 0; i < moveCount; i++)
            {
                b.MakeMove(moves[i]);
                int score = -Search(b, depth - 1, -beta, -alpha);
                b.UnmakeMove();

                // Fail-hard beta cutoff
                if (score >= beta)
                {
                    TT.Store(b.ZobristKey, depth, beta, 2);
                    return beta;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    if (score > alpha)
                    {
                        alpha = score;
                    }
                }
            }

            // 5. TT Storage
            int flag = 0; // Exact Bound
            if (bestScore <= originalAlpha)
            {
                flag = 1; // Upper Bound (Failed low)
            }

            TT.Store(b.ZobristKey, depth, bestScore, flag);
            return bestScore;
        }
    }
}