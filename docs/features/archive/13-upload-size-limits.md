# 13 — Upload size limits become settings

Status: **Archived** — merged to `develop` in PR #50 · [00-mission-2-sprint.md](../00-mission-2-sprint.md)
Source: [PRD](../../PRD.md) — Decision #24 (amended) + "Media storage & delivery" + "API surface"

## Summary

The upload caps are compile-time constants: `Constant.Upload.ImageSizeCapBytes` is `10 MiB` and
`VideoSizeCapBytes` is `200 MiB`, and the refusal message is composed from the same constants. An
operator cannot change either without editing the source and shipping a build, and the numbers
themselves were never chosen — they are what feature 06 happened to write down.

This feature makes both caps **configuration**, raises the defaults, and keeps an **absolute
ceiling** that no setting can exceed. The ceiling is not a formality: it is what still refuses a
runaway or hostile upload, and it is the only value that can live in a route attribute, because
`[RequestSizeLimit]` and `[RequestFormLimits]` take compile-time constants.

The shape to copy is already in the codebase. `SignedUrlLifetime` holds a configured value, a
`Default`, a `Maximum`, and an `Applied` that clamps one to the other — and it exists precisely
because "the cap cannot be raised by configuration and neither caller can round differently". The
upload caps get the same treatment, for the same reason.

## Story

As the operator of this journal I want to raise the upload ceiling without a rebuild, so that a
large photo or a long video can be accepted when the deployment has the room for it — while the
application still refuses an upload that is plainly out of bounds.

## Dependencies

- [06-media-upload](06-media-upload.md) — the upload slice and the caps this feature
  replaces

## The caps

| Constant | Value | Role |
|---|---|---|
| `DefaultImageSizeCapBytes` | `25 * 1024 * 1024` | applied when nothing is configured; replaces today's fixed `10 MiB` |
| `DefaultVideoSizeCapBytes` | `200 * 1024 * 1024` | applied when nothing is configured; **unchanged**, because video behaviour is unchanged in this mission |
| `MaxImageSizeCapBytes` | `100 * 1024 * 1024` | the ceiling, and the value the route attributes carry |
| `MaxVideoSizeCapBytes` | `512 * 1024 * 1024` | the ceiling, and the value the route attributes carry |

Configuration keys are flat and unprefixed, as `MediaUrlTtlMinutes` is: `ImageUploadCapBytes` and
`VideoUploadCapBytes`. They are read **only in the composition root** and parsed into a
`TrailBlaze.Model` type exposing `AppliedImageBytes` and `AppliedVideoBytes`; no library layer sees
`IConfiguration`, which is the rule `SignedUrlLifetime`'s registration already follows.

An absent setting is a **default**, not a startup failure — so these are read the optional way, not
through `RequireSetting`.

## Acceptance criteria

- [x] Both caps are read from configuration at startup and applied to upload validation; no cap is
      read anywhere else, and no library-layer type takes `IConfiguration`
- [x] A cap absent from configuration applies the default for its kind; a value at or below the
      ceiling is honoured exactly; a value above the ceiling is **clamped to the ceiling**, not
      refused and not silently accepted; a non-positive value applies the default
- [x] The two kinds keep **separate** caps: a file over the image cap is refused even when it is
      under the video cap, and the other way round
- [x] A file at the applied cap is accepted, and one byte over it is refused — the boundary itself
      is asserted, not only values inside it
- [x] The refusal names the applied cap, and the message is computed from the cap in force rather
      than from a constant, so a raised cap is reported correctly
- [ ] An upload larger than the ceiling is refused by the pipeline's request-size limit, and one
      **between the applied cap and the ceiling** reaches the service and is refused there with the
      400 above — a bare 413 is never the answer to a file the configured cap would have accepted.
      **Verification-only** — see below; the Api tier has no token and every upload route is
      `[Authorize]`, so the attribute cannot be reached by a test in this solution
