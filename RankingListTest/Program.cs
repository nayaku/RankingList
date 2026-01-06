using System;

namespace RankingListTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Ranking List Concurrent Test ===");
            Console.WriteLine();
            
            // Test parameters
            int initialUsers = 1000;      // Initial number of users in ranking list
            int totalOperations = 100000;   // Total number of operations to perform
            int concurrencyLevel = 100;    // Number of concurrent tasks
            
            // Create and run test
            var test = new RankingListConcurrentTest(initialUsers, totalOperations, concurrencyLevel);
            test.RunTest();
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
