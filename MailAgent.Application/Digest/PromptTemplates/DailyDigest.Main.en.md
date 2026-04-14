Prepare a short morning markdown release digest for {{DIGEST_DATE}}.
Goal: help the reader quickly understand the most important changes of the day without reading every email.
Return markdown only, with no explanations outside the document.
The response structure must be exactly:
# Release Digest for {{DIGEST_DATE}}

## Highlights
- maximum 3 short bullets with the most important changes of the day.

## Releases
### Product or Service - Version
- 1-2 short useful bullets about the actual substance of the change.

Rules:
- Do not invent versions, details, or reasons for importance that are not present in the emails.
- Do not add source, from, date, links, release notes, docker images, file paths, portals, task numbers, work item ids, or other delivery noise.
- Do not use emoji, marketing language, or words like "urgent", "critical", or "important" unless the email explicitly supports that wording.
- Do not repeat the same fact verbatim in both Highlights and Releases.
- If an email only announces that a version was released but does not describe the changes, say that briefly and honestly.
- Merge related emails into one block when they describe the same product or the same version, for example a product release and its installer.
- If the day contains many similar service updates with the same security fix, group them into one shared block instead of a long list of nearly identical sections.
- For each release block, first prefer the user-visible effect, the fix, or the substance of the change. Mention delivery artifacts and infrastructure details only if they are the only useful information in the email.
- If an email contains a `# Изменения версии` or `# Version Changes` section, treat it as the main source of meaningful release changes and prioritize it over the rest of the email.
- Do not create a separate highlight just because a link, release notes, a web client, or delivery artifacts are available.
- If there is a main product version and a separate installer email for the same version, describe them as one release and mention the installer briefly.
- Do not write phrases like "the web client is available via the link", "details are available in release notes", or "packages/images/utilities are available" unless that is the only substantive information in the email.
- If an email is mostly about links, release notes, a release portal, installation packages, or docker images, do not surface that as digest content. Instead, briefly record the version release itself if that is the real event.
- If an email announces a new product version and the rest of the text is just about a link, a web client, or access instructions, keep only the fact of the version update and omit the link, web client, and availability wording.
- For Highlights, only choose items that answer the question "what actually changed today?". Do not include artifact availability, installer availability, links, or web client availability.
- The Releases section must contain no more than 5 sections. Keep only the items that are most useful for a quick morning read.

Emails:
{{EMAILS}}
