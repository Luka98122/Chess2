using System;

namespace ChessEngine
{
    public static class Bot
    {
        public static int recommendMove(Board b, Span<Move> moves, int moveCount)
        {
            //-1000 crni dominatuje
            //+1000 beli dominatuje

            if (b.SideToMove == 0) //white
            {
                int best = 0;
                int bInd = 0;

                for (int i = 0; i < moveCount; i++)
                {
                    b.MakeMove(moves[i]);
                    int score = b.GetBoardEval();
                    if (score > best)
                    {
                        best = score;
                        bInd = i;
                    }
                    b.UnmakeMove();
                }
                return bInd;
            }
            else
            { //black
                int best = 10000000;
                int bInd = 0;

                for (int i = 0; i < moveCount; i++)
                {
                    b.MakeMove(moves[i]);
                    int score = b.GetBoardEval();
                    if (score < best)
                    {
                        best = score;
                        bInd = i;
                    }
                    b.UnmakeMove();
                }
                return bInd;
            }

            return -1;
        }
        
    }
}