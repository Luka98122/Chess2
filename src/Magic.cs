using System;
using static ChessEngine.EngineHelpers;
using static ChessEngine.KnightMoveGenerator;
using static ChessEngine.RookMoveGenerator;
using System.Numerics;


namespace ChessEngine
{
    public static class MagicFinder
    {
        private static Random rnd = new Random();

        // Generates a random 64-bit integer
        private static ulong GetRandomUlong()
        {
            byte[] buffer = new byte[8];
            rnd.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }

        // Bitwise ANDing 3 random numbers dramatically reduces the number of 1s.
        private static ulong GetSparseRandomUlong()
        {
            return GetRandomUlong() & GetRandomUlong() & GetRandomUlong();
        }

        public static ulong FindRookMagic(int square, int relevantBits)
        {
            int permutationCount = 1 << relevantBits;
            ulong[] blockers = new ulong[permutationCount];
            ulong[] attacks = new ulong[permutationCount];

            // 1. Fetch the mask using the generator we already built
            ulong mask = RookMoveGenerator.CreateRookMask(square);

            // 2. Pre-calculate all blocker states and their true attacks
            ulong blockerPattern = 0;
            int i = 0;
            do
            {
                blockers[i] = blockerPattern;
                attacks[i] = RookMoveGenerator.CalculateNaiveRookAttacks(square, blockerPattern);
                i++;
                blockerPattern = (blockerPattern - mask) & mask;
            } while (blockerPattern != 0);

            // 3. Brute force loop
            for (int attempt = 0; attempt < 100000000; attempt++)
            {
                ulong magic = GetSparseRandomUlong();

                // Skip invalid magics early (a magic number must map to at least the required bits)
                if (BitOperations.PopCount((mask * magic) & 0xFF00000000000000UL) < 6)
                    continue;

                ulong[] usedAttacks = new ulong[permutationCount];
                bool[] isUsed = new bool[permutationCount];
                bool fail = false;

                // 4. Test the magic number against every permutation
                for (int j = 0; j < permutationCount; j++)
                {
                    int magicIndex = (int)((blockers[j] * magic) >> (64 - relevantBits));

                    if (!isUsed[magicIndex])
                    {
                        isUsed[magicIndex] = true;
                        usedAttacks[magicIndex] = attacks[j];
                    }
                    else if (usedAttacks[magicIndex] != attacks[j])
                    {
                        // Collision! Two different blocker states resulted in the same index, 
                        // but they require different attack boards. This magic fails.
                        fail = true;
                        break;
                    }
                }

                if (!fail)
                {
                    return magic; // We found a winner
                }
            }

            Console.WriteLine($"Failed to find rook magic for square {square}");
            return 0UL;
        }

        // Helper method to generate and print the full array of 64 magic numbers
        public static void GenerateAllRookMagics()
        {
            Console.WriteLine("public static readonly ulong[] RookMagics = new ulong[64] {");
            for (int square = 0; square < 64; square++)
            {
                // We use PopCount on the mask to know exactly how many relevant bits this square has
                ulong mask = RookMoveGenerator.CreateRookMask(square);
                int relevantBits = BitOperations.PopCount(mask);

                ulong magic = FindRookMagic(square, relevantBits);
                Console.WriteLine($"    0x{magic:X16}UL, // Square {square}");
            }
            Console.WriteLine("};");
        }

        public static ulong FindBishopMagic(int square, int relevantBits)
        {
            int permutationCount = 1 << relevantBits;
            ulong[] blockers = new ulong[permutationCount];
            ulong[] attacks = new ulong[permutationCount];

            // 1. Fetch the exact mask your engine uses
            ulong mask = BishopMoveGenerator.CreateBishopMask(square);

            // 2. Pre-calculate all blocker states and their true attacks
            ulong blockerPattern = 0;
            int i = 0;
            do
            {
                blockers[i] = blockerPattern;
                attacks[i] = BishopMoveGenerator.CalculateNaiveBishopAttacks(square, blockerPattern);
                i++;
                blockerPattern = (blockerPattern - mask) & mask;
            } while (blockerPattern != 0);

            // 3. Brute force loop
            for (int attempt = 0; attempt < 100000000; attempt++)
            {
                ulong magic = GetSparseRandomUlong();

                // Skip invalid magics early 
                if (BitOperations.PopCount((mask * magic) & 0xFF00000000000000UL) < 6)
                    continue;

                ulong[] usedAttacks = new ulong[permutationCount];
                bool[] isUsed = new bool[permutationCount];
                bool fail = false;

                // 4. Test the magic number against every permutation
                for (int j = 0; j < permutationCount; j++)
                {
                    int magicIndex = (int)((blockers[j] * magic) >> (64 - relevantBits));

                    if (!isUsed[magicIndex])
                    {
                        isUsed[magicIndex] = true;
                        usedAttacks[magicIndex] = attacks[j];
                    }
                    else if (usedAttacks[magicIndex] != attacks[j])
                    {
                        // Destructive Collision! 
                        fail = true;
                        break;
                    }
                }

                if (!fail)
                {
                    return magic; // Found a winner
                }
            }

            Console.WriteLine($"Failed to find bishop magic for square {square}");
            return 0UL;
        }

        public static void GenerateAllBishopMagics()
        {
            Console.WriteLine("public static readonly ulong[] BishopMagics = new ulong[64] {");
            for (int square = 0; square < 64; square++)
            {
                ulong mask = BishopMoveGenerator.CreateBishopMask(square);
                int relevantBits = BitOperations.PopCount(mask);

                ulong magic = FindBishopMagic(square, relevantBits);
                Console.WriteLine($"    0x{magic:X16}UL, // Square {square}");
            }
            Console.WriteLine("};");
        }
    }
}