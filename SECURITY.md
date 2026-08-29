# Security and privacy reporting

## Reporting a vulnerability or privacy issue

Report security vulnerabilities and privacy concerns **privately** by email to **contact@honest-ink.org** (Honest Ink, Inc.) with the subject line `[FOUNDRY SECURITY]`, with **spacejunk572@gmail.com** as the fallback address. Do not open a public issue for security or privacy reports.

Include what you can: affected component or document, reproduction steps, impact assessment, and any suggested remediation. You should receive an acknowledgment within seven days. Coordinated disclosure is appreciated; you will be credited in the release notes unless you prefer otherwise.

## Especially: student data

If you discover student work, student data, or identifying classroom material anywhere in this repository, its history, its issues, or its CI artifacts, report it privately and immediately as above. Do not quote, screenshot, or redistribute the material in the report beyond what is necessary to locate it. History rewriting and takedown will be handled by the maintainer with priority over all other work.

## Scope

In scope: this repository, its released packages, its CI configuration, the `.ocfproj` format's safe-path handling, recipe validation, the approval-gate architecture, and any data-lane bypass (a path by which Amber or Restricted content could persist, egress, or escape its contract).

Threat model, required controls, and verification obligations are documented in the implementation plan (§7, §18, §19). Prompt-injection resistance, hidden-persistence forensics, and egress allowlisting are release-gated evidence, not aspirations.

## Supported versions

Pre-release: only the latest commit on the default branch is supported. From 1.0 onward, the support and patch policy is published with each release (implementation plan, Gate 3 commitments).
