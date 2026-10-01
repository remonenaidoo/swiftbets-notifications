INSERT INTO notifications.account_status (user_id, status, changed_at)
VALUES (@UserId, @Status, @ChangedAt)
ON CONFLICT (user_id) DO UPDATE SET status = excluded.status, changed_at = excluded.changed_at
WHERE notifications.account_status.changed_at < excluded.changed_at;
