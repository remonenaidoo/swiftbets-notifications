-- Each customer's in-app messages. The id is derived from the event that caused it, so a redelivery adds nothing.
CREATE TABLE notifications.inbox
(
    message_id uuid        PRIMARY KEY,
    user_id    uuid        NOT NULL,
    category   text        NOT NULL,
    title      text        NOT NULL,
    body       text        NOT NULL,
    created_at timestamptz NOT NULL,
    read_at    timestamptz NULL
);

CREATE INDEX ix_notifications_inbox_user ON notifications.inbox (user_id, created_at DESC);
