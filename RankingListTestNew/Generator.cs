using RankingListNew;
using System.Text.Json;

namespace RankingListTestNew
{
    // 测试初始类
    public class TestInitial
    {
        public List<User> Users { get; set; }
        public List<TestOperation> Operations { get; set; }
    }

    public class Generator
    {
        private static readonly DateTime InitialUserCreateTime = new(2026, 1, 1);
        private static readonly double[] OperationRatio = [0.1, 0.2, 0.3, 0.2, 0.2]; // AddUser, UpdateUser, GetUserRank, GetTopN, GetAroundUser
        private string _testName;
        private int _userNum;
        private int _operationNum;
        private int _currentUserId;
        private int _currentOperationId;
        private Dictionary<int, int> _userIdToScore;
        private OperationType? _limitOperationType;


        public Generator(string testName, int userNum, int operationNum, OperationType? limitOperationType)
        {
            _testName = testName;
            _userNum = userNum;
            _operationNum = operationNum;
            _limitOperationType = limitOperationType;
        }

        /// <summary>
        /// 生成初始用户数据
        /// </summary>
        /// <param name="random"></param>
        /// <returns></returns>
        private List<User> GenerateInitialUsers(Random random)
        {
            _userIdToScore = [];
            List<User> users = [];
            for (int i = 0; i < _userNum; i++)
            {
                var user = new User(
                     i + 1,
                     GeneratePowerLawScore(random),
                     InitialUserCreateTime.AddSeconds(i)
                );
                _userIdToScore[user.Id] = user.Score;
                users.Add(user);
                _currentUserId = user.Id;
            }

            return users;
        }

        /// <summary>
        /// 生成操作列表
        /// </summary>
        /// <param name="random"></param>
        /// <param name="operationNum"></param>
        /// <returns></returns>
        private List<TestOperation> GenerateOperations(Random random)
        {
            var operations = new List<TestOperation>(_operationNum);
            for (int i = 0; i < _operationNum; i++)
            {
                var operation = new TestOperation
                {
                    Id = ++_currentOperationId,
                };

                if (_limitOperationType == null)
                {
                    double operationType = random.NextDouble();
                    double cumulative = 0;
                    for (int j = 0; j < OperationRatio.Length; j++)
                    {
                        cumulative += OperationRatio[j];
                        if (operationType < cumulative)
                        {
                            operation.Type = (OperationType)j;
                            break;
                        }
                    }
                }
                else
                {
                    operation.Type = _limitOperationType.Value;
                }

                switch (operation.Type)
                {
                    case OperationType.AddUser:
                        operation.UserId = ++_currentUserId;
                        operation.ScoreOrN = GeneratePowerLawScore(random, 100);
                        _userIdToScore[operation.UserId] = operation.ScoreOrN;
                        break;
                    case OperationType.UpdateUser:
                        {
                            operation.UserId = random.Next(1, _currentUserId);
                            int score = _userIdToScore[operation.UserId];
                            operation.ScoreOrN = score + GeneratePowerLawScore(random, 100);
                            break;
                        }
                    case OperationType.GetUserRank:
                        operation.UserId = random.Next(1, _currentUserId + 1);
                        break;
                    case OperationType.GetTopN:
                        operation.UserId = random.Next(1, _currentUserId + 1);
                        operation.ScoreOrN = random.Next(1, 100);
                        break;
                    case OperationType.GetAroundUser:
                        operation.UserId = random.Next(1, _currentUserId + 1);
                        operation.ScoreOrN = random.Next(1, 20);
                        break;
                }

                operations.Add(operation);
            }

            return operations;
        }

        // 生成初始化测试数据
        public void GenerateTestInitialData()
        {
            Random random = new(42);
            // 生成初始用户数据
            List<User> initialUsers = GenerateInitialUsers(random);
            // 生成操作列表
            List<TestOperation> operations = GenerateOperations(random);

            TestInitial testInitial = new()
            {
                Users = initialUsers,
                Operations = operations,
            };
            // 储存测试数据
            using FileStream fs = new(_testName + ".json", FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize(fs, testInitial, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"测试数据已生成并保存到 {_testName}.json");
        }

        // 生成幂律分布的分数
        private static int GeneratePowerLawScore(Random random, int maxScore = 1000000)
        {
            // 简单的幂律分布生成
            double uniform = random.NextDouble();
            return (int)(Math.Pow(uniform, 2) * maxScore);
        }
    }
}
