# SES module (placeholder)

Creates optional SES domain/email identity and a configuration set for alert mail.

**DO NOT `terraform apply` until assessment day.**

## CloudWatch metrics notes

After SES is verified and sending is enabled:

| Metric (AWS/SES) | Why watch it |
|------------------|--------------|
| `Send` | Volume of API sends from the app |
| `Delivery` | Successful handoff to recipient MTA |
| `Bounce` | Hard/soft bounce rate — investigate identity/list hygiene |
| `Complaint` | Spam complaints — throttle / review templates |
| `Reject` | SES rejected before send (policy / content) |
| `Reputation.BounceRate` / `Reputation.ComplaintRate` | Account reputation (config set has reputation metrics on) |

Suggested prep (not applied here):

1. Alarm on bounce/complaint rate thresholds for the `${name_prefix}-alerts` configuration set.
2. Optional SNS topic for bounce/complaint notifications → ops mailbox or incident channel.
3. Dashboards live under CloudWatch → SES or custom namespace once metrics flow.

HIPAA: alert templates must stay free of PHI; SES bodies are not an audit log substitute.
