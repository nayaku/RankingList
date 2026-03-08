#include "pch.h"
#include "BucketBRTreeRankingList.h"
#include <iostream>

/*
* UserBucket 类实现
*/
inline int BucketBRTreeRankingList::UserBucket::IndexOf(const User& user) const
{
	return std::lower_bound(Users, Users + UserCount, user) - Users;
}

int BucketBRTreeRankingList::UserBucket::Insert(const User& user)
{
	int index = IndexOf(user);

	// 移动元素为新用户腾出空间
	memmove(Users + index + 1, Users + index, (UserCount - index) * sizeof(User));
	//std::move_backward(pUsers + index, pUsers + UserCount, pUsers + UserCount + 1);
	Users[index] = user;
	UserCount++;

	return index;
}

int BucketBRTreeRankingList::UserBucket::Remove(const User& user)
{
	int index = IndexOf(user);
	assert(index >= 0);

	// 移动元素覆盖要删除的用户
	memmove(Users + index, Users + index + 1, (UserCount - index - 1) * sizeof(User));
	//std::move(pUsers.begin() + index + 1, pUsers.begin() + UserCount, pUsers.begin() + index);
	UserCount--;

	return index;
}

BucketBRTreeRankingList::UserBucket* BucketBRTreeRankingList::UserBucket::Split(const User& user, int& userIndex)
{
	int mid = UserCount / 2;
	userIndex = IndexOf(user);

	UserBucket* newBucket = new UserBucket();
	int newUserCount = UserCount - mid;

	if (userIndex >= mid)
	{
		// 将mid到userIndex-1的元素复制到新桶
		memcpy(newBucket->Users, Users + mid, (userIndex - mid) * sizeof(User));
		// 将新用户插入到新桶的userIndex - mid位置
		newBucket->Users[userIndex - mid] = user;
		// 将userIndex到UserCount-1的元素复制到新桶
		memcpy(newBucket->Users + userIndex - mid + 1, Users + userIndex, (UserCount - userIndex) * sizeof(User));
		newUserCount++;
	}
	else
	{
		// 将mid到UserCount-1的元素复制到新桶
		memcpy(newBucket->Users, Users + mid, (UserCount - mid) * sizeof(User));
	}

	UserCount = mid;
	newBucket->UserCount = newUserCount;

	// 如果用户索引小于mid，将用户插入到当前桶
	if (userIndex < mid)
	{
		Insert(user);
	}

	return newBucket;
}

void BucketBRTreeRankingList::UserBucket::Combine(const UserBucket* other)
{
	// 将other的元素复制到当前桶的末尾
	memcpy(Users + UserCount, other->Users, other->UserCount * sizeof(User));
	UserCount += other->UserCount;
}

/*
* TreeNode 类实现
*/
BucketBRTreeRankingList::TreeNode::TreeNode()
	: Count(0), Left(nullptr), Right(nullptr), Parent(nullptr), Bucket(nullptr), Color(ColorEnum::Red)
{
}

BucketBRTreeRankingList::TreeNode::~TreeNode()
{
	delete Left;
	delete Right;
	delete Bucket;
}

void BucketBRTreeRankingList::TreeNode::UpdateLeftUser(TreeNode* node)
{
	while (node->Parent != nullptr && node == node->Parent->Left)
	{
		node->Parent->LeftUser = node->LeftUser;
		node = node->Parent;
	}
}

void BucketBRTreeRankingList::TreeNode::UpdateRightUser(TreeNode* node)
{
	while (node->Parent != nullptr && node == node->Parent->Right)
	{
		node->Parent->RightUser = node->RightUser;
		node = node->Parent;
	}
}

void BucketBRTreeRankingList::TreeNode::MoveFromChild(TreeNode* child)
{
	assert(child->Count == Count);
	Left = child->Left;
	Right = child->Right;
	if (Left != nullptr) Left->Parent = this;
	if (Right != nullptr) Right->Parent = this;
	Bucket = child->Bucket;

	// 清空子节点的内容
	child->Left = nullptr;
	child->Right = nullptr;
	child->Bucket = nullptr;
}

