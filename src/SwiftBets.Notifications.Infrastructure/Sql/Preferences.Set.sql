INSERT INTO notifications.preferences (user_id, event, email, in_app, updated_at) VALUES (@UserId, @Event, @Email, @InApp, now())
ON CONFLICT (user_id, event) DO UPDATE SET email = excluded.email, in_app = excluded.in_app, updated_at = excluded.updated_at;
