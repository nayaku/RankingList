using System.Text;

namespace RankingList
{
    /// <summary>
    /// Binary serializer utility class for efficient binary serialization/deserialization
    /// </summary>
    public static class BinarySerializer
    {
        #region Serialization Methods

        /// <summary>
        /// Serialize an integer to binary
        /// </summary>
        public static void SerializeInt(BinaryWriter writer, int value)
        {
            writer.Write(value);
        }

        /// <summary>
        /// Serialize a long to binary
        /// </summary>
        public static void SerializeLong(BinaryWriter writer, long value)
        {
            writer.Write(value);
        }

        /// <summary>
        /// Serialize a bool to binary (1 byte)
        /// </summary>
        public static void SerializeBool(BinaryWriter writer, bool value)
        {
            writer.Write(value);
        }

        /// <summary>
        /// Serialize a DateTime to binary (ticks as long)
        /// </summary>
        public static void SerializeDateTime(BinaryWriter writer, DateTime value)
        {
            writer.Write(value.Ticks);
        }

        /// <summary>
        /// Serialize a Guid to binary (16 bytes)
        /// </summary>
        public static void SerializeGuid(BinaryWriter writer, Guid value)
        {
            writer.Write(value.ToByteArray());
        }

        /// <summary>
        /// Serialize a string to binary (length + UTF-8 bytes)
        /// </summary>
        public static void SerializeString(BinaryWriter writer, string? value)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }
            
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        /// <summary>
        /// Serialize a User object to binary
        /// </summary>
        public static void SerializeUser(BinaryWriter writer, User user)
        {
            SerializeInt(writer, user.ID);
            SerializeInt(writer, user.Score);
            SerializeDateTime(writer, user.LastActive);
        }

        /// <summary>
        /// Serialize a RankingListSingleResponse object to binary
        /// </summary>
        public static void SerializeRankingListSingleResponse(BinaryWriter writer, RankingListSingleResponse response)
        {
            SerializeUser(writer, response.User);
            SerializeInt(writer, response.Rank);
        }

        /// <summary>
        /// Serialize a RankingListMutiResponse object to binary
        /// </summary>
        public static void SerializeRankingListMutiResponse(BinaryWriter writer, RankingListMutiResponse response)
        {
            // Serialize TopNUsers array
            if (response.TopNUsers == null)
            {
                SerializeInt(writer, 0);
            }
            else
            {
                SerializeInt(writer, response.TopNUsers.Length);
                foreach (var user in response.TopNUsers)
                {
                    SerializeRankingListSingleResponse(writer, user);
                }
            }

            // Serialize RankingAroundUsers array
            if (response.RankingAroundUsers == null)
            {
                SerializeInt(writer, 0);
            }
            else
            {
                SerializeInt(writer, response.RankingAroundUsers.Length);
                foreach (var user in response.RankingAroundUsers)
                {
                    SerializeRankingListSingleResponse(writer, user);
                }
            }

            // Serialize TotalUsers
            SerializeInt(writer, response.TotalUsers);
        }

        /// <summary>
        /// Serialize an array of User objects to binary
        /// </summary>
        public static void SerializeUserArray(BinaryWriter writer, User[]? users)
        {
            if (users == null)
            {
                SerializeInt(writer, 0);
                return;
            }
            
            SerializeInt(writer, users.Length);
            foreach (var user in users)
            {
                SerializeUser(writer, user);
            }
        }

        #endregion

        #region Deserialization Methods

        /// <summary>
        /// Deserialize an integer from binary
        /// </summary>
        public static int DeserializeInt(BinaryReader reader)
        {
            return reader.ReadInt32();
        }

        /// <summary>
        /// Deserialize a long from binary
        /// </summary>
        public static long DeserializeLong(BinaryReader reader)
        {
            return reader.ReadInt64();
        }

        /// <summary>
        /// Deserialize a bool from binary
        /// </summary>
        public static bool DeserializeBool(BinaryReader reader)
        {
            return reader.ReadBoolean();
        }

        /// <summary>
        /// Deserialize a DateTime from binary (ticks as long)
        /// </summary>
        public static DateTime DeserializeDateTime(BinaryReader reader)
        {
            long ticks = reader.ReadInt64();
            return new DateTime(ticks);
        }

        /// <summary>
        /// Deserialize a Guid from binary (16 bytes)
        /// </summary>
        public static Guid DeserializeGuid(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(16);
            return new Guid(bytes);
        }

        /// <summary>
        /// Deserialize a string from binary
        /// </summary>
        public static string? DeserializeString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == -1)
                return null;
            
            byte[] bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Deserialize a User object from binary
        /// </summary>
        public static User DeserializeUser(BinaryReader reader)
        {
            return new User
            {
                ID = DeserializeInt(reader),
                Score = DeserializeInt(reader),
                LastActive = DeserializeDateTime(reader)
            };
        }

        /// <summary>
        /// Deserialize a RankingListSingleResponse object from binary
        /// </summary>
        public static RankingListSingleResponse DeserializeRankingListSingleResponse(BinaryReader reader)
        {
            return new RankingListSingleResponse
            {
                User = DeserializeUser(reader),
                Rank = DeserializeInt(reader)
            };
        }

        /// <summary>
        /// Deserialize a RankingListMutiResponse object from binary
        /// </summary>
        public static RankingListMutiResponse DeserializeRankingListMutiResponse(BinaryReader reader)
        {
            // Deserialize TopNUsers array
            int topNCount = DeserializeInt(reader);
            RankingListSingleResponse[] topNUsers = new RankingListSingleResponse[topNCount];
            for (int i = 0; i < topNCount; i++)
            {
                topNUsers[i] = DeserializeRankingListSingleResponse(reader);
            }

            // Deserialize RankingAroundUsers array
            int aroundCount = DeserializeInt(reader);
            RankingListSingleResponse[] aroundUsers = new RankingListSingleResponse[aroundCount];
            for (int i = 0; i < aroundCount; i++)
            {
                aroundUsers[i] = DeserializeRankingListSingleResponse(reader);
            }

            // Deserialize TotalUsers
            int totalUsers = DeserializeInt(reader);

            return new RankingListMutiResponse
            {
                TopNUsers = topNUsers,
                RankingAroundUsers = aroundUsers,
                TotalUsers = totalUsers
            };
        }

        /// <summary>
        /// Deserialize an array of User objects from binary
        /// </summary>
        public static User[] DeserializeUserArray(BinaryReader reader)
        {
            int count = DeserializeInt(reader);
            User[] users = new User[count];
            for (int i = 0; i < count; i++)
            {
                users[i] = DeserializeUser(reader);
            }
            return users;
        }

        #endregion
    }
}