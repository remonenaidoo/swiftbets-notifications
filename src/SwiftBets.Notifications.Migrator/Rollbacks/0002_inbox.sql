-- Rolls back 0002_inbox. Every customer's in-app messages are lost.
DROP TABLE IF EXISTS notifications.inbox;
