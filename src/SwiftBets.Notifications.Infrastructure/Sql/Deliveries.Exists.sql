SELECT EXISTS (SELECT 1 FROM notifications.deliveries WHERE notification_id = @NotificationId);
