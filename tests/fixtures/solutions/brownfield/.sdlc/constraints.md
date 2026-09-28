# Solution constraints - brownfield

## LOC_01 - Tenant id comes from the token, never the request

**Applies to:** a diff, at verification.

**Forbids:** a change that reads a tenant identifier from route values, query string, form
fields or a request body anywhere under `src/`.

**Why:** a request-supplied tenant id is a cross-tenant read waiting to happen, and the two
sources are indistinguishable in a diff. Existing code that does this predates adoption and is
not a finding; a change that adds another one is.
