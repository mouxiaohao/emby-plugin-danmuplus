## Why

In 2.0.3r8, a manual custom Season search can successfully receive provider results and still show zero candidates. A live Emby reproduction with `one punch` and `one punch man` proved that DandanPlay returns all seasons, including Season 3, but the post-search eligibility gate rejects the Chinese result titles because they are not textually similar to the English alias used as the custom keyword.

## What Changes

- Treat a manual custom keyword as provider-discovery input rather than a second hard title-identity requirement after the provider has returned valid results.
- Keep identifier, title-presence, media-type, provider-failure, global scoring, and confidence rules intact.
- Continue to apply strict metadata title eligibility to automatic library-import searches that do not have an explicit user-entered keyword.
- Preserve spaces, literal `+`, punctuation, and non-ASCII text through the existing provider request paths; no encoding rewrite is required because live official DandanPlay tests confirmed `%20`, form-style `+`, and literal-plus `%2B` all work.
- Add regression coverage for cross-language aliases and punctuation in the exact manual MatchPreview path.

## Capabilities

### New Capabilities

### Modified Capabilities

- `season-danmu-matching`: manual custom search SHALL retain valid provider-returned candidates even when the query is an alias or another language, while automatic search retains metadata-based eligibility.

## Impact

- Affects Season manual MatchPreview candidate eligibility and its deterministic tests.
- Does not change provider HTTP contracts, persisted bindings, candidate scoring, automatic matching safety, episode mapping, or download behavior.
- Requires live verification on the deployed Emby workflow with DandanPlay and representative enabled providers.
