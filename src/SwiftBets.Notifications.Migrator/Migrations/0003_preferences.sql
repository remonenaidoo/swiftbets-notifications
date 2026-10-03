-- Where a customer wants to hear about each kind of event. No row means both email and in-app.
CREATE TABLE notifications.preferences
(
    user_id    uuid        NOT NULL,
    event      text        NOT NULL,
    email      boolean     NOT NULL,
    in_app     boolean     NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (user_id, event)
);
