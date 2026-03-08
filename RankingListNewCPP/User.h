#pragma once
#include <ctime>
struct User
{
	int Id;
	int Score;
	time_t LastActive;

	User(int id = 0, int score = 0, time_t lastActive = 0)
		: Id(id), Score(score), LastActive(lastActive) {
	}

	friend bool operator==(const User& a, const User& b) {
		return a.Id == b.Id;
	}

	friend bool operator<(const User& a, const User& b) {
		if (a.Score != b.Score) {
			return a.Score > b.Score; // Score higher is better
		}
		else if (a.LastActive != b.LastActive) {
			return a.LastActive < b.LastActive; // More recent activity is better
		}
		return a.Id < b.Id; // Tie-breaking by Id
	}
};