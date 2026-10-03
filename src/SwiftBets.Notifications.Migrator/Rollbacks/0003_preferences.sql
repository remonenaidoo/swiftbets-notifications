-- Rolls back 0003_preferences. Customers return to receiving every message on both channels.
DROP TABLE IF EXISTS notifications.preferences;
