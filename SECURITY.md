# Security Policy

## Supported versions

UnifyECS is currently under active development. There may not be formal
versioned releases yet; security fixes will generally be applied to the main
branch and any maintained release branches.

## Reporting vulnerabilities

If you believe you have found a security vulnerability affecting UnifyECS
or its code generation patterns:

- **Do not** open a public GitHub issue with exploit details.
- Instead, please report the issue privately using GitHub's security advisory feature
  or by contacting the maintainers directly through the contact method
  documented in the repository.

When reporting, please include:

- A description of the issue and potential impact.
- Steps to reproduce, if possible.
- Any relevant environment details (.NET version, OS, ECS backend, etc.).
- Whether the issue affects code generation, runtime, or specific backend adapters.

The maintainers will aim to:

1. Acknowledge receipt of your report.
2. Investigate and confirm the issue.
3. Prepare and publish a fix, along with appropriate release notes.

## Security considerations

UnifyECS generates code at compile-time. When reviewing security:

- Ensure generated code doesn't introduce injection vulnerabilities.
- Validate that source generators handle malformed input gracefully.
- Check that backend adapters properly sanitize user data.
- Review that diagnostics don't leak sensitive information.
