SELECT notification_id AS "NotificationId", user_id AS "UserId", channel AS "Channel", template AS "Template",
       status AS "Status", reason AS "Reason", recorded_at AS "RecordedAt"
FROM notifications.deliveries
WHERE user_id = @UserId
ORDER BY recorded_at DESC
LIMIT @Limit;