- [x] **The cover and avatar routes carry the same request-size limits as media upload.** Today only
      `UploadMedia` does; the two image routes carry neither, because the `10 MiB` image cap sat
      below both Kestrel's default body limit and the form parser's — and both say so in their own
      comments, one of which names raising these numbers as the moment the arrangement has to change.
      Raising the image cap past those defaults would answer an acceptable cover with a bare **413**,
      so both routes take the attributes too, against the image ceiling
- [x] **The avatar route's buffered body is bounded deliberately, not incidentally.** It validates the
      cap against a body already read into memory rather than a streamed one, which its own comment
      records as acceptable *only while the largest upload is 10 MB*. A raised cap makes that
      allocation correspondingly larger, so the ceiling and the buffering are decided together rather
      than the number moving on its own — the route limit is now what bounds it, and the comment at
      `UserController.SetAvatar` says so
- [x] `Constant.Message.ImageTooLarge` and `VideoTooLarge` stop being constants composed from the
      caps — they become methods over the applied cap, and every existing assertion that compared
      against the constant is updated to the method
- [x] The old `ImageSizeCapBytes` / `VideoSizeCapBytes` names are **deleted**, not left beside the
      new ones, so the compiler enumerates every use rather than one of them quietly keeping the old
      number

## Tests (TDD)

- **Unit** — the clamping type is a pure function over two numbers and belongs here: absent gives the
  default, above the ceiling clamps, inside is honoured, non-positive gives the default, and the two
  kinds are held apart. The upload validator's boundary cases (at the cap, one byte over) live here
  too, and existing cap assertions in the service tier's upload tests move from the old constants to
  the applied cap the harness configured.
- **Host** — the composition root resolves the type, reads a configured cap, and falls back to the
  default when the setting is absent.
- **Not container-tagged, not database-tagged.** This feature touches no schema and no blob; every
  claim above is decidable from configuration and a stream.

### Verification-only — the 413-versus-400 criterion

An oversize request on a route being answered with the service's 400 rather than a bare 413, on all
three upload routes, is **not tested and cannot be tested in this solution**. All three routes are
`[Authorize]`, the Api tier holds no token and wires no test authentication scheme, so a request it
sends is refused with 401 before the request-size attribute is ever consulted — the tier's documented
"admission, not answers" limit. Building a test-auth harness to reach one attribute is a larger change
than this feature, so the criterion is verified by hand instead:

```
Verification: 2026-10-05 — not yet run. With a token in hand, POST a file just above the applied cap
and just under the ceiling to /api/activities/{id}/cover, /api/users/me/avatar and
/api/activities/{id}/media, and confirm each answers 400 with the cap named rather than 413.
```

Until that is run the claim is verified by reading the attributes, not by executing a request.

## Notes / non-goals

- **A cap is not a memory bound, and the file says so rather than implying otherwise.** The
  request-size attribute is a *transport* ceiling, and the form parser spools a body over its buffer
  limit to a temp file, so a 512 MB video is disk rather than RAM. The image path is different:
  [14](../14-image-thumbnails.md) decodes the bytes in-process, so the image cap is also a per-request
  memory budget — and a budget is per request, so concurrent uploads multiply it.
- **The avatar route buffers its body, and this feature makes that buffer bigger.** It is the one
  upload path that holds the bytes in memory before validation rather than streaming them, and its
  comment says so and says why it was acceptable at `10 MB`. Raising the cap to 25 MB by default and
  100 MB at the ceiling raises that per-request allocation by the same factor. Streaming it is a
  larger change than this feature and is not done here — but the number should not move without the
  consequence being recorded, which is why it is a criterion above rather than a footnote.
- **The video ceiling is a temp-disk budget too**, against a container's ephemeral volume. Several
  concurrent large uploads can exhaust it. The ceiling is a bound on that, not a promise it is
  ample; a deployment that raises the cap takes the volume with it.
- **The count cap is not touched.** `MediaLimit.PerContributorPerActivity` stays exactly as Decision
  #24 sets it — this feature is about size, and the per-contributor count is a different control with
  a different reason.
- **No new route, no new field, no schema change.** This feature changes where two numbers come from.
- **Not a re-opening of Decision #24's reasoning**, only its numbers: the count was raised and
  re-scoped to per-contributor-per-activity at feature 06's review, and that decision stands.
