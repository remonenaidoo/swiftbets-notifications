SELECT message_id AS MessageId, user_id AS UserId, category AS Category, title AS Title, body AS Body, created_at AS CreatedAt, read_at AS ReadAt
FROM notifications.inbox WHERE user_id = @UserId ORDER BY created_at DESC LIMIT @Limit;
