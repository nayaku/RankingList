#pragma once
#include <vector>
#include "User.h"

class IRankingList
{
public:
	virtual int AddUser(const User& user) = 0;
	virtual int UpdateUser(const User& user) = 0;
	virtual int GetUserRank(int userId) = 0;
	virtual int GetTopN(int topN, User* pOutUsers) = 0;
	virtual int GetArroundUser(int userId, int arroundN, User* pOutUsers) = 0;
	virtual int GetUserCount() = 0;
	virtual ~IRankingList() = default;
};