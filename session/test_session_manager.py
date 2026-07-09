"""
Unit tests for the Session Management Module.
"""

import os
import shutil
import tempfile
import time
import unittest
from datetime import datetime, timedelta

from session_manager import Session, SessionManager


class TestSession(unittest.TestCase):
    """Tests for the Session class."""

    def test_session_creation(self):
        """Test session is created with correct attributes."""
        session = Session(
            session_id="test-123",
            user_id="user-456",
            data={"key": "value"},
            expires_in=3600,
        )

        self.assertEqual(session.session_id, "test-123")
        self.assertEqual(session.user_id, "user-456")
        self.assertEqual(session.data, {"key": "value"})
        self.assertIsInstance(session.created_at, datetime)
        self.assertIsInstance(session.last_accessed, datetime)
        self.assertIsInstance(session.expires_at, datetime)

    def test_session_not_expired(self):
        """Test session is not expired when within timeout."""
        session = Session(
            session_id="test-123",
            user_id="user-456",
            expires_in=3600,
        )
        self.assertFalse(session.is_expired())

    def test_session_expired(self):
        """Test session is expired when past timeout."""
        session = Session(
            session_id="test-123",
            user_id="user-456",
            expires_in=-1,  # Already expired
        )
        self.assertTrue(session.is_expired())

    def test_session_touch(self):
        """Test touch updates last_accessed timestamp."""
        session = Session(session_id="test-123", user_id="user-456")
        original_accessed = session.last_accessed
        time.sleep(0.1)
        session.touch()
        self.assertGreater(session.last_accessed, original_accessed)

    def test_session_to_dict(self):
        """Test session can be serialized to dictionary."""
        session = Session(
            session_id="test-123",
            user_id="user-456",
            data={"key": "value"},
        )
        data = session.to_dict()

        self.assertEqual(data["session_id"], "test-123")
        self.assertEqual(data["user_id"], "user-456")
        self.assertEqual(data["data"], {"key": "value"})
        self.assertIn("created_at", data)
        self.assertIn("last_accessed", data)
        self.assertIn("expires_at", data)

    def test_session_from_dict(self):
        """Test session can be deserialized from dictionary."""
        original = Session(
            session_id="test-123",
            user_id="user-456",
            data={"key": "value"},
        )
        data = original.to_dict()
        restored = Session.from_dict(data)

        self.assertEqual(restored.session_id, original.session_id)
        self.assertEqual(restored.user_id, original.user_id)
        self.assertEqual(restored.data, original.data)


