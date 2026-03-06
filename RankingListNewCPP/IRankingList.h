#pragma once
#include <vector>
#include "User.h"

class IRankingList
{
public:
	virtual int AddUser(const User& user) = 0;
	virtual int UpdateUser(const User& user) = 0;
	virtual int GetUserRank(int userId) const = 0;
	virtual int GetTopN(int topN, User* pOutUsers) const = 0;
	virtual int GetArroundUser(int userId, int arroundN, User* pOutUsers) const = 0;
	virtual int GetUserCount() const = 0;
	virtual ~IRankingList() = default;
};