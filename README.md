# SFSWebForm

An ASP.NET Core 9 MVC app for Stellantis Financial Services that tracks incidents and sends notification emails about them, hosted on IIS at `/Internal/SFSWebForm/stage`.

## What it does

- Create and track incidents (impacted application/system, causal system, root cause, resources engaged, ETA, next steps, status).
- Each incident automatically generates notification emails (initial outage, updates, resolution) that can be edited, prioritized, and sent to recipients picked from the org directory.
- An update log records the full history of an incident: status updates, resolution, reopening, and every email send/edit/priority/sender change.

## Sign-in

Users sign in with their Microsoft Entra ID (Azure AD) account via OpenID Connect. There is no local user list — anyone in the tenant can sign in, and the signed-in identity is used both to display who's working an incident and, by default, as the "from" address when they send a notification email.

## Sending email

Notifications are sent through Microsoft Graph (`Mail.Send`, Application permission), which lets the app send **as** a specific mailbox rather than through the signed-in user's own SMTP session. For each email you can set:

- **Recipients** — an address-bar-style picker that searches the org directory as you type (or accepts a raw address, e.g. a distribution list) and auto-saves as you add/remove people.
- **Priority** (Normal / Important / Critical) — adds a colored banner to the email, marks it high-importance in Outlook, prefixes the subject line, and (for Critical) turns the email's card red in the UI immediately.
- **Send From** — an optional list of shared mailboxes (configured in `MailSettings:SharedMailboxes`) you can send as, if your Outlook has delegate access to them. If left as "Me", the email sends as whoever clicks Send.

Sender resolution order: an explicitly-picked shared mailbox → the signed-in user's own email → `MailSettings:SenderMailbox` as a last-resort fallback.

## Configuration (`appsettings.json`)

| Section | Key | Purpose |
|---|---|---|
| `AzureAd` | `TenantId` / `ClientId` / `ClientSecret` | Entra ID app registration used for sign-in. Needs a Web platform redirect URI (`https://<host>/signin-oidc`) and delegated `User.Read`/`openid`/`profile`/`email`. |
| `MailSettings` | `TenantId` / `ClientId` / `ClientSecret` | Same app registration, used for Graph `Mail.Send` (Application permission) and directory search. |
| `MailSettings` | `SenderMailbox` | Fallback "from" address if the signed-in user has no email claim. |
| `MailSettings` | `DefaultRecipients` | Used when an email's own recipients field is left blank. |
| `MailSettings` | `SharedMailboxes` | List of `{ "DisplayName": "...", "Address": "..." }` entries selectable in the "Send From" picker. |
| `Testing` | `DebugMode` / `WindowsAuthenticationEnabled` | Set `WindowsAuthenticationEnabled` to `false` to bypass real Entra ID sign-in for testing on a server without a redirect URI configured yet. Must be `true` for any real deployment. |

Directory search (the recipients picker) additionally requires the **`User.Read.All`** Microsoft Graph Application permission, admin-consented, on the same app registration as `MailSettings`.

## Operational note: database schema changes

This app uses EF Core's `EnsureCreated()` rather than migrations, so it only creates the SQLite database file (`sfs_incidents.db`) if it doesn't already exist — it will **not** alter an existing file's schema. After deploying a change that touches a model (a new field, a changed column nullability, etc.), delete `sfs_incidents.db` on the server and let the app recreate it on next startup.

## Changelog

Grouped by feature area, most recent first within each group.

**Directory search**
- Reuse a single `GraphServiceClient` (registered as a singleton) instead of re-authenticating with Azure AD on every keystroke-driven search, which was making results feel slow.
- Fixed the recipients search box wiping out typed text when Enter/comma was pressed before a match was picked.
- Stopped filtering out shared mailboxes (e.g. team inboxes) from directory search results.

**Incident form fields**
- Renamed "Application/System" to "Impacted Application / System"; added a "Causal System" field.
- Made Root Cause, Team/Resources Engaged, ETA, and Next Steps required on both Create and Add Update.
- Changed the default "Sending Team / Group" to "IT Communications".

**Email sending**
- Fixed emails always sending "as" one hardcoded mailbox regardless of who was actually signed in.
- Added a configurable "Send From" shared-mailbox picker per email, for users with Outlook delegate access to other mailboxes.
- Added a per-email Priority (Normal/Important/Critical) with a styled HTML banner, Outlook high-importance flag, and an instant red highlight on Critical in the UI.
- Restyled the HTML email body (bold/underlined headers and labels, proper line breaks) to render correctly in Outlook instead of collapsing into one paragraph.
- Fixed a race condition where clicking Send could read recipients before an in-flight auto-save had landed.
- Fixed Send/EditEmail/recipient search breaking when the app is hosted under an IIS sub-path.

**Recipients**
- Moved recipients from a single incident-level field to a per-email, always-visible, auto-saving address-bar-style picker (replacing the old hardcoded/Edit-gated field).
- Allowed typing a raw address (e.g. a distribution list) in addition to picking directory matches.

**Update log**
- Logs email sends/failures, content edits, priority changes, sender changes, and resolution drafts, in addition to status updates.

**Sign-in**
- Replaced hardcoded/allow-listed login with real Microsoft Entra ID OAuth.
- Added `Testing:DebugMode` / `Testing:WindowsAuthenticationEnabled` config toggles to allow testing without a working Entra ID redirect URI.

**Bug fixes**
- Fixed a `NOT NULL` constraint failure (`IncidentUpdates.NextExpectedUpdate`) that was blocking every email Save and Send, caused by a form-validation attribute unintentionally also constraining the database column.