int BucketBRTreeRankingList::TreeNode::Insert(const User& user)
{
	assert(Bucket != nullptr);
	int userIndexInBucket = Bucket->Insert(user);
	if (userIndexInBucket == 0)
	{
		LeftUser = user;
		UpdateLeftUser(this);
	}
	else if (userIndexInBucket == Bucket->UserCount - 1)
	{
		RightUser = user;
		UpdateRightUser(this);
	}

	Count++;
	return userIndexInBucket;
}

void BucketBRTreeRankingList::TreeNode::Remove(const User& user)
{
	assert(Bucket != nullptr);
	int userIndexInBucket = Bucket->Remove(user);
	if (Bucket->IsEmpty())
	{
		if (Parent != nullptr)
		{
			if (this == Parent->Left)
			{
				Parent->LeftUser = Parent->Right->LeftUser;
				UpdateLeftUser(Parent);
			}
			else
			{
				Parent->RightUser = Parent->Left->RightUser;
				UpdateRightUser(Parent);
			}
		}
	}
	else if (userIndexInBucket == 0)
	{
		LeftUser = Bucket->GetMinUser();
		UpdateLeftUser(this);
	}
	else if (userIndexInBucket == Bucket->UserCount)
	{
		RightUser = Bucket->GetMaxUser();
		UpdateRightUser(this);
	}

	Count--;
}

void BucketBRTreeRankingList::TreeNode::Split(const User& user, int& userIndexInBucket)
{
	assert(Bucket != nullptr);
	UserBucket* newBucket = Bucket->Split(user, userIndexInBucket);

	Left = new TreeNode();
	Left->Bucket = Bucket;
	Left->Count = Bucket->UserCount;
	Left->LeftUser = Bucket->GetMinUser();
	Left->RightUser = Bucket->GetMaxUser();
	Left->Parent = this;

	Right = new TreeNode();
	Right->Bucket = newBucket;
	Right->Count = newBucket->UserCount;
	Right->LeftUser = newBucket->GetMinUser();
	Right->RightUser = newBucket->GetMaxUser();
	Right->Parent = this;

	Bucket = nullptr;
	Count++;

	if (userIndexInBucket == 0)
	{
		UpdateLeftUser(Left);
	}
	else if (userIndexInBucket == Count - 1)
	{
		UpdateRightUser(Right);
	}

	assert(Count == Left->Count + Right->Count);
}

void BucketBRTreeRankingList::TreeNode::CombineChild()
{
	assert(Left != nullptr && Right != nullptr);
	assert(Left->Bucket != nullptr && Right->Bucket != nullptr);

	Bucket = Left->Bucket;
	Bucket->Combine(Right->Bucket);

	assert(Bucket->UserCount == Count);
	assert(Bucket->GetMinUser() == LeftUser);
	assert(Bucket->GetMaxUser() == RightUser);

	// 释放右节点的资源，但保留左节点的Bucket（因为已经转移到当前节点）
	delete Right->Bucket;
	Right->Bucket = nullptr;
	delete Right;
	Right = nullptr;
	delete Left;
	Left = nullptr;
}

/*
* BucketBRTreeRankingList 类实现
*/
std::vector<BucketBRTreeRankingList::UserBucket*> BucketBRTreeRankingList::BuildBucket(const User* pUsers, int userCount)
{
	int bucketNum = userCount / INITIAL_BUCKET_SIZE;
	if (userCount % INITIAL_BUCKET_SIZE != 0) bucketNum++;

	std::vector<UserBucket*> buckets(bucketNum);
	for (int i = 0; i < bucketNum; i++)
	{
		int l = i * INITIAL_BUCKET_SIZE;
		int r = (std::min)((i + 1) * INITIAL_BUCKET_SIZE, userCount);
		int userCount = r - l;

		buckets[i] = new UserBucket();
		memcpy(buckets[i]->Users, pUsers + l, userCount * sizeof(User));
		buckets[i]->UserCount = userCount;
	}

	return buckets;
}

