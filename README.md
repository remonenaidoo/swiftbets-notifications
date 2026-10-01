# swiftbets-notifications

Sends customer notifications for every SwiftBets service. Services publish `NotificationRequestedV1`; this service renders the template, applies the marketing rules, delivers it, and records what happened.

| Part | What it does |
|---|---|
| `SwiftBets.Notifications.Application` | The decision: a redelivered request is ignored, other channels are skipped, marketing to a restricted account is suppressed, an unrenderable request is rejected, the rest are sent. Templates and the data keys each needs. |
| `SwiftBets.Notifications.Infrastructure` | Kafka consumers for `notifications.notification-requested.v1` and `identity.account-status-changed.v1`, Postgres (`sb_notifications`), SMTP through MailKit |
| `SwiftBets.Notifications.Api` | Health and metrics, and `GET /admin/users/{id}/notifications` for staff with `notifications.deliveries.read` |
| `SwiftBets.Notifications.Migrator` | Postgres migrations with rollbacks; `Migrator:RollbackTo=<n>` reverses them |

- **Channels:** email only for now. Push and in-app requests are recorded as skipped.
- **Provider:** any SMTP relay (Mailpit in dev). With no `Email:SmtpHost`, requests are recorded as skipped, never as sent.
- **Privacy:** message bodies are never stored; only the template, outcome and reason.
- **Metric:** `swiftbets_notifications_deliveries_total{channel,template,status}`.

Images: `ghcr.io/remonenaidoo/swiftbets-notifications` and `swiftbets-notifications-migrator`.

## Build and test

```bash
../swiftbets-platform/scripts/fetch-shared-packages.sh .
dotnet test
```

Integration tests run the real host against Postgres and Redpanda in Docker, with an in-process SMTP server.
