#!/usr/bin/env python3
"""One-shot generator (kept for provenance): adds the nullable users.email column
and its unique index to AppDbContextModelSnapshot.cs, then derives the Phase06
EmailAuth designer from that updated snapshot. Run from the repository root.

The hand-written migration is
backend/src/KaamLink.Infrastructure/Persistence/Migrations/20261027000000_Phase06EmailAuth.cs.
"""
from __future__ import annotations

import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SNAP = os.path.join(ROOT, "backend/src/KaamLink.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs")
DESIGNER = os.path.join(ROOT, "backend/src/KaamLink.Infrastructure/Persistence/Migrations/20261027000000_Phase06EmailAuth.Designer.cs")

with open(SNAP, encoding="utf-8") as handle:
    text = handle.read()

if 'b.Property<string>("Email")' not in text:
    property_anchor = (
        '                    b.Property<string>("DisplayName")\n'
        '                        .HasMaxLength(120)\n'
        '                        .HasColumnType("character varying(120)")\n'
        '                        .HasColumnName("display_name");\n'
    )
    assert text.count(property_anchor) == 1, f"snapshot DisplayName anchor count: {text.count(property_anchor)}"
    text = text.replace(
        property_anchor,
        property_anchor
        + '\n'
        + '                    b.Property<string>("Email")\n'
        + '                        .HasMaxLength(320)\n'
        + '                        .HasColumnType("character varying(320)")\n'
        + '                        .HasColumnName("email");\n',
    )

if 'b.HasIndex("Email")' not in text:
    index_anchor = (
        '                    b.HasIndex("PhoneNumber")\n'
        '                        .IsUnique()\n'
        '                        .HasDatabaseName("ux_users_phone_number");\n'
    )
    assert text.count(index_anchor) == 1, f"snapshot phone index anchor count: {text.count(index_anchor)}"
    text = text.replace(
        index_anchor,
        '                    b.HasIndex("Email")\n'
        '                        .IsUnique()\n'
        '                        .HasDatabaseName("ux_users_email");\n\n'
        + index_anchor,
    )

with open(SNAP, "w", encoding="utf-8") as handle:
    handle.write(text)

designer = text.replace(
    "using Microsoft.EntityFrameworkCore.Infrastructure;\nusing Microsoft.EntityFrameworkCore.Storage.ValueConversion;",
    "using Microsoft.EntityFrameworkCore.Infrastructure;\nusing Microsoft.EntityFrameworkCore.Migrations;\nusing Microsoft.EntityFrameworkCore.Storage.ValueConversion;",
)
designer = designer.replace(
    '    [DbContext(typeof(AppDbContext))]\n'
    '    partial class AppDbContextModelSnapshot : ModelSnapshot\n'
    '    {\n'
    '        protected override void BuildModel(ModelBuilder modelBuilder)',
    '    [DbContext(typeof(AppDbContext))]\n'
    '    [Migration("20261027000000_Phase06EmailAuth")]\n'
    '    partial class Phase06EmailAuth\n'
    '    {\n'
    '        protected override void BuildTargetModel(ModelBuilder modelBuilder)',
)
assert '[Migration("20261027000000_Phase06EmailAuth")]' in designer
assert 'b.Property<string>("Email")' in designer
assert 'ux_users_email' in designer

with open(DESIGNER, "w", encoding="utf-8") as handle:
    handle.write(designer)

print("OK: snapshot updated and Phase06EmailAuth designer generated")
