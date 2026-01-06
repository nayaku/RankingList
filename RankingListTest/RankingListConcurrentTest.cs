using System.Diagnostics;
using RankingList;

namespace RankingListTest
{
    public class RankingListConcurrentTest
    {
        private readonly RankingList.SimpleRankingList _rankingList;
        private readonly int _totalOperations;
        private readonly int _concurrencyLevel;
        
        private int _currentConcurrency;
        private int _maxConcurrency;
        private long _totalResponseTime;
        private int _completedOperations;
        private readonly object _lock = new object();
        
        public RankingListConcurrentTest(int initialUsers, int totalOperations, int concurrencyLevel)
        {
            // Initialize ranking list with some users
            var users = new List<User>();
            for (int i = 1; i <= initialUsers; i++)
            {
                users.Add(new User
                {
                    ID = i,
                    Score = new Random().Next(1000, 10000),
                    LastActive = DateTime.Now.AddSeconds(-i)
                });
            }
            _rankingList = new RankingList(users);
            _totalOperations = totalOperations;
            _concurrencyLevel = concurrencyLevel;
        }
        
        public void RunTest()
        {
            Console.WriteLine("Starting concurrent test...");
            Console.WriteLine($"Initial users: {_rankingList.GetTotalUsers()}");
            Console.WriteLine($"Total operations: {_totalOperations}");
            Console.WriteLine($"Concurrency level: {_concurrencyLevel}");
            
            var sw = Stopwatch.StartNew();
            
            // Create and start tasks
            var tasks = new List<Task>();
            for (int i = 0; i < _concurrencyLevel; i++)
            {
                tasks.Add(Task.Run(() => PerformOperations()));
            }
            
            // Wait for all tasks to complete
            Task.WaitAll(tasks.ToArray());
            
            sw.Stop();
            
            // Calculate results
            double averageResponseTime = (double)_totalResponseTime / _completedOperations;
            
            Console.WriteLine("\nTest Results:");
            Console.WriteLine($"Total time elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"Completed operations: {_completedOperations}");
            Console.WriteLine($"Maximum concurrent operations: {_maxConcurrency}");
            Console.WriteLine($"Average response time: {averageResponseTime:F2} ms");
        }
        
        private void PerformOperations()
        {
            var random = new Random(Guid.NewGuid().GetHashCode());
            
            for (int i = 0; i < _totalOperations / _concurrencyLevel; i++)
            {
                // 80% GetRankingListMutiResponse, 20% UpdateUser
                if (random.NextDouble() < 0.8)
                {
                    PerformGetRankingOperation(random);
                }
                else
                {
                    PerformUpdateOperation(random);
                }
            }
        }
        
        private void PerformGetRankingOperation(Random random)
        {
            int userId = random.Next(1, _rankingList.GetTotalUsers() + 1);
            int topN = random.Next(5, 20);
            int range = random.Next(3, 10);
            
            // Record start time and increment current concurrency
            int currentConcurrency = Interlocked.Increment(ref _currentConcurrency);
            
            // Update max concurrency
            lock (_lock)
            {
                if (currentConcurrency > _maxConcurrency)
                {
                    _maxConcurrency = currentConcurrency;
                }
            }
            
            var sw = Stopwatch.StartNew();
            
            // Perform the operation
            _rankingList.GetRankingListMutiResponse(userId, topN, range);
            
            sw.Stop();
            
            // Decrement current concurrency and update metrics
            Interlocked.Decrement(ref _currentConcurrency);
            Interlocked.Add(ref _totalResponseTime, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref _completedOperations);
        }
        
        private void PerformUpdateOperation(Random random)
        {
            int userId = random.Next(1, _rankingList.GetTotalUsers() + 1);
            
            // Record start time and increment current concurrency
            int currentConcurrency = Interlocked.Increment(ref _currentConcurrency);
            
            // Update max concurrency
            lock (_lock)
            {
                if (currentConcurrency > _maxConcurrency)
                {
                    _maxConcurrency = currentConcurrency;
                }
            }
            
            var sw = Stopwatch.StartNew();
            
            // Perform the operation
            _rankingList.UpdateUser(new User
            {
                ID = userId,
                Score = new Random().Next(1000, 10000),
                LastActive = DateTime.Now
            });
            
            sw.Stop();
            
            // Decrement current concurrency and update metrics
            Interlocked.Decrement(ref _currentConcurrency);
            Interlocked.Add(ref _totalResponseTime, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref _completedOperations);
        }
    }
}
