# Code signing readiness: what gate 7 actually needs

**Date:** 11 September 2026 · **Kind:** research record · **Status:** no certificate obtained; gate 7 unmoved

This records what was established about obtaining an Authenticode signing certificate for Honest Ink, Inc., and the two conclusions that change what gate 7 is worth pursuing. It is research, not legal or purchasing advice. Every price and vendor rule below was observed on 11 September 2026 from the vendor's own page, is indicative only, and **must be re-checked before anything is paid for**. Nothing here authorizes a purchase, a signature, a tag, a release, or a distribution.

## The question, and the short answer

Can the entity apply for an OV code-signing certificate now, or must it wait for the Form 1023-EZ determination?

**It can apply now, and the determination is irrelevant to issuance.** The strings `501(c)`, `tax-exempt` and `charitable` appear nowhere in the 114-page CA/Browser Forum *Baseline Requirements for the Issuance and Management of Publicly-Trusted Code Signing Certificates*, v3.11.0 (16 June 2026). "Non-profit organizations" appears exactly once, in the § 1.6.1 definition of who counts as an Organizational Applicant. Waiting for the determination would be waiting for nothing.

## Two findings that change the plan

**Signing does not remove the warning, and no certificate will.** Microsoft's *SmartScreen reputation for Windows app developers* page, in its certificate-options table, states that with a valid OV **or EV** certificate an app is "flagged as unrecognized until reputation accumulates." Microsoft is explicit that the historical EV exemption is gone — "that behavior was removed in 2024" — and that "paying a premium for EV solely to avoid SmartScreen warnings is no longer justified." Reputation is accrued, not purchased: Microsoft describes "several weeks and hundreds of clean installs from a wide audience," with no mechanism to request review for consumer endpoints. For a bounded tool aimed at schoolteachers, that audience may never exist.

An earlier reading of gate 7 in this repository treated signing as the cheap unlock for distribution. On the evidence it is necessary but not sufficient, and it does not buy what it was assumed to buy.

**The managed-district path may not depend on it at all.** A district deploying through Intune as a Win32 application, or allowlisting a publisher through the Trusted Publishers store or App Control catalog signing, does not rely on SmartScreen reputation. That path is available to a pilot district today, without any certificate, and should be offered in parallel rather than treated as a fallback.

## The real blocker is not the certificate

It is the **bank account**. The cheapest compliant path, Azure Artifact Signing, requires a paid Azure subscription, and Microsoft's FAQ states that Artifact Signing "doesn't support free, trial, or sponsored Azure subscriptions." A paid subscription needs a payment method; the corporation has none yet. Every other step waits behind that one.

Two further conditions are real but ordinary. Under CSCBR § 3.2.2.1(4), an organization formed less than three years before the request triggers an **additional identity check of the certificate requester** under § 3.2.3.1 — a legible government photo identification plus separate address verification. That applies to this entity until August 2029 and is a paperwork step, not a bar. And CAs will not accept contact details the applicant merely asserts; Sectigo states that "self-provided or unverified contact information cannot be used," which is why an independently verifiable listing matters more than it looks.

Two things that are **not** blockers, contrary to reasonable expectation. The home principal office is permitted: § 3.2.2.1.1 requires only that the address be the applicant's address of existence or operation, sourced from a government agency, a Reliable Data Source, a site visit or an Attestation Letter. The site visit with permanent signage lives only in the EV section, § 3.2.2.2.3.1, and even there a private residence is a facility type to record rather than a disqualifier. Headcount is invisible: nothing in § 3.2.2.1 or § 4.1.1.1 references board size, payroll or banking for a non-EV certificate.

## Hardware key custody is unavoidable and shapes the release

CSCBR § 6.2.7.4.1, effective 1 June 2023 and current in v3.11.0, requires subscriber code-signing private keys to be generated, stored and used in a hardware crypto module certified to at least FIPS 140-2 Level 2 or Common Criteria EAL4+. Three compliant shapes exist: the subscriber's own certified module; a cloud key-generation-and-protection solution where the key never leaves the module boundary and all access is logged; or an audited Signing Service, which must meet the higher FIPS 140-2 Level 3 under § 6.2.7.3. § 6.1.2 forecloses delivery of a private key as a file. A TPM appears only in the pre-June-2023 list.

The practical consequence for this repository: a USB token cannot sign on a GitHub-hosted runner, so token-based signing in CI would require a self-hosted runner holding a cached PIN — worse than the present typist-invoked script. A cloud signing service avoids that entirely.

