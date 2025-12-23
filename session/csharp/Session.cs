using System;
using System.Collections.Generic;

namespace ToolBox.Session
{
    /// <summary>
    /// Represents a user session with associated data.
    /// </summary>
    public class Session
    {
        /// <summary>
        /// Gets or sets the unique identifier for the session.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user associated with the session.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the custom data stored in the session.
        /// </summary>
        public Dictionary<string, object> Data { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the session was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last session access.
        /// </summary>
        public DateTime LastAccessed { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the session will expire.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the Session class.
        /// </summary>
        public Session()
        {
            Data = new Dictionary<string, object>();
        }

        /// <summary>
        /// Initializes a new instance of the Session class with specified parameters.
        /// </summary>
        /// <param name="sessionId">Unique identifier for the session.</param>
        /// <param name="userId">ID of the user associated with the session.</param>
        /// <param name="data">Custom data to store in the session.</param>
        /// <param name="expiresInSeconds">Session lifetime in seconds.</param>
        public Session(string sessionId, string userId, Dictionary<string, object>? data = null, int expiresInSeconds = 3600)
        {
            SessionId = sessionId;
            UserId = userId;
            Data = data ?? new Dictionary<string, object>();
            CreatedAt = DateTime.UtcNow;
            LastAccessed = CreatedAt;
            ExpiresAt = CreatedAt.AddSeconds(expiresInSeconds);
        }

        /// <summary>
        /// Checks if the session has expired.
        /// </summary>
        /// <returns>True if the session has expired, false otherwise.</returns>
        public bool IsExpired()
        {
            return DateTime.UtcNow > ExpiresAt;
        }

        /// <summary>
        /// Updates the last accessed timestamp.
        /// </summary>
        public void Touch()
        {
            LastAccessed = DateTime.UtcNow;
        }
    }
}
