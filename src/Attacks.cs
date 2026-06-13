using System;
using System.Runtime.InteropServices;

namespace ChessEngine
{
    public static class Attacks
    {
        public static Dictionary<int, ulong> KnightPreCalcs = new Dictionary<int, ulong>();
        public static void PreCalculateKnightAttacks()
        {
            int[] dy = { -2, -2, -1, -1, 1, 1, 2, 2 };
            int[] dx = { -1, 1, -2, 2, -2, 2, -1, 1 };

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int pos = y * 8 + x;
                    ulong res = 0UL;

                    for (int i = 0; i < 8; i++)
                    {
                        int targetY = y + dy[i];
                        int targetX = x + dx[i];

                        if (targetY >= 0 && targetY < 8 && targetX >= 0 && targetX < 8)
                        {
                            int targetPos = targetY * 8 + targetX;
                            res |= 1UL << targetPos; //flipuje tu poziciju bitboarda na keca
                        }
                    }

                    KnightPreCalcs[pos] = res;
                }
            }
        }

        
    }
}