BucketBRTreeRankingList::TreeNode* BucketBRTreeRankingList::BuildTree(int l, int r, int depth, int maxDepth, const std::vector<UserBucket*>& buckets)
{
	TreeNode* node = new TreeNode();
	node->Color = (maxDepth - depth) % 2 == 0 ? ColorEnum::Red : ColorEnum::Black;

	if (l + 1 == r)
	{
		node->Count = buckets[l]->UserCount;
		node->Bucket = buckets[l];
		node->LeftUser = buckets[l]->GetMinUser();
		node->RightUser = buckets[l]->GetMaxUser();
		return node;
	}

	int mid = (l + r) >> 1;
	node->Left = BuildTree(l, mid, depth + 1, maxDepth, buckets);
	node->Left->Parent = node;
	node->LeftUser = node->Left->LeftUser;

	node->Right = BuildTree(mid, r, depth + 1, maxDepth, buckets);
	node->Right->Parent = node;
	node->RightUser = node->Right->RightUser;

	node->Count = node->Left->Count + node->Right->Count;

	return node;
}

BucketBRTreeRankingList::BucketBRTreeRankingList(User* pUsers, int userCount)
//: _root(new TreeNode())
{
	std::sort(pUsers, pUsers + userCount);

	std::vector<UserBucket*> buckets = BuildBucket(pUsers, userCount);

	// 没有用户
	int maxDepth = static_cast<int>(std::ceil(std::log(buckets.size() - 1) / std::log(2))) + 1;
	if (userCount == 0)
	{
		_root = new TreeNode();
	}
	else
	{
		_root = BuildTree(0, buckets.size(), 1, maxDepth, buckets);
	}

	_root->Color = ColorEnum::Black;

	// 填充用户映射
	for (int i = 0; i < userCount; i++)
	{
		_userMap[pUsers[i].Id] = pUsers[i];
	}
}

BucketBRTreeRankingList::~BucketBRTreeRankingList()
{
	delete _root;
}

void BucketBRTreeRankingList::AddUser(const User& user, int& rankCount)
{
	TreeNode* node = _root;
	while (node->Right != nullptr)
	{
		node->Count++;
		if (user < node->Right->LeftUser)
		{
			node = node->Left;
		}
		else
		{
			rankCount += node->Left->Count;
			node = node->Right;
		}
	}

	// 叶子节点
	int userIndexInBucket;
	if (node->IsFull())
	{
		// 分裂TreeNode
		node->Split(user, userIndexInBucket);
		rankCount += userIndexInBucket;

		// 调节树
		if (node->Color == ColorEnum::Red)
		{
			// 红色必定不是根节点，因此父节点必定存在
			TreeNode* parentNode = node->Parent;
			TreeNode* siblingNode = (parentNode->Left == node) ? parentNode->Right : parentNode->Left;

			// 兄弟必定为红色
			assert(siblingNode->Color == ColorEnum::Red);

			node->Color = ColorEnum::Black;
			siblingNode->Color = ColorEnum::Black;
			parentNode->Color = ColorEnum::Red;
			FixAfterAdd(parentNode);
		}
	}
	else
	{
		// 加入bucket
		userIndexInBucket = node->Insert(user);
		rankCount += userIndexInBucket;
	}
}

void BucketBRTreeRankingList::FixAfterAdd(TreeNode* node)
{
	while (node != _root && node->Parent->Color == ColorEnum::Red)
	{
		TreeNode* parentNode = node->Parent;
		// 父亲为红
		TreeNode* grandParentNode = parentNode->Parent;
		TreeNode* uncleNode = (grandParentNode->Left == parentNode) ? grandParentNode->Right : grandParentNode->Left;

		if (uncleNode->Color == ColorEnum::Red)
		{
			// 叔叔为红
			parentNode->Color = ColorEnum::Black;
			uncleNode->Color = ColorEnum::Black;
			grandParentNode->Color = ColorEnum::Red;
			node = grandParentNode;
		}
		else
		{
			// 叔叔为黑
			if (parentNode == grandParentNode->Left)
			{
				if (node == parentNode->Right)
				{
					// 左旋转
					parentNode = RotateLeft(parentNode);
					// node不需要多余赋值
				}

				// 变色
				parentNode->Color = ColorEnum::Black;
				grandParentNode->Color = ColorEnum::Red;
				// 右旋转
				RotateRight(grandParentNode);
			}
			else
			{
				if (node == parentNode->Left)
				{
					// 右旋转
					parentNode = RotateRight(parentNode);
				}

				// 变色
				parentNode->Color = ColorEnum::Black;
				grandParentNode->Color = ColorEnum::Red;
				// 左旋转
				RotateLeft(grandParentNode);
			}

			break;
		}
	}

	_root->Color = ColorEnum::Black;
}

