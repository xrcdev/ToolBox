using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ToolBox.Session
{
    /// <summary>
    /// Manages user sessions with persistence and thread safety.
    /// </summary>
    public class SessionManager
    {
        private readonly ConcurrentDictionary<string, Session> _sessions;
        private readonly int _sessionTimeout;
        private readonly string? _storagePath;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the SessionManager class.
        /// </summary>
        /// <param name="sessionTimeout">Default session timeout in seconds.</param>
        /// <param name="storagePath">Path to store session files (null for in-memory only).</param>
        public SessionManager(int sessionTimeout = 3600, string? storagePath = null)
        {
            _sessionTimeout = sessionTimeout;
            _storagePath = storagePath;
            _sessions = new ConcurrentDictionary<string, Session>();

            if (!string.IsNullOrEmpty(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
                LoadSessions();
            }
        }

        private void LoadSessions()
        {
            if (string.IsNullOrEmpty(_storagePath))
                return;

            foreach (var sessionFile in Directory.GetFiles(_storagePath, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(sessionFile);
                    var session = JsonSerializer.Deserialize<Session>(json);
                    if (session != null && !session.IsExpired())
                    {
                        _sessions[session.SessionId] = session;
                    }
                    else
                    {
                        File.Delete(sessionFile);
                    }
                }
                catch
                {
                    File.Delete(sessionFile);
                }
            }
        }

        private void SaveSession(Session session)
        {
            if (string.IsNullOrEmpty(_storagePath))
                return;

            var sessionFile = Path.Combine(_storagePath, $"{session.SessionId}.json");
            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(sessionFile, json);
        }

        private void DeleteSessionFile(string sessionId)
        {
            if (string.IsNullOrEmpty(_storagePath))
                return;

            var sessionFile = Path.Combine(_storagePath, $"{sessionId}.json");
            if (File.Exists(sessionFile))
            {
                File.Delete(sessionFile);
            }
        }

        /// <summary>
        /// Creates a new session.
        /// </summary>
        /// <param name="userId">ID of the user to associate with the session.</param>
        /// <param name="data">Custom data to store in the session.</param>
        /// <param name="expiresInSeconds">Session lifetime in seconds (uses default if null).</param>
        /// <returns>The session ID of the newly created session.</returns>
        public string CreateSession(string userId, Dictionary<string, object>? data = null, int? expiresInSeconds = null)
        {
            var sessionId = Guid.NewGuid().ToString();
            var session = new Session(sessionId, userId, data, expiresInSeconds ?? _sessionTimeout);

            lock (_lock)
            {
                _sessions[sessionId] = session;
                SaveSession(session);
            }

            return sessionId;
        }

        /// <summary>
        /// Gets a session by ID.
        /// </summary>
        /// <param name="sessionId">The session ID to look up.</param>
        /// <returns>The session if found and not expired, null otherwise.</returns>
        public Session? GetSession(string sessionId)
        {
            lock (_lock)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    if (session.IsExpired())
                    {
                        DeleteSession(sessionId);
                        return null;
                    }
                    session.Touch();
                    SaveSession(session);
                    return session;
                }
                return null;
            }
        }

        /// <summary>
        /// Updates session data.
        /// </summary>
        /// <param name="sessionId">The session ID to update.</param>
        /// <param name="data">New data to add/update in the session.</param>
        /// <param name="merge">If true, merge with existing data; if false, replace.</param>
        /// <returns>True if the session was updated, false if not found.</returns>
        public bool UpdateSession(string sessionId, Dictionary<string, object> data, bool merge = true)
        {
            lock (_lock)
            {
                var session = GetSession(sessionId);
                if (session == null)
                    return false;

                if (merge)
                {
                    foreach (var kvp in data)
                    {
                        session.Data[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    session.Data = data;
                }

                session.Touch();
                SaveSession(session);
                return true;
            }
        }

        /// <summary>
        /// Deletes a session.
        /// </summary>
        /// <param name="sessionId">The session ID to delete.</param>
        /// <returns>True if the session was deleted, false if not found.</returns>
        public bool DeleteSession(string sessionId)
        {
            lock (_lock)
            {
                if (_sessions.TryRemove(sessionId, out _))
                {
                    DeleteSessionFile(sessionId);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets all sessions for a user.
        /// </summary>
        /// <param name="userId">The user ID to look up sessions for.</param>
        /// <returns>List of sessions belonging to the user.</returns>
        public List<Session> GetUserSessions(string userId)
        {
            lock (_lock)
            {
                return _sessions.Values
                    .Where(s => s.UserId == userId && !s.IsExpired())
                    .ToList();
            }
        }

        /// <summary>
        /// Removes all expired sessions.
        /// </summary>
        /// <returns>Number of sessions removed.</returns>
        public int CleanupExpired()
        {
            lock (_lock)
            {
                var expired = _sessions.Values
                    .Where(s => s.IsExpired())
                    .Select(s => s.SessionId)
                    .ToList();

                foreach (var sessionId in expired)
                {
                    DeleteSession(sessionId);
                }

                return expired.Count;
            }
        }

        /// <summary>
        /// Gets the number of active sessions.
        /// </summary>
        public int Count => _sessions.Count;

        /// <summary>
        /// Removes all sessions.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                var sessionIds = _sessions.Keys.ToList();
                foreach (var sessionId in sessionIds)
                {
                    DeleteSession(sessionId);
                }
            }
        }
    }
}