## The paths, ranked on eligibility rather than price

**Azure Artifact Signing** is most likely correct. No hardware token; keys never released to the subscriber; signs from CI. Microsoft Learn states "Starts at $9.99/month," while the Azure pricing page currently renders a placeholder. The widely repeated three-year organization-age rule **appears to be out of date** — it was a public-preview intake limit; the current prerequisites state only geographic restrictions, and a Microsoft employee answered this precise question on Microsoft Q&A on 17 August 2026 with "no minimum org age restrictions." **That is not settled enough to spend money on** — see the unverified list. Onboarding allows **three document attempts in total**, after which Microsoft "can't proceed further with the onboarding," and every document must be issued within the previous twelve months. Validation takes one to twenty business days and cannot be expedited.

**Conventional OV from a commercial CA** works and is well understood. SSL.com listed OV code signing at $129.00 per year with a stated three-to-five day validation, a YubiKey at $379.00, and eSigner cloud signing separately at $20.00 per month for a twenty-signature tier — all read once, from one vendor page. A release here signs roughly ten to sixteen first-party files, which makes a twenty-signature tier marginal.

**SignPath Foundation** is free and this project's GPL-3.0-or-later licence qualifies, but the certificate is issued to SignPath Foundation, which becomes the publisher of record. A district allowlisting a publisher would allowlist SignPath rather than Honest Ink. It also requires "a certain verifiable reputation" for end-user executables, which is the thing a new application lacks.

**Certum's open-source tier is ineligible** and should not be pursued. Certum's own support documentation states that open-source code-signing certificates "are issued only for individuals," and the subject carries the developer's personal name. Taking it would put a person rather than the corporation on every signature, undoing the reason the entity was formed.

**The district's own certificate** remains a genuine fallback that costs nothing.

EV of any kind is not recommended: its operational-existence test is a harder gate for a new entity and, since 2024, it buys nothing at the SmartScreen layer.

## Consequence for the finalizer, when a path is chosen

`tools/finalize-signed-package.ps1` takes a mandatory `-AllowedSignerThumbprint`, an array of exact forty-character SHA-1 thumbprints, and refuses any first-party file whose signer falls outside that set.

Against a conventional OV certificate that contract is sound and needs re-setting only at reissue, which CSCBR § 6.3.2 now bounds at 460 days for certificates issued on or after 1 March 2026.

Against Azure Artifact Signing it degrades. Microsoft states those certificates "are renewed daily and are valid for only 72 hours," and warns that pinning to a thumbprint "isn't durable"; the durable handle is a custom extended key usage under the prefix `1.3.6.1.4.1.311.97`. A thumbprint pin would have to be read back after signing and passed in, which converts a constraint that refuses an unexpected signer into a record of whatever signed. **If that path is chosen, the finalizer needs an EKU mode alongside the thumbprint mode; the thumbprint mode stays correct for the other path.** No change has been made, because the choice has not been made.

The script's existing timestamp requirement — it refuses any first-party file lacking a `TimeStamperCertificate` — is already correct and becomes load-bearing. With short-lived certificates, a timestamp is the only thing that makes a signature outlive the certificate that made it.

## Unverified; check before buying

- **Artifact Signing's organization-age rule.** Rests on the current prerequisites' silence plus one Microsoft employee's answer; an AI-generated reply on the same thread asserts the opposite, which is probably why the three-year belief persists. Settle it in writing before paying.
- **The $9.99 monthly figure**, which Microsoft Learn states and the Azure pricing page does not currently show.
- **All SSL.com figures**, from one page read once.
- **Whether Maryland SDAT publishes a telephone number** for Department ID `D27600584`; the Business Express page is JavaScript-rendered and could not be read. This determines how much independent-listing work is avoidable.
- **Nonprofit or education discounts** at any CA. No published program was found on any vendor's own site.
- **Whether SignPath Foundation's shared certificate already carries suppressing reputation.** Plausible, untested.
- **Token prices and shipping times** from any CA, which are not confirmable from primary pages.

## What this record is not

It is not a purchase authorization, a vendor selection, or a release decision. It obtains no certificate and moves no gate: gate 7 still requires either a district certificate or an organizational certificate that does not yet exist. It does not make the entity tax-exempt, and nothing here may be read as describing it so while Form 1023-EZ remains undetermined.
