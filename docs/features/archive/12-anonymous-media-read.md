# 12 — Anonymous read of a Public activity's media

Status: **Archived** — merged to `develop` in PR #43 · [00-mission-1-sprint.md](../00-mission-1-sprint.md)
Source: [PRD](../../PRD.md) — Decisions #2/#7/#26/#30 + "Permissions" + "Media storage & delivery".

## Summary

A **reversal**, not a new capability. Decision #2 held that images and videos require sign-in at
*every* visibility level, so a signed-out visitor to a `Public` activity saw its text and its cover
and a padlock reading *"Sign in to view 5 photos and videos."* The user asked for that media to be
visible without signing in, and chose to treat it as a product change rather than a defect: the
behaviour was specified in the PRD, drawn in the Figma export, and asserted by a passing test, so
there was no regression to reproduce.

The rule this feature puts in its place is the one already used for everything else about a `Public`
entry: **the activity's `Type` decides who may read it, media included.** A visitor reads a `Public`
activity's media exactly as they read its text; `Shared` and `Private` media stay out of reach, and
they are answered as **404** rather than 403 so the media surface cannot be used to probe for an
entry.

What makes the change small is that the authorization rule was never the blocker. `VisibleTo`
already answers `Public` for an anonymous caller. What blocked the read was a second, redundant gate
in front of it — the `[Authorize]` route attribute plus a `!caller.IsSignedIn` service guard sitting
immediately **before** the correct `CanRead` call. The feature deletes that gate rather than writing
a new rule.

The `media` container stays private. A SAS URL is still the only way to the bytes, and it is still
minted only after the caller has been authorized to read the activity.

## Story

As a visitor who has not signed in, I want to see the photos and videos on a `Public` activity so
that a shared journal reads as a journal rather than as a sign-up prompt.

## Dependencies

- [06-media-upload](06-media-upload.md) (the listing and the metadata payload)
- [07-sas-delivery](07-sas-delivery.md) (the mint this feature widens the audience of)
- [09-permission-enforcement](09-permission-enforcement.md) (the visibility rule now applied
  to a surface it previously refused outright)

## Acceptance criteria

### The read

- [x] `GET /api/activity/{id}/media` succeeds for a caller with no token when the activity is
      `Public`, returning its items (`An_anonymous_caller_lists_a_public_activitys_media`)
- [x] the same route answers **404** — not 401, not a redacted 200 — for a `Shared` activity, a
      `Private` activity, and an id that names nothing, the three being indistinguishable from one
      another (`An_anonymous_caller_is_refused_a_listing_he_may_not_read`,
      `An_anonymous_caller_cannot_list_or_mint_a_deleted_activitys_media`)
- [x] `GET /api/media/{id}/url` mints a short-lived SAS URL for a caller with no token when the
      item's activity is `Public` (`An_anonymous_caller_may_mint_a_public_activitys_media`)
- [x] the same route answers **404** for an anonymous caller when the activity is `Shared`,
      `Private`, or absent (`An_anonymous_caller_is_refused_before_storage_is_reached`,
      `An_item_that_names_nothing_mints_nothing`)
- [x] the mint still happens **after** the visibility check: for each of those refusals the request
      reaches storage no times — the count is the whole assertion, made by the one recording double
      the strategy sanctions (`An_anonymous_caller_is_refused_before_storage_is_reached`)
- [x] the media of a **soft-deleted** `Public` activity is neither listed nor minted, by any caller
      (`An_anonymous_caller_cannot_list_or_mint_a_deleted_activitys_media`)

### What the write surface keeps

- [x] `POST /api/activity/{id}/media` and `DELETE /api/media/{id}` still answer **401** to a caller
      with no token — the two were not opened, only the two reads
      (`A_media_mutation_route_turns_an_anonymous_caller_away`)
- [x] the anonymous surface is asserted as a closed list of **four** routes, so a later controller
      that forgets `[Authorize]` fails the check rather than answering 200 to the internet
      (`The_routes_an_anonymous_caller_may_reach_are_exactly_four`)

### What a visitor must not be handed

- [x] an anonymous listing carries **no uploader user id** — the property is absent from the
      serialised payload, not sent as null — because the `users` primary key *is* the Entra object
      id (Decision #30, and the key-shape note in the PRD)
      (`An_anonymous_listing_withholds_the_uploaders_id`, `A_withheld_id_is_absent_rather_than_null`)
- [x] an authenticated listing **does** carry it, and every listing carries the uploader's display
      name (`A_signed_in_listing_carries_the_uploaders_id`, `A_listing_names_each_uploader`)
- [x] no response, anonymous or authenticated, carries a blob path
      (`The_response_has_no_blob_path_property`, `A_listing_never_carries_a_blob_path`)

### The app

**Left unticked, and this is the only group that is.** Every criterion above names the test that
holds it; these three name a rendered page, and no tier of this solution renders one. They are
[11](../11-e2e-verification.md)'s manual pass, which the PR that merged this feature did not run —
so the mark here is "not claimed" rather than "not done".

- [ ] a signed-out session renders a `Public` activity's media instead of the sign-in prompt, and
      the prompt no longer exists anywhere in the app
- [ ] the export's copy stops telling a visitor that media needs sign-in, in both `en` and `zh`
- [ ] media still groups by uploader for a signed-out reader, falling back to the display name when
      the id is withheld

## Out of scope, stated rather than implied

- **The `media` container does not become public.** It holds the covers of `Shared` and `Private`
  activities as well as every item (`docs/PRD.md`, "Media storage & delivery"), so a public container
  would expose exactly what this feature exists to keep in.
- **The pre-existing seam drift is not fixed here.** `scripts/web-seam.py` reports two failures on
  `develop` before this branch (`src/web/.figma/make/site.json`, `src/web/.gitignore`); they are
  unrelated and stay.
- **No browser walkthrough.** The API answers above are executed by the test tiers. The rendered
  page is [11](../11-e2e-verification.md)'s manual pass, and this feature does not claim it.
