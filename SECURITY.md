# Security Policy

## Supported versions

This repository is a portfolio and learning project rather than a deployed
production service. Security fixes are applied to the latest revision on
`main`; older revisions are not maintained as supported releases.

| Version | Supported |
| --- | --- |
| Latest `main` | Yes |
| Earlier revisions | No |

## Reporting a vulnerability

Please use GitHub's private vulnerability reporting feature when it is enabled
for the repository. If private reporting is unavailable, contact the repository
owner privately through their GitHub profile before opening a public issue.

Do not include exploit details, credentials, personal data, or active secrets in
public issues or pull requests. A useful report includes the affected component,
reproduction conditions, potential impact, and a suggested mitigation when
available.

## Scope notes

The current project intentionally omits authentication, authorization, TLS
termination, rate limiting, distributed tracing backends, and production secret
management. These limitations are documented and should be considered before
adapting the code for a deployed environment.
