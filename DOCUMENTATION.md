# Chess2 (StockfishV0) — Complete Documentation

## Project Overview

A C# chess application with a custom bitboard-based chess engine and a Windows Forms GUI. Supports human-vs-human (PVP), human-vs-AI (PVAI), and AI-vs-AI (AIVAI) game modes, plus custom FEN position loading. The engine features magic bitboards, Zobrist hashing with a transposition table, negamax search with alpha-beta pruning, quiescence search, MVV-LVA move ordering, iterative deepening, and piece-square table evaluation.

- **Solution file:** `StockfishV0.sln`
- **Engine project:** `chessEngine/` (class library, net8.0)
- **GUI project:** `StockfishV0/` (Windows Forms, net8.0-windows)

---

## File: `StockfishV0/Program.cs`

### Class: `Program` (static, internal)

#### `static void Main()`
- **What it does:** Application entry point. Initializes Windows Forms configuration, sets compatible text rendering to false, and runs the main `ChessForm`.
- **Input:** None (command-line args ignored)
- **Output:** None (runs the Windows Forms message loop)
- **Used by:** The .NET runtime as the entry point

---

## File: `chessEngine/src/Main.cs`

### Struct: `Move`

Represents a single chess move.

| Field | Type | Description |
|-------|------|-------------|
| `FromSquare` | `int` | Source square (0-63) |
| `ToSquare` | `int` | Destination square (0-63) |
| `PieceType` | `int` | Piece type index (0=WP, 1=WN, 2=WB, 3=WR, 4=WQ, 5=WK, 6=BP, 7=BN, 8=BB, 9=BR, 10=BQ, 11=BK) |
| `IsCapture` | `bool` | Whether the move captures an enemy piece |
| `IsPromotion` | `bool` | Whether the move is a pawn promotion |
| `PromotedPieceType` | `int` | The piece type the pawn promotes to (default -1) |
| `IsEnPassant` | `bool` | Whether the move is an en passant capture |
| `IsCastle` | `bool` | Whether the move is a castling move |

#### `Move(int from, int to, int piece, bool isCapture = false)`
- **What it does:** Constructor. Initializes a move with source, destination, piece type, and capture flag. Sets `IsPromotion`, `IsCastle`, `IsEnPassant` to false and `PromotedPieceType` to -1.
- **Input:**
  - `from` (int) — source square 0-63
  - `to` (int) — destination square 0-63
  - `piece` (int) — piece type 0-11
  - `isCapture` (bool, optional) — whether it's a capture
- **Output:** A new `Move` instance
- **Used by:** All move generators (`Moves.cs`), `MakeMove`/`UnmakeMove`, `ScoreMove`, `Think`, `Search`, test setup

---

### Struct: `BoardStateInfo`

Stores the board state before a move is made, enabling `UnmakeMove`.

| Field | Type | Description |
|-------|------|-------------|
| `MoveMade` | `Move` | The move that was just applied |
| `CapturedPieceType` | `int` | The piece type index that was captured (or -1) |
| `CastlingRights` | `byte` | Castling rights before the move |
| `EnPassantSquare` | `int` | En passant target square before the move |
| `HalfMoveClock` | `int` | Half-move clock before the move |
| `GameType` | `int` | Game phase before the move (0=early, 1=mid, 2=end) |
| `ZobristKey` | `ulong` | Zobrist hash before the move |

---

### Class: `Board`

Core board representation using 12 `ulong` bitboards (6 white + 6 black pieces).

#### Fields

| Field | Type | Description |
|-------|------|-------------|
| `Pieces` | `ulong[12]` | 12 bitboards: indices 0-5=White(P,N,B,R,Q,K), 6-11=Black(P,N,B,R,Q,K) |
| `SideToMove` | `int` | 0 for White, 1 for Black |
| `CastlingRights` | `byte` | Bitmask: bit0=WhiteKingside, bit1=WhiteQueenside, bit2=BlackKingside, bit3=BlackQueenside |
| `EnPassantSquare` | `int` | En passant target square, or -1 |
| `HalfMoveClock` | `int` | Half-moves since last capture/pawn push (for 50-move rule) |
| `GameType` | `int` | Game phase: 0=early, 1=mid, 2=end |
| `ZobristKey` | `ulong` | Current Zobrist hash of the board |
| `CastlingRightsMask` | `byte[64]` (static) | Per-square mask to AND with castling rights when a square is touched |
| `vals` | `int[12]` (static) | Piece values: `{100,300,300,500,900,6767, -100,-300,-300,-500,-900,-6767}` |

#### `ulong GenerateKey()`
- **What it does:** Generates a full Zobrist hash by XORing: all piece placements (piece type + square), side to move, castling rights, and en passant square.
- **Input:** None (uses instance fields)
- **Output:** `ulong` — the 64-bit Zobrist hash
- **Used by:** `MakeMove` (line 212) to update `ZobristKey` after each move

#### `void MakeMove(Move move)`
- **What it does:** Applies a move to the board. Saves the current state to `_stateHistory` before modifying. Handles: removing captured pieces, updating castling rights via `CastlingRightsMask`, clearing/moving piece bitboards, castling rook relocation, promotion (replaces pawn with promoted piece), updating half-move clock, updating `GameType` based on piece count, flipping `SideToMove`, and regenerating the Zobrist key.
- **Input:** `move` (Move) — the move to apply
- **Output:** None (mutates `this`)
- **Used by:** `allMoves.GenerateAllLegalMoves` (for legality filtering), `Bot.Search`, `Bot.Think`, `Bot.QuiescenceSearch`, `MakeHumanMove`, `MakeBotMoveIfNeeded`, `OpeningMoveTest`

#### `void UnmakeMove()`
- **What it does:** Reverses the last move by restoring the previous `BoardStateInfo`. Restores: side to move, castling rights, en passant, half-move clock, game type, piece bitboards (including restored captured piece), castled rook positions. Does NOT restore the Zobrist key from history — reads it from `_stateHistory[_historyPly]` (the entry that was just popped).
- **Input:** None
- **Output:** None (mutates `this`)
- **Used by:** `allMoves.GenerateAllLegalMoves`, `Bot.Search`, `Bot.QuiescenceSearch`, `Bot.Think`

#### `void ClearPieceAtTarget(int square, int opponentColor)`
- **What it does:** Clears all opponent piece bits at a given square using a mask (AND with complement). Iterates through 6 opponent piece bitboards.
- **Input:**
  - `square` (int) — target square 0-63
  - `opponentColor` (int) — 0 for White, 1 for Black
- **Output:** None (mutates `Pieces[]`)
- **Used by:** Not called anywhere in the codebase (dead code — capture removal is done inline in `MakeMove`)

#### `bool IsSquareAttacked(int square, int attackerColor)`
- **What it does:** Determines if a given square is attacked by any piece of the specified color. Checks in order: knights (precomputed table), kings (precomputed table), pawns (bit-shift captures), bishops/queens (magic bitboards), rooks/queens (magic bitboards). Returns true on first found attacker, false otherwise.
- **Input:**
  - `square` (int) — square to check (0-63)
  - `attackerColor` (int) — 0 for White attackers, 1 for Black attackers
- **Output:** `bool` — true if the square is under attack
- **Used by:** `GetBoardState` (for check detection), `GetCheapestAttackerValue`, `GetBoardEval` (for hanging piece defense check and check bonus), `KingMoveGenerator.GetKingMoves` (castling legality), `Bot.Search` (terminal node check)

#### `int GetBoardState()`
- **What it does:** Generates all legal moves for the side to move. If legal moves exist and half-move clock < 100, returns -1 (normal). If no legal moves exist, checks if the king is in check: if yes, returns the attacker's color (that player wins); if no, returns 2 (stalemate). Also returns 2 if half-move clock reaches 100 (50-move rule draw).
- **Input:** None (uses instance fields)
- **Output:** `int` — -1 = normal, 0 = White wins, 1 = Black wins, 2 = draw/stalemate
- **Used by:** `CheckGameOverState` (in ChessForm.cs)

#### `int GetCheapestAttackerValue(int sq, int attackerColor, ulong occupied)`
- **What it does:** Finds the cheapest (lowest-value) piece of the given color that attacks the given square. Checks in order of piece value: pawns (100), knights (300), bishops (300), rooks (500), queens (900), kings (6767). Returns 99999 if no attacker is found. Uses magic bitboards for bishops/rooks/queens and precomputed tables for knights/kings.
- **Input:**
  - `sq` (int) — the target square 0-63
  - `attackerColor` (int) — 0=White, 1=Black
  - `occupied` (ulong) — OR of all occupied squares
- **Output:** `int` — the value of the cheapest attacker (100, 300, 500, 900, 6767) or 99999 if unattacked
- **Used by:** `GetBoardEval` (for hanging piece evaluation)

#### `int GetBoardEval(int depth = 1)`
- **What it does:** Evaluates the current position from White's perspective in centipawns. Positive = White advantage. Computes: (1) material value + piece-square table positional bonus for all pieces, (2) -50 bonus if White is in check, +50 if Black is in check, (3) if `depth > 0`, penalizes hanging/undefended pieces (penalty = pieceValue/2 if undefended or attacked by a cheaper piece). Hanging piece evaluation iterates White pieces (0-4) and Black pieces (6-10).
- **Input:**
  - `depth` (int, default 1) — if > 0, enables hanging piece evaluation
- **Output:** `int` — centipawn evaluation score (positive = White advantage)
- **Used by:** `Bot.QuiescenceSearch` (stand-pat score), `MakeHumanMove`, `MakeBotMoveIfNeeded`, `LoadFenPosition`

