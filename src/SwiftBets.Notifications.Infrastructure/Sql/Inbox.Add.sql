INSERT INTO notifications.inbox (message_id, user_id, category, title, body, created_at)
VALUES (@MessageId, @UserId, @Category, @Title, @Body, @CreatedAt) ON CONFLICT (message_id) DO NOTHING;
