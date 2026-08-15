#!/usr/bin/env pwsh

# If the migration this produces contains columns or tables you never touched - deadminned,
# suspended, ipintel_cache - the model snapshots were rolled back by a previous
# remove-migration. Restore them from git and run this again:
#
#   git checkout -- Content.Server.Database/Migrations/Sqlite/SqliteServerDbContextModelSnapshot.cs
#   git checkout -- Content.Server.Database/Migrations/Postgres/PostgresServerDbContextModelSnapshot.cs
#
# The reason is explained in remove-migration.ps1: the migration ids in this repository do not sort
# in the order the migrations were written, so EF's idea of "the previous migration" is the oldest
# model state rather than the newest.
#
# Two other things worth knowing when the migration touches character profiles:
#
#   - EF pairs a dropped column with an added one and offers a rename. That carries the old values
#     across, which for prototype ids means rows holding ids of prototypes that no longer exist.
#     Rewrite the rename as a DropColumn plus an AddColumn with a real default.
#   - Migrations are source, not build output. All three files per context - the migration, its
#     Designer snapshot and the updated top-level snapshot - are committed. Without them the schema
#     never changes on any deployment.

param([String]$name)

if ($name -eq "")
{
    Write-Error "must specify migration name"
    exit
}

dotnet ef migrations add --context SqliteServerDbContext --output-dir Migrations/Sqlite $name
dotnet ef migrations add --context PostgresServerDbContext --output-dir Migrations/Postgres $name
