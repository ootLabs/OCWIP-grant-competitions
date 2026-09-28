-- Runs only once, on an empty data volume, and never on a managed
-- PostgreSQL. After changing this file:
--   docker compose down -v && docker compose up
-- Otherwise the change appears to do nothing.

-- Application tables are created by migrations owned by the backend, not here.
-- This file is for things a migration cannot do, and today there are none
-- (T-113): gen_random_uuid() is built into PostgreSQL since version 13, so
-- pgcrypto is not needed, unaccent was never used, and every session is set
-- to UTC by the application's own connection (PostgresDbContextOptions), so
-- no ALTER DATABASE with a hard coded database name either.
SELECT 1;
