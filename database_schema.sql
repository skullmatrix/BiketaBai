CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `availability_statuses` (
    `status_id` int NOT NULL AUTO_INCREMENT,
    `status_name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_availability_statuses` PRIMARY KEY (`status_id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `bike_types` (
    `bike_type_id` int NOT NULL AUTO_INCREMENT,
    `type_name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(255) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_bike_types` PRIMARY KEY (`bike_type_id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `booking_statuses` (
    `status_id` int NOT NULL AUTO_INCREMENT,
    `status_name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_booking_statuses` PRIMARY KEY (`status_id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `payment_methods` (
    `method_id` int NOT NULL AUTO_INCREMENT,
    `method_name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_payment_methods` PRIMARY KEY (`method_id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `transaction_types` (
    `type_id` int NOT NULL AUTO_INCREMENT,
    `type_name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_transaction_types` PRIMARY KEY (`type_id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `users` (
    `user_id` int NOT NULL AUTO_INCREMENT,
    `full_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `email` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `password_hash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `phone` varchar(20) CHARACTER SET utf8mb4 NULL,
    `address` varchar(255) CHARACTER SET utf8mb4 NULL,
    `is_renter` tinyint(1) NOT NULL,
    `is_owner` tinyint(1) NOT NULL,
    `is_admin` tinyint(1) NOT NULL,
    `profile_photo_url` varchar(255) CHARACTER SET utf8mb4 NULL,
    `id_document_url` varchar(255) CHARACTER SET utf8mb4 NULL,
    `is_verified_owner` tinyint(1) NOT NULL,
    `verification_date` datetime(6) NULL,
    `verification_status` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `is_email_verified` tinyint(1) NOT NULL,
    `email_verification_token` varchar(100) CHARACTER SET utf8mb4 NULL,
    `email_verification_token_expires` datetime(6) NULL,
    `password_reset_token` varchar(100) CHARACTER SET utf8mb4 NULL,
    `password_reset_token_expires` datetime(6) NULL,
    `is_suspended` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL,
    `deleted_at` datetime(6) NULL,
    `last_login_at` datetime(6) NULL,
    `login_count` int NOT NULL,
    CONSTRAINT `PK_users` PRIMARY KEY (`user_id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `bikes` (
    `bike_id` int NOT NULL AUTO_INCREMENT,
    `owner_id` int NOT NULL,
    `bike_type_id` int NOT NULL,
    `brand` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `model` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `view_count` int NOT NULL,
    `booking_count` int NOT NULL,
    `description` text CHARACTER SET utf8mb4 NULL,
    `hourly_rate` decimal(10,2) NOT NULL,
    `daily_rate` decimal(10,2) NOT NULL,
    `availability_status_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL,
    `deleted_at` datetime(6) NULL,
    `deleted_by` varchar(100) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_bikes` PRIMARY KEY (`bike_id`),
    CONSTRAINT `FK_bikes_availability_statuses_availability_status_id` FOREIGN KEY (`availability_status_id`) REFERENCES `availability_statuses` (`status_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_bikes_bike_types_bike_type_id` FOREIGN KEY (`bike_type_id`) REFERENCES `bike_types` (`bike_type_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_bikes_users_owner_id` FOREIGN KEY (`owner_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `notifications` (
    `notification_id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `title` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `message` text CHARACTER SET utf8mb4 NOT NULL,
    `notification_type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `is_read` tinyint(1) NOT NULL,
    `action_url` varchar(255) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_notifications` PRIMARY KEY (`notification_id`),
    CONSTRAINT `FK_notifications_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `points` (
    `points_id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `total_points` int NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_points` PRIMARY KEY (`points_id`),
    CONSTRAINT `FK_points_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `wallets` (
    `wallet_id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `balance` decimal(10,2) NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_wallets` PRIMARY KEY (`wallet_id`),
    CONSTRAINT `FK_wallets_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `bike_images` (
    `image_id` int NOT NULL AUTO_INCREMENT,
    `bike_id` int NOT NULL,
    `image_url` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `is_primary` tinyint(1) NOT NULL,
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
    `base_rate` decimal(10,2) NOT NULL,
    `service_fee` decimal(10,2) NOT NULL,
    `total_amount` decimal(10,2) NOT NULL,
    `booking_status_id` int NOT NULL,
    `distance_saved_km` decimal(10,2) NULL,
    `pickup_location` varchar(255) CHARACTER SET utf8mb4 NULL,
    `return_location` varchar(255) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `cancellation_reason` varchar(500) CHARACTER SET utf8mb4 NULL,
    `cancelled_at` datetime(6) NULL,
    `is_deleted` tinyint(1) NOT NULL,
    `deleted_at` datetime(6) NULL,
    `owner_confirmed_at` datetime(6) NULL,
    `renter_confirmed_pickup_at` datetime(6) NULL,
    `renter_confirmed_return_at` datetime(6) NULL,
    `special_instructions` varchar(500) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_bookings` PRIMARY KEY (`booking_id`),
    CONSTRAINT `FK_bookings_bikes_bike_id` FOREIGN KEY (`bike_id`) REFERENCES `bikes` (`bike_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_bookings_booking_statuses_booking_status_id` FOREIGN KEY (`booking_status_id`) REFERENCES `booking_statuses` (`status_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_bookings_users_renter_id` FOREIGN KEY (`renter_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `points_history` (
    `history_id` int NOT NULL AUTO_INCREMENT,
    `points_id` int NOT NULL,
    `points_change` int NOT NULL,
    `points_before` int NOT NULL,
    `points_after` int NOT NULL,
    `reason` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `reference_id` varchar(100) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_points_history` PRIMARY KEY (`history_id`),
    CONSTRAINT `FK_points_history_points_points_id` FOREIGN KEY (`points_id`) REFERENCES `points` (`points_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `credit_transactions` (
    `transaction_id` int NOT NULL AUTO_INCREMENT,
    `wallet_id` int NOT NULL,
    `transaction_type_id` int NOT NULL,
    `amount` decimal(10,2) NOT NULL,
    `balance_before` decimal(10,2) NOT NULL,
    `balance_after` decimal(10,2) NOT NULL,
    `description` varchar(255) CHARACTER SET utf8mb4 NULL,
    `reference_id` varchar(100) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_credit_transactions` PRIMARY KEY (`transaction_id`),
    CONSTRAINT `FK_credit_transactions_transaction_types_transaction_type_id` FOREIGN KEY (`transaction_type_id`) REFERENCES `transaction_types` (`type_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_credit_transactions_wallets_wallet_id` FOREIGN KEY (`wallet_id`) REFERENCES `wallets` (`wallet_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `payments` (
    `payment_id` int NOT NULL AUTO_INCREMENT,
    `booking_id` int NOT NULL,
    `payment_method_id` int NOT NULL,
    `amount` decimal(10,2) NOT NULL,
    `payment_status` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `transaction_reference` varchar(100) CHARACTER SET utf8mb4 NULL,
    `payment_date` datetime(6) NOT NULL,
    `refund_amount` decimal(10,2) NULL,
    `refund_date` datetime(6) NULL,
    `notes` varchar(500) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_payments` PRIMARY KEY (`payment_id`),
    CONSTRAINT `FK_payments_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_payments_payment_methods_payment_method_id` FOREIGN KEY (`payment_method_id`) REFERENCES `payment_methods` (`method_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `ratings` (
    `rating_id` int NOT NULL AUTO_INCREMENT,
    `booking_id` int NOT NULL,
    `bike_id` int NULL,
    `rater_id` int NOT NULL,
    `rated_user_id` int NOT NULL,
    `rating_value` int NOT NULL,
    `review` text CHARACTER SET utf8mb4 NULL,
    `rating_category` varchar(50) CHARACTER SET utf8mb4 NULL,
    `is_renter_rating_owner` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `is_flagged` tinyint(1) NOT NULL,
    CONSTRAINT `PK_ratings` PRIMARY KEY (`rating_id`),
    CONSTRAINT `FK_ratings_bikes_bike_id` FOREIGN KEY (`bike_id`) REFERENCES `bikes` (`bike_id`),
    CONSTRAINT `FK_ratings_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ratings_users_rated_user_id` FOREIGN KEY (`rated_user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ratings_users_rater_id` FOREIGN KEY (`rater_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `reports` (
    `report_id` int NOT NULL AUTO_INCREMENT,
    `reporter_id` int NOT NULL,
    `report_type` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `subject` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `description` TEXT CHARACTER SET utf8mb4 NOT NULL,
    `reported_user_id` int NULL,
    `reported_bike_id` int NULL,
    `booking_id` int NULL,
    `status` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `assigned_to` int NULL,
    `priority` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `admin_notes` TEXT CHARACTER SET utf8mb4 NULL,
    `resolution` TEXT CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `resolved_at` datetime(6) NULL,
    CONSTRAINT `PK_reports` PRIMARY KEY (`report_id`),
    CONSTRAINT `FK_reports_bikes_reported_bike_id` FOREIGN KEY (`reported_bike_id`) REFERENCES `bikes` (`bike_id`),
    CONSTRAINT `FK_reports_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`),
    CONSTRAINT `FK_reports_users_assigned_to` FOREIGN KEY (`assigned_to`) REFERENCES `users` (`user_id`),
    CONSTRAINT `FK_reports_users_reported_user_id` FOREIGN KEY (`reported_user_id`) REFERENCES `users` (`user_id`),
    CONSTRAINT `FK_reports_users_reporter_id` FOREIGN KEY (`reporter_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

INSERT INTO `availability_statuses` (`status_id`, `status_name`)
VALUES (1, 'Available'),
(2, 'Rented'),
(3, 'Maintenance'),
(4, 'Inactive');

INSERT INTO `bike_types` (`bike_type_id`, `description`, `type_name`)
VALUES (1, 'Off-road cycling', 'Mountain Bike'),
(2, 'Paved road cycling', 'Road Bike'),
(3, 'Versatile for various terrains', 'Hybrid Bike'),
(4, 'E-bike with motor assistance', 'Electric Bike'),
(5, 'Urban commuting', 'City/Commuter Bike'),
(6, 'Tricks and stunts', 'BMX'),
(7, 'Compact and portable', 'Folding Bike');

INSERT INTO `booking_statuses` (`status_id`, `status_name`)
VALUES (1, 'Pending'),
(2, 'Active'),
(3, 'Completed'),
(4, 'Cancelled');

INSERT INTO `payment_methods` (`method_id`, `method_name`)
VALUES (1, 'Wallet'),
(2, 'GCash'),
(3, 'QRPH'),
(4, 'Cash');

INSERT INTO `transaction_types` (`type_id`, `type_name`)
VALUES (1, 'Load'),
(2, 'Withdrawal'),
(3, 'Rental Payment'),
(4, 'Rental Earnings'),
(5, 'Refund'),
(6, 'Service Fee');

CREATE INDEX `IX_bike_images_bike_id` ON `bike_images` (`bike_id`);

CREATE INDEX `IX_bikes_availability_status_id` ON `bikes` (`availability_status_id`);

CREATE INDEX `IX_bikes_bike_type_id` ON `bikes` (`bike_type_id`);

CREATE INDEX `IX_bikes_booking_count` ON `bikes` (`booking_count`);

CREATE INDEX `IX_bikes_hourly_rate_daily_rate` ON `bikes` (`hourly_rate`, `daily_rate`);

CREATE INDEX `IX_bikes_is_deleted` ON `bikes` (`is_deleted`);

CREATE INDEX `IX_bikes_owner_id` ON `bikes` (`owner_id`);

CREATE INDEX `IX_bikes_view_count` ON `bikes` (`view_count`);

CREATE INDEX `IX_bookings_bike_id` ON `bookings` (`bike_id`);

CREATE INDEX `IX_bookings_booking_status_id` ON `bookings` (`booking_status_id`);

CREATE INDEX `IX_bookings_is_deleted` ON `bookings` (`is_deleted`);

CREATE INDEX `IX_bookings_renter_id` ON `bookings` (`renter_id`);

CREATE INDEX `IX_bookings_start_date` ON `bookings` (`start_date`);

CREATE INDEX `IX_credit_transactions_transaction_type_id` ON `credit_transactions` (`transaction_type_id`);

CREATE INDEX `IX_credit_transactions_wallet_id` ON `credit_transactions` (`wallet_id`);

CREATE INDEX `IX_notifications_user_id_is_read` ON `notifications` (`user_id`, `is_read`);

CREATE INDEX `IX_payments_booking_id` ON `payments` (`booking_id`);

CREATE INDEX `IX_payments_payment_method_id` ON `payments` (`payment_method_id`);

CREATE UNIQUE INDEX `IX_points_user_id` ON `points` (`user_id`);

CREATE INDEX `IX_points_history_points_id` ON `points_history` (`points_id`);

CREATE INDEX `IX_ratings_bike_id` ON `ratings` (`bike_id`);

CREATE INDEX `IX_ratings_booking_id` ON `ratings` (`booking_id`);

CREATE INDEX `IX_ratings_is_flagged` ON `ratings` (`is_flagged`);

CREATE INDEX `IX_ratings_rated_user_id` ON `ratings` (`rated_user_id`);

CREATE INDEX `IX_ratings_rater_id` ON `ratings` (`rater_id`);

CREATE INDEX `IX_ratings_rating_value` ON `ratings` (`rating_value`);

CREATE INDEX `IX_reports_assigned_to` ON `reports` (`assigned_to`);

CREATE INDEX `IX_reports_booking_id` ON `reports` (`booking_id`);

CREATE INDEX `IX_reports_reported_bike_id` ON `reports` (`reported_bike_id`);

CREATE INDEX `IX_reports_reported_user_id` ON `reports` (`reported_user_id`);

CREATE INDEX `IX_reports_reporter_id` ON `reports` (`reporter_id`);

CREATE UNIQUE INDEX `IX_users_email` ON `users` (`email`);

CREATE INDEX `IX_users_is_deleted` ON `users` (`is_deleted`);

CREATE INDEX `IX_users_is_email_verified` ON `users` (`is_email_verified`);

CREATE INDEX `IX_users_last_login_at` ON `users` (`last_login_at`);

CREATE UNIQUE INDEX `IX_wallets_user_id` ON `wallets` (`user_id`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251029073438_CleanOptimizedDatabase', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `users` ADD `address_verified` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `users` ADD `address_verified_at` datetime(6) NULL;

ALTER TABLE `users` ADD `id_extracted_address` varchar(500) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `users` ADD `id_verified` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `users` ADD `id_verified_at` datetime(6) NULL;

ALTER TABLE `users` ADD `store_address` varchar(500) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `users` ADD `store_name` varchar(255) CHARACTER SET utf8mb4 NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251118102041_AddRegistrationValidationFields', '8.0.0');

COMMIT;

START TRANSACTION;

CREATE TABLE `phone_otps` (
    `otp_id` int NOT NULL AUTO_INCREMENT,
    `phone_number` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `otp_code` varchar(6) CHARACTER SET utf8mb4 NOT NULL,
    `expires_at` datetime(6) NOT NULL,
    `is_verified` tinyint(1) NOT NULL,
    `verified_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL,
    `attempts` int NOT NULL,
    `max_attempts` int NOT NULL,
    CONSTRAINT `PK_phone_otps` PRIMARY KEY (`otp_id`)
) CHARACTER SET=utf8mb4;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251121061026_AddPhoneOtpTable', '8.0.0');

COMMIT;

START TRANSACTION;

INSERT INTO `payment_methods` (`method_id`, `method_name`)
VALUES (5, 'PayMaya'),
(6, 'Credit/Debit Card');

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251121071618_AddPayMayaAndCardsPaymentMethods', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `users` ADD `geofence_radius_km` decimal(65,30) NULL;

ALTER TABLE `users` ADD `store_latitude` double NULL;

ALTER TABLE `users` ADD `store_longitude` double NULL;

CREATE TABLE `location_tracking` (
    `tracking_id` int NOT NULL AUTO_INCREMENT,
    `booking_id` int NOT NULL,
    `latitude` double NOT NULL,
    `longitude` double NOT NULL,
    `distance_from_store_km` decimal(10,2) NULL,
    `is_within_geofence` tinyint(1) NOT NULL,
    `tracked_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_location_tracking` PRIMARY KEY (`tracking_id`),
    CONSTRAINT `FK_location_tracking_bookings_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`booking_id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_location_tracking_booking_id` ON `location_tracking` (`booking_id`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251126042848_AddGeofencingSupport', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `payments` ADD `owner_verified_at` datetime(6) NULL;

ALTER TABLE `payments` ADD `owner_verified_by` int NULL;

ALTER TABLE `bookings` ADD `quantity` int NOT NULL DEFAULT 0;

ALTER TABLE `bikes` ADD `quantity` int NOT NULL DEFAULT 0;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251130150759_AddQuantityAndPaymentVerification', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `users` ADD `id_document_back_url` varchar(255) CHARACTER SET utf8mb4 NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251202043221_AddIdDocumentBackUrl', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `bikes` ADD `store_id` int NULL;

CREATE TABLE `stores` (
    `store_id` int NOT NULL AUTO_INCREMENT,
    `owner_id` int NOT NULL,
    `store_name` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `store_address` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `store_latitude` double NULL,
    `store_longitude` double NULL,
    `geofence_radius_km` decimal(65,30) NULL,
    `is_primary` tinyint(1) NOT NULL,
    `is_active` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `is_deleted` tinyint(1) NOT NULL,
    `deleted_at` datetime(6) NULL,
    CONSTRAINT `PK_stores` PRIMARY KEY (`store_id`),
    CONSTRAINT `FK_stores_users_owner_id` FOREIGN KEY (`owner_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_payments_owner_verified_by` ON `payments` (`owner_verified_by`);

CREATE INDEX `IX_bikes_store_id` ON `bikes` (`store_id`);

CREATE INDEX `IX_stores_owner_id` ON `stores` (`owner_id`);

ALTER TABLE `bikes` ADD CONSTRAINT `FK_bikes_stores_store_id` FOREIGN KEY (`store_id`) REFERENCES `stores` (`store_id`) ON DELETE SET NULL;

ALTER TABLE `payments` ADD CONSTRAINT `FK_payments_users_owner_verified_by` FOREIGN KEY (`owner_verified_by`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT;


                INSERT INTO stores (owner_id, store_name, store_address, store_latitude, store_longitude, geofence_radius_km, is_primary, is_active, created_at, updated_at, is_deleted, deleted_at)
                SELECT 
                    user_id,
                    COALESCE(store_name, 'My Store'),
                    COALESCE(store_address, ''),
                    store_latitude,
                    store_longitude,
                    geofence_radius_km,
                    true,
                    true,
                    created_at,
                    updated_at,
                    is_deleted,
                    deleted_at
                FROM users
                WHERE is_owner = true 
                    AND (store_name IS NOT NULL OR store_address IS NOT NULL)
                    AND user_id NOT IN (SELECT owner_id FROM stores);
            


                UPDATE bikes b
                INNER JOIN stores s ON b.owner_id = s.owner_id AND s.is_primary = true
                SET b.store_id = s.store_id
                WHERE b.store_id IS NULL;
            

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251202050030_NormalizeDatabase', '8.0.0');

COMMIT;

START TRANSACTION;

DROP TABLE `points_history`;

DROP TABLE `points`;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251202074331_RemovePointsSystem', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `bookings` ADD `location_permission_denied_at` datetime(6) NULL;

ALTER TABLE `bookings` ADD `location_permission_granted` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251202142515_AddLocationPermissionTracking', '8.0.0');

COMMIT;

START TRANSACTION;

ALTER TABLE `bookings` ADD `is_reported_lost` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `bookings` ADD `lost_report_notes` varchar(500) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `bookings` ADD `reported_lost_at` datetime(6) NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251202214211_AddLostBikeReporting', '8.0.0');

COMMIT;

