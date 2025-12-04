-- ============================================================
-- BiketaBai 3.0 - Linear Database Schema (1 Relationship Per Table)
-- ============================================================
-- This schema minimizes relationships by using a linear/chain structure
-- Each table has only ONE foreign key relationship to maintain simplicity
-- Key design principles:
-- 1. Linear chain structure: User → Store → Bike → Booking → Payment
-- 2. Each table has only one parent relationship
-- 3. Related data embedded or accessed through the chain
-- 4. Denormalized where necessary to maintain single relationships
-- ============================================================

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Level 1: Core Entity (No Foreign Keys)
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
    CONSTRAINT `UQ_users_email` UNIQUE (`email`),
    CONSTRAINT `UQ_users_phone` UNIQUE (`phone`)  -- Ensure phone numbers are unique per user
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Level 2: User-Dependent Tables (Only FK to Users)
-- ============================================================

CREATE TABLE `phone_otps` (
    `otp_id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,  -- Only FK: to users (phone OTPs are user-specific)
    `phone_number` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `otp_code` varchar(6) CHARACTER SET utf8mb4 NOT NULL,
    `expires_at` datetime(6) NOT NULL,
    `is_verified` tinyint(1) NOT NULL DEFAULT 0,
    `verified_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL,
    `attempts` int NOT NULL DEFAULT 0,
    `max_attempts` int NOT NULL DEFAULT 5,
    CONSTRAINT `PK_phone_otps` PRIMARY KEY (`otp_id`),
    CONSTRAINT `FK_phone_otps_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `stores` (
    `store_id` int NOT NULL AUTO_INCREMENT,
    `owner_id` int NOT NULL,  -- Only FK: to users
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

-- Bike types are now VARCHAR in bikes table (denormalized for linear structure)
-- Valid values: 'Mountain Bike', 'Road Bike', 'Hybrid Bike', 'Electric Bike', 
--               'City/Commuter Bike', 'BMX', 'Folding Bike'

-- ============================================================
-- Level 3: Store-Dependent Tables (Only FK to Stores)
-- ============================================================

CREATE TABLE `bikes` (
    `bike_id` int NOT NULL AUTO_INCREMENT,
    `store_id` int NOT NULL,  -- Only FK: to stores
    `bike_type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,  -- Denormalized: Mountain Bike, Road Bike, etc.
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
    CONSTRAINT `FK_bikes_stores_store_id` FOREIGN KEY (`store_id`) REFERENCES `stores` (`store_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Level 4: Bike-Dependent Tables (Only FK to Bikes)
-- ============================================================

CREATE TABLE `bike_images` (
    `image_id` int NOT NULL AUTO_INCREMENT,
    `bike_id` int NOT NULL,  -- Only FK: to bikes
    `image_url` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `is_primary` tinyint(1) NOT NULL DEFAULT 0,
    `uploaded_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_bike_images` PRIMARY KEY (`image_id`),
    CONSTRAINT `FK_bike_images_bikes_bike_id` FOREIGN KEY (`bike_id`) REFERENCES `bikes` (`bike_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `bookings` (
    `booking_id` int NOT NULL AUTO_INCREMENT,
    `bike_id` int NOT NULL,  -- Only FK: to bikes (renter_id denormalized)
    `renter_user_id` int NOT NULL,  -- Denormalized: stored directly (no FK constraint)
    `renter_full_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,  -- Denormalized
    `renter_email` varchar(100) CHARACTER SET utf8mb4 NOT NULL,  -- Denormalized
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
    CONSTRAINT `FK_bookings_bikes_bike_id` FOREIGN KEY (`bike_id`) REFERENCES `bikes` (`bike_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Level 5: Booking-Dependent Tables (Only FK to Bookings)
-- ============================================================

CREATE TABLE `payments` (
    `payment_id` int NOT NULL AUTO_INCREMENT,
    `booking_id` int NOT NULL,  -- Only FK: to bookings
    `payment_method` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `amount` decimal(10,2) NOT NULL,
    `payment_status` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
    `transaction_reference` varchar(100) CHARACTER SET utf8mb4 NULL,
    `payment_date` datetime(6) NOT NULL,
    `refund_amount` decimal(10,2) NULL,
    `refund_date` datetime(6) NULL,
    `owner_verified_at` datetime(6) NULL,
    `owner_verified_by_user_id` int NULL,  -- Denormalized (no FK constraint)
    `owner_verified_by_name` varchar(100) CHARACTER SET utf8mb4 NULL,  -- Denormalized
    `notes` varchar(500) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_payments` PRIMARY KEY (`payment_id`),
    CONSTRAINT `FK_payments_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `location_tracking` (
    `tracking_id` int NOT NULL AUTO_INCREMENT,
    `booking_id` int NOT NULL,  -- Only FK: to bookings
    `latitude` double NOT NULL,
    `longitude` double NOT NULL,
    `distance_from_store_km` decimal(10,2) NULL,
    `is_within_geofence` tinyint(1) NOT NULL DEFAULT 1,
    `tracked_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_location_tracking` PRIMARY KEY (`tracking_id`),
    CONSTRAINT `FK_location_tracking_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `ratings` (
    `rating_id` int NOT NULL AUTO_INCREMENT,
    `booking_id` int NOT NULL,  -- Only FK: to bookings (all user/bike data denormalized)
    `rater_user_id` int NOT NULL,  -- Denormalized (no FK constraint)
    `rater_full_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,  -- Denormalized
    `rated_user_id` int NOT NULL,  -- Denormalized (no FK constraint)
    `rated_user_full_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,  -- Denormalized
    `bike_id` int NOT NULL,  -- Denormalized (no FK constraint)
    `bike_brand_model` varchar(200) CHARACTER SET utf8mb4 NOT NULL,  -- Denormalized
    `rating_value` int NOT NULL,
    `review` text CHARACTER SET utf8mb4 NULL,
    `rating_category` varchar(50) CHARACTER SET utf8mb4 NULL,
    `is_renter_rating_owner` tinyint(1) NOT NULL DEFAULT 1,
    `created_at` datetime(6) NOT NULL,
    `is_flagged` tinyint(1) NOT NULL DEFAULT 0,
    CONSTRAINT `PK_ratings` PRIMARY KEY (`rating_id`),
    CONSTRAINT `FK_ratings_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE,
    CONSTRAINT `CHK_rating_value` CHECK (`rating_value` >= 1 AND `rating_value` <= 5)
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Level 6: Payment-Dependent Tables (Only FK to Payments)
-- ============================================================

-- (No tables at this level - payments is a leaf node)

-- ============================================================
-- Special Tables: User Notifications (Only FK to Users)
-- ============================================================

CREATE TABLE `notifications` (
    `notification_id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,  -- Only FK: to users
    `title` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `message` text CHARACTER SET utf8mb4 NOT NULL,
    `notification_type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `is_read` tinyint(1) NOT NULL DEFAULT 0,
    `action_url` varchar(255) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_notifications` PRIMARY KEY (`notification_id`),
    CONSTRAINT `FK_notifications_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Special Tables: Reports (Only FK to Users)
-- ============================================================

CREATE TABLE `reports` (
    `report_id` int NOT NULL AUTO_INCREMENT,
    `reporter_user_id` int NOT NULL,  -- Only FK: to users (all other IDs denormalized)
    `reporter_full_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,  -- Denormalized
    `report_type` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `subject` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `description` text CHARACTER SET utf8mb4 NOT NULL,
    `reported_user_id` int NULL,  -- Denormalized (no FK constraint)
    `reported_user_full_name` varchar(100) CHARACTER SET utf8mb4 NULL,  -- Denormalized
    `reported_bike_id` int NULL,  -- Denormalized (no FK constraint)
    `reported_bike_brand_model` varchar(200) CHARACTER SET utf8mb4 NULL,  -- Denormalized
    `booking_id` int NULL,  -- Denormalized (no FK constraint)
    `status` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
    `assigned_to_user_id` int NULL,  -- Denormalized (no FK constraint)
    `assigned_to_full_name` varchar(100) CHARACTER SET utf8mb4 NULL,  -- Denormalized
    `priority` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Medium',
    `admin_notes` text CHARACTER SET utf8mb4 NULL,
    `resolution` text CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `resolved_at` datetime(6) NULL,
    CONSTRAINT `PK_reports` PRIMARY KEY (`report_id`),
    CONSTRAINT `FK_reports_users_reporter_user_id` FOREIGN KEY (`reporter_user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

-- ============================================================
-- Indexes for Performance
-- ============================================================

-- Users indexes
CREATE INDEX `IX_users_is_deleted` ON `users` (`is_deleted`);
CREATE INDEX `IX_users_is_email_verified` ON `users` (`is_email_verified`);
CREATE INDEX `IX_users_last_login_at` ON `users` (`last_login_at`);
CREATE INDEX `IX_users_email` ON `users` (`email`);
CREATE INDEX `IX_users_phone` ON `users` (`phone`);  -- Index for unique phone number constraint

-- Stores indexes
CREATE INDEX `IX_stores_owner_id` ON `stores` (`owner_id`);
CREATE INDEX `IX_stores_is_deleted` ON `stores` (`is_deleted`);

-- Bikes indexes
CREATE INDEX `IX_bikes_store_id` ON `bikes` (`store_id`);
CREATE INDEX `IX_bikes_bike_type` ON `bikes` (`bike_type`);  -- Index for denormalized bike type
CREATE INDEX `IX_bikes_availability_status` ON `bikes` (`availability_status`);
CREATE INDEX `IX_bikes_is_deleted` ON `bikes` (`is_deleted`);
CREATE INDEX `IX_bikes_view_count` ON `bikes` (`view_count`);
CREATE INDEX `IX_bikes_booking_count` ON `bikes` (`booking_count`);

-- Bike Images indexes
CREATE INDEX `IX_bike_images_bike_id` ON `bike_images` (`bike_id`);

-- Bookings indexes
CREATE INDEX `IX_bookings_bike_id` ON `bookings` (`bike_id`);
CREATE INDEX `IX_bookings_renter_user_id` ON `bookings` (`renter_user_id`);  -- Index for denormalized field
CREATE INDEX `IX_bookings_booking_status` ON `bookings` (`booking_status`);
CREATE INDEX `IX_bookings_start_date` ON `bookings` (`start_date`);
CREATE INDEX `IX_bookings_is_deleted` ON `bookings` (`is_deleted`);

-- Payments indexes
CREATE INDEX `IX_payments_booking_id` ON `payments` (`booking_id`);
CREATE INDEX `IX_payments_payment_status` ON `payments` (`payment_status`);

-- Location Tracking indexes
CREATE INDEX `IX_location_tracking_booking_id` ON `location_tracking` (`booking_id`);
CREATE INDEX `IX_location_tracking_tracked_at` ON `location_tracking` (`tracked_at`);

-- Ratings indexes
CREATE INDEX `IX_ratings_booking_id` ON `ratings` (`booking_id`);
CREATE INDEX `IX_ratings_rater_user_id` ON `ratings` (`rater_user_id`);  -- Index for denormalized field
CREATE INDEX `IX_ratings_rated_user_id` ON `ratings` (`rated_user_id`);  -- Index for denormalized field
CREATE INDEX `IX_ratings_rating_value` ON `ratings` (`rating_value`);
CREATE INDEX `IX_ratings_is_flagged` ON `ratings` (`is_flagged`);

-- Notifications indexes
CREATE INDEX `IX_notifications_user_id_is_read` ON `notifications` (`user_id`, `is_read`);
CREATE INDEX `IX_notifications_created_at` ON `notifications` (`created_at`);

-- Reports indexes
CREATE INDEX `IX_reports_reporter_user_id` ON `reports` (`reporter_user_id`);
CREATE INDEX `IX_reports_reported_user_id` ON `reports` (`reported_user_id`);  -- Index for denormalized field
CREATE INDEX `IX_reports_reported_bike_id` ON `reports` (`reported_bike_id`);  -- Index for denormalized field
CREATE INDEX `IX_reports_booking_id` ON `reports` (`booking_id`);  -- Index for denormalized field
CREATE INDEX `IX_reports_assigned_to_user_id` ON `reports` (`assigned_to_user_id`);  -- Index for denormalized field
CREATE INDEX `IX_reports_status` ON `reports` (`status`);

-- Phone OTPs indexes
CREATE INDEX `IX_phone_otps_user_id` ON `phone_otps` (`user_id`);
CREATE INDEX `IX_phone_otps_phone_number` ON `phone_otps` (`phone_number`);
CREATE INDEX `IX_phone_otps_expires_at` ON `phone_otps` (`expires_at`);

-- ============================================================
-- Initial Data (Seed Data)
-- ============================================================

-- Bike types are now VARCHAR fields in bikes table
-- Valid bike_type values: 'Mountain Bike', 'Road Bike', 'Hybrid Bike', 
-- 'Electric Bike', 'City/Commuter Bike', 'BMX', 'Folding Bike'

-- ============================================================
-- Schema Statistics
-- ============================================================
-- Total Tables: 11
-- Total Foreign Keys: 11 (each table has exactly 1 FK)
-- 
-- Linear Structure (1 relationship per table):
-- Level 1: users (0 FKs - root)
-- Level 2: stores (1 FK to users), phone_otps (1 FK to users), notifications (1 FK to users), reports (1 FK to users)
-- Level 3: bikes (1 FK to stores)
-- Level 4: bike_images (1 FK to bikes), bookings (1 FK to bikes)
-- Level 5: payments (1 FK to bookings), location_tracking (1 FK to bookings), ratings (1 FK to bookings)
--
-- Design Principles:
-- 1. Each table has exactly 1 relationship line in ERD (or 0 for root/standalone)
-- 2. Lookup tables removed - all converted to VARCHAR fields
-- 3. Denormalized data stored directly (renter info in bookings, bike type in bikes, etc.)
-- 4. Foreign key constraints removed for denormalized fields
-- 5. Indexes added to denormalized fields for query performance
--
-- ERD Structure (Ultra-Clean):
-- users → stores → bikes → bookings → payments
--                          ↓
--                      bike_images
--                          ↓
--                      location_tracking
--                          ↓
--                      ratings
-- users → notifications
-- users → reports
-- users → phone_otps
--
-- Phone Number Uniqueness:
-- - Phone numbers are unique per user (UQ_users_phone constraint)
-- - Phone OTPs are linked to users (1 FK to users)
-- - Each phone number can only belong to one user
--
-- Result: Only 11 relationship lines total in ERD!
-- ============================================================