void BucketBRTreeRankingList::RemoveUser(const User& user)
{
	TreeNode* node = _root;
	while (node->Right != nullptr)
	{
		node->Count--;
		node = (user < node->Right->LeftUser) ? node->Left : node->Right;
	}

	// 叶子节点
	node->Remove(user);
	if (node == _root)
		return;

	TreeNode* parent = node->Parent;
	ColorEnum parentColor = parent->Color;
	TreeNode* siblingNode = (parent->Left == node) ? parent->Right : parent->Left;
	ColorEnum siblingColor = siblingNode->Color;

	if (node->IsEmpty())
	{
		parent->MoveFromChild(siblingNode);
		parent->Color = ColorEnum::Black;

		if (parentColor == ColorEnum::Black && siblingColor == ColorEnum::Black)
		{
			// 合并以后就会少了一个黑，需要调整
			FixAfterDel(parent);
		}
	}
	else if (siblingNode->Bucket != nullptr && parent->Count < (BUCKET_SIZE >> 2))
	{
		parent->CombineChild();
		parent->Color = ColorEnum::Black;

		if (parentColor == ColorEnum::Black && siblingColor == ColorEnum::Black)
		{
			// 合并以后就会少了一个黑，需要调整
			FixAfterDel(parent);
		}
	}
}

void BucketBRTreeRankingList::FixAfterDel(TreeNode* node)
{
	while (node != _root && node->Color == ColorEnum::Black)
	{
		TreeNode* parentNode = node->Parent;

		if (node == parentNode->Left)
		{
			TreeNode* siblingNode = parentNode->Right;

			// 兄弟节点为红
			if (siblingNode->Color == ColorEnum::Red)
			{
				// 变色
				siblingNode->Color = ColorEnum::Black;
				parentNode->Color = ColorEnum::Red;
				// 左旋转
				RotateLeft(parentNode);
				siblingNode = parentNode->Right;
			}

			// 兄弟节点为黑
			if (siblingNode->Left->Color == ColorEnum::Black && siblingNode->Right->Color == ColorEnum::Black)
			{
				// 变色
				siblingNode->Color = ColorEnum::Red;
				node = parentNode;
			}
			else
			{
				if (siblingNode->Right->Color == ColorEnum::Black)
				{
					// 变色
					siblingNode->Left->Color = ColorEnum::Black;
					siblingNode->Color = ColorEnum::Red;
					// 右旋转
					siblingNode = RotateRight(siblingNode);
				}

				// 变色
				siblingNode->Color = parentNode->Color;
				parentNode->Color = ColorEnum::Black;
				siblingNode->Right->Color = ColorEnum::Black;
				// 左旋转
				RotateLeft(parentNode);
				node = _root;
			}
		}
		else
		{
			TreeNode* siblingNode = parentNode->Left;

			// 兄弟节点为红
			if (siblingNode->Color == ColorEnum::Red)
			{
				// 变色
				siblingNode->Color = ColorEnum::Black;
				parentNode->Color = ColorEnum::Red;
				// 右旋转
				RotateRight(parentNode);
				siblingNode = parentNode->Left;
			}

			// 兄弟节点为黑
			if (siblingNode->Left->Color == ColorEnum::Black && siblingNode->Right->Color == ColorEnum::Black)
			{
				// 变色
				siblingNode->Color = ColorEnum::Red;
				node = parentNode;
			}
			else
			{
				if (siblingNode->Left->Color == ColorEnum::Black)
				{
					// 变色
					siblingNode->Right->Color = ColorEnum::Black;
					siblingNode->Color = ColorEnum::Red;
					// 左旋转
					siblingNode = RotateLeft(siblingNode);
				}

				// 变色
				siblingNode->Color = parentNode->Color;
				parentNode->Color = ColorEnum::Black;
				siblingNode->Left->Color = ColorEnum::Black;
				// 右旋转
				RotateRight(parentNode);
				node = _root;
			}
		}
	}

	// 根节点
	node->Color = ColorEnum::Black;
}

