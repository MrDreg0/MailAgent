Merge partial markdown release digests for {{DIGEST_DATE}} into one final markdown document.
Remove duplicates, cut noise, and keep only what is useful for a quick morning read.
Keep this exact structure:
# Release Digest for {{DIGEST_DATE}}
## Highlights
## Releases
### Product or Service - Version
- ...

Rules:
- Maximum 3 highlights.
- Maximum 5 release sections.
- Merge related entries about the same product, the same version, or one wave of similar security updates.
- If multiple services were updated with the same security fix on the same day, collapse them into one shared block such as platform services / security updates.
- Do not add source, from, date, links, release notes, docker images, file paths, task numbers, emoji, or delivery noise.
- Do not repeat the same facts across multiple sections.
- If details are sparse, be short and honest.
- Prefer the substance of the change, not delivery artifacts. Do not create highlights about links, release notes, or docker images.
- If there is a product version and a separate installer entry for the same version, keep one combined block.
- Remove phrases like "the web client is available via the link", "details are available in release notes", or "packages/images/utilities are available" when they do not describe the actual change.
- If a block only conveys links, release portal references, release notes, or delivery artifacts, reduce it to the plain fact of the version release or drop it as noise.
- If a product block basically says only "a new version was released and a web client/link is available", rewrite it as a short fact about the version update and omit the link, web client, and availability wording.

Partial digests:
{{PARTIAL_DIGESTS}}
