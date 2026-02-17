using System;
using System.Collections.Generic;
using System.Text;

namespace RankingListTestNew
{
    public class FastGenerator
    {
        public void Generate()
        {
            GenerateAll();
        }

        private void GenerateAll()
        {
            for (int i = 1000; i < 1_0000_0000; i*=100)
            {
                int userNum = i;
                int operationNum = i*100;
                string testName = $"t{userNum}_{operationNum}";
                Console.WriteLine($"{testName} {userNum} {operationNum}");
                var generator = new Generator(testName, userNum, operationNum);
                generator.Generate();
            }
        }
    }
}
