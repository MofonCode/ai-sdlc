---
type: llm
---

PASS if the response identifies that both slices claim `src/storage/audit_store_contract`, says
this is not safe to fan out as written, and directs the fix at the plan — merging the slices,
extracting the shared file into a third slice both depend on, or otherwise making the manifests
disjoint.

FAIL if the response says the manifest is ready to fan out; misses the shared file entirely; or
proposes to proceed and resolve the collision afterwards through merging, locking, coordination
between the agents, or retries.
