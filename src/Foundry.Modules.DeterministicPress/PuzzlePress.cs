// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

/// <summary>
/// A tiny explicit generator (splitmix-mixed xorshift32) so the presses' seeded
/// determinism rests on this module's own arithmetic, not on the base library's
/// unpledged <c>Random</c> algorithm. Same seed, same sequence, forever.
/// </summary>
internal sealed class SeededPrng
{
    private uint _state;

    public SeededPrng(int seed)
    {
        var z = (uint)seed + 0x9E3779B9u;
        z ^= z >> 16;
        z *= 0x21F0AAADu;
        z ^= z >> 15;
        z *= 0x735A2D97u;
        z ^= z >> 15;
        _state = z == 0 ? 0x9E3779B9u : z;
    }

    public int Next(int bound)
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return (int)(_state % (uint)bound);
    }

    public void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}

/// <summary>
/// Puzzle Press (handover 2026-08-29, forge item 2): seeded-deterministic bingo
/// boards and word searches from a teacher's own list. The teacher chooses the
/// seed; the same seed reproduces the same pages — never random at print time.
/// Teacher text is placed verbatim, never interpreted or reordered in the output.
/// </summary>
public static class PuzzlePress
{
    private const int BingoSide = 5;

    public static ArtifactDocument BingoBoards(IReadOnlyList<string> entries, int cards, int seed, bool freeCenter = true, double cellMm = 32, PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var needed = BingoSide * BingoSide - (freeCenter ? 1 : 0);
        if (entries.Any(string.IsNullOrWhiteSpace) || entries.Distinct(StringComparer.Ordinal).Count() != entries.Count)
        {
            throw new ArgumentException("Entries must be non-blank and distinct.", nameof(entries));
        }

        if (entries.Count < needed)
        {
            throw new ArgumentException($"A card draws {needed} entries; the list has {entries.Count}.", nameof(entries));
        }

        if (cards is < 1 or > 40)
        {
            throw new ArgumentException("Between one and forty cards per run.", nameof(cards));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var gridSize = BingoSide * cellMm;
        if (cellMm <= 0 || gridSize > width - 2 * marginMm || gridSize + 12 > height - 2 * marginMm)
        {
            throw new ArgumentException("Five cells and the footer must fit inside the margins.", nameof(cellMm));
        }

        var left = (width - gridSize) / 2;
        var top = (height - gridSize - 12) / 2;
        var prng = new SeededPrng(seed);
        var center = BingoSide / 2;

        var nodes = new List<DocumentNode>();
        for (var card = 0; card < cards; card++)
        {
            var order = Enumerable.Range(0, entries.Count).ToList();
            prng.Shuffle(order);

            var primitives = new List<VectorPrimitive>();
            var drawn = 0;
            for (var row = 0; row < BingoSide; row++)
            {
                for (var col = 0; col < BingoSide; col++)
                {
                    var x = left + col * cellMm;
                    var y = top + row * cellMm;
                    primitives.Add(new RectShape(x, y, cellMm, cellMm, 0.5));

                    var isFree = freeCenter && row == center && col == center;
                    if (isFree)
                    {
                        // The free-center star is GEOMETRY, not a glyph:
                        // U+2605 has no WinAnsi encoding, and the free cell
                        // must survive the native PDF press like every other
                        // millimeter (found by the Studio Sampler).
                        primitives.AddRange(FreeCenterStar(x + cellMm / 2, y + cellMm / 2, cellMm * 0.32));
                    }
                    else
                    {
                        primitives.Add(new TextLabel(
                            x + cellMm / 2, y + cellMm / 2 + 1.5, entries[order[drawn++]], 4.5));
                    }
                }
            }

            primitives.Add(new TextLabel(width / 2, top + gridSize + 8,
                $"Card {card + 1} of {cards} · seed {seed}", 3.5));

            nodes.Add(new VectorGraphic(width, height, primitives,
                $"Bingo card {card + 1} of {cards}: five by five, drawn from the teacher's {entries.Count}-entry list with seed {seed}{(freeCenter ? ", free center" : "")}"));
        }

        return new ArtifactDocument(nodes);
    }

    /// <summary>A five-point star as five chords of a pentagon — exact constants, no runtime trigonometry.</summary>
    private static IEnumerable<LineSeg> FreeCenterStar(double centerX, double centerY, double radiusMm)
    {
        (double X, double Y)[] vertices =
        [
            (0, -1), (0.951056516, -0.309016994), (0.587785252, 0.809016994),
            (-0.587785252, 0.809016994), (-0.951056516, -0.309016994),
        ];

        for (var k = 0; k < 5; k++)
        {
            var (X, Y) = vertices[k];
            var to = vertices[(k + 2) % 5];
            yield return new LineSeg(
                centerX + X * radiusMm, centerY + Y * radiusMm,
                centerX + to.X * radiusMm, centerY + to.Y * radiusMm, 0.7);
        }
    }

    public static ArtifactDocument WordSearch(IReadOnlyList<string> words, int seed, int gridSize = 12, bool diagonals = true, bool backwards = false, double cellMm = 12, PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm, bool includeAnswerKey = true)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (gridSize is < 6 or > 20)
        {
            throw new ArgumentException("Grids between 6 and 20 cells square.", nameof(gridSize));
        }

        if (words.Count is < 1 or > 24)
        {
            throw new ArgumentException("Between one and twenty-four words.", nameof(words));
        }

        var placedWords = words.Select(w => (w ?? string.Empty).Trim().ToUpperInvariant()).ToList();
        if (placedWords.Any(w => w.Length < 2 || w.Length > gridSize || !w.All(char.IsLetter)))
        {
            throw new ArgumentException("Every word: letters only, at least two, no longer than the grid side.", nameof(words));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var gridMm = gridSize * cellMm;
        if (cellMm <= 0 || gridMm > width - 2 * marginMm)
        {
            throw new ArgumentException("The grid must fit inside the margins.", nameof(cellMm));
        }

        var bankRows = (placedWords.Count + 2) / 3;
        var top = marginMm + 8;
        if (top + gridMm + 10 + bankRows * 6 > height - marginMm)
        {
            throw new ArgumentException("Grid plus word bank must fit one page; fewer words or smaller cells.", nameof(words));
        }

        var (grid, used) = PlaceWords(placedWords, gridSize, seed, diagonals, backwards);

        var left = (width - gridMm) / 2;
        var puzzle = new List<VectorPrimitive> { new RectShape(left, top, gridMm, gridMm, 0.5) };
        var key = new List<VectorPrimitive> { new RectShape(left, top, gridMm, gridMm, 0.5) };

        for (var row = 0; row < gridSize; row++)
        {
            for (var col = 0; col < gridSize; col++)
            {
                var x = left + col * cellMm + cellMm / 2;
                var y = top + row * cellMm + cellMm / 2 + 1.8;
                puzzle.Add(new TextLabel(x, y, grid[row, col].ToString(), 5));
                if (used[row, col])
                {
                    key.Add(new TextLabel(x, y, grid[row, col].ToString(), 5));
                }
            }
        }

        // The word bank, in the teacher's own order — never resorted.
        var bankTop = top + gridMm + 10;
        var columnWidth = (width - 2 * marginMm) / 3;
        for (var i = 0; i < placedWords.Count; i++)
        {
            puzzle.Add(new TextLabel(
                marginMm + i % 3 * columnWidth,
                bankTop + i / 3 * 6,
                placedWords[i],
                4,
                TextAnchor.Start));
        }

        var nodes = new List<DocumentNode>
        {
            new VectorGraphic(width, height, puzzle,
                $"A {gridSize} by {gridSize} word search hiding {placedWords.Count} teacher-chosen words, seed {seed}, with the word bank beneath"),
        };

        if (includeAnswerKey)
        {
            nodes.Add(new TeacherOnlyNotice("Answer key: the same grid with the filler letters omitted."));
            nodes.Add(new VectorGraphic(width, height, key,
                $"Word search answer key: only the {placedWords.Count} hidden words' letters, seed {seed}"));
        }

        return new ArtifactDocument(nodes);
    }

    private static (char[,] Grid, bool[,] Used) PlaceWords(IReadOnlyList<string> words, int gridSize, int seed, bool diagonals, bool backwards)
    {
        var directions = new List<(int Dr, int Dc)> { (1, 0), (0, 1) };
        if (diagonals)
        {
            directions.Add((1, 1));
            directions.Add((-1, 1));
        }

        if (backwards)
        {
            directions.AddRange(directions.Select(d => (-d.Dr, -d.Dc)).ToList());
        }

        var prng = new SeededPrng(seed);
        var grid = new char[gridSize, gridSize];
        var used = new bool[gridSize, gridSize];

        // Longest first: a deterministic placement order that leaves the tight
        // fits for last is the one that succeeds most often. The printed word
        // bank keeps the teacher's order regardless.
        foreach (var word in words.OrderByDescending(w => w.Length))
        {
            var candidates = new List<(int Row, int Col, int Dr, int Dc)>();
            foreach (var (dr, dc) in directions)
            {
                for (var row = 0; row < gridSize; row++)
                {
                    for (var col = 0; col < gridSize; col++)
                    {
                        var endRow = row + dr * (word.Length - 1);
                        var endCol = col + dc * (word.Length - 1);
                        if (endRow >= 0 && endRow < gridSize && endCol >= 0 && endCol < gridSize)
                        {
                            candidates.Add((row, col, dr, dc));
                        }
                    }
                }
            }

            prng.Shuffle(candidates);

            var placed = false;
            foreach (var (row, col, dr, dc) in candidates)
            {
                if (Fits(grid, word, row, col, dr, dc))
                {
                    for (var i = 0; i < word.Length; i++)
                    {
                        grid[row + dr * i, col + dc * i] = word[i];
                        used[row + dr * i, col + dc * i] = true;
                    }

                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                throw new ArgumentException($"\"{word}\" cannot be placed with this seed; try fewer or shorter words, or a larger grid.", nameof(words));
            }
        }

        var alphabet = words.SelectMany(w => w).Distinct().OrderBy(c => c).ToList();
        for (var row = 0; row < gridSize; row++)
        {
            for (var col = 0; col < gridSize; col++)
            {
                if (grid[row, col] == '\0')
                {
                    grid[row, col] = alphabet[prng.Next(alphabet.Count)];
                }
            }
        }

        return (grid, used);
    }

    private static bool Fits(char[,] grid, string word, int row, int col, int dr, int dc)
    {
        for (var i = 0; i < word.Length; i++)
        {
            var cell = grid[row + dr * i, col + dc * i];
            if (cell != '\0' && cell != word[i])
            {
                return false;
            }
        }

        return true;
    }
}
