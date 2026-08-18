-- Runs only once, on an empty data volume. After changing this file:
--   docker compose down -v && docker compose up
-- Otherwise the change appears to do nothing.

-- Application tables are created by migrations owned by the backend, not here.
-- This file is for things a migration cannot do: extensions and database level
-- settings that must exist before the first migration runs.

-- gen_random_uuid(), used for primary keys instead of guessable sequences.
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Accent and case insensitive search over Polish organisation names.
CREATE EXTENSION IF NOT EXISTS unaccent;

-- Every timestamp in this system is stored in UTC. Applicants and operators
-- think in local time, and the October clock change lands in the middle of the
-- competition season, so the conversion happens at the edges, never in the data.
ALTER DATABASE ocwip SET timezone TO 'UTC';
