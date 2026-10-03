UPDATE notifications.inbox SET read_at = COALESCE(read_at, @ReadAt) WHERE user_id = @UserId AND message_id = @MessageId;
