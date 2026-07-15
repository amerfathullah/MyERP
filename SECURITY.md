# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in MyERP, please report it responsibly.

**Do NOT open a public GitHub issue for security vulnerabilities.**

### How to Report

1. Email your findings to the project maintainers with the subject line: `[SECURITY] <brief description>`
2. Include:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

### What to Expect

- **Acknowledgement**: Within 48 hours of your report.
- **Assessment**: We will evaluate the vulnerability and determine severity within 7 days.
- **Resolution**: Critical vulnerabilities will be patched within 14 days. Lower severity issues will be addressed in the next scheduled release.
- **Disclosure**: We will coordinate with you on public disclosure timing after a fix is available.

## Security Practices

### Application Security

- All API endpoints require authentication and authorization via ABP permissions.
- Input validation is enforced at system boundaries using Data Annotations and FluentValidation.
- DTOs are used exclusively for API communication — domain entities are never exposed directly.
- Raw SQL with string concatenation is prohibited; all database access uses parameterized queries via Entity Framework Core.
- Secrets are stored in environment variables or ABP Settings — never in source code.

### Authentication & Authorization

- OAuth2 / OpenID Connect via OpenIddict.
- Role-based and permission-based access control through ABP Identity module.
- Multi-tenancy isolation enforced at the data layer.

### Data Protection

- PostgreSQL with encrypted connections (TLS).
- Audit logging enabled for all sensitive operations via ABP AuditLogging module.
- PDPA compliance for personal data handling (field-level security, consent tracking).
- Financial transactions are immutable — cancellation uses reversal entries, never deletion.

### Dependency Management

- NuGet and npm dependencies are reviewed for known vulnerabilities.
- Dependabot or equivalent tooling is used for automated dependency updates.

## Scope

The following are **in scope** for security reports:

- Authentication/authorization bypasses
- SQL injection, XSS, CSRF
- Sensitive data exposure
- Privilege escalation
- Multi-tenant data leakage
- LHDN e-Invoice signing/submission security issues

The following are **out of scope**:

- Denial of service (DoS) attacks
- Social engineering
- Issues in third-party dependencies with existing public CVEs (report upstream)
- Issues requiring physical access to the server
