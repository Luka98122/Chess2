# Chess2 (StockfishV0) — Issues Report

| Severity | Count |
|----------|-------|
| Critical | 3 |
| High | 3 |
| Medium | 5 |
| Low | 12 |
| **Total** | **23** |

---

## Critical

### 1. `Board.Clone()` is incomplete — AI sandbox broken
- **File:** `chessEngine/src/Main.cs:504-524`
- **Description:** `Clone()` only copies `Pieces`, `SideToMove`, and `GameType`. It does **not** copy `CastlingRights`, `EnPassantSquare`, `HalfMoveClock`, `ZobristKey`, or `_stateHistory`/`_historyPly`.
- **Impact:** `ChessForm.cs:1344` clones the board for the AI search sandbox. Without castling rights, the AI may attempt illegal castling. Without the Zobrist key, transposition table lookups fail. Without the half-move clock, the 50-move rule doesn't work in search.
- **Suggested fix:** Copy all state fields in `Clone()`.

### 2. En passant is never generated or handled
- **File:** `chessEngine/src/Main.cs:108-213` (`MakeMove`), `chessEngine/src/Moves.cs` (all generators)
- **Description:** The `Move` struct has `IsEnPassant` (line 24) and the board has `EnPassantSquare` (line 61), but:
  - No generator ever sets `IsEnPassant = true` on any move.
  - `MakeMove` never sets `EnPassantSquare` when a pawn double-pushes.
  - There is no code to remove the captured pawn on an en passant capture.
- **Impact:** A fundamental chess rule is completely absent. The engine plays illegal chess.
- **Suggested fix:** In `MakeMove`, when a pawn double-pushes, set `EnPassantSquare`. In `PawnMoveGenerator`, generate en passant captures when the target square matches `EnPassantSquare` and set `IsEnPassant = true`. In `MakeMove`, on en passant captures, clear the captured pawn's square.

### 3. Zobrist key restored from wrong history entry (`UnmakeMove`)
- **File:** `chessEngine/src/Main.cs:258`
- **Description:** After decrementing `_historyPly` on line 217, the code reads `_stateHistory[_historyPly].ZobristKey` to restore the Zobrist key. The correct source is `state.ZobristKey` (the `ref` variable already pointing to the popped entry). The current code reads one entry too far back (the next-older state).
- **Impact:** The Zobrist hash desynchronizes from the board after each `UnmakeMove`, breaking the transposition table entirely. The TT may return wrong results.
- **Suggested fix:** Use `this.ZobristKey = state.ZobristKey;` instead of re-indexing `_stateHistory`.

---

## High

### 4. `_stateHistory` array has no bounds checking
- **File:** `chessEngine/src/Main.cs:75,111,217`
- **Description:** `_stateHistory` is a fixed 2048-element array. `MakeMove` increments `_historyPly` and writes without bounds check. `UnmakeMove` decrements without underflow check. A deep search or long game exceeding 2048 moves will throw `IndexOutOfRangeException`.
- **Impact:** Crashes the engine during deep search or long games.
- **Suggested fix:** Add bounds check with a resize strategy, or use `Array.Resize`, or ensure the array size exceeds maximum possible depth (search depth × max branching factor).

### 5. `return default` sends invalid move to real board
- **File:** `chessEngine/src/Bot.cs:202`
- **Description:** When `Think()` finds no legal moves, it returns `default(Move)` — a struct with all fields zeroed: `FromSquare=0, ToSquare=0, PieceType=0` (White Pawn a1→a1). This invalid move is executed on the real board at `ChessForm.cs:1350`.
- **Impact:** Making an invalid move can corrupt the board state or cause undefined behavior. The caller should detect game over (checkmate/stalemate) instead.
- **Suggested fix:** Change return type to `Move?` or use a sentinel like `{ FromSquare = -1 }`, and have the caller check before calling `MakeMove`. Declare game over when no legal moves exist.

### 6. `async void` with no exception handling
- **File:** `StockfishV0/ChessForm.cs:1308`
- **Description:** `MakeBotMoveIfNeeded()` is declared `async void` instead of `async Task`. There is no `try-catch` anywhere in the method body. Any exception thrown after an `await` (e.g., from `Bot.Think()`) cannot be caught and will crash the entire application.
- **Impact:** An unhandled AI exception terminates the process.
- **Suggested fix:** Change to `async Task`, wrap the body in `try-catch`, and handle/log errors gracefully.

