SELECT event AS Event, email AS Email, in_app AS InApp FROM notifications.preferences WHERE user_id = @UserId;
