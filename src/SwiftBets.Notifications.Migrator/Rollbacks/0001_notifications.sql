-- Destructive: drops the delivery history and the status projection (rebuilt from identity's events on replay).
DROP TABLE IF EXISTS notifications.account_status;
DROP TABLE IF EXISTS notifications.deliveries;
DROP SCHEMA IF EXISTS notifications;
