using System;
using System.Collections.Generic;
using System.Text;

namespace RankingListTestNew
{
    /// <summary>
    /// 操作类型枚举
    /// </summary>
    public enum OperationType
    {
        AddUser,
        UpdateUser,
        GetUserRank,
        GetTopN,
        GetAroundUser
    }

    /// <summary>
    /// 测试操作类
    /// </summary>
    public struct TestOperation
    {
        public int Id;
        public OperationType Type;
        public int UserId;
        public int ScoreOrN;
    }
}
