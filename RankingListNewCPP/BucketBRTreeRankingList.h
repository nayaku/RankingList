#pragma once
#include "IRankingList.h"
#include <vector>
#include <unordered_map>
#include <cassert>
#include <cmath>
#include <algorithm>
#include "User.h"

class BucketBRTreeRankingList : public IRankingList
{
private:
    static const int BUCKET_SIZE = 256; // 每个bucket的用户数量
    static const int INITIAL_BUCKET_SIZE = BUCKET_SIZE / 2; // 初始每个bucket的用户数量

    enum class ColorEnum : uint8_t
    {
        Red = 0,
        Black = 1
    };

    class UserBucket;

    class TreeNode
    {
    public:
        int Count;
        User LeftUser;
        User RightUser;
        TreeNode* Left;
        TreeNode* Right;
        TreeNode* Parent;
        UserBucket* Bucket;
        ColorEnum Color;

        TreeNode();
        ~TreeNode();

        bool IsFull() const { return Count >= BUCKET_SIZE; }
        bool IsEmpty() const { return Count == 0; }

        void MoveFromChild(TreeNode* child);
        int Insert(const User& user);
        void Remove(const User& user);
        void Split(const User& user, int& userIndexInBucket);
        void CombineChild();

    private:
        static void UpdateLeftUser(TreeNode* node);
        static void UpdateRightUser(TreeNode* node);
    };

    class UserBucket
    {
    public:
        std::vector<User> Users;
        int UserCount;

        UserBucket();
        UserBucket(const std::vector<User>& users, int userCount);
        ~UserBucket() = default;

        const User& GetMinUser() const { return Users[0]; }
        const User& GetMaxUser() const { return Users[UserCount - 1]; }
        bool IsFull() const { return UserCount >= Users.size(); }
        bool IsEmpty() const { return UserCount == 0; }
        int IndexOf(const User& user) const;

        int Insert(const User& user);
        int Remove(const User& user);
        UserBucket* Split(const User& user, int& userIndex);
        void Combine(const UserBucket* other);
    };

    TreeNode* _root;
    std::unordered_map<int, User> _userMap;

public:
    BucketBRTreeRankingList(const std::vector<User>& users);
    virtual ~BucketBRTreeRankingList() override;

    virtual int AddUser(const User& user) override;
    virtual int UpdateUser(const User& user) override;
    virtual User GetUserRank(int userId) const override;
    virtual int GetTopN(int topN, User* pOutUsers) const override;
    virtual int GetArroundUser(int userId, int arroundN, User* pOutUsers) const override;
    virtual int GetUserCount() const override;

private:
    std::vector<UserBucket*> BuildBucket(std::vector<User>& users);
    TreeNode* BuildTree(int l, int r, int depth, int maxDepth, const std::vector<UserBucket*>& buckets);
    void AddUser(const User& user, int& rankCount);
    void RemoveUser(const User& user);
    void FixAfterAdd(TreeNode* node);
    void FixAfterDel(TreeNode* node);
    TreeNode* RotateLeft(TreeNode* x);
    TreeNode* RotateRight(TreeNode* x);
    void CheckTree();
    int CheckTree(TreeNode* node);
};

