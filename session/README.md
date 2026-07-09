# Session Management

This folder contains session management utilities for handling user or application sessions.

## Features

- Session creation and management
- Session persistence (file-based storage)
- Session expiration and timeout handling
- Thread-safe session operations

## Usage

### Python

```python
from session_manager import SessionManager

# Create a new session manager
manager = SessionManager()

# Create a new session
session_id = manager.create_session(user_id="user123", data={"key": "value"})

# Get session data
session = manager.get_session(session_id)

# Update session
manager.update_session(session_id, {"new_key": "new_value"})

# Delete session
manager.delete_session(session_id)
```

### C# (.NET)

```csharp
using ToolBox.Session;

// Create a new session manager
var manager = new SessionManager();

// Create a new session
var sessionId = manager.CreateSession("user123", new Dictionary<string, object> {
    { "key", "value" }
});

// Get session data
var session = manager.GetSession(sessionId);

// Update session
manager.UpdateSession(sessionId, new Dictionary<string, object> {
    { "new_key", "new_value" }
});

// Delete session
manager.DeleteSession(sessionId);
```

## Session Structure

Each session contains:
- `session_id`: Unique identifier for the session
- `user_id`: ID of the user associated with the session
- `created_at`: Timestamp when the session was created
- `last_accessed`: Timestamp of the last session access
- `expires_at`: Timestamp when the session will expire
- `data`: Custom data associated with the session

## Configuration

You can configure the session manager with custom settings:

```python
manager = SessionManager(
    session_timeout=3600,  # Session timeout in seconds (default: 1 hour)
    storage_path="./sessions"  # Path to store session files
)
```

## License

This project is licensed under GPL-3.0 - see the [LICENSE](../LICENSE) file for details.
