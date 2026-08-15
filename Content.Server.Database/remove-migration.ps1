#!/usr/bin/env pwsh

# WARNING, read before using this in Project Ember.
#
# `ef migrations remove` rebuilds the top-level model snapshot from the Designer snapshot of
# whichever migration EF considers the previous one. EF orders migrations by the timestamp in their
# id, and in this repository that order does not match the order the migrations were written:
#
#   migration           id (what EF sorts by)   committed
#   ClothingRemoval     2025-06-04              2024-07-26
#   IPIntel             2024-11-22              2025-01-12
#   AdminStatus         2024-12-23              2025-01-15
#
# ClothingRemoval is a fork migration written before the other two and carrying an id ahead of both,
# so EF treats the oldest model state as the newest. Its Designer snapshot knows nothing about
# IPIntel or AdminStatus - and neither of those knows about the other, since all three were authored
# on separate branches and merged.
#
# Removing a migration therefore silently rolls the snapshot back past features that are already in
# the database, and the next add-migration offers to create them again. The symptom is a new
# migration containing columns and tables you never touched.
#
# The committed top-level snapshot is correct. So after running this, restore it and only then add:
#
#   git checkout -- Content.Server.Database/Migrations/Sqlite/SqliteServerDbContextModelSnapshot.cs
#   git checkout -- Content.Server.Database/Migrations/Postgres/PostgresServerDbContextModelSnapshot.cs
#
# Fixing this properly would mean regenerating the Designer snapshot of an applied migration, which
# is rewriting history for something the top-level snapshot already holds correctly.

param([String]$name)

if ($name -eq "")
{
    Write-Error "must specify migration name"
    exit
}

dotnet ef migrations remove --context SqliteServerDbContext $name
dotnet ef migrations remove --context PostgresServerDbContext $name

Write-Host ""
Write-Host "The model snapshots have been rolled back and are probably now missing IPIntel and"
Write-Host "AdminStatus - see the comment at the top of this script. Restore them before adding a"
Write-Host "new migration, or the new one will try to recreate features that already exist:"
Write-Host ""
Write-Host "  git checkout -- Content.Server.Database/Migrations/Sqlite/SqliteServerDbContextModelSnapshot.cs"
Write-Host "  git checkout -- Content.Server.Database/Migrations/Postgres/PostgresServerDbContextModelSnapshot.cs"