BucketBRTreeRankingList::TreeNode* BucketBRTreeRankingList::RotateLeft(TreeNode* x)
{
	assert(x->Right != nullptr && x->Left != nullptr);
	assert(x->Right->Left != nullptr && x->Right->Right != nullptr);

	TreeNode* y = x->Right;
	x->Right = y->Left;
	x->Right->Parent = x;
	y->Left = x;
	y->Parent = x->Parent;
	x->Parent = y;

	if (y->Parent != nullptr)
	{
		if (x == y->Parent->Left)
		{
			y->Parent->Left = y;
		}
		else if (x == y->Parent->Right)
		{
			y->Parent->Right = y;
		}
		else
		{
			assert(false);
		}
	}

	x->RightUser = x->Right->RightUser;
	y->LeftUser = x->LeftUser;
	x->Count = x->Left->Count + x->Right->Count;
	y->Count = y->Left->Count + y->Right->Count;

	if (y->Parent == nullptr)
		_root = y;

	return y;
}

BucketBRTreeRankingList::TreeNode* BucketBRTreeRankingList::RotateRight(TreeNode* x)
{
	assert(x->Left != nullptr && x->Right != nullptr);
	assert(x->Left->Left != nullptr && x->Left->Right != nullptr);

	TreeNode* y = x->Left;
	x->Left = y->Right;
	x->Left->Parent = x;
	y->Right = x;
	y->Parent = x->Parent;
	x->Parent = y;

	if (y->Parent != nullptr)
	{
		if (x == y->Parent->Left)
		{
			y->Parent->Left = y;
		}
		else
		{
			y->Parent->Right = y;
		}
	}

	x->LeftUser = x->Left->LeftUser;
	y->RightUser = x->RightUser;
	x->Count = x->Left->Count + x->Right->Count;
	y->Count = y->Left->Count + y->Right->Count;

	if (y->Parent == nullptr)
		_root = y;

	return y;
}

int BucketBRTreeRankingList::AddUser(const User& user)
{
	assert(_userMap.find(user.Id) == _userMap.end());
	_userMap[user.Id] = user;

	int rankCount = 0;
	if (_root->Count == 0)
	{
		UserBucket* bucket = new UserBucket();
		bucket->Insert(user);
		_root->Bucket = bucket;
		_root->Count = 1;
		_root->LeftUser = user;
		_root->RightUser = user;
	}
	else
	{
		AddUser(user, rankCount);
	}

	return rankCount;
}

int BucketBRTreeRankingList::UpdateUser(const User& newUser)
{
	User oldUser = _userMap[newUser.Id];
	RemoveUser(oldUser);

	int rankCount = 0;
	AddUser(newUser, rankCount);

	_userMap[newUser.Id] = newUser;

	return rankCount;
}

int BucketBRTreeRankingList::GetUserRank(int userId)
{
	assert(_userMap.find(userId) != _userMap.end());
	User user = _userMap.at(userId);

	int rankCount = 0;
	TreeNode* node = _root;

	while (node->Right != nullptr)
	{
		assert(node->Left != nullptr && node->Right != nullptr);
		if (user < node->Right->LeftUser)
		{
			node = node->Left;
		}
		else
		{
			rankCount += node->Left->Count;
			node = node->Right;
		}
	}

	UserBucket* bucket = node->Bucket;
	int userIndexInBucket = bucket->IndexOf(user);
	assert(userIndexInBucket >= 0);
	rankCount += userIndexInBucket;

	return rankCount;
}

int BucketBRTreeRankingList::GetTopN(int topN, User* pOutUsers)
{
	if (pOutUsers == nullptr || topN <= 0)
		return 0;

	TreeNode* node = _root;

	// 获取排名靠前的叶子节点
	while (node->Left != nullptr)
	{
		node = node->Left;
	}

	UserBucket* bucket = node->Bucket;
	topN = (std::min)(topN, _root->Count);
	int rankCount = 0;
	int n = (std::min)(bucket->UserCount, topN - rankCount);

	// 复制前n个用户到输出数组
	memcpy(pOutUsers + rankCount,bucket->Users, n * sizeof(User));
	rankCount += n;

	// 缺少的用户数
	while (rankCount < topN)
	{
		// 查找tNode的右区间的叶子节点
		TreeNode* tNode = node;
		while (tNode != tNode->Parent->Left)
		{
			tNode = tNode->Parent;
			if (tNode->Parent == nullptr) // 已经到根节点
				break;
		}

		if (tNode->Parent == nullptr) // 已经到根节点，没有更多用户了
			break;

		tNode = tNode->Parent->Right;
		while (tNode->Left != nullptr)
		{
			tNode = tNode->Left;
		}

		bucket = tNode->Bucket;
		n = (std::min)(bucket->UserCount, topN - rankCount);

		// 复制n个用户到输出数组
		memcpy(pOutUsers + rankCount, bucket->Users, n * sizeof(User));
		rankCount += n;

		node = tNode;
	}

	return rankCount;
}

