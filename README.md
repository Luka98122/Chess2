# Chess-Engine

A custom chess engine with a Windows Forms GUI, built from scratch in C# using bitboards, magic move generation, and modern search techniques. This was the final project for my Sophomore year at [Računarska gimnazija](https://rg.edu.rs).

Co-developed with [Stribor Pavlović](https://github.com/spav-s).

## Engine Highlights

- **Bitboard board representation** -- the entire board fits in 12 `ulong`s (one per piece type/color), with pre-computed occupancy bitboards for fast attack lookups. All move generation is done via bitwise shifts and masks.

- **Magic bitboards** -- sliding piece attacks (rooks, bishops, queens) computed in O(1) using the perfect hashing trick. Each square has a relevant blocker mask; multiplying by a pre-found magic number and shifting right collapses the permutation into a compact index. Magic numbers were brute-force discovered using sparse random candidates.

- **Alpha-beta search with iterative deepening** -- the engine searches incrementally from depth 1 up to the target depth (6–10 depending on game phase), always having a best move ready in case time runs out. Each iteration re-orders root moves by the best-found line.

- **Transposition table** -- a 16M-entry hash table (`0x1000000`) indexed via Zobrist key, storing exact, alpha, and beta-bound scores. Entries are replaced on a depth-preferred basis, and the TT move is used to order moves at every node.

- **Zobrist hashing** -- position keys computed as XORs of pre-initialized random 64-bit values for each piece-on-square, side to move, castling rights, and en passant target. The key is incrementally updated during make/unmake via XOR deltas, avoiding full recalculation.

- **Quiescence search** -- after the main search reaches depth 0, a capture-only search runs to resolve hanging pieces and avoid the horizon effect, using the same alpha-beta window.

- **Null move pruning** -- at depths >= 3, when the side to move is not in check, a null move (passing the turn) is tried with a reduced search. If the score still raises beta, the node is pruned.

- **Late move reduction** -- quiet moves beyond the first 4 candidates at depths >= 3 are searched at a reduced depth first. Only if the reduced search beats alpha is a full-depth re-search triggered.

- **Move ordering** -- captures are scored by MVV-LVA (most valuable victim, least valuable attacker), promotions get a +9000 bonus, and the TT best move is always tried first. This maximizes alpha-beta cutoffs.

- **Hanging piece evaluation** -- the static eval detects undefended or poorly defended pieces by finding the cheapest attacker on each square. If a piece is not defended or defended by a piece worth more than the attacker, it loses half its value.

- **Dynamic search depth** -- the engine adapts search depth to game phase: early game = 8, middlegame = 10, endgame = 12 plies.

## Features

- Three game modes: Player vs Player, Player vs AI, and AI vs AI
- Board editor with FEN import/export -- place pieces freely and run the engine from any position
- Puzzle solver -- reads puzzles from a CSV file, solves them with the engine, and exports results
- Visual annotations -- right-click arrows and square highlighting on the board
- Live evaluation bar with numerical score
- Checkmate, stalemate, and threefold repetition detection

## Build & Run

```pwsh
dotnet restore
dotnet build -c Release
dotnet run --project Frontend/Frontend.csproj -c Release
```

Or open `main.sln` in Visual Studio 2022+ and press F5.

## Project Structure

```
├── main.sln
├── Puzzles.csv                         # Lichess training puzzles (1000 positions)
├── chessEngine/                        # Class library -- the chess engine
│   └── src/
│       ├── Main.cs                     # Board, Move, bitboard ops, incremental Zobrist, eval
│       ├── Bot.cs                      # Zobrist keys, TT, quiescence, NMP, LMR, alpha-beta search
│       ├── Moves.cs                    # Move generation (pawns, knights, magic sliders, king, castling)
│       ├── Helpers.cs                  # FEN parser, board init, display helpers
│       ├── Magic.cs                    # Brute-force magic number finder
│       ├── PST.cs                      # Piece-square tables
│       └── Tests.cs                    # Unit tests for move generation
└── Frontend/                           # Windows Forms GUI
    ├── Program.cs
    ├── ChessForm.cs                    # Game board, menus, puzzle solver UI, settings
    └── Assets/Pieces/                  # Piece images (PNG)
```

## Credits

- Puzzle dataset courtesy of [lichess.org](https://lichess.org)
- Invaluable chess programming resources from the [Chess Programming Wiki](https://www.chessprogramming.org/Main_Page)
