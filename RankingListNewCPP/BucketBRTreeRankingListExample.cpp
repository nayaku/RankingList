#include "pch.h"
#include "BucketBRTreeRankingList.h"
#include <iostream>
#include <vector>

int main() {
    // 创建一些测试用户
    std::vector<User> users = {
        User(1, 100, time(nullptr)),
        User(2, 200, time(nullptr) - 100),
        User(3, 150, time(nullptr) - 50),
        User(4, 250, time(nullptr) - 200),
        User(5, 180, time(nullptr) - 150)
    };

    // 创建排行榜实例
    BucketBRTreeRankingList rankingList(users);

    // 测试获取用户总数
    std::cout << "用户总数: " << rankingList.GetUserCount() << std::endl;

    // 测试获取前N名用户
    const int topN = 3;
    User topUsers[topN];
    int count = rankingList.GetTopN(topN, topUsers);
    std::cout << "\n前" << topN << "名用户: " << std::endl;
    for (int i = 0; i < count; ++i) {
        std::cout << "排名 " << i + 1 << ": 用户ID=" << topUsers[i].Id << ", 分数=" << topUsers[i].Score << std::endl;
    }

    // 测试添加新用户
    User newUser(6, 190, time(nullptr) - 80);
    int rank = rankingList.AddUser(newUser);
    std::cout << "\n添加新用户: 用户ID=" << newUser.Id << ", 分数=" << newUser.Score << ", 排名=" << rank + 1 << std::endl;

    // 测试更新用户
    User updatedUser(3, 220, time(nullptr) - 30);
    rank = rankingList.UpdateUser(updatedUser);
    std::cout << "\n更新用户: 用户ID=" << updatedUser.Id << ", 新分数=" << updatedUser.Score << ", 新排名=" << rank + 1 << std::endl;

    // 再次测试获取前N名用户
    count = rankingList.GetTopN(topN, topUsers);
    std::cout << "\n更新后的前" << topN << "名用户: " << std::endl;
    for (int i = 0; i < count; ++i) {
        std::cout << "排名 " << i + 1 << ": 用户ID=" << topUsers[i].Id << ", 分数=" << topUsers[i].Score << std::endl;
    }

    // 测试获取用户周围的用户
    const int arroundN = 2;
    User aroundUsers[arroundN * 2 + 1];
    int userId = 3;
    count = rankingList.GetArroundUser(userId, arroundN, aroundUsers);
    std::cout << "\n用户ID=" << userId << "周围的" << arroundN << "名用户: " << std::endl;
    for (int i = 0; i < count; ++i) {
        std::cout << "用户ID=" << aroundUsers[i].Id << ", 分数=" << aroundUsers[i].Score << std::endl;
    }

    return 0;
}