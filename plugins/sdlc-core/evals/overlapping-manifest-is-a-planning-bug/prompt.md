---
max_turns: 6
allowed_tools: [Skill]
tags: [parallel-safety]
---

Here is the slice manifest from our implementation plan. I want to launch one implementer agent
per slice, all at the same time. Is this ready to fan out?

    slices:
      - id: storage
        files: ["src/Storage/SnapshotWriter.cs", "src/Storage/ISnapshotStore.cs"]
        depends_on: []
      - id: api
        files: ["src/Api/SnapshotController.cs", "src/Storage/ISnapshotStore.cs"]
        depends_on: []
