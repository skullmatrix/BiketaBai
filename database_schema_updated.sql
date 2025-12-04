-- ============================================================
-- BiketaBai 3.0 - Updated Database Schema (Without Wallet)
-- ============================================================
-- This schema is optimized, normalized, and wallet functionality removed
-- Key features:
-- 1. VARCHAR status fields (no lookup tables)
-- 2. No wallet/wallet transactions
-- 3. Normalized store data
-- 4. Optimized indexes
-- ============================================================

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Core Tables
-- ============================================================

CREATE TABLE `users` (
    `user_id` int NOT NULL AUTO_INCREMENT,
    `full_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `email` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `password_hash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `phone` varchar(20) CHARACTER SET utf8mb4 NULL,
    `address` varchar(255) CHARACTER SET utf8mb4 NULL,
    `is_renter` tinyint(1) NOT NULL DEFAULT 0,
    `is_owner` tinyint(1) NOT NULL DEFAULT 0,
    `is_admin` tinyint(1) NOT NULL DEFAULT 0,
    `profile_photo_url` varchar(255) CHARACTER SET utf8mb4 NULL,
    `id_document_url` varchar(255) CHARACTER SET utf8mb4 NULL,
    `id_document_back_url` varchar(255) CHARACTER SET utf8mb4 NULL,
    `is_verified_owner` tinyint(1) NOT NULL DEFAULT 0,
    `verification_date` datetime(6) NULL,
    `verification_status` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
    `is_email_verified` tinyint(1) NOT NULL DEFAULT 0,
    `email_verification_token` varchar(100) CHARACTER SET utf8mb4 NULL,
    `email_verification_token_expires` datetime(6) NULL,
    `password_reset_token` varchar(100) CHARACTER SET utf8mb4 NULL,
    `password_reset_token_expires` datetime(6) NULL,
    `is_suspended` tinyint(1) NOT NULL DEFAULT 0,
    `id_verified` tinyint(1) NOT NULL DEFAULT 0,
    `id_verified_at` datetime(6) NULL,
    `address_verified` tinyint(1) NOT NULL DEFAULT 0,
    `address_verified_at` datetime(6) NULL,
    `id_extracted_address` varchar(500) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
    `deleted_at` datetime(6) NULL,
    `last_login_at` datetime(6) NULL,
    `login_count` int NOT NULL DEFAULT 0,
    CONSTRAINT `PK_users` PRIMARY KEY (`user_id`),
    CONSTRAINT `UQ_users_email` UNIQUE (`email`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `bike_types` (
    `bike_type_id` int NOT NULL AUTO_INCREMENT,
    `type_name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(255) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_bike_types` PRIMARY KEY (`bike_type_id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `stores` (
    `store_id` int NOT NULL AUTO_INCREMENT,
    `owner_id` int NOT NULL,
    `store_name` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `store_address` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `store_latitude` double NULL,
    `store_longitude` double NULL,
    `geofence_radius_km` decimal(10,2) NULL,
    `is_primary` tinyint(1) NOT NULL DEFAULT 1,
    `is_active` tinyint(1) NOT NULL DEFAULT 1,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
    `deleted_at` datetime(6) NULL,
    CONSTRAINT `PK_stores` PRIMARY KEY (`store_id`),
    CONSTRAINT `FK_stores_users_owner_id` FOREIGN KEY (`owner_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `bikes` (
    `bike_id` int NOT NULL AUTO_INCREMENT,
    `owner_id` int NOT NULL,
    `store_id` int NULL,
    `bike_type_id` int NOT NULL,
    `brand` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `model` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `description` text CHARACTER SET utf8mb4 NULL,
    `hourly_rate` decimal(10,2) NOT NULL,
    `daily_rate` decimal(10,2) NOT NULL,
    `quantity` int NOT NULL DEFAULT 1,
    `availability_status` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Available',
    `view_count` int NOT NULL DEFAULT 0,
    `booking_count` int NOT NULL DEFAULT 0,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
    `deleted_at` datetime(6) NULL,
    `deleted_by` varchar(100) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_bikes` PRIMARY KEY (`bike_id`),
    CONSTRAINT `FK_bikes_users_owner_id` FOREIGN KEY (`owner_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_bikes_bike_types_bike_type_id` FOREIGN KEY (`bike_type_id`) REFERENCES `bike_types` (`bike_type_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_bikes_stores_store_id` FOREIGN KEY (`store_id`) REFERENCES `stores` (`store_id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `bike_images` (
    `image_id` int NOT NULL AUTO_INCREMENT,
    `bike_id` int NOT NULL,
    `image_url` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `is_primary` tinyint(1) NOT NULL DEFAULT 0,
    `uploaded_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_bike_images` PRIMARY KEY (`image_id`),
    CONSTRAINT `FK_bike_images_bikes_bike_id` FOREIGN KEY (`bike_id`) REFERENCES `bikes` (`bike_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `bookings` (
    `booking_id` int NOT NULL AUTO_INCREMENT,
    `renter_id` int NOT NULL,
    `bike_id` int NOT NULL,
    `start_date` datetime(6) NOT NULL,
    `end_date` datetime(6) NOT NULL,
    `actual_return_date` datetime(6) NULL,
    `rental_hours` decimal(10,2) NOT NULL,
    `quantity` int NOT NULL DEFAULT 1,
    `base_rate` decimal(10,2) NOT NULL,
    `service_fee` decimal(10,2) NOT NULL,
    `total_amount` decimal(10,2) NOT NULL,
    `booking_status` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
    `distance_saved_km` decimal(10,2) NULL,
    `pickup_location` varchar(255) CHARACTER SET utf8mb4 NULL,
    `return_location` varchar(255) CHARACTER SET utf8mb4 NULL,
    `special_instructions` varchar(500) CHARACTER SET utf8mb4 NULL,
    `location_permission_granted` tinyint(1) NOT NULL DEFAULT 0,
    `location_permission_denied_at` datetime(6) NULL,
    `is_reported_lost` tinyint(1) NOT NULL DEFAULT 0,
    `reported_lost_at` datetime(6) NULL,
    `lost_report_notes` varchar(500) CHARACTER SET utf8mb4 NULL,
    `cancellation_reason` varchar(500) CHARACTER SET utf8mb4 NULL,
    `cancelled_at` datetime(6) NULL,
    `owner_confirmed_at` datetime(6) NULL,
    `renter_confirmed_pickup_at` datetime(6) NULL,
    `renter_confirmed_return_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
    `deleted_at` datetime(6) NULL,
    CONSTRAINT `PK_bookings` PRIMARY KEY (`booking_id`),
    CONSTRAINT `FK_bookings_bikes_bike_id` FOREIGN KEY (`bike_id`) REFERENCES `bikes` (`bike_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_bookings_users_renter_id` FOREIGN KEY (`renter_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Payment Tables (No Wallet)
-- ============================================================

CREATE TABLE `payments` (
    `payment_id` int NOT NULL AUTO_INCREMENT,
    `booking_id` int NOT NULL,
    `payment_method` varchar(50) CHARACTER SET utf8mb4 NOT NULL, -- GCash, QRPH, Cash, PayMaya, Credit/Debit Card (Wallet removed)
    `amount` decimal(10,2) NOT NULL,
    `payment_status` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
    `transaction_reference` varchar(100) CHARACTER SET utf8mb4 NULL,
    `payment_date` datetime(6) NOT NULL,
    `refund_amount` decimal(10,2) NULL,
    `refund_date` datetime(6) NULL,
    `owner_verified_at` datetime(6) NULL,
    `owner_verified_by` int NULL,
    `notes` varchar(500) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_payments` PRIMARY KEY (`payment_id`),
    CONSTRAINT `FK_payments_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_payments_users_owner_verified_by` FOREIGN KEY (`owner_verified_by`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Rating & Review Tables
-- ============================================================

CREATE TABLE `ratings` (
    `rating_id` int NOT NULL AUTO_INCREMENT,
    `booking_id` int NOT NULL,
    `bike_id` int NOT NULL,
    `rater_id` int NOT NULL,
    `rated_user_id` int NOT NULL,
    `rating_value` int NOT NULL,
    `review` text CHARACTER SET utf8mb4 NULL,
    `rating_category` varchar(50) CHARACTER SET utf8mb4 NULL,
    `is_renter_rating_owner` tinyint(1) NOT NULL DEFAULT 1,
    `created_at` datetime(6) NOT NULL,
    `is_flagged` tinyint(1) NOT NULL DEFAULT 0,
    CONSTRAINT `PK_ratings` PRIMARY KEY (`rating_id`),
    CONSTRAINT `FK_ratings_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ratings_bikes_bike_id` FOREIGN KEY (`bike_id`) REFERENCES `bikes` (`bike_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ratings_users_rater_id` FOREIGN KEY (`rater_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ratings_users_rated_user_id` FOREIGN KEY (`rated_user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT,
    CONSTRAINT `CHK_rating_value` CHECK (`rating_value` >= 1 AND `rating_value` <= 5)
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Communication & Tracking Tables
-- ============================================================

CREATE TABLE `notifications` (
    `notification_id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `title` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `message` text CHARACTER SET utf8mb4 NOT NULL,
    `notification_type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `is_read` tinyint(1) NOT NULL DEFAULT 0,
    `action_url` varchar(255) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_notifications` PRIMARY KEY (`notification_id`),
    CONSTRAINT `FK_notifications_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `location_tracking` (
    `tracking_id` int NOT NULL AUTO_INCREMENT,
    `booking_id` int NOT NULL,
    `latitude` double NOT NULL,
    `longitude` double NOT NULL,
    `distance_from_store_km` decimal(10,2) NULL,
    `is_within_geofence` tinyint(1) NOT NULL DEFAULT 1,
    `tracked_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_location_tracking` PRIMARY KEY (`tracking_id`),
    CONSTRAINT `FK_location_tracking_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Security & Verification Tables
-- ============================================================

CREATE TABLE `phone_otps` (
    `otp_id` int NOT NULL AUTO_INCREMENT,
    `phone_number` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `otp_code` varchar(6) CHARACTER SET utf8mb4 NOT NULL,
    `expires_at` datetime(6) NOT NULL,
    `is_verified` tinyint(1) NOT NULL DEFAULT 0,
    `verified_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL,
    `attempts` int NOT NULL DEFAULT 0,
    `max_attempts` int NOT NULL DEFAULT 5,
    CONSTRAINT `PK_phone_otps` PRIMARY KEY (`otp_id`)
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Admin & Reporting Tables
-- ============================================================

CREATE TABLE `reports` (
    `report_id` int NOT NULL AUTO_INCREMENT,
    `reporter_id` int NOT NULL,
    `report_type` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `subject` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `description` text CHARACTER SET utf8mb4 NOT NULL,
    `reported_user_id` int NULL,
    `reported_bike_id` int NULL,
    `booking_id` int NULL,
    `status` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
    `assigned_to` int NULL,
    `priority` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Medium',
    `admin_notes` text CHARACTER SET utf8mb4 NULL,
    `resolution` text CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `resolved_at` datetime(6) NULL,
    CONSTRAINT `PK_reports` PRIMARY KEY (`report_id`),
    CONSTRAINT `FK_reports_users_reporter_id` FOREIGN KEY (`reporter_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_reports_users_reported_user_id` FOREIGN KEY (`reported_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL,
    CONSTRAINT `FK_reports_bikes_reported_bike_id` FOREIGN KEY (`reported_bike_id`) REFERENCES `bikes` (`bike_id`) ON DELETE SET NULL,
    CONSTRAINT `FK_reports_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE SET NULL,
    CONSTRAINT `FK_reports_users_assigned_to` FOREIGN KEY (`assigned_to`) REFERENCES `users` (`user_id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Indexes for Performance
-- ============================================================

-- Users indexes
CREATE INDEX `IX_users_is_deleted` ON `users` (`is_deleted`);
CREATE INDEX `IX_users_is_email_verified` ON `users` (`is_email_verified`);
CREATE INDEX `IX_users_last_login_at` ON `users` (`last_login_at`);

-- Bikes indexes
CREATE INDEX `IX_bikes_owner_id` ON `bikes` (`owner_id`);
CREATE INDEX `IX_bikes_store_id` ON `bikes` (`store_id`);
CREATE INDEX `IX_bikes_bike_type_id` ON `bikes` (`bike_type_id`);
CREATE INDEX `IX_bikes_availability_status` ON `bikes` (`availability_status`);
CREATE INDEX `IX_bikes_is_deleted` ON `bikes` (`is_deleted`);
CREATE INDEX `IX_bikes_view_count` ON `bikes` (`view_count`);
CREATE INDEX `IX_bikes_booking_count` ON `bikes` (`booking_count`);
CREATE INDEX `IX_bikes_hourly_rate_daily_rate` ON `bikes` (`hourly_rate`, `daily_rate`);

-- Bike Images indexes
CREATE INDEX `IX_bike_images_bike_id` ON `bike_images` (`bike_id`);

-- Bookings indexes
CREATE INDEX `IX_bookings_renter_id` ON `bookings` (`renter_id`);
CREATE INDEX `IX_bookings_bike_id` ON `bookings` (`bike_id`);
CREATE INDEX `IX_bookings_booking_status` ON `bookings` (`booking_status`);
CREATE INDEX `IX_bookings_start_date` ON `bookings` (`start_date`);
CREATE INDEX `IX_bookings_is_deleted` ON `bookings` (`is_deleted`);

-- Payments indexes
CREATE INDEX `IX_payments_booking_id` ON `payments` (`booking_id`);
CREATE INDEX `IX_payments_owner_verified_by` ON `payments` (`owner_verified_by`);
CREATE INDEX `IX_payments_payment_status` ON `payments` (`payment_status`);

-- Ratings indexes
CREATE INDEX `IX_ratings_booking_id` ON `ratings` (`booking_id`);
CREATE INDEX `IX_ratings_bike_id` ON `ratings` (`bike_id`);
CREATE INDEX `IX_ratings_rater_id` ON `ratings` (`rater_id`);
CREATE INDEX `IX_ratings_rated_user_id` ON `ratings` (`rated_user_id`);
CREATE INDEX `IX_ratings_rating_value` ON `ratings` (`rating_value`);
CREATE INDEX `IX_ratings_is_flagged` ON `ratings` (`is_flagged`);

-- Notifications indexes
CREATE INDEX `IX_notifications_user_id_is_read` ON `notifications` (`user_id`, `is_read`);
CREATE INDEX `IX_notifications_created_at` ON `notifications` (`created_at`);

-- Location Tracking indexes
CREATE INDEX `IX_location_tracking_booking_id` ON `location_tracking` (`booking_id`);
CREATE INDEX `IX_location_tracking_tracked_at` ON `location_tracking` (`tracked_at`);

-- Phone OTPs indexes
CREATE INDEX `IX_phone_otps_phone_number` ON `phone_otps` (`phone_number`);
CREATE INDEX `IX_phone_otps_expires_at` ON `phone_otps` (`expires_at`);

-- Reports indexes
CREATE INDEX `IX_reports_reporter_id` ON `reports` (`reporter_id`);
CREATE INDEX `IX_reports_reported_user_id` ON `reports` (`reported_user_id`);
CREATE INDEX `IX_reports_reported_bike_id` ON `reports` (`reported_bike_id`);
CREATE INDEX `IX_reports_booking_id` ON `reports` (`booking_id`);
CREATE INDEX `IX_reports_assigned_to` ON `reports` (`assigned_to`);
CREATE INDEX `IX_reports_status` ON `reports` (`status`);

-- Stores indexes
CREATE INDEX `IX_stores_owner_id` ON `stores` (`owner_id`);
CREATE INDEX `IX_stores_is_deleted` ON `stores` (`is_deleted`);

-- ============================================================
-- Initial Data (Seed Data)
-- ============================================================

INSERT INTO `bike_types` (`bike_type_id`, `type_name`, `description`)
VALUES 
    (1, 'Mountain Bike', 'Off-road cycling'),
    (2, 'Road Bike', 'Paved road cycling'),
    (3, 'Hybrid Bike', 'Versatile for various terrains'),
    (4, 'Electric Bike', 'E-bike with motor assistance'),
    (5, 'City/Commuter Bike', 'Urban commuting'),
    (6, 'BMX', 'Tricks and stunts'),
    (7, 'Folding Bike', 'Compact and portable');

-- ============================================================
-- Valid Status Values Reference
-- ============================================================

-- Bike Availability Status values:
--   - 'Available'
--   - 'Rented'
--   - 'Maintenance'
--   - 'Inactive'

-- Booking Status values:
--   - 'Pending'
--   - 'Active'
--   - 'Completed'
--   - 'Cancelled'

-- Payment Method values (Wallet feature removed):
--   - 'GCash' (via PayMongo payment gateway)
--   - 'QRPH' (via PayMongo payment gateway)
--   - 'Cash' (manual verification by owner)
--   - 'PayMaya' (via PayMongo payment gateway)
--   - 'Credit/Debit Card' (via PayMongo payment gateway)
-- NOTE: Wallet payment method has been removed - no wallet balance tracking

-- Payment Status values:
--   - 'Pending'
--   - 'Completed'
--   - 'Failed'
--   - 'Refunded'

-- ============================================================
-- Schema Statistics
-- ============================================================
-- Total Tables: 14 (reduced from 20)
-- Tables Removed:
--   - wallets (wallet feature removed)
--   - credit_transactions (wallet transactions removed)
--   - availability_statuses (converted to VARCHAR)
--   - booking_statuses (converted to VARCHAR)
--   - payment_methods (converted to VARCHAR)
--   - transaction_types (removed with wallet)
-- 
-- Payment Methods Still Available:
--   ✅ GCash (via PayMongo)
--   ✅ QRPH (via PayMongo)
--   ✅ Cash (manual verification)
--   ✅ PayMaya (via PayMongo)
--   ✅ Credit/Debit Card (via PayMongo)
--   ❌ Wallet (REMOVED - no wallet balance/transactions)
-- Total Foreign Keys: 14 (reduced from 22)
-- ============================================================