int BucketBRTreeRankingList::GetArroundUser(int userId, int arroundN, User* pOutUsers)
{
	if (pOutUsers == nullptr || arroundN <= 0)
		return 0;

	assert(_userMap.find(userId) != _userMap.end());
	User user = _userMap.at(userId);

	int rankCount = 0;
	TreeNode* node = _root;

	// 1. 找到对应的位置
	while (node->Right != nullptr)
	{
		assert(node->Left != nullptr && node->Right != nullptr);
		if (user < node->Right->LeftUser)
		{
			node = node->Left;
		}
		else
		{
			rankCount += node->Left->Count;
			node = node->Right;
		}
	}

	UserBucket* bucket = node->Bucket;
	int userIndexInBucket = bucket->IndexOf(user);
	assert(userIndexInBucket >= 0);
	rankCount += userIndexInBucket;

	// 2. 准备结果
	int offset = 0; // 结果数组内的偏移，用于处理用户排名过靠前，存在数据空位的情况
	int leftNum = arroundN, rightNum = arroundN; // 需求数目

	if (rankCount < arroundN)
	{
		// 用户排名过靠前，无法获取足够的左边用户
		leftNum = rankCount;
		offset = rankCount - arroundN;
	}

	if (rankCount + arroundN + 1 > _root->Count)
	{
		// 用户排名过靠后，无法获取足够的右边用户
		rightNum = _root->Count - rankCount - 1;
	}

	std::vector<User> result(leftNum + rightNum + 1);

	// 3. 把桶内的用户填充到结果数组中
	// 左边计数
	int leftCount = (std::min)(userIndexInBucket, leftNum);
	// 右边计数
	int rightCount = (std::min)(bucket->UserCount - userIndexInBucket - 1, rightNum);

	memcpy(result.data() + arroundN - leftCount + offset,
		bucket->Users + userIndexInBucket - leftCount,
		(leftCount + rightCount + 1) * sizeof(User));

	// 4. 获取缺少的用户
	TreeNode* tNode = node;
	while (leftCount < leftNum)
	{
		// 查找tNode的左区间的叶子节点
		while (tNode != tNode->Parent->Right)
		{
			tNode = tNode->Parent;
			if (tNode->Parent == nullptr) // 已经到根节点
				break;
		}

		if (tNode->Parent == nullptr) // 已经到根节点，没有更多用户了
			break;

		tNode = tNode->Parent->Left;
		while (tNode->Right != nullptr)
		{
			tNode = tNode->Right;
		}

		bucket = tNode->Bucket;
		int n = (std::min)(bucket->UserCount, leftNum - leftCount);

		memcpy(result.data() + arroundN - leftCount - n + offset,
			bucket->Users + bucket->UserCount - n,
			n * sizeof(User));
		leftCount += n;
	}

	tNode = node;
	while (rightCount < rightNum)
	{
		// 查找tNode的右区间的叶子节点
		while (tNode != tNode->Parent->Left)
		{
			tNode = tNode->Parent;
			if (tNode->Parent == nullptr) // 已经到根节点
				break;
		}

		if (tNode->Parent == nullptr) // 已经到根节点，没有更多用户了
			break;

		tNode = tNode->Parent->Right;
		while (tNode->Left != nullptr)
		{
			tNode = tNode->Left;
		}

		bucket = tNode->Bucket;
		int n = (std::min)(bucket->UserCount, rightNum - rightCount);

		memcpy(result.data() + arroundN + rightCount + 1 + offset,
			bucket->Users,
			n * sizeof(User));

		rightCount += n;
	}

	// 复制结果到输出数组
	std::copy(result.begin(), result.end(), pOutUsers);

	return result.size();
}

int BucketBRTreeRankingList::GetUserCount()
{
	return _root->Count;
}