#### `Board Clone()`
- **What it does:** Creates a deep copy of the board for AI sandboxing. Copies the entire `Pieces` array, `SideToMove`, and `GameType`. **Note:** Does NOT copy castling rights, en passant, half-move clock, Zobrist key, or history — this is a notable omission that may cause search bugs.
- **Input:** None
- **Output:** `Board` — a new board with copied piece bitboards, side to move, and game type
- **Used by:** `MakeBotMoveIfNeeded` (to create an AI sandbox)

---

## File: `chessEngine/src/Moves.cs`

### Class: `KnightMoveGenerator` (static)

#### `Dictionary<int, ulong> KnightPreCalcs`
- **What it is:** Precomputed attack bitboards for knights on each of the 64 squares. Maps square index (0-63) to a bitboard of all squares a knight attacks from that square.

#### `void PreCalculateKnightMoves()`
- **What it does:** Precomputes all 64 knight attack bitboards. For each square, generates the 8 possible L-shaped moves (dy±2,dx±1 and dy±1,dx±2), filtering out off-board moves. Stores result in `KnightPreCalcs`.
- **Input:** None
- **Output:** None (populates `KnightPreCalcs`)
- **Used by:** `EngineHelpers.init()`

#### `int GetKnightMoves(Board b, ulong knights, int color, Span<Move> moves)`
- **What it does:** Generates all pseudo-legal knight moves for a given color. Iterates over the `knights` bitboard, looks up precomputed attacks for each knight's square, masks out friendly piece squares, and creates a `Move` for each valid destination (with capture flag set if destination contains an enemy piece).
- **Input:**
  - `b` (Board) — current board state
  - `knights` (ulong) — bitboard of knight positions for the color
  - `color` (int) — 0=White, 1=Black
  - `moves` (Span<Move>) — output span to write moves into
- **Output:** `int` — number of moves generated
- **Used by:** `allMoves.GenerateAllPseudoLegalMoves`

---

### Class: `PawnMoveGenerator` (static)

#### Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `NotFileA` | `0xFEFEFEFEFEFEFEFEUL` | Mask to exclude A-file (prevents wrap-around on left captures) |
| `NotFileH` | `0x7F7F7F7F7F7F7F7FUL` | Mask to exclude H-file (prevents wrap-around on right captures) |

#### `int AddPawnMove(Span<Move> moves, int moveCount, int from, int to, int piece, bool isCapture)`
- **What it does:** Adds a pawn move to the span. If the destination is on the promotion rank (row 0 or row 7), generates 4 promotion variants (Queen, Rook, Bishop, Knight). Otherwise adds a single normal move.
- **Input:**
  - `moves` (Span<Move>) — output span
  - `moveCount` (int) — current count of moves already in the span
  - `from` (int) — source square
  - `to` (int) — destination square
  - `piece` (int) — piece type (0=WP, 6=BP)
  - `isCapture` (bool) — whether it's a capture
- **Output:** `int` — updated move count
- **Used by:** `GetPawnMoves` (exclusively)

#### `int GetPawnMoves(Board b, ulong pawns, int color, Span<Move> moves)`
- **What it does:** Generates all pseudo-legal pawn moves for a given color using bitboard operations. For White: single pushes (shift left 8, masked to empty squares), double pushes (single-push pawns on rank 2 shifted left 8 again), left captures (shift left 7, masked to black pieces, not A-file), right captures (shift left 9, masked to black pieces, not H-file). For Black: analogous with right shifts. Calls `AddPawnMove` for each destination (handles promotions).
- **Input:**
  - `b` (Board) — current board
  - `pawns` (ulong) — bitboard of pawn positions for the color
  - `color` (int) — 0=White, 1=Black
  - `moves` (Span<Move>) — output span
- **Output:** `int` — number of moves generated
- **Used by:** `allMoves.GenerateAllPseudoLegalMoves`

---

### Class: `RookMoveGenerator` (static)

#### Static Data

| Field | Type | Description |
|-------|------|-------------|
| `RookMasks` | `ulong[64]` | Relevant blocker masks for each square |
| `RookAttacks` | `ulong[64][]` | Precomputed attack arrays for each square (jagged: one array per square) |
| `RookRelevantBits` | `int[64]` | Number of relevant bits per square (popcount of mask) |
| `RookMagics` | `ulong[64]` | Hardcoded 64 magic numbers (found by `MagicFinder`) |

#### `int GetRookMoves(Board b, ulong rooks, int color, Span<Move> moves)`
- **What it does:** Generates all pseudo-legal rook moves using O(1) magic bitboard lookup. For each rook, computes `blockers = occupied & RookMasks[square]`, then `magicIndex = (blockers * RookMagics[square]) >> (64 - relevantBits)`, then looks up precomputed attacks. Masks out friendly pieces.
- **Input:**
  - `b` (Board) — current board
  - `rooks` (ulong) — bitboard of rook positions
  - `color` (int) — 0=White, 1=Black
  - `moves` (Span<Move>) — output span
- **Output:** `int` — number of moves generated
- **Used by:** `allMoves.GenerateAllPseudoLegalMoves`, `QueenMoveGenerator.GetQueenMoves`

#### `void PreCalculateRookAttacks()`
- **What it does:** Pre-populates all rook attack tables. For each square: creates the blocker mask, allocates a lookup array of size 2^(relevantBits), and uses Carry-Rippler enumeration to iterate all blocker subsets, computing each magic index and storing the naive attack bitboard.
- **Input:** None
- **Output:** None (populates `RookMasks`, `RookRelevantBits`, `RookAttacks`)
- **Used by:** `EngineHelpers.init()`

#### `ulong CreateRookMask(int square)`
- **What it does:** Creates the relevant blocker mask for rook moves from a given square. Includes all squares in the four straight directions (N/S/E/W) up to but not including the board edges.
- **Input:** `square` (int) — square 0-63
- **Output:** `ulong` — bitboard of relevant blocker squares
- **Used by:** `PreCalculateRookAttacks`, `MagicFinder.FindRookMagic`, `MagicFinder.GenerateAllRookMagics`

#### `ulong CalculateNaiveRookAttacks(int square, ulong blockers)`
- **What it does:** Computes rook attack squares naively via ray-casting in all 4 directions. In each direction, walks outward from the square, adding each square to the attack bitboard, and stops when it hits a blocker (which is still included in attacks).
- **Input:**
  - `square` (int) — source square 0-63
  - `blockers` (ulong) — bitboard of all blockers
- **Output:** `ulong` — bitboard of all attacked squares
- **Used by:** `PreCalculateRookAttacks`, `MagicFinder.FindRookMagic`

---

### Class: `BishopMoveGenerator` (static)

#### Static Data

| Field | Type | Description |
|-------|------|-------------|
| `BishopMasks` | `ulong[64]` | Relevant blocker masks for each square |
| `BishopAttacks` | `ulong[64][]` | Precomputed attack arrays (jagged) |
| `BishopRelevantBits` | `int[64]` | Relevant bits per square |
| `BishopMagics` | `ulong[64]` | Hardcoded 64 bishop magic numbers |

#### `int GetBishopMoves(Board b, ulong bishops, int color, Span<Move> moves)`
- **What it does:** Generates all pseudo-legal bishop moves using O(1) magic bitboard lookup. Identical pattern to `GetRookMoves` but uses bishop masks, magics, and attack tables.
- **Input:**
  - `b` (Board) — current board
  - `bishops` (ulong) — bitboard of bishop positions
  - `color` (int) — 0=White, 1=Black
  - `moves` (Span<Move>) — output span
- **Output:** `int` — number of moves generated
- **Used by:** `allMoves.GenerateAllPseudoLegalMoves`, `QueenMoveGenerator.GetQueenMoves`

#### `void PreCalculateBishopAttacks()`
- **What it does:** Pre-populates all bishop attack tables using Carry-Rippler enumeration, identical approach to rook precalculation.
- **Input:** None
- **Output:** None (populates `BishopMasks`, `BishopRelevantBits`, `BishopAttacks`)
- **Used by:** `EngineHelpers.init()`

#### `ulong CreateBishopMask(int square)`
- **What it does:** Creates the relevant blocker mask for bishop moves from a given square. Includes squares in the 4 diagonal directions (NE/NW/SE/SW) up to but not including board edges.
- **Input:** `square` (int) — square 0-63
- **Output:** `ulong` — bitboard of relevant blocker squares
- **Used by:** `PreCalculateBishopAttacks`, `MagicFinder.FindBishopMagic`, `MagicFinder.GenerateAllBishopMagics`

#### `ulong CalculateNaiveBishopAttacks(int square, ulong blockers)`
- **What it does:** Computes bishop attack squares naively via ray-casting in all 4 diagonal directions, stopping at blockers (inclusive).
- **Input:**
  - `square` (int) — source square 0-63
  - `blockers` (ulong) — blocker bitboard
- **Output:** `ulong` — bitboard of attacked squares
- **Used by:** `PreCalculateBishopAttacks`, `MagicFinder.FindBishopMagic`

---

### Class: `QueenMoveGenerator` (static)

#### `int GetQueenMoves(Board b, ulong queens, int color, Span<Move> moves)`
- **What it does:** Generates all pseudo-legal queen moves by OR-ing rook and bishop attacks (since a queen is a rook + bishop). Uses both magic bitboard lookups for each queen square, then combines the results and masks out friendly squares.
- **Input:**
  - `b` (Board) — current board
  - `queens` (ulong) — bitboard of queen positions
  - `color` (int) — 0=White, 1=Black
  - `moves` (Span<Move>) — output span
