# Stack integration authorization — pull requests 16 through 25

**6 September 2026.** The typist's session-specific authorization for the acts recorded in the
[stacked pull-request containment record](2026-09-06-stacked-pull-request-containment.md).

This record exists because merging is not an act an agent may assume. AGENTS.md's reserved list names
repository visibility, publication, release tagging and distribution, correspondence, public filings
and account creation, and does not name a source merge — but nine of the ten pull request bodies, and
three governance records the merge itself delivered, held "main merge" and "old-PR closure" by name.
The only prior authorization in this stack, the
[recipe replacement authorization](2026-09-05-recipe-replacement-authorization.md), ends at
"This authorizes compatibility implementation" and does not reach a merge. Absence from a list is not
authorization when the records impose a hold; the hold is lifted by a person, in a session, in words.

## What was asked

Whether pull request 25, at exact head `113132957811377e7d217fa6561daeed2314a3b6` — whose own hosted CI
`34052991011` (3,078/3,078) and CodeQL `34052991008` had both concluded success — could be merged into
`main` with a merge commit, landing all fifteen commits of pull requests 16 through 24; whether pull
requests 17 through 24 could be retargeted to `main` beforehand; whether all ten could be closed with a
short public comment each; whether the nine obsolete `codex/*` branches could then be deleted; whether
a follow-up record pull request could be opened and merged; and whether, if deleting a base closed a
dependent, that base could be restored and the request reopened.

The question was put together with the measured case for and against, including six findings from a
union review of the merge as a single change that no pull request body described, and the measured fact
that the green at the tip is masked by test-scheduling isolation rather than cured.

## The authorization, as given

> Authorized, 6 September 2026. Pull request 25 at head 1131329 may be merged into main with a merge
> commit; pull requests 17–24 may be retargeted to main beforehand; all ten may be closed with a short
> public comment each, subject to my reviewing the ten texts together before any is posted; the nine
> obsolete codex/* branches may be deleted afterwards; a follow-up record pull request may be opened
> and merged; and if deleting a base closes a dependent, that base ref may be restored and the request
> reopened.
>
> Conditioned on the containment record naming the six union findings as open items, the press-spec
> overwrite being repaired to strike form, and the @0.2.0 replacements being described as non-default
> and unadmitted but reachable rather than "held."
>
> This authorizes no recipe admission, schema ratification, version tag, release, distribution, site
> publication, correspondence, or council or protected-seat claim.

The comment condition was discharged separately in the same session: the ten drafted texts were put to
the typist together, and authorization to post them was given before any was posted.

## The three conditions, and how each was met

1. **The six union findings named as open items.** Recorded in the containment record under
   *Six findings from the union review, recorded as open items*, each with its measurement and each
   stated as unsettled by the merge.
2. **The press-spec overwrite repaired to strike form.** Thirteen passages in
   [the module specification](../modules/deterministic-press-spec.md) are restored with the deleted
   wording struck in place and a dated note. One passage could not be restored literally without
   reintroducing a string a truth guard forbids in that file; its exact prior wording is preserved,
   struck, in the containment record, and the specification says plainly why it is not repeated there.
   The specification version stamp is deliberately unchanged: versioning is a typist act.
3. **The replacements described as reachable.** The containment record states that the replacement
   recipes are non-default and unadmitted **but reachable in one click** from a Recipe version control
   that ships on every press and every module door and mode, and that a teacher who selects one may
   review, approve, print, export and save with it.

## What this authorization did not confer

No recipe admission, schema ratification, version tag, release, distribution, site publication,
correspondence, public filing, or council or protected-seat claim. It did not lift any compatibility
hold in the implementation register, close any sighting, or diagnose any failure. It authorized a
source merge, the pull-request closures and their comments, the branch deletions, and this record's own
pull request — nothing else.

The site workflow remains `workflow_dispatch` only and was not run. Publication remains a separate act
requiring its own authorization.
