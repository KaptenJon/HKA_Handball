# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| latest  | :white_check_mark: |

Only the latest release receives security updates.

## Reporting a Vulnerability

If you discover a security vulnerability in HKA Handball, please report it responsibly:

1. **Do not** open a public issue.
2. Use [GitHub's private vulnerability reporting](https://github.com/KaptenJon/HKA_Handball/security/advisories/new) to submit the report.
3. Include a clear description of the vulnerability, steps to reproduce, and any potential impact.

You should receive an acknowledgment within 7 days. Once the vulnerability is confirmed, a fix will be released as soon as possible.

## Security Practices

This project follows these security practices:

- **No data collection** — the app runs fully offline with no network calls, analytics, or tracking (see [Privacy Policy](PRIVACY_POLICY.md)).
- **Dependency updates** — [Dependabot](.github/dependabot.yml) is configured to check for NuGet and GitHub Actions updates weekly.
- **Secret management** — signing credentials are stored in GitHub Secrets and never committed to the repository.
- **Keystore protection** — `.gitignore` blocks `*.keystore` and `*.jks` files from being committed.