- **Output:** `int` — number of moves generated
- **Used by:** `allMoves.GenerateAllPseudoLegalMoves`

---

### Class: `KingMoveGenerator` (static)

#### `Dictionary<int, ulong> KingPreCalcs`
- **What it is:** Precomputed attack bitboards for kings on each of the 64 squares. Maps square index to a bitboard of all 8 adjacent squares (filtered to on-board).

#### `void PreCalculateKingMoves()`
- **What it does:** Precomputes all 64 king attack bitboards. For each square, generates 8 adjacent squares (8 compass directions) filtering out off-board moves.
- **Input:** None
- **Output:** None (populates `KingPreCalcs`)
- **Used by:** `EngineHelpers.init()`

#### `int GetKingMoves(Board b, ulong kings, int color, Span<Move> moves)`
- **What it does:** Generates all pseudo-legal king moves including castling. For normal moves: looks up precomputed attacks, masks out friendly squares. For castling: checks each castling right bit (1=WhiteKingside, 2=WhiteQueenside, 4=BlackKingside, 8=BlackQueenside), verifies all squares between king and rook are empty, and verifies that the king does not pass through or land on an attacked square.
- **Input:**
  - `b` (Board) — current board
  - `kings` (ulong) — bitboard of king position (should contain exactly 1 bit)
  - `color` (int) — 0=White, 1=Black
  - `moves` (Span<Move>) — output span
- **Output:** `int` — number of moves generated (normal king moves + castling)
- **Used by:** `allMoves.GenerateAllPseudoLegalMoves`

---

### Class: `allMoves` (static)

#### `int GenerateAllPseudoLegalMoves(Board b, Span<Move> moves, int col)`
- **What it does:** Aggregates all pseudo-legal moves for a given color. Calls each piece generator in order: pawns, knights, rooks, bishops, queens, kings. Uses `Span.Slice` to partition the output span for each generator. Returns total count.
- **Input:**
  - `b` (Board) — current board
  - `moves` (Span<Move>) — output span (must be at least 218 elements for safety; 256 used in practice)
  - `col` (int) — color to generate moves for (0=White, 1=Black)
- **Output:** `int` — total number of pseudo-legal moves
- **Used by:** `GenerateAllLegalMoves` (internally)

#### `int GenerateAllLegalMoves(Board b, Span<Move> moves, int col)`
- **What it does:** Generates all strictly legal moves by first generating pseudo-legal moves, then filtering each move by making it, checking if the king is left in check, and unmaking it. Only passes through moves that are valid. Performs in-place filtering (compacts the `moves` span).
- **Input:**
  - `b` (Board) — current board
  - `moves` (Span<Move>) — output span
  - `col` (int) — color 0=White, 1=Black
- **Output:** `int` — number of legal moves
- **Used by:** `Board.GetBoardState`, `Bot.QuiescenceSearch`, `Bot.Think`, `Bot.Search`, `HasLegalMoves`, `ShowLegalMoveHintsForSquare`, `EngineHelpers.showMoves`, `EngineHelpers.showMoves2`, `Tests.RunTest`, `Tests.allTests`

---

## File: `chessEngine/src/Bot.cs`

### Class: `Zobrist` (static)

Randomly generated (seed 1337) Zobrist keys for incremental hashing.

#### Fields

| Field | Type | Description |
|-------|------|-------------|
| `Pieces` | `ulong[12, 64]` | Zobrist keys for each piece type at each square |
| `Castling` | `ulong[16]` | Zobrist keys for each of 16 castling right combinations |
| `EnPassant` | `ulong[64]` | Zobrist keys for each possible en passant file |
| `SideToMove` | `ulong` | Zobrist key XOR'd when it's Black's turn |

#### Static constructor (`static Zobrist()`)
- **What it does:** Initializes all Zobrist keys using a deterministic `Random(1337)` for reproducibility. Generates keys for all 12×64 piece-square combinations, 16 castling combinations, 64 en passant files, and the side-to-move key using a local `NextRandom()` helper.
- **Input:** None
- **Output:** None (populates static fields)
- **Used by:** Called automatically by the runtime when `Zobrist` is first accessed

---

### Struct: `TTEntry`

Represents a single transposition table entry.

| Field | Type | Description |
|-------|------|-------------|
| `Key` | `ulong` | Full Zobrist hash key (for collision detection) |
| `Depth` | `int` | Search depth at which this entry was stored |
| `Score` | `int` | The stored evaluation score |
| `Flag` | `int` | 0=Exact, 1=Alpha/UpperBound, 2=Beta/LowerBound |

---

### Class: `TT` (static)

Transposition table with ~4.1 million entries.

#### Fields

| Field | Type | Description |
|-------|------|-------------|
| `Size` | `const int` | `0x400000` (~4,194,304 entries) |
| `Entries` | `TTEntry[]` | The transposition table array |
| `CacheHits` | `int` (static) | Running counter of cache hits |

#### `void Store(ulong key, int depth, int score, int flag)`
- **What it does:** Stores an entry in the transposition table using `key % Size` as the index. Always overwrites (no replacement strategy).
- **Input:**
  - `key` (ulong) — Zobrist hash key
  - `depth` (int) — search depth
  - `score` (int) — evaluation score
  - `flag` (int) — bound type (0=Exact, 1=Upper, 2=Lower)
- **Output:** None
- **Used by:** `Bot.Search`

#### `bool TryProbe(ulong key, int depth, int alpha, int beta, out int score)`
- **What it does:** Probes the transposition table for a cached entry. If the key matches and cached depth >= requested depth, checks the bound flag: Exact (0) returns the score directly; Upper bound (1) returns alpha if entry score <= alpha; Lower bound (2) returns beta if entry score >= beta. Increments `CacheHits` and prints a debug message every 10,000 hits.
- **Input:**
  - `key` (ulong) — Zobrist hash
  - `depth` (int) — requested search depth
  - `alpha` (int) — current alpha bound
  - `beta` (int) — current beta bound
  - `score` (out int) — output score if hit
- **Output:** `bool` — true if a valid TT hit was found
- **Used by:** `Bot.Search`

---

### Class: `Bot` (static)

The AI engine. Contains the search algorithm.

#### Fields

| Field | Type | Description |
|-------|------|-------------|
| `Infinity` | `const int` | 2,000,000 (used as initial alpha/beta bounds) |
| `cache` | `Dictionary<...>` | Unused dictionary (dead code) |

#### `int ScoreMove(Move m)`
- **What it does:** MVV-LVA (Most Valuable Victim - Least Valuable Attacker) move ordering. Assigns a score: +9000 for promotions (highest priority), +1000 minus absolute attacker value for captures (so a pawn capturing a queen gets 1000-100=900, while a queen capturing a pawn gets 1000-900=100). Non-capture non-promotion moves score 0.
- **Input:** `m` (Move) — the move to score
- **Output:** `int` — heuristic score (higher = better to search first)
- **Used by:** `QuiescenceSearch`, `Think`, `Search`

#### `int QuiescenceSearch(Board b, int alpha, int beta)`
- **What it does:** A quiescence search that only considers capture moves at leaf nodes to avoid the horizon effect. First computes the stand-pat score (positional evaluation from the perspective of the side to move), returning beta on cutoff. Then generates all legal moves, orders them by `ScoreMove` using selection sort, and recursively searches only captures. Returns alpha (improved if better capture sequences are found).
- **Input:**
  - `b` (Board) — current board state
  - `alpha` (int) — alpha bound
  - `beta` (int) — beta bound
- **Output:** `int` — quiescence-evaluated score
- **Used by:** `Search` (at depth 0), recursively by itself

#### `Move Think(Board b, int targetDepth, int topX)`
- **What it does:** The main AI entry point. Performs iterative deepening from depth 1 to `targetDepth`. For each depth, evaluates all legal moves with negamax alpha-beta search, tracking the best move. Initial move ordering is done once before the deepening loop via `ScoreMove` selection sort. Returns the best move found at the deepest completed iteration. If no legal moves exist, returns `default(Move)`.
- **Input:**
  - `b` (Board) — current board state
  - `targetDepth` (int) — maximum search depth
  - `topX` (int) — unused parameter (likely intended for multi-PV but not implemented)
- **Output:** `Move` — the best move found
- **Used by:** `MakeBotMoveIfNeeded` (via `Task.Run`)

#### `int Search(Board b, int depth, int alpha, int beta)`
- **What it does:** Negamax alpha-beta search with transposition table support. At each node: (1) probes the TT for a cutoff, (2) at depth 0, delegates to `QuiescenceSearch`, (3) generates all legal moves with MVV-LVA ordering via selection sort, (4) iterates moves recursively with negamax (-Search with swapped alpha/beta), (5) on beta cutoff, stores a LowerBound entry and returns beta, (6) after searching all moves, stores the result (Exact or UpperBound) in the TT and returns bestScore. Terminal nodes with no legal moves return -30000-depth (checkmate, preferring faster mates) or 0 (stalemate).
- **Input:**
  - `b` (Board) — current board state
  - `depth` (int) — remaining search depth
  - `alpha` (int) — alpha bound
  - `beta` (int) — beta bound
- **Output:** `int` — evaluated score from the perspective of the side to move
- **Used by:** `Think`, recursively by itself

---

## File: `chessEngine/src/Helpers.cs`

### Class: `EngineHelpers` (static)

#### `void init()`
- **What it does:** Initializes all precomputed data structures. Calls `InitializeNotationMaps`, then precalculates king moves, knight moves, bishop magic attacks, and rook magic attacks. (Commented-out code shows it was once used with `MagicFinder` to generate magic numbers.)
- **Input:** None
- **Output:** None (side effects)
- **Used by:** `InitializeEngineBoard` (in ChessForm.cs)

