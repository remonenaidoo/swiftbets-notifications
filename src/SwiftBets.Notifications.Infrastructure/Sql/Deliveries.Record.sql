INSERT INTO notifications.deliveries (notification_id, user_id, channel, template, status, reason, recorded_at)
VALUES (@NotificationId, @UserId, @Channel, @Template, @Status, @Reason, @RecordedAt)
ON CONFLICT (notification_id) DO NOTHING;
