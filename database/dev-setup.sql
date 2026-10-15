-- One-time local development setup. Run as a MySQL admin (e.g. root) in MySQL Workbench.
-- Replace CHANGE_ME with a password of your choice, then store the same password in user-secrets
-- (see README "Local setup"). Never commit a real password.
--
-- The app account may only touch databases whose names start with "grocery_": grocery_dev for
-- running the app, and grocery_test* databases that the integration tests create and drop.
-- Production uses a separate, narrower account (no DROP/CREATE DATABASE) — see the deployment docs.

CREATE DATABASE IF NOT EXISTS grocery_dev
  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

CREATE USER IF NOT EXISTS 'grocery_app'@'localhost' IDENTIFIED BY 'CHANGE_ME';
GRANT ALL PRIVILEGES ON `grocery\_%`.* TO 'grocery_app'@'localhost';
FLUSH PRIVILEGES;