#### `string SquareToString(int square)` (private)
- **What it does:** Converts a square index (0-63) to algebraic notation (e.g., 0 → "a1", 63 → "h8").
- **Input:** `square` (int) — square 0-63
- **Output:** `string` — algebraic notation
- **Used by:** `showMoves`

#### `void showMoves(Board b, Span<Move> possibleMoves)`
- **What it does:** Debug/display function. Generates all legal moves, builds a bitboard of all destination squares, and renders it using `renderBitboard`. (The parameter `possibleMoves` is used as the output span but is also passed in, which is redundant — the function allocates its own via `GenerateAllLegalMoves`.)
- **Input:**
  - `b` (Board) — board state
  - `possibleMoves` (Span<Move>) — pre-allocated span (unused except passed through)
- **Output:** None (prints to console)
- **Used by:** Not called in the codebase (debug utility)

#### `void showMoves2(Board b, Span<Move> moves)`
- **What it does:** Pretty-prints all moves in the span to the console with flags: side to move, from→to notation, capture flag, en passant flag, castle flag, promotion flag with piece character.
- **Input:**
  - `b` (Board) — board state
  - `moves` (Span<Move>) — pre-generated moves
- **Output:** None (prints to console)
- **Used by:** `Tests.RunTest`

#### `Dictionary<int, string> IndexToNotation`
- **What it is:** Maps square index (0-63) to algebraic notation string (e.g., 0→"a1").

#### `Dictionary<string, int> NotationToIndex`
- **What it is:** Reverse map: algebraic notation string to square index.

#### `void InitializeNotationMaps()`
- **What it does:** Populates both `IndexToNotation` and `NotationToIndex` dictionaries for all 64 squares by iterating ranks and files.
- **Input:** None
- **Output:** None (populates dictionaries)
- **Used by:** `EngineHelpers.init()`

#### `void InitializeStartingPosition(Board b)`
- **What it does:** Sets up the standard chess starting position on a board by assigning hardcoded hex bitboards for all 12 piece types and setting `SideToMove = 0` (White).
- **Input:** `b` (Board) — board to initialize
- **Output:** None (mutates `b`)
- **Used by:** `InitializeEngineBoard` (ChessForm.cs)

#### `bool TryLoadFen(Board b, string fen, out string error)`
- **What it does:** Full FEN string parser. Parses 4-6 FEN fields: (1) piece placement — 8 ranks separated by `/`, digits represent empty squares, letters represent pieces; converts FEN rank order to engine rank order (FEN rank 8 = engine rank 0). (2) Side to move — w or b. (3) Castling rights — KQkq or -. (4) En passant square (validated but not actively stored by the current implementation — note: en passant is only partially handled). (5) Half-move clock. (6) Full-move number. Validates: exactly 8 ranks, correct square counts, exactly 1 white and 1 black king. After parsing, calls `RemoveImpossibleCastlingRights`. Returns false with an error message on any validation failure.
- **Input:**
  - `b` (Board) — board to populate
  - `fen` (string) — FEN string
  - `error` (out string) — error message if parsing fails
- **Output:** `bool` — true if successful
- **Used by:** `LoadFenPosition` (ChessForm.cs)

#### `int FenCharToPieceType(char c)` (private)
- **What it does:** Maps a FEN character to a piece type index: uppercase = White (P=0, N=1, B=2, R=3, Q=4, K=5), lowercase = Black (p=6, n=7, b=8, r=9, q=10, k=11). Returns -1 for unknown characters.
- **Input:** `c` (char) — FEN piece character
- **Output:** `int` — piece type 0-11, or -1
- **Used by:** `TryLoadFen`

#### `bool TryParseCastlingRights(Board b, string castlingText, out string error)` (private)
- **What it does:** Parses the castling rights FEN field. Sets `b.CastlingRights` as a bitmask: K→bit0, Q→bit1, k→bit2, q→bit3. Validates no duplicate characters and only valid characters (KQkq). Returns false on error.
- **Input:**
  - `b` (Board) — board to set castling rights on
  - `castlingText` (string) — FEN castling field
  - `error` (out string) — error output
- **Output:** `bool` — true if valid
- **Used by:** `TryLoadFen`

#### `void RemoveImpossibleCastlingRights(Board b)` (private)
- **What it does:** After loading a FEN, removes castling rights if the king or rook is not in the correct starting position. Checks: White king must be on e1 (square 4) and rook on h1 (7) for kingside, or rook on a1 (0) for queenside. Analogous for black (e8=60, h8=63, a8=56). Masks out the corresponding bit if conditions aren't met.
- **Input:** `b` (Board) — board with potentially invalid castling rights
- **Output:** None (mutates `b.CastlingRights`)
- **Used by:** `TryLoadFen`

#### `int CountBits(ulong value)` (private)
- **What it does:** Counts the number of set bits in a `ulong` using Brian Kernighan's algorithm (`n &= n-1` loop).
- **Input:** `value` (ulong) — the number to count bits in
- **Output:** `int` — population count
- **Used by:** `TryLoadFen` (to verify exactly 1 king per side)

#### `void renderBitboard(ulong board, string title = "Bitboard")`
- **What it does:** Pretty-prints a 64-bit bitboard as an 8x8 ASCII grid to the console, with rank numbers on the left, file letters at the bottom, and a framed header with the given title. Prints a `1` for set bits and `.` for empty squares. Also prints the decimal value of the bitboard.
- **Input:**
  - `board` (ulong) — the bitboard to render
  - `title` (string, default "Bitboard") — header title
- **Output:** None (prints to console)
- **Used by:** `showMoves`

---

## File: `chessEngine/src/Magic.cs`

### Class: `MagicFinder` (static)

Brute-force magic number finder for rooks and bishops.

#### Fields

| Field | Type | Description |
|-------|------|-------------|
| `rnd` | `Random` (private, static) | Random number generator |

#### `ulong GetRandomUlong()` (private)
- **What it does:** Generates a random 64-bit unsigned integer by filling an 8-byte buffer with random bytes and converting to `ulong`.
- **Input:** None
- **Output:** `ulong` — random 64-bit value
- **Used by:** `GetSparseRandomUlong`

#### `ulong GetSparseRandomUlong()` (private)
- **What it does:** Generates a sparse random `ulong` by ANDing three random `ulong`s together, reducing the number of 1-bits (which improves magic number quality).
- **Input:** None
- **Output:** `ulong` — sparse random value
- **Used by:** `FindRookMagic`, `FindBishopMagic`

#### `ulong FindRookMagic(int square, int relevantBits)`
- **What it does:** Brute-force searches for a rook magic number for a given square. Uses Carry-Rippler to enumerate all 2^relevantBits blocker permutations, computes their true attack boards, then tests up to 100 million random magic candidates. A magic is valid if it creates a collision-free perfect hash mapping blockers to attacks. Skips candidates with too few upper bits (popcount < 6).
- **Input:**
  - `square` (int) — square 0-63
  - `relevantBits` (int) — number of relevant blocker bits
- **Output:** `ulong` — the found magic number, or 0 if failed
- **Used by:** `GenerateAllRookMagics`

#### `void GenerateAllRookMagics()`
- **What it does:** Generates and prints all 64 rook magic numbers to the console in C# array literal format, one per square. Useful for regenerating the hardcoded magic table.
- **Input:** None
- **Output:** None (prints to console)
- **Used by:** Not called in the codebase (commented out in `init()`)

#### `ulong FindBishopMagic(int square, int relevantBits)`
- **What it does:** Same as `FindRookMagic` but for bishops. Uses bishop mask and naive bishop attacks.
- **Input:**
  - `square` (int) — square 0-63
  - `relevantBits` (int) — number of relevant blocker bits
- **Output:** `ulong` — found magic number or 0
- **Used by:** `GenerateAllBishopMagics`

#### `void GenerateAllBishopMagics()`
- **What it does:** Generates and prints all 64 bishop magic numbers in C# array literal format.
- **Input:** None
- **Output:** None (prints to console)
- **Used by:** Not called in the codebase (commented out)

---

## File: `chessEngine/src/PST.cs`

### Class: `PST` (static)

Piece-Square Tables for positional evaluation.

#### Static Data — Piece-Square Tables (all from White's perspective)

| Table | Type | Description |
|-------|------|-------------|
| `Pawns` | `int[64]` | Encourages pushing and center control; +5 to +30 for advancement, massive +50/+90 bonus near promotion |
| `Knights` | `int[64]` | Penalizes edges/corners (-50), rewards center squares (+20 max) |
| `Bishops` | `int[64]` | Penalizes edges (-20), bonuses for long diagonals (+10 max) |
| `Rooks` | `int[64]` | Small bonus for 7th rank (+5), slight center file bonus |
| `Queens` | `int[64]` | Penalty for early advancement (-20 back rank), small center bonuses (+5) |
| `KingMidgame` | `int[64]` | Heavy penalty for center/advanced squares (-50), strong bonus for castled corners (+20/+30) |

#### `int GetScore(int pieceType, int square)`
- **What it does:** Returns the positional evaluation bonus for a given piece type at a given square. Normalizes piece type (subtracts 6 for black pieces), mirrors the board for Black by XOR-ing the square with 56 (which flips rank, preserving file), then indexes into the appropriate piece-square table.
- **Input:**
  - `pieceType` (int) — piece type 0-11
  - `square` (int) — square 0-63
- **Output:** `int` — positional bonus in centipawns
- **Used by:** `Board.GetBoardEval`

---

## File: `chessEngine/src/Tests.cs`

