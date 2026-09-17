---
max_turns: 6
allowed_tools: [Skill]
tags: [parallel-safety]
---

Here is the slice manifest from our implementation plan. I want to launch one implementer agent
per slice, all at the same time. Is this ready to fan out?

    slices:
      - id: storage
        files: ["src/storage/audit_writer", "src/storage/audit_store_contract"]
        depends_on: []
      - id: api
        files: ["src/api/audit_endpoint", "src/storage/audit_store_contract"]
        depends_on: []
