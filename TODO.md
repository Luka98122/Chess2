### 1. Advanced Move Generation 
- [ ] `GeneratePawnMoves(Board b, int color)`: Shift operations (`<< 8`, `>> 8`) for single pushes, double pushes, and captures.
- [ ] `GenerateSlidingAttacks(int square, ulong occupiedSquares, int pieceType)`: Attack rays for Bishops, Rooks, and Queens (highly recommend researching Magic Bitboards for this).
- [ ] `IsSquareAttacked(int square, int attackerColor, Board b)`: Boolean threat detection used for checking king safety and castling path legality.
- [ ] `GenerateAllPseudoLegalMoves(Board b)`: Aggregator that iterates through all bitboards to compile possible moves (ideally into a `Span<Move>` to reduce garbage collection).

### 2. Search Algorithm
- [ ] `Search(Board b, int depth)`: The root function that manages the recursion loop, iterative deepening, and time limits.
- [ ] `AlphaBeta(Board b, int depth, int alpha, int beta)`: The core Minimax algorithm, highly optimized to prune branches that don't need evaluating.
- [ ] `QuiescenceSearch(Board b, int alpha, int beta)`: An extension of AlphaBeta that only looks at captures to ensure the position is "quiet" before scoring, solving the Horizon Effect.

### 3. Evaluation
- [ ] `Evaluate(Board b)`: Calculates the score of a static board position. Typically starts with material counting and Piece-Square Tables (PSTs). 

### 4. Transposition Table
- [ ] `GenerateZobristHash(Board b)`: Uses XOR operations with a pre-initialized table of random 64-bit numbers to generate a unique ID for any board state.
- [ ] `StoreHash(ulong hash, int score, Move bestMove, int depth)`: Writes evaluation data to your cache.
- [ ] `ProbeHash(ulong hash)`: Reads from your cache so you don't calculate the exact same board state twice if reached via a different move order.
