﻿namespace Apollo.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Web.Helpers;
    using Apollo.Models.Apollo;
    using MySql.Data.MySqlClient;

    public class ApolloDBHandler : IDisposable
    {
        private static readonly string ConnectionKey = ConfigurationManager.AppSettings["DatabaseConnectionString"];
        private MySqlConnection dbConnection;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApolloDBHandler"/> class.
        /// </summary>
        public ApolloDBHandler()
        {
            // Connect to the database and open the connection.
            dbConnection = new MySqlConnection(ConnectionKey);
            dbConnection.Open();
        }

        public enum BridgingTables
        {
            LIKED_ALBUMS, PASSED_ALBUMS, RECOMMEND
        }

        /// <summary>
        /// Gets the user_id from the database using the associated username.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <returns>The user_id</returns>
        public string GetUserID(string username)
        {
            // Setup SQL query.
            string sql = "SELECT user_id FROM user WHERE username = @username";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@username", username);

            // Query Database.
            object result = query.ExecuteScalar();

            // Get result.
            if (result != null)
            {
                return result.ToString();
            }
            else
            {
                throw new ArgumentException($"Unable to retrieve user_id for {username}", "username");
            }
        }

        /// <summary>
        /// Inserts the album into the database using the provided album model.
        /// </summary>
        /// <param name="album">The album model.</param>
        public void InsertAlbum(Album album)
        {
            // Setup SQL query.
            string sql = "INSERT INTO albums (albumName, albumURI, albumArtist, albumImageLink) VALUES (@albumName, @albumURI, @albumArtist, @albumImageLink)";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@albumName", album.Name);
            query.Parameters.AddWithValue("@albumURI", album.Uri);
            query.Parameters.AddWithValue("@albumArtist", album.Artist);
            query.Parameters.AddWithValue("@albumImageLink", album.ImageLink);

            // Query Database.
            query.ExecuteNonQuery();
        }

        /// <summary>
        /// Bridges the user and album identifier using an album ID.
        /// </summary>
        /// <param name="userID">The user identifier.</param>
        /// <param name="albumID">The album identifier.</param>
        /// <param name="table">The table.</param>
        public void BridgeUserAndAlbum_AlbumID(string userID, string albumID, BridgingTables table)
        {
            // Setup SQL query.
            string sql = $"INSERT INTO {table.ToString().ToLower()} (user_id, album_id) VALUES (@userID, @albumID)";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@userID", userID);
            query.Parameters.AddWithValue("@albumID", albumID);

            // Query Database.
            query.ExecuteNonQuery();
        }

        /// <summary>
        /// Bridges the user and album identifier using a album URI.
        /// </summary>
        /// <param name="userID">The user identifier.</param>
        /// <param name="albumURI">The album URI.</param>
        /// <param name="table">The table.</param>
        public void BridgeUserAndAlbum_AlbumURI(string userID, string albumURI, BridgingTables table)
        {
            // Setup SQL query.
            string sql = $"INSERT INTO {table.ToString().ToLower()} (user_id, album_id) VALUES (@userID, (SELECT album_id FROM albums WHERE albumURI = @albumURI))";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@userID", userID);
            query.Parameters.AddWithValue("@albumURI", albumURI);

            // Query Database.
            query.ExecuteNonQuery();
        }

        /// <summary>
        /// Gets all albums from one of the bridging tables.
        /// </summary>
        /// <param name="userID">The user identifier.</param>
        /// <param name="table">The bridging table.</param>
        /// <returns>A List of Albums retrieved from the specified table.</returns>
        public List<Album> GetAlbumsFromBridge(string userID, BridgingTables table)
        {
            // Setup SQL query.
            string sql = $"SELECT albumName, albumArtist, albumURI, albumImageLink FROM albums JOIN {table.ToString().ToLower()} USING (album_id) WHERE user_id = @userID";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@userID", userID);

            // Query Database.
            using (MySqlDataReader results = query.ExecuteReader())
            {
                // Read results into List of Albums.
                List<Album> albums = new List<Album>();
                while (results.Read())
                {
                    albums.Add(new Album(results.GetString(0), results.GetString(1), results.GetString(2), results.GetString(3)));
                }

                return albums;
            }
        }

        public List<Album> GetAlbumsFromBridge(string userID, BridgingTables table, int limit, int offset)
        {
            // Setup SQL query.
            string sql = $"SELECT albumName, albumArtist, albumURI, albumImageLink FROM albums JOIN {table.ToString().ToLower()} USING (album_id) WHERE user_id = @userID LIMIT {offset}, {limit}";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@userID", userID);

            // Query Database.
            using (MySqlDataReader results = query.ExecuteReader())
            {
                // Read results into List of Albums.
                List<Album> albums = new List<Album>();
                while (results.Read())
                {
                    albums.Add(new Album(results.GetString(0), results.GetString(1), results.GetString(2), results.GetString(3)));
                }

                return albums;
            }
        }

        /// <summary>
        /// Logs in the specified username with the provided password.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>The user_id</returns>
        public string Login(string username, string password)
        {
            // Setup SQL query.
            string sql = "SELECT user_id, password FROM user WHERE username = @username";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@username", username);

            // Query Database.
            using (MySqlDataReader results = query.ExecuteReader())
            {
                if (results.Read())
                {
                    // Check the password.
                    if (Crypto.VerifyHashedPassword(results.GetString(1), password))
                    {
                        return results.GetString(0);
                    }
                    else
                    {
                        throw new Exception($"Invalid password for {username}.");
                    }
                }
                else
                {
                    throw new ArgumentException("Username does not exist.");
                }
            }
        }

        /// <summary>
        /// Registers the specified username.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="email">The email.</param>
        /// <returns>The user_id for the newly registered user</returns>
        public string Register(string username, string password, string email)
        {
            // Setup SQL query.
            string sql = "INSERT INTO user (username, password, email) VALUES (@username, @password, @email); SELECT LAST_INSERT_ID();";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@username", username);
            query.Parameters.AddWithValue("@password", password);
            query.Parameters.AddWithValue("@email", email);

            // Query database and return the new user_id.
            return query.ExecuteScalar().ToString();
        }

        /// <summary>
        /// Gets the album from the database using the albumURI.
        /// </summary>
        /// <param name="albumURI">The album URI.</param>
        /// <returns>The specified album</returns>
        public Album GetAlbum(string albumURI)
        {
            // Setup SQL query.
            string sql = "SELECT albumName, albumArtist, albumURI, albumImageLink FROM albums WHERE albumURI = @albumUri";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@albumUri", albumURI);

            // Query database.
            using (MySqlDataReader result = query.ExecuteReader())
            {
                if (!result.Read())
                {
                    throw new ArgumentException("Cannot find album in database");
                }

                // Create the album model and return.
                return new Album(result.GetString(0), result.GetString(1), result.GetString(2), result.GetString(3));
            }
        }

        /// <summary>
        /// Gets all listened albums in the database.
        /// </summary>
        /// <param name="userID">The user identifier.</param>
        /// <returns>List of all albums related to the userID</returns>
        public List<Album> GetAllListenedAlbums(string userID)
        {
            // Setup SQL query.
            string sql = $"SELECT albumName, albumArtist, albumURI, albumImageLink FROM albums " +
                         $"LEFT JOIN {BridgingTables.LIKED_ALBUMS.ToString().ToLower()} USING (album_id) " +
                         $"LEFT JOIN {BridgingTables.PASSED_ALBUMS.ToString().ToLower()} USING (album_id) " +
                         $"LEFT JOIN {BridgingTables.RECOMMEND.ToString().ToLower()} USING (album_id) " +
                         $"WHERE {BridgingTables.LIKED_ALBUMS.ToString().ToLower()}.user_id = @userID " +
                         $"OR {BridgingTables.PASSED_ALBUMS.ToString().ToLower()}.user_id = @userID " +
                         $"OR {BridgingTables.RECOMMEND.ToString().ToLower()}.user_id = @userID ";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@userID", userID);

            // Query Database.
            using (MySqlDataReader results = query.ExecuteReader())
            {
                // Read results into List of Albums.
                List<Album> albums = new List<Album>();
                while (results.Read())
                {
                    albums.Add(new Album(results.GetString(0), results.GetString(1), results.GetString(2), results.GetString(3)));
                }

                return albums;
            }
        }

        /// <summary>
        /// Gets the email associated with the userID.
        /// </summary>
        /// <param name="userID">The user identifier.</param>
        /// <returns>The email associated with the user identifier.</returns>
        public string GetEmail(string userID)
        {
            // Setup SQL query.
            string sql = "SELECT email FROM user WHERE user_id = @userID";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@userID", userID);

            // Query database.
            string result = query.ExecuteScalar() as string;

            if (result == null)
            {
                throw new ArgumentException("Cannot find email from user_id");
            }

            // Create the album model and return.
            return result;
        }

        /// <summary>
        /// Changes the password for a user.
        /// </summary>
        /// <param name="userID">The user identifier.</param>
        /// <param name="oldPass">The old password.</param>
        /// <param name="newPass">The new password.</param>
        /// <returns>True if the password is changed succesfully. False otherwise.</returns>
        public bool ChangePassword(string userID, string oldPass, string newPass)
        {
            // Setup SQL query.
            string sql = "SELECT password FROM user WHERE user_id = @userID";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@userID", userID);

            // Query Database.
            string hashedPass = query.ExecuteScalar() as string;

            // Check passwords.
            if (Crypto.VerifyHashedPassword(hashedPass, oldPass))
            {
                // Setup SQL query.
                sql = "UPDATE user SET password = @newPass WHERE user_id = @userID";
                query = new MySqlCommand(sql, dbConnection);
                query.Parameters.AddWithValue("@userID", userID);
                query.Parameters.AddWithValue("@newPass", Crypto.HashPassword(newPass));

                // Query database.
                int result = query.ExecuteNonQuery();

                return result == 1;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Changes the email.
        /// </summary>
        /// <param name="userID">The user identifier.</param>
        /// <param name="newEmail">The new email.</param>
        /// <returns>True if the email is succesfully changed. False otherwise.</returns>
        public bool ChangeEmail(string userID, string newEmail)
        {
            // Setup SQL query.
            string sql = "UPDATE user SET email = @newEmail WHERE user_id = @userID";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@userID", userID);
            query.Parameters.AddWithValue("@newEmail", newEmail);

            // Query database.
            int result;
            try
            {
                result = query.ExecuteNonQuery();
                return result == 1;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Removes an album from a bridging table.
        /// </summary>
        /// <param name="userID">The user identifier.</param>
        /// <param name="albumURI">The album URI.</param>
        /// <param name="table">The bridging table.</param>
        /// <returns>True if the album is removed. False otherwise.</returns>
        public bool RemoveAlbumFromBridge(string userID, string albumURI, BridgingTables table)
        {
            // Setup SQL query.
            string sql = $"DELETE FROM {table.ToString().ToLower()} WHERE user_id = @userID AND album_id = (SELECT album_id FROM albums WHERE albumURI = @albumURI)";
            MySqlCommand query = new MySqlCommand(sql, dbConnection);
            query.Parameters.AddWithValue("@userID", userID);
            query.Parameters.AddWithValue("@albumURI", albumURI);

            // Query database.
            int result = query.ExecuteNonQuery();

            return result == 1;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            dbConnection.Clone();
        }
    }
}