### Class: `Tests` (static)

#### Delegate: `MoveGenerator`
- **What it is:** Delegate type `int MoveGenerator(Board b, ulong pieces, int color, Span<Move> moves)`. Defined but never used in the codebase.

#### `void OpeningMoveTest()`
- **What it does:** Sets up a board via `MakeMove` for 1.e4 e6 and renders the resulting position side-by-side using `RenderSideBySide`.
- **Input:** None
- **Output:** None (prints to console)
- **Used by:** `allTests`

#### `void allTests()`
- **What it does:** Runs 21 test scenarios covering: rook moves (open center, blocked/capturable), bishop moves, queen moves (open and crowded), knight moves (center and edge), pawn pushes and captures (both colors), knight pinned to king, king legal moves near enemies, pawn promotion (push and capture), castling (both sides open, castling through check), absolute pin (pinned pawn cannot move, pinned piece can capture pinner), and the opening move test. Each test sets up a specific board, generates legal moves, and renders the board + attack map side by side.
- **Input:** None
- **Output:** None (prints to console)
- **Used by:** Not called in the codebase (manual test runner)

#### `Board SetupBoard(params (int pieceType, int square)[] placements)` (private)
- **What it does:** Creates a board with only the specified pieces. Clears all default pieces and castling rights, places each specified piece, dynamically restores castling rights if king+rook are on home squares, and places dummy kings (a1 for white, h8 for black) if no king was specified (to prevent crashes from `TrailingZeroCount` on empty king bitboards).
- **Input:** `placements` — tuple array of (pieceType, square)
- **Output:** `Board` — configured board
- **Used by:** `RunTest` (indirectly via inline calls) and all individual tests in `allTests`

#### `void RunTest(string title, Board b, int color, int targetPieceType, int targetSquare)` (private)
- **What it does:** Runs a single test. Sets side to move, generates all legal moves, filters to moves from the target square (or all if targetSquare == -1), builds an attack bitboard, renders the board and attack map side by side via `RenderSideBySide`, and prints formatted moves via `showMoves2`.
- **Input:**
  - `title` (string) — test name
  - `b` (Board) — test board
  - `color` (int) — side to move (0=White, 1=Black)
  - `targetPieceType` (int) — piece type to test (or -1)
  - `targetSquare` (int) — square to filter moves from (or -1 for all)
- **Output:** None (prints to console)
- **Used by:** `allTests`

#### `void RenderSideBySide(string title, Board b, ulong attacks, int moveCount)` (private)
- **What it does:** Prints a side-by-side ASCII representation: left side shows the board state with piece characters (P,N,B,R,Q,K and lowercase for black), right side shows the attack bitboard with `x` for attacked squares and `.` for empty. Includes rank/file labels and a header with move count.
- **Input:**
  - `title` (string) — section header
  - `b` (Board) — board state
  - `attacks` (ulong) — attack target bitboard
  - `moveCount` (int) — number of moves (displayed in header)
- **Output:** None (prints to console)
- **Used by:** `RunTest`, `OpeningMoveTest`

---

## File: `StockfishV0/ChessForm.cs`

### Class: `ChessForm` (extends `Form`)

The main application window. Contains menu navigation and game mode selection.

#### Fields

| Field | Type | Description |
|-------|------|-------------|
| `mainMenuPanel` | `Panel` | Main menu screen |
| `playPanel` | `Panel` | Game mode selection screen |
| `colorSelectPanel` | `Panel` | Color selection screen (PVAI) |
| `gamePanel` | `Panel` | Game board screen |
| `settingsPanel` | `Panel` | FEN settings screen |
| `chessBoard` | `ChessBoardControl` | The chess board control |
| `gameModeLabel` | `Label` | Displays current game mode |
| `fenTextBox` | `TextBox` | FEN input field |
| `fenStatusLabel` | `Label` | FEN load status |
| `currentGameMode` | `GameMode` | Current game mode (enum: Pvp, Pvai, Aivai) |

#### `ChessForm()` (constructor)
- **What it does:** Initializes the form with title "StockfishV0", centered on screen, dark background, and 1000×900 client size. Builds all 5 screen panels, adds them to controls, and shows the main menu.
- **Input:** None
- **Output:** A new `ChessForm` instance
- **Used by:** `Program.Main`

#### `void BuildMainMenuScreen()`
- **What it does:** Creates the main menu panel with a title label ("StockfishV0", 42pt) and two buttons: PLAY and SETTINGS. Buttons are wired to `PlayButton_Click` and `SettingsButton_Click`.
- **Input:** None
- **Output:** None (populates `mainMenuPanel`)
- **Used by:** Constructor

#### `void BuildPlayScreen()`
- **What it does:** Creates the game mode selection panel with title ("Choose Game Mode", 36pt) and buttons: PVP, PVAI, AIVAI, BACK. Buttons wired to `PvpButton_Click`, `PvaiButton_Click`, `AivaiButton_Click`, `BackButtonToMain_Click`.
- **Input:** None
- **Output:** None (populates `playPanel`)
- **Used by:** Constructor

#### `void BuildColorSelectScreen()`
- **What it does:** Creates the color selection panel with title ("Choose Your Color", 36pt) and buttons: WHITE, BLACK, BACK. Buttons wired to `PlayWhiteButton_Click`, `PlayBlackButton_Click`, `BackButtonToPlay_Click`.
- **Input:** None
- **Output:** None (populates `colorSelectPanel`)
- **Used by:** Constructor

#### `void BuildGameScreen()`
- **What it does:** Creates the game panel containing the `ChessBoardControl` (docked fill), a "Menu" button (top-left), and a game mode label. The button is brought to front so it overlays the chess board.
- **Input:** None
- **Output:** None (populates `gamePanel`)
- **Used by:** Constructor

#### `void BuildSettingsScreen()`
- **What it does:** Creates the FEN settings panel with title ("Settings", 36pt), a label, a text box pre-filled with the starting position FEN, a "LOAD FEN" button (wired to `LoadFenButton_Click`), a status label, and a BACK button.
- **Input:** None
- **Output:** None (populates `settingsPanel`)
- **Used by:** Constructor

#### `TableLayoutPanel CreateMenuLayout()`
- **What it does:** Creates a standardized `TableLayoutPanel` with 1 column, 7 rows, and fixed percentage row heights (22%, 13%, 11%, 11%, 11%, 11%, 21%). All menu screens use this layout.
- **Input:** None
- **Output:** `TableLayoutPanel` — configured layout
- **Used by:** `BuildMainMenuScreen`, `BuildPlayScreen`, `BuildColorSelectScreen`, `BuildSettingsScreen`

#### `Label CreateTitleLabel(string text, int fontSize)`
- **What it does:** Creates a centered white Arial label with bold font, docked fill.
- **Input:**
  - `text` (string) — label text
  - `fontSize` (int) — font size in points
- **Output:** `Label` — configured label
- **Used by:** All `Build*Screen` methods

#### `Button CreateMenuButton(string text)`
- **What it does:** Creates a styled green button (240×60, Arial 18pt bold, white text, green background RGB 75,105,55, flat style, hand cursor).
- **Input:** `text` (string) — button text
- **Output:** `Button` — configured button
- **Used by:** All `Build*Screen` methods

#### `void PlayButton_Click(object sender, EventArgs e)`
- **What it does:** Navigates to the Play screen (game mode selection).
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** PLAY button on main menu

#### `void SettingsButton_Click(object sender, EventArgs e)`
- **What it does:** Navigates to the Settings screen (FEN loading).
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** SETTINGS button on main menu

#### `void LoadFenButton_Click(object sender, EventArgs e)`
- **What it does:** Attempts to load the FEN string from the text box via `chessBoard.LoadFenPosition`. On success: updates game mode label to "CUSTOM FEN", shows green status, and navigates to game screen. On failure: shows red error message.
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** LOAD FEN button on settings screen

#### `void PvpButton_Click(object sender, EventArgs e)`
- **What it does:** Starts a PVP game (human vs human) with no AI and flip-board-every-move enabled.
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** PVP button on play screen

#### `void PvaiButton_Click(object sender, EventArgs e)`
- **What it does:** Navigates to the color select screen (for PVAI mode).
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** PVAI button on play screen

#### `void AivaiButton_Click(object sender, EventArgs e)`
- **What it does:** Starts an AIVAI game (AI vs AI spectating) with AI enabled for both sides.
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** AIVAI button on play screen

#### `void PlayWhiteButton_Click(object sender, EventArgs e)`
- **What it does:** Starts a PVAI game with the human playing White.
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** WHITE button on color select screen

#### `void PlayBlackButton_Click(object sender, EventArgs e)`
- **What it does:** Starts a PVAI game with the human playing Black.
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** BLACK button on color select screen

#### `void BackButtonToMain_Click(object sender, EventArgs e)`
- **What it does:** Navigates back to the main menu.
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** BACK buttons on play and settings screens

#### `void BackButtonToPlay_Click(object sender, EventArgs e)`
- **What it does:** Navigates back to the play screen (from color select).
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** BACK button on color select screen

#### `void MenuButton_Click(object sender, EventArgs e)`
- **What it does:** Stops the AI loop and returns to the main menu.
- **Input:** Standard event handler args
- **Output:** None
- **Used by:** Menu button on game screen

#### `void StartGame(GameMode gameMode, bool useAi, int playerColor)`
- **What it does:** Configures and starts a new game. Sets the current game mode, updates the label, determines whether to flip the board every move (only for PVP), calls `chessBoard.StartNewGame` with the appropriate parameters, and navigates to the game screen.
- **Input:**
  - `gameMode` (GameMode) — mode enum value
  - `useAi` (bool) — whether AI is enabled
  - `playerColor` (int) — human color (0=White, 1=Black)
- **Output:** None
- **Used by:** `PvpButton_Click`, `PlayWhiteButton_Click`, `PlayBlackButton_Click`, `AivaiButton_Click`

#### `string GetGameModeText(GameMode gameMode, int playerColor)`
- **What it does:** Returns a human-readable string for the current game mode: "PVP", "PVAI WHITE", "PVAI BLACK", or "AIVAI".
- **Input:**
  - `gameMode` (GameMode) — mode enum
  - `playerColor` (int) — 0=White, 1=Black
- **Output:** `string` — mode description
- **Used by:** `StartGame`

#### Screen Navigation Methods (each shows one panel, hides others, and brings it to front)
- `ShowMainMenuScreen()` — Shows main menu panel
- `ShowPlayScreen()` — Shows play panel
- `ShowColorSelectScreen()` — Shows color select panel
- `ShowGameScreen()` — Shows game panel, also calls `BeginInvoke` to focus the chess board
- `ShowSettingsScreen()` — Shows settings panel
- `InitializeComponent()` — Empty WinForms designer method

---

### Class: `ChessBoardControl` (extends `Control`)

The core chess board UI control. Handles rendering, mouse input, drag-drop, arrow drawing, and AI integration.

#### Fields (selected key fields)

| Field | Type | Description |
|-------|------|-------------|
| `engineBoard` | `Board` | The current chess position |
| `aiEnabled` | `bool` | Whether AI is active |
| `humanColor` | `int` | Human's color (0=White, 1=Black) |
| `aiColor` | `int` | AI's color |
| `boardPerspective` | `int` | 0=White at bottom, 1=Black at bottom |
| `flipBoardEveryMove` | `bool` | Whether to flip board after each move |
| `boardInputLocked` | `bool` | Whether input is disabled (during AI thinking / board flip) |
| `aiVsAiEnabled` | `bool` | AI vs AI spectating mode |
| `showEngineBar` | `bool` | Whether eval bar is visible (toggled with E key) |
| `engineEvalCentipawns` | `int` | Current evaluation in centipawns |
| `gameIsOver` | `bool` | Game over flag |
| `gameOverState` | `int` | Game outcome (-1=ongoing, 0=WhiteWon, 1=BlackWon, 2=Draw) |
| `selectedPieceLegalMoves` | `List<Move>` | Legal moves for the selected piece |
| `arrows` | `List<BoardArrow>` | Drawn arrows (right-click) |
| `coloredSquares` | `bool[64]` | Highlighted squares (right-click same square) |
| `moveDots` | `bool[8,8]` | Dot hints for legal move destinations |
| `captureCircles` | `bool[8,8]` | Circle hints for capture move destinations |
| `lastMoveFromSquare` / `lastMoveToSquare` | `int` | Last move squares for highlight |
| `promotionDropdown` | `ComboBox` | Promotion piece selector |
| `pendingPromotionMove` | `Move` | Stored move awaiting promotion selection |

#### `ChessBoardControl()` (constructor)
- **What it does:** Enables double-buffering and resize-redraw. Initializes the engine board, loads piece images from disk, and wires up mouse (down/move/up) and keyboard events.
- **Input:** None
- **Output:** A new `ChessBoardControl` instance
- **Used by:** `ChessForm.BuildGameScreen`

#### `void StartNewGame(bool useAi, int playerColor, bool shouldFlipBoardEveryMove, bool shouldAiVsAi)`
- **What it does:** Configures all game mode state, resets the board to starting position, updates board perspective, and queues the first bot move if needed.
- **Input:**
  - `useAi` (bool) — enable AI
  - `playerColor` (int) — human color (0=White, 1=Black)
  - `shouldFlipBoardEveryMove` (bool) — flip after each move (PVP)
  - `shouldAiVsAi` (bool) — AI vs AI mode
- **Output:** None
- **Used by:** `ChessForm.StartGame`

#### `void StopAiLoop()`
- **What it does:** Disables AI, clears queued moves, unlocks input, hides promotion dropdown.
- **Input:** None
- **Output:** None
- **Used by:** `ChessForm.MenuButton_Click`, `LoadFenPosition`

#### `bool LoadFenPosition(string fen, out string error)`
- **What it does:** Stops AI, creates a fresh board, calls `EngineHelpers.TryLoadFen` to parse the FEN, sets the loaded board as the active board, configures settings (no AI, flip enabled, perspective = side to move), recalculates eval, and redraws. Returns success/failure.
- **Input:**
  - `fen` (string) — FEN string
  - `error` (out string) — error message if failed
- **Output:** `bool` — true if loaded successfully
- **Used by:** `ChessForm.LoadFenButton_Click`

#### `void ResetBoard()`
- **What it does:** Resets to the standard starting position. Clears game-over state, move highlights, selection, arrows, colored squares, and promotion state. Calls `Invalidate()` to redraw.
- **Input:** None
- **Output:** None
- **Used by:** `StartNewGame`

#### `void UpdateBoardPerspectiveForTurn()`
- **What it does:** If `flipBoardEveryMove` is enabled, sets `boardPerspective` to the current side to move and redraws.
- **Input:** None
- **Output:** None
- **Used by:** `StartNewGame`, `MakeBotMoveIfNeeded`, `QueueBoardPerspectiveFlip` timer callback

#### `void QueueBoardPerspectiveFlip()`
- **What it does:** If flipping is enabled, locks input and creates a timer that flips the board after a short delay (`boardFlipDelayMs` = 20ms), then unlocks input.
- **Input:** None
- **Output:** None
- **Used by:** `MakeHumanMove`

#### `void InitializeEngineBoard()`
- **What it does:** One-time initialization (first call only) of `EngineHelpers.init()` (precalculates moves, magic tables, etc.), then creates a fresh board at the starting position.
- **Input:** None
- **Output:** None
- **Used by:** Constructor, `ResetBoard`

#### `void ChessBoardControl_KeyDown(object sender, KeyEventArgs e)`
- **What it does:** Keyboard handler. 'E' toggles the eval bar visibility. 'C' clears all arrows and colored squares.
- **Input:** Standard key event args
- **Output:** None
- **Used by:** Wired to `KeyDown` event

#### `void ClearColoredSquares()`
- **What it does:** Sets all 64 entries in `coloredSquares` to false.
- **Input:** None
- **Output:** None
- **Used by:** `ChessBoardControl_KeyDown`, `LoadFenPosition`, `ResetBoard`

#### `void ChessBoardControl_MouseDown(object sender, MouseEventArgs e)`
- **What it does:** Handles left and right click on the board. 
  - Left click: if game is over or input locked or not human's turn, clears selection. If a piece is already selected and the click is on a legal target, makes the move. Otherwise, if clicking on own piece, selects it and shows legal move hints; begins drag.
  - Right click: begins arrow drawing mode (sets `isDrawingArrow`, captures mouse, changes cursor to Cross).
- **Input:** Standard mouse event args
- **Output:** None
- **Used by:** Wired to `MouseDown`

#### `void ChessBoardControl_MouseMove(object sender, MouseEventArgs e)`
- **What it does:** During drag: updates `dragPoint`. During arrow drawing: updates the current arrow endpoint.
- **Input:** Standard mouse event args
- **Output:** None
- **Used by:** Wired to `MouseMove`

#### `void ChessBoardControl_MouseUp(object sender, MouseEventArgs e)`
- **What it does:**
  - Right button: finalizes the arrow (unless start==end, which toggles colored square highlight). Clears arrow drawing state.
  - Left button: ends drag. If dropped on a different square from the start, attempts to make the selected move. Clears drag state.
- **Input:** Standard mouse event args
- **Output:** None
- **Used by:** Wired to `MouseUp`

#### `void ShowLegalMoveHintsForSquare(int engineSquare)`
- **What it does:** Generates all legal moves for the current side, filters to moves from the given square, populates `selectedPieceLegalMoves` list, and sets up `moveDots` / `captureCircles` for the destinations.
- **Input:** `engineSquare` (int) — selected piece square 0-63
- **Output:** None
- **Used by:** `SelectPieceAtSquare`

#### `void SelectPieceAtSquare(int row, int col, int engineSquare, int pieceType)`
- **What it does:** Records selection state (row, col, engineSquare, dragged piece code, hasSelectedPiece=true) and shows legal move hints.
- **Input:**
  - `row` (int) — visual row
  - `col` (int) — visual column
  - `engineSquare` (int) — engine square 0-63
  - `pieceType` (int) — piece type 0-11
- **Output:** None
- **Used by:** `ChessBoardControl_MouseDown`

#### `void ClearSelectedPiece()`
- **What it does:** Clears selection state, drag state, move hints, and the legal moves list.
- **Input:** None
- **Output:** None
- **Used by:** Many places (mouse handlers, game start/reset, bot move, etc.)

#### `void SetLastMoveHighlight(Move move)`
- **What it does:** Records the from/to squares of the last move for highlighting during rendering.
- **Input:** `move` (Move) — the move just made
- **Output:** None
- **Used by:** `MakeHumanMove`, `MakeBotMoveIfNeeded`

#### `void CheckGameOverState()`
- **What it does:** Calls `engineBoard.GetBoardState()`. If -1 (normal), clears game-over flags. Otherwise sets `gameIsOver = true`, hides promotion, clears selection, unlocks input, clears AI queue, and sets the game-over title/subtitle ("WHITE WON"/"BLACK WON" for checkmate, "DRAW" for stalemate).
- **Input:** None
- **Output:** None
- **Used by:** `MakeHumanMove`, `MakeBotMoveIfNeeded`, `LoadFenPosition`

#### `bool TryMakeSelectedMoveToSquare(int targetEngineSquare)`
- **What it does:** Checks if the click target is a legal destination for the selected piece. If the move is a promotion, shows the promotion dropdown instead of making the move. Otherwise calls `MakeHumanMove`. Returns true if a move was made.
- **Input:** `targetEngineSquare` (int) — target square 0-63
- **Output:** `bool` — true if move was made
- **Used by:** `ChessBoardControl_MouseDown`, `ChessBoardControl_MouseUp`

#### `void MakeHumanMove(Move move)`
- **What it does:** Executes a move on the board (via `MakeMove`), records last move highlight, updates evaluation, checks game-over state, and if game continues, queues a board perspective flip and bot move.
- **Input:** `move` (Move) — the move to make
- **Output:** None
- **Used by:** `TryMakeSelectedMoveToSquare`, `PromotionDropdown_SelectedIndexChanged`

#### `void ShowPromotionDropdown(Move move)` / `void HidePromotionDropdown()`
- **What it does:** `ShowPromotionDropdown` creates a `ComboBox` with Queen/Rook/Bishop/Knight options, positions it near the promotion square, and locks input. `HidePromotionDropdown` disposes the dropdown and unlocks input.
- **Input:** `move` (Move) for show; none for hide
- **Output:** None
- **Used by:** `TryMakeSelectedMoveToSquare` / `PromotionDropdown_SelectedIndexChanged`, `CheckGameOverState`, `StopAiLoop`, `ResetBoard`, `LoadFenPosition`

#### `void PositionPromotionDropdown()`
- **What it does:** Calculates the dropdown position based on the promotion destination square's visual coordinates and board layout metrics. Clamps to screen bounds.
- **Input:** None
- **Output:** None
- **Used by:** `ShowPromotionDropdown`, `OnPaint` (reposition on resize)

#### `void PromotionDropdown_SelectedIndexChanged(object sender, EventArgs e)`
- **What it does:** When the user selects a promotion piece (index > 0), sets `PromotedPieceType` on the pending move (4=Queen, 3=Rook, 2=Bishop, 1=Knight), hides the dropdown, clears selection, and executes the move via `MakeHumanMove`.
- **Input:** Standard event args
- **Output:** None
- **Used by:** Wired to dropdown's `SelectedIndexChanged`

#### `bool IsHumanTurn()`
- **What it does:** Returns true if it's the human's turn. False if AI-vs-AI mode, or if it's the AI's turn in PVAI mode, or if AI is disabled entirely.
- **Input:** None
- **Output:** `bool` — true if human can move
- **Used by:** `ChessBoardControl_MouseDown`

#### `void QueueBotMoveIfNeeded()`
- **What it does:** If AI is enabled, game is not over, no bot move is already queued, and it's the AI's turn (or AIVAI mode), sets `aiMoveQueued = true` and either calls `MakeBotMoveIfNeeded` immediately (no delay) or schedules it with a 500ms timer (for AIVAI spectating delay).
- **Input:** None
- **Output:** None
- **Used by:** `StartNewGame`, `MakeHumanMove`, `MakeBotMoveIfNeeded` (for AIVAI chaining)

#### `bool HasLegalMoves()`
- **What it does:** Generates all legal moves for the current side and returns whether count > 0. Used as a pre-check before running the AI to avoid errors.
- **Input:** None
- **Output:** `bool` — true if the side to move has legal moves
- **Used by:** `MakeBotMoveIfNeeded`

#### `async void MakeBotMoveIfNeeded()`
- **What it does:** The AI move execution. Checks preconditions (AI enabled, game not over, AI's turn). Verifies legal moves exist. Sets search depth: 5 for early/mid game (`GameType <= 1`), 8 for endgame (`GameType == 2`). Clones the board for thread safety, runs `Bot.Think` on a background thread via `Task.Run`, then applies the returned move on the main board, updates highlight/eval/game-over, updates perspective, clears selection, redraws, unlocks input, clears the queued flag, and chains another bot move if in AIVAI mode.
- **Input:** None
- **Output:** None (async void)
- **Used by:** `QueueBotMoveIfNeeded` (directly or via timer)

#### `void AddMoveHintForLegalMove(Move move)` / `void ClearMoveHints()`
- **What it does:** `AddMoveHintForLegalMove` converts the destination square to visual coordinates and sets `captureCircles[row,col]` or `moveDots[row,col]` depending on whether the move is a capture. `ClearMoveHints` resets both arrays.
- **Input:** `move` (Move) for add; none for clear
- **Output:** None
- **Used by:** `ShowLegalMoveHintsForSquare`, `SelectPieceAtSquare` / `ClearSelectedPiece`

#### `int VisualToEngineSquare(int row, int col)`
- **What it does:** Converts visual board coordinates (row 0-7, col 0-7) to engine square (0-63), accounting for `boardPerspective`. When White is at bottom, (row 0, col 0) = a8 (square 56). When Black is at bottom, (row 0, col 0) = h1 (square 7).
- **Input:**
  - `row` (int) — visual row 0-7
  - `col` (int) — visual column 0-7
- **Output:** `int` — engine square 0-63
- **Used by:** Mouse handlers, arrow drawing

#### `void EngineSquareToVisual(int square, out int row, out int col)`
- **What it does:** Reverse of `VisualToEngineSquare`. Converts engine square to visual row/col based on perspective.
- **Input:** `square` (int) — engine square 0-63
- **Output:** `row` (out int), `col` (out int) — visual coordinates
- **Used by:** `AddMoveHintForLegalMove`, `DrawSquareHighlight`, `DrawSelection`, `DrawSingleArrow`, `DrawColoredSquares`

#### `int GetPieceTypeAtSquare(int square)`
- **What it does:** Returns the piece type index (0-11) at the given square, or -1 if empty.
- **Input:** `square` (int) — engine square 0-63
- **Output:** `int` — piece type or -1
- **Used by:** `ChessBoardControl_MouseDown`

#### `int GetColorFromPieceType(int pieceType)`
- **What it does:** Returns 0 for white pieces (0-5) and 1 for black pieces (6-11).
- **Input:** `pieceType` (int) — piece type 0-11
- **Output:** `int` — color 0=White, 1=Black
- **Used by:** `ChessBoardControl_MouseDown`

#### `string GetPieceCodeFromPieceType(int pieceType)`
- **What it does:** Maps piece type to image file code: 0→"wP", 1→"wN", 2→"wB", 3→"wR", 4→"wQ", 5→"wK", and b* for 6-11.
- **Input:** `pieceType` (int) — piece type 0-11
- **Output:** `string` — piece code (e.g., "wP", "bK")
- **Used by:** `SelectPieceAtSquare`, `DrawPieces`

#### `bool IsInsideBoard(int row, int col)`
- **What it does:** Checks if visual row/col are within 0-7 bounds.
- **Input:**
  - `row` (int)
  - `col` (int)
- **Output:** `bool`
- **Used by:** `AddMoveHintForLegalMove`, `DrawSquareHighlight`

#### `void LoadPieceImages()` / `string FindPiecesFolder()`
- **What it does:** `FindPiecesFolder` searches upward from the application's base directory for `Assets/Pieces`. `LoadPieceImages` loads all 12 PNG piece images (wP, wR, wN, wB, wQ, wK, bP, bR, bN, bB, bQ, bK) into the `pieceImages` dictionary.
- **Input:** None
- **Output:** None (populates `pieceImages`) / `string` (folder path or null)
- **Used by:** Constructor

#### `bool GetLayoutMetrics(out int engineX, out int engineY, out int engineHeight, out int boardX, out int boardY, out int squareSize)`
- **What it does:** Calculates the layout geometry for the board and eval bar based on client size, padding, and eval bar visibility. Returns board coordinates, square size, and eval bar rectangle. Board is square (min of available width/height), rounded down to multiple of 8.
- **Input:** None
- **Output:** 6 `out int` values describing the layout; `bool` — false if layout invalid
- **Used by:** `GetSquareFromPoint`, `OnPaint`, `PositionPromotionDropdown`

#### `bool GetSquareFromPoint(Point point, out int row, out int col)`
- **What it does:** Converts a mouse point to visual board row/col. Returns false if the point is outside the board area.
- **Input:**
  - `point` (Point) — mouse coordinates
  - `row`, `col` (out int) — output visual coordinates
- **Output:** `bool` — true if point is on a square
- **Used by:** Mouse handlers

#### `protected override void OnPaint(PaintEventArgs e)`
- **What it does:** Main render pipeline. In order: clears background, sets anti-aliasing modes, gets layout, repositions promotion dropdown if open, then draws: eval bar → board squares → last move highlight → colored squares → selection highlight → coordinates → move hints → arrows → pieces → dragged piece → game-over overlay.
- **Input:** Standard paint event args
- **Output:** None (renders to Graphics)
- **Used by:** WinForms framework (on Invalidate/Resize/etc.)

#### `void DrawEngineBar(Graphics g, int engineX, int engineY, int engineHeight)`
- **What it does:** Draws the evaluation bar. Maps centipawn evaluation (-1000 to +1000) to a white/black proportion (100 centipawns ≈ 5% advantage). Clamps extreme values. Draws a white rectangle (top = White advantage), black rectangle (bottom = Black advantage), border, and numeric eval text.
- **Input:** Graphics, position/size of bar area
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `void DrawEngineEvalText(Graphics g, int engineX, int engineY, int engineHeight, int evalCentipawns)`
- **What it does:** Draws the evaluation number (e.g., "+0.35" or "-1.20") below the eval bar.
- **Input:** Graphics, position, and eval value
- **Output:** None (draws)
- **Used by:** `DrawEngineBar`

#### `void DrawGameOverScreen(Graphics g, int boardX, int boardY, int squareSize)`
- **What it does:** If game is over, draws a semi-transparent black overlay on the board and a rounded white box centered with the game-over title and subtitle.
- **Input:** Graphics, board position/size
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `GraphicsPath RoundedRect(Rectangle bounds, int radius)`
- **What it does:** Creates a `GraphicsPath` for a rounded rectangle with the given corner radius.
- **Input:**
  - `bounds` (Rectangle) — bounding rectangle
  - `radius` (int) — corner radius
- **Output:** `GraphicsPath` — the rounded rectangle path
- **Used by:** `DrawGameOverScreen`

#### `void DrawColoredSquares(Graphics g, int boardX, int boardY, int squareSize)`
- **What it does:** Renders right-click highlighted squares in a reddish color (light=#EB7D6A, dark=#D36C50).
- **Input:** Graphics, board position/size
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `void DrawMoveHints(Graphics g, int boardX, int boardY, int squareSize)`
- **What it does:** Draws legal move hints: small semi-transparent dots for non-capture destinations, and circle outlines for capture destinations.
- **Input:** Graphics, board position/size
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `void DrawArrows(Graphics g, int boardX, int boardY, int squareSize)`
- **What it does:** Iterates all stored arrows and draws each via `DrawSingleArrow`. Also draws the preview arrow while the user is dragging (right-click).
- **Input:** Graphics, board position/size
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `void DrawSingleArrow(Graphics g, int boardX, int boardY, int squareSize, BoardArrow arrow, bool preview)`
- **What it does:** Determines if the arrow is a knight-move arrow (L-shape: rowDiff=2,colDiff=1 or vice versa) or a straight arrow, and delegates to `DrawKnightArrow` or `DrawStraightArrow`.
- **Input:**
  - Graphics, board position/size
  - `arrow` (BoardArrow) — the arrow to draw
  - `preview` (bool) — whether this is a live preview (lower alpha)
- **Output:** None (draws)
- **Used by:** `DrawArrows`

#### `void DrawStraightArrow(Graphics g, int boardX, int boardY, int squareSize, BoardVisualArrow arrow, bool preview)`
- **What it does:** Creates a 2-point path (center of start square to center of end square) and calls `DrawChessComArrow` to render it.
- **Input:** Graphics, position/size, arrow, preview flag
- **Output:** None (draws)
- **Used by:** `DrawSingleArrow`, `DrawKnightArrow` (fallback)

#### `void DrawKnightArrow(Graphics g, int boardX, int boardY, int squareSize, BoardVisualArrow arrow, bool preview)`
- **What it does:** Draws an L-shaped arrow for knight moves. Creates intermediate corner points: if vertical distance is 2 and horizontal is 1, the path goes 2 squares vertically then 1 horizontally. If horizontal distance is 2 and vertical is 1, the path reverses. Delegates rendering to `DrawChessComArrow`.
- **Input:** Graphics, position/size, arrow, preview flag
- **Output:** None (draws)
- **Used by:** `DrawSingleArrow`

#### `void DrawChessComArrow(Graphics g, int squareSize, List<PointF> points, bool preview)`
- **What it does:** Draws a Chess.com-style arrow with a thick body and triangular arrowhead. Calculates the direction vector of the last segment, positions the arrowhead base, draws the body using a `GraphicsPath`, and fills the arrowhead as a filled polygon. Arrow color is orange (#F5B226) with alpha 185 (normal) or 135 (preview). Line width and head size scale with square size.
- **Input:**
  - `g` (Graphics)
  - `squareSize` (int) — for scaling
  - `points` (List<PointF>) — path points (minimum 2)
  - `preview` (bool) — alpha for preview
- **Output:** None (draws)
- **Used by:** `DrawStraightArrow`, `DrawKnightArrow`

#### `PointF GetSquareCenter(int boardX, int boardY, int squareSize, int row, int col)`
- **What it does:** Returns the pixel center point of a visual board square.
- **Input:** Board layout and visual row/col
- **Output:** `PointF` — center coordinates
- **Used by:** `DrawStraightArrow`, `DrawKnightArrow`

#### `void DrawBoard(Graphics g, int boardX, int boardY, int squareSize)`
- **What it does:** Draws the 8×8 board with alternating light (#EEEED2) and dark (#769656) squares.
- **Input:** Graphics, board position/size
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `void DrawLastMoveHighlight(Graphics g, int boardX, int boardY, int squareSize)`
- **What it does:** Highlights the from and to squares of the last move using `DrawSquareHighlight`.
- **Input:** Graphics, board position/size
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `void DrawSquareHighlight(Graphics g, int boardX, int boardY, int squareSize, int engineSquare)`
- **What it does:** Draws a yellow-tinted highlight on a single square (used for last move and selection).
- **Input:** Graphics, position/size, engine square
- **Output:** None (draws)
- **Used by:** `DrawLastMoveHighlight`

#### `Color GetHighlightColorForVisualSquare(int row, int col)`
- **What it does:** Returns `#F5F682` for light squares and `#B9CA43` for dark squares (yellow highlight).
- **Input:** Visual row/col
- **Output:** `Color`
- **Used by:** `DrawSquareHighlight`, `DrawSelection`

#### `void DrawSelection(Graphics g, int boardX, int boardY, int squareSize)`
- **What it does:** Highlights the currently selected piece's square.
- **Input:** Graphics, board position/size
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `void DrawCoordinates(Graphics g, int boardX, int boardY, int squareSize)`
- **What it does:** Draws rank numbers (1-8) on the left edge and file letters (a-h) on the bottom edge. Color inverts from the square color. Ranks and files adapt to board perspective.
- **Input:** Graphics, board position/size
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `void DrawPieces(Graphics g, int boardX, int boardY, int squareSize)`
- **What it does:** Iterates all 12 piece bitboards and draws each piece image at its visual square position. Skips the dragged piece (it's drawn separately by `DrawDraggedPiece`).
- **Input:** Graphics, board position/size
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `void DrawPiece(Graphics g, string code, int boardX, int boardY, int row, int col, int squareSize)`
- **What it does:** Draws a single piece image centered in its square. If the image is missing, falls back to `DrawMissingPieceDebugText`.
- **Input:**
  - `code` (string) — piece code (e.g., "wP")
  - Board layout and visual row/col
- **Output:** None (draws)
- **Used by:** `DrawPieces`

#### `void DrawDraggedPiece(Graphics g, int squareSize)`
- **What it does:** Draws the piece image at the current drag point (mouse position), following the cursor.
- **Input:** Graphics, square size (for piece scaling)
- **Output:** None (draws)
- **Used by:** `OnPaint`

#### `void DrawMissingPieceDebugText(Graphics g, string code, int boardX, int boardY, int row, int col, int squareSize)`
- **What it does:** Fallback: draws the piece code as red text if the image file is missing.
- **Input:** Graphics, piece code, board layout
- **Output:** None (draws)
- **Used by:** `DrawPiece`

---

## File: `chessEngine/Class1.cs`

### Class: `Class1`

- **What it does:** Empty placeholder class with no methods or fields. Leftover from project template.
- **Used by:** Nothing

---

## Data Flow Summary

```
Program.Main()
  └─ new ChessForm()
       └─ BuildGameScreen() → new ChessBoardControl()
            └─ constructor → InitializeEngineBoard()
                 └─ EngineHelpers.init() [one-time]
                      ├─ InitializeNotationMaps()
                      ├─ PreCalculateKingMoves()
                      ├─ PreCalculateKnightMoves()
                      ├─ PreCalculateBishopAttacks() → uses BishopMagics, CreateBishopMask, CalculateNaiveBishopAttacks
                      └─ PreCalculateRookAttacks() → uses RookMagics, CreateRookMask, CalculateNaiveRookAttacks

User clicks PLAY → StartGame() → chessBoard.StartNewGame()
  └─ ResetBoard() → InitializeStartingPosition()
  └─ QueueBotMoveIfNeeded() → [if AI turn] MakeBotMoveIfNeeded()

MakeBotMoveIfNeeded():
  └─ Board.Clone()
  └─ Task.Run(() => Bot.Think(clone, depth, topX))
       └─ allMoves.GenerateAllLegalMoves()
       └─ iterative deepening loop:
            └─ Bot.Search(depth, alpha, beta)
                 ├─ TT.TryProbe()
                 ├─ allMoves.GenerateAllLegalMoves()
                 ├─ ScoreMove() ordering
                 ├─ recursive Search(depth-1, -beta, -alpha)
                 │    └─ [depth==0] → QuiescenceSearch()
                 │         ├─ Board.GetBoardEval()
                 │         ├─ allMoves.GenerateAllLegalMoves()
                 │         └─ recursive captures only
                 └─ TT.Store()
  └─ engineBoard.MakeMove(botMove) [on main thread]
       └─ Board.GenerateKey() → Zobrist XOR operations

Human makes move:
  ChessBoardControl_MouseDown → SelectPieceAtSquare → ShowLegalMoveHintsForSquare
  ChessBoardControl_MouseUp → TryMakeSelectedMoveToSquare → MakeHumanMove
       └─ engineBoard.MakeMove()
       └─ engineBoard.GetBoardEval() [uses PST.GetScore, GetCheapestAttackerValue]
       └─ CheckGameOverState() → Board.GetBoardState()
       └─ QueueBotMoveIfNeeded()
```

