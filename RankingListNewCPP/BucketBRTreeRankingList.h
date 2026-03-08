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

	enum ColorEnum : uint8_t
	{
		Red = 0,
		Black = 1
	};

	class UserBucket
	{
	public:
		User Users[BUCKET_SIZE];
		int UserCount;

		const User& GetMinUser() const { return Users[0]; }
		const User& GetMaxUser() const { return Users[UserCount - 1]; }
		inline bool IsFull() const { return UserCount >= BUCKET_SIZE; }
		inline bool IsEmpty() const { return UserCount == 0; }
		inline int IndexOf(const User& user) const;

		int Insert(const User& user);
		int Remove(const User& user);
		UserBucket* Split(const User& user, int& userIndex);
		void Combine(const UserBucket* other);
	};

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

		inline bool IsFull() const { return Count >= BUCKET_SIZE; }
		inline bool IsEmpty() const { return Count == 0; }

		void MoveFromChild(TreeNode* child);
		int Insert(const User& user);
		void Remove(const User& user);
		void Split(const User& user, int& userIndexInBucket);
		void CombineChild();

	private:
		static void UpdateLeftUser(TreeNode* node);
		static void UpdateRightUser(TreeNode* node);
	};

	TreeNode* _root;
	std::unordered_map<int, User> _userMap;

public:
	BucketBRTreeRankingList(User* pUsers, int userCount);
	virtual ~BucketBRTreeRankingList() override;

	virtual int AddUser(const User& user) override;
	virtual int UpdateUser(const User& user) override;
	virtual int GetUserRank(int userId)  override;
	virtual int GetTopN(int topN, User* pOutUsers)  override;
	virtual int GetArroundUser(int userId, int arroundN, User* pOutUsers) override;
	virtual int GetUserCount() override;

private:
	std::vector<UserBucket*> BuildBucket(const User* pUsers, int userCount);
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

