"""
Session Management Module

This module provides a simple session management system for handling
user or application sessions with persistence and timeout support.
"""

import json
import os
import threading
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Dict, Optional


def _utcnow() -> datetime:
    """Get current UTC time as timezone-aware datetime."""
    return datetime.now(timezone.utc)


class Session:
    """Represents a user session with associated data."""

    def __init__(
        self,
        session_id: str,
        user_id: str,
        data: Optional[Dict[str, Any]] = None,
        expires_in: int = 3600,
    ):
        """
        Initialize a new session.

        Args:
            session_id: Unique identifier for the session
            user_id: ID of the user associated with the session
            data: Custom data to store in the session
            expires_in: Session lifetime in seconds (default: 1 hour)
        """
        self.session_id = session_id
        self.user_id = user_id
        self.data = data or {}
        self.created_at = _utcnow()
        self.last_accessed = self.created_at
        self.expires_at = self.created_at + timedelta(seconds=expires_in)

    def is_expired(self) -> bool:
        """Check if the session has expired."""
        return _utcnow() > self.expires_at

    def touch(self) -> None:
        """Update the last accessed timestamp."""
        self.last_accessed = _utcnow()

    def to_dict(self) -> Dict[str, Any]:
        """Convert session to dictionary for serialization."""
        return {
            "session_id": self.session_id,
            "user_id": self.user_id,
            "data": self.data,
            "created_at": self.created_at.isoformat(),
            "last_accessed": self.last_accessed.isoformat(),
            "expires_at": self.expires_at.isoformat(),
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Session":
        """Create a session from a dictionary."""
        session = cls(
            session_id=data["session_id"],
            user_id=data["user_id"],
            data=data.get("data", {}),
        )
        session.created_at = datetime.fromisoformat(data["created_at"])
        session.last_accessed = datetime.fromisoformat(data["last_accessed"])
        session.expires_at = datetime.fromisoformat(data["expires_at"])
        return session


class SessionManager:
    """Manages user sessions with persistence and thread safety."""

    def __init__(
        self,
        session_timeout: int = 3600,
        storage_path: Optional[str] = None,
    ):
        """
        Initialize the session manager.

        Args:
            session_timeout: Default session timeout in seconds
            storage_path: Path to store session files (None for in-memory only)
        """
        self.session_timeout = session_timeout
        self.storage_path = Path(storage_path) if storage_path else None
        self._sessions: Dict[str, Session] = {}
        self._lock = threading.RLock()

        if self.storage_path:
            self.storage_path.mkdir(parents=True, exist_ok=True)
            self._load_sessions()

    def _load_sessions(self) -> None:
        """Load existing sessions from storage."""
        if not self.storage_path:
            return

        for session_file in self.storage_path.glob("*.json"):
            try:
                with open(session_file, "r") as f:
                    data = json.load(f)
                    session = Session.from_dict(data)
                    if not session.is_expired():
                        self._sessions[session.session_id] = session
                    else:
                        session_file.unlink()
            except (json.JSONDecodeError, KeyError):
                session_file.unlink()

    def _save_session(self, session: Session) -> None:
        """Save a session to storage."""
        if not self.storage_path:
            return

        session_file = self.storage_path / f"{session.session_id}.json"
        with open(session_file, "w") as f:
            json.dump(session.to_dict(), f, indent=2)

    def _delete_session_file(self, session_id: str) -> None:
        """Delete a session file from storage."""
        if not self.storage_path:
            return

        session_file = self.storage_path / f"{session_id}.json"
        if session_file.exists():
            session_file.unlink()

    def create_session(
        self,
        user_id: str,
        data: Optional[Dict[str, Any]] = None,
        expires_in: Optional[int] = None,
    ) -> str:
        """
        Create a new session.

        Args:
            user_id: ID of the user to associate with the session
            data: Custom data to store in the session
            expires_in: Session lifetime in seconds (uses default if None)

        Returns:
            The session ID of the newly created session
        """
        session_id = str(uuid.uuid4())
        session = Session(
            session_id=session_id,
            user_id=user_id,
            data=data,
            expires_in=expires_in or self.session_timeout,
        )

        with self._lock:
            self._sessions[session_id] = session
            self._save_session(session)

        return session_id

    def get_session(self, session_id: str) -> Optional[Session]:
        """
        Get a session by ID.

        Args:
            session_id: The session ID to look up

        Returns:
            The session if found and not expired, None otherwise
        """
        with self._lock:
            session = self._sessions.get(session_id)
            if session:
                if session.is_expired():
                    self.delete_session(session_id)
                    return None
                session.touch()
                self._save_session(session)
            return session

    def update_session(
        self,
        session_id: str,
        data: Dict[str, Any],
        merge: bool = True,
    ) -> bool:
        """
        Update session data.

        Args:
            session_id: The session ID to update
            data: New data to add/update in the session
            merge: If True, merge with existing data; if False, replace

        Returns:
            True if the session was updated, False if not found
        """
        with self._lock:
            session = self.get_session(session_id)
            if not session:
                return False

            if merge:
                session.data.update(data)
            else:
                session.data = data

            session.touch()
            self._save_session(session)
            return True

    def delete_session(self, session_id: str) -> bool:
        """
        Delete a session.

        Args:
            session_id: The session ID to delete

        Returns:
            True if the session was deleted, False if not found
        """
        with self._lock:
            if session_id in self._sessions:
                del self._sessions[session_id]
                self._delete_session_file(session_id)
                return True
            return False

    def get_user_sessions(self, user_id: str) -> list[Session]:
        """
        Get all sessions for a user.

        Args:
            user_id: The user ID to look up sessions for

        Returns:
            List of sessions belonging to the user
        """
        with self._lock:
            return [
                session
                for session in self._sessions.values()
                if session.user_id == user_id and not session.is_expired()
            ]

    def cleanup_expired(self) -> int:
        """
        Remove all expired sessions.

        Returns:
            Number of sessions removed
        """
        with self._lock:
            expired = [
                session_id
                for session_id, session in self._sessions.items()
                if session.is_expired()
            ]
            for session_id in expired:
                self.delete_session(session_id)
            return len(expired)

    def count(self) -> int:
        """Get the number of active sessions."""
        with self._lock:
            return len(self._sessions)

    def clear(self) -> None:
        """Remove all sessions."""
        with self._lock:
            for session_id in list(self._sessions.keys()):
                self.delete_session(session_id)