---

## Medium

### 7. Missing `KingEndgame` piece-square table
- **File:** `chessEngine/src/PST.cs`
- **Description:** `PST` has `KingMidgame` but no `KingEndgame` table. `GetScore` always uses `KingMidgame` regardless of `GameType`. The `Board.GameType` field (0=early, 1=mid, 2=end) is never passed to `GetScore`.
- **Impact:** In endgames, the king should centralize and be active, but the midgame table penalizes central squares and rewards corner hiding. The AI plays endgames poorly.
- **Suggested fix:** Add a `KingEndgame` table with values encouraging centralization. Pass `GameType` to `GetScore` and interpolate between midgame and endgame tables.

### 8. Promotion piece type offset is inconsistent for Black in display code
- **File:** `chessEngine/src/Moves.cs:103-108`, `chessEngine/src/Helpers.cs:82-83`
- **Description:** `AddPawnMove` generates `PromotedPieceType = 4, 3, 2, 1` (Queen, Rook, Bishop, Knight) for both colors. `MakeMove` computes the actual piece as `move.PromotedPieceType + 6 * SideToMove`, which works correctly for the engine. However, `showMoves2` in `Helpers.cs` indexes `pieceChars[m.PromotedPieceType]` directly without the color offset — for Black promotions this shows the wrong piece character.
- **Impact:** Display-only bug for Black promotions in debug output.
- **Suggested fix:** Apply `+ 6 * SideToMove` in `showMoves2`, or store absolute piece type in the `Move` struct.

### 9. Transposition table uses "always overwrite" strategy
- **File:** `chessEngine/src/Bot.cs:53-56`
- **Description:** `TT.Store` always overwrites the entry at the hashed index, even if the existing entry has greater search depth. A depth-preferred replacement strategy would retain more valuable entries.
- **Impact:** Reduces TT hit rate, leading to more nodes searched and slower AI.
- **Suggested fix:** Only overwrite if `newDepth >= existingDepth` or use a two-tier bucket scheme.

### 10. Expensive hanging-piece evaluation on every `GetBoardEval` call
- **File:** `chessEngine/src/Main.cs:445-498`
- **Description:** The hanging-piece evaluation iterates all pieces of both colors, calling `GetCheapestAttackerValue` and `IsSquareAttacked` (both use magic bitboard lookups) for each piece. This runs on every evaluation call, including in `QuiescenceSearch` stand-pat and after every human/bot move.
- **Impact:** Significant performance overhead in search. Slows down the AI.
- **Suggested fix:** Only compute hanging pieces when `depth == 0` or cache results. Consider incremental update.

### 11. `GenerateAllLegalMoves` makes/unmakes every pseudo-legal move
- **File:** `chessEngine/src/Moves.cs:936-960`
- **Description:** To filter pseudo-legal moves to legal, the code `MakeMove`/`UnmakeMove`s every single candidate. For positions with many pseudo-legal moves (e.g., queens in endgames), this is expensive.
- **Impact:** Performance overhead, especially in endgame search.
- **Suggested fix:** Check legality without making/unmaking by testing if the king would be in check after the move using bitboard operations and pinned-piece analysis.

---

## Low

### 12. Dead code: `Class1.cs` stub file
- **File:** `chessEngine/Class1.cs`
- **Description:** Empty `Class1` in the wrong namespace (`chessEngine` lowercase). Leftover from project creation.
- **Suggested fix:** Delete the file.

### 13. Dead code: unused `cache` Dictionary in Bot
- **File:** `chessEngine/src/Bot.cs:90`
- **Description:** `public static Dictionary<(Board board, int depth, int a, int b, int c, int sideToMove), int> cache = new();` is declared but never read from or written to. Using `Board` as a key would also fail because `Board` doesn't override `Equals`/`GetHashCode`.
- **Suggested fix:** Remove the field.