class TestSessionManager(unittest.TestCase):
    """Tests for the SessionManager class."""

    def setUp(self):
        """Set up test fixtures."""
        self.manager = SessionManager()

    def test_create_session(self):
        """Test session creation returns valid session ID."""
        session_id = self.manager.create_session(
            user_id="user-123",
            data={"key": "value"},
        )

        self.assertIsNotNone(session_id)
        self.assertIsInstance(session_id, str)
        self.assertTrue(len(session_id) > 0)

    def test_get_session(self):
        """Test retrieving a session."""
        session_id = self.manager.create_session(
            user_id="user-123",
            data={"key": "value"},
        )

        session = self.manager.get_session(session_id)

        self.assertIsNotNone(session)
        self.assertEqual(session.session_id, session_id)
        self.assertEqual(session.user_id, "user-123")
        self.assertEqual(session.data, {"key": "value"})

    def test_get_nonexistent_session(self):
        """Test retrieving a non-existent session returns None."""
        session = self.manager.get_session("nonexistent-id")
        self.assertIsNone(session)

    def test_update_session_merge(self):
        """Test updating session with merge."""
        session_id = self.manager.create_session(
            user_id="user-123",
            data={"key1": "value1"},
        )

        result = self.manager.update_session(
            session_id,
            {"key2": "value2"},
            merge=True,
        )

        self.assertTrue(result)
        session = self.manager.get_session(session_id)
        self.assertEqual(session.data, {"key1": "value1", "key2": "value2"})

    def test_update_session_replace(self):
        """Test updating session with replace."""
        session_id = self.manager.create_session(
            user_id="user-123",
            data={"key1": "value1"},
        )

        result = self.manager.update_session(
            session_id,
            {"key2": "value2"},
            merge=False,
        )

        self.assertTrue(result)
        session = self.manager.get_session(session_id)
        self.assertEqual(session.data, {"key2": "value2"})

    def test_update_nonexistent_session(self):
        """Test updating a non-existent session returns False."""
        result = self.manager.update_session(
            "nonexistent-id",
            {"key": "value"},
        )
        self.assertFalse(result)

    def test_delete_session(self):
        """Test deleting a session."""
        session_id = self.manager.create_session(user_id="user-123")

        result = self.manager.delete_session(session_id)

        self.assertTrue(result)
        self.assertIsNone(self.manager.get_session(session_id))

    def test_delete_nonexistent_session(self):
        """Test deleting a non-existent session returns False."""
        result = self.manager.delete_session("nonexistent-id")
        self.assertFalse(result)

    def test_get_user_sessions(self):
        """Test getting all sessions for a user."""
        self.manager.create_session(user_id="user-123")
        self.manager.create_session(user_id="user-123")
        self.manager.create_session(user_id="user-456")

        sessions = self.manager.get_user_sessions("user-123")

        self.assertEqual(len(sessions), 2)
        for session in sessions:
            self.assertEqual(session.user_id, "user-123")

    def test_cleanup_expired(self):
        """Test cleanup of expired sessions."""
        # Create an expired session (negative expires_in ensures it's already expired)
        self.manager.create_session(
            user_id="user-123",
            expires_in=-1,  # Already expired
        )
        # Create a valid session
        valid_id = self.manager.create_session(
            user_id="user-456",
            expires_in=3600,
        )

        removed = self.manager.cleanup_expired()

        self.assertEqual(removed, 1)
        self.assertIsNotNone(self.manager.get_session(valid_id))

    def test_count(self):
        """Test session count."""
        self.manager.create_session(user_id="user-123")
        self.manager.create_session(user_id="user-456")

        self.assertEqual(self.manager.count(), 2)

    def test_clear(self):
        """Test clearing all sessions."""
        self.manager.create_session(user_id="user-123")
        self.manager.create_session(user_id="user-456")

        self.manager.clear()

        self.assertEqual(self.manager.count(), 0)


class TestSessionManagerWithPersistence(unittest.TestCase):
    """Tests for SessionManager with file persistence."""

    def setUp(self):
        """Set up test fixtures."""
        self.temp_dir = tempfile.mkdtemp()
        self.manager = SessionManager(storage_path=self.temp_dir)

    def tearDown(self):
        """Clean up test fixtures."""
        shutil.rmtree(self.temp_dir)

    def test_session_persisted_to_file(self):
        """Test session is saved to file."""
        session_id = self.manager.create_session(user_id="user-123")

        session_file = os.path.join(self.temp_dir, f"{session_id}.json")
        self.assertTrue(os.path.exists(session_file))

    def test_session_deleted_from_file(self):
        """Test session file is deleted."""
        session_id = self.manager.create_session(user_id="user-123")
        session_file = os.path.join(self.temp_dir, f"{session_id}.json")

        self.manager.delete_session(session_id)

        self.assertFalse(os.path.exists(session_file))

    def test_sessions_loaded_on_init(self):
        """Test sessions are loaded from files on initialization."""
        session_id = self.manager.create_session(
            user_id="user-123",
            data={"key": "value"},
        )

        # Create a new manager pointing to the same storage
        new_manager = SessionManager(storage_path=self.temp_dir)

        session = new_manager.get_session(session_id)
        self.assertIsNotNone(session)
        self.assertEqual(session.data, {"key": "value"})


if __name__ == "__main__":
    unittest.main()
