Extract only the meaningful changes from one release email.
Goal: separate useful release changes from delivery noise before building the final digest.
Return only a short markdown block with:
- at most 3 short bullets;
- each bullet must describe only the substance of the change.

Rules:
- Do not add source, from, date, links, release notes, docker images, file paths, portals, installer availability, web client availability, or other delivery noise.
- If the email is mostly about links, delivery artifacts, or access instructions, but it does announce a version release, keep only the short fact of the version update.
- If the email contains very few details, say briefly and honestly that the version was released and the change details were not described.
- Do not invent changes that are not present in the email.
- Do not surface raw snake_case, CamelCase, test-style identifiers, table names, file names, method names, or internal class names when the meaning can be explained in plain human language.
- If the email describes a problem through an internal identifier, table name, file name, method name, test name, or variable name, rewrite it into a human-readable description of what was fixed, where the problem appeared, or what behavior changed.
- Do not leave a bare technical identifier inside a bullet without a human-readable explanation.
- If an exact technical identifier is truly necessary for meaning, mention it only once and pair it with a short human-readable explanation where possible.
- Do not add headings, prefixes, or commentary outside the bullet list.

Email:
Subject: {{SUBJECT}}
From: {{FROM}}
Date: {{DATE_UTC}}Z
Body preview:
{{BODY_PREVIEW}}
