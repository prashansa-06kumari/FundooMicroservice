CREATE TABLE IF NOT EXISTS "Notes"
(
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL,
    "Title" VARCHAR(200) NOT NULL,
    "Content" TEXT NOT NULL,
    "Color" VARCHAR(30) NULL,
    "IsPinned" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsArchived" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsTrashed" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_Notes_UserId"
    ON "Notes" ("UserId");

CREATE TABLE IF NOT EXISTS "Labels"
(
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL,
    "Name" VARCHAR(100) NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Labels_UserId_Name"
    ON "Labels" ("UserId", "Name");

CREATE TABLE IF NOT EXISTS "NoteLabels"
(
    "NoteId" UUID NOT NULL,
    "LabelId" UUID NOT NULL,

    CONSTRAINT "PK_NoteLabels"
        PRIMARY KEY ("NoteId", "LabelId"),

    CONSTRAINT "FK_NoteLabels_Notes_NoteId"
        FOREIGN KEY ("NoteId")
        REFERENCES "Notes" ("Id")
        ON DELETE CASCADE,

    CONSTRAINT "FK_NoteLabels_Labels_LabelId"
        FOREIGN KEY ("LabelId")
        REFERENCES "Labels" ("Id")
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_NoteLabels_LabelId"
    ON "NoteLabels" ("LabelId");