### 14. Dead code: unused `MoveGenerator` delegate
- **File:** `chessEngine/src/Tests.cs:7`
- **Description:** `public delegate int MoveGenerator(Board b, ulong pieces, int color, Span<Move> moves);` is declared but never used.
- **Suggested fix:** Remove the delegate.

### 15. Dead code: `ClearPieceAtTarget` never called
- **File:** `chessEngine/src/Main.cs:262-273`
- **Description:** Method is defined but never referenced. Capture logic is handled inline in `MakeMove`.
- **Suggested fix:** Remove the unused method.

### 16. Dead code: unused `boardPerspective_coords` field
- **File:** `StockfishV0/ChessForm.cs:511`
- **Description:** `private int boardPerspective_coords = 0;` is declared but never referenced anywhere.
- **Suggested fix:** Remove the field.

### 17. Dead code: unused `Random random` field
- **File:** `StockfishV0/ChessForm.cs:458`
- **Description:** `private readonly Random random = new Random();` is never used.
- **Suggested fix:** Remove the field.

### 18. Dead code: unused `topX` parameter in `Think()`
- **File:** `chessEngine/src/Bot.cs:198`
- **Description:** The `topX` parameter is accepted but never referenced in the method body. Callers pass values of 50 or 25.
- **Suggested fix:** Remove the parameter, or implement multi-PV as apparently intended.

### 19. Dead code: commented-out initialization
- **File:** `chessEngine/src/Helpers.cs:10-13`
- **Description:** `init()` has commented-out magic number generation calls. Dead configuration code.
- **Suggested fix:** Remove the commented lines.

### 20. `allTests()` test suite never invoked
- **File:** `chessEngine/src/Tests.cs:39`
- **Description:** The comprehensive 21-scenario test suite is defined but never called from `Program.cs`, `ChessForm.cs`, or anywhere.
- **Suggested fix:** Add a command-line flag or menu option to run tests, or convert to a unit test project.

### 21. Repeated `Timer` allocation/disposal pattern
- **File:** `StockfishV0/ChessForm.cs:672-685,1279-1291`
- **Description:** `QueueBoardPerspectiveFlip()` and `QueueBotMoveIfNeeded()` create new `System.Windows.Forms.Timer` instances on every call, which self-dispose in their `Tick` handlers. This is allocation-heavy.
- **Suggested fix:** Use a single reusable timer or `Task.Delay` with `CancellationToken`.

### 22. O(n²) selection sort for move ordering
- **File:** `chessEngine/src/Bot.cs:145-168,314-338`
- **Description:** Move ordering uses selection sort, which is O(n²). For typical move counts (30–40) this is acceptable but suboptimal.
- **Suggested fix:** Use `Array.Sort` with a custom comparer, or insertion sort for nearly-ordered lists.

### 23. Magic numbers throughout the codebase
- **Files:** Multiple (`Main.cs`, `Magic.cs`, `Moves.cs`, `Helpers.cs`, `Bot.cs`)
- **Description:** Many hardcoded numeric constants without named identifiers:
  - Square indices for castling (`4`=e1, `6`=g1, etc.)
  - `0xFF00000000000000UL`, `0x0000000000FF0000UL` for bitboard masks
  - `-30000 - depth` for mate score
  - `50` centipawn check bonus
  - `6767` as king value
  - `99999` as "not attacked" sentinel
  - `2000000` as infinity
- **Impact:** Code smell, harder to maintain and understand.
- **Suggested fix:** Replace with named constants (e.g., `const int MateScore = -30000;`).

---

## Additional Notes

- **No unit testing framework** — the project has no NUnit/xUnit/MSTest packages. All testing is manual via `Tests.allTests()`.
- **No CI/CD** — no GitHub Actions, GitLab CI, or Azure Pipelines configuration.
- **No `.editorconfig`** — no code style enforcement.
- **Build artifacts committed** — `bin/` and `obj/` directories are tracked in git. The `.gitignore` is minimal and should be expanded.
- **Orphaned build outputs** — root-level `bin/` and `obj/` directories target `net9.0`/`net10.0` but no root `.csproj` exists. These are leftover artifacts.
- **Nullable enabled in engine, disabled in GUI** — inconsistent null-safety across the solution.
