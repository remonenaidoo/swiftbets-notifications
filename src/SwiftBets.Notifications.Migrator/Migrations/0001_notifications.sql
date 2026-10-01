CREATE SCHEMA IF NOT EXISTS notifications;

-- One row per request; the notification id makes a redelivered request a no-op. Bodies are never stored.
CREATE TABLE notifications.deliveries
(
    notification_id uuid        PRIMARY KEY,
    user_id         uuid        NOT NULL,
    channel         text        NOT NULL,
    template        text        NOT NULL,
    status          smallint    NOT NULL,
    reason          text        NULL,
    recorded_at     timestamptz NOT NULL
);

CREATE INDEX ix_notifications_deliveries_user ON notifications.deliveries (user_id, recorded_at DESC);

-- Latest account status per user, from identity's status events; read by the marketing check.
CREATE TABLE notifications.account_status
(
    user_id    uuid        PRIMARY KEY,
    status     text        NOT NULL,
    changed_at timestamptz NOT NULL
);
