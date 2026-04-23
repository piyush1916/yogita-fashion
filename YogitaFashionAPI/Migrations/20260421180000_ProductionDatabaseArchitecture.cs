#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using YogitaFashionAPI.Data;

namespace YogitaFashionAPI.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260421180000_ProductionDatabaseArchitecture")]
    public partial class ProductionDatabaseArchitecture : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            CreateHelpers(migrationBuilder);
            CreateTablesIfMissing(migrationBuilder);
            AddMissingColumns(migrationBuilder);
            NormalizeLegacyData(migrationBuilder);
            BackfillOrderLineItems(migrationBuilder);
            HardenColumns(migrationBuilder);
            RemoveDriftColumns(migrationBuilder);
            AddIndexes(migrationBuilder);
            AddForeignKeys(migrationBuilder);
            DropHelpers(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            CreateHelpers(migrationBuilder);
            DropForeignKeys(migrationBuilder);
            migrationBuilder.Sql("DROP TABLE IF EXISTS `orderitems`;");
            migrationBuilder.Sql("CALL DropColumnIfExists('orders', 'CouponId');");
            migrationBuilder.Sql("CALL DropColumnIfExists('products', 'UpdatedAt');");
            DropHelpers(migrationBuilder);
        }

        private static void CreateHelpers(MigrationBuilder migrationBuilder)
        {
            DropHelpers(migrationBuilder);

            migrationBuilder.Sql(@"
CREATE PROCEDURE AddColumnIfMissing(IN tableName VARCHAR(64), IN columnName VARCHAR(64), IN ddl TEXT)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tableName AND COLUMN_NAME = columnName
    ) THEN
        SET @sql = ddl;
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END");

            migrationBuilder.Sql(@"
CREATE PROCEDURE DropColumnIfExists(IN tableName VARCHAR(64), IN columnName VARCHAR(64))
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tableName AND COLUMN_NAME = columnName
    ) THEN
        SET @sql = CONCAT('ALTER TABLE `', tableName, '` DROP COLUMN `', columnName, '`');
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END");

            migrationBuilder.Sql(@"
CREATE PROCEDURE AddIndexIfMissing(IN tableName VARCHAR(64), IN indexName VARCHAR(64), IN ddl TEXT)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tableName AND INDEX_NAME = indexName
    ) THEN
        SET @sql = ddl;
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END");

            migrationBuilder.Sql(@"
CREATE PROCEDURE DropForeignKeyIfExists(IN tableName VARCHAR(64), IN constraintName VARCHAR(64))
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.TABLE_CONSTRAINTS
        WHERE CONSTRAINT_SCHEMA = DATABASE()
          AND TABLE_NAME = tableName
          AND CONSTRAINT_NAME = constraintName
          AND CONSTRAINT_TYPE = 'FOREIGN KEY'
    ) THEN
        SET @sql = CONCAT('ALTER TABLE `', tableName, '` DROP FOREIGN KEY `', constraintName, '`');
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END");

            migrationBuilder.Sql(@"
CREATE PROCEDURE AddForeignKeyIfMissing(IN tableName VARCHAR(64), IN constraintName VARCHAR(64), IN ddl TEXT)
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.TABLE_CONSTRAINTS
        WHERE CONSTRAINT_SCHEMA = DATABASE()
          AND TABLE_NAME = tableName
          AND CONSTRAINT_NAME = constraintName
          AND CONSTRAINT_TYPE = 'FOREIGN KEY'
    ) THEN
        SET @sql = ddl;
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END");
        }

        private static void DropHelpers(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS AddForeignKeyIfMissing;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS DropForeignKeyIfExists;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS AddIndexIfMissing;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS DropColumnIfExists;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS AddColumnIfMissing;");
        }

        private static void CreateTablesIfMissing(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `users` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Name` VARCHAR(100) NOT NULL DEFAULT '',
    `Email` VARCHAR(191) NOT NULL,
    `Password` VARCHAR(255) NOT NULL,
    `Phone` VARCHAR(30) NOT NULL DEFAULT '',
    `City` VARCHAR(100) NOT NULL DEFAULT '',
    `Role` VARCHAR(50) NOT NULL DEFAULT 'Customer',
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `products` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Name` VARCHAR(150) NOT NULL,
    `Price` DECIMAL(10,2) NOT NULL DEFAULT 0,
    `OriginalPrice` DECIMAL(10,2) NOT NULL DEFAULT 0,
    `Mrp` DECIMAL(10,2) NOT NULL DEFAULT 0,
    `Category` VARCHAR(100) NOT NULL DEFAULT '',
    `Brand` VARCHAR(100) NOT NULL DEFAULT '',
    `ImageUrl` VARCHAR(500) NOT NULL DEFAULT '',
    `Description` LONGTEXT NOT NULL,
    `Size` VARCHAR(100) NOT NULL DEFAULT '',
    `Color` VARCHAR(100) NOT NULL DEFAULT '',
    `Stock` INT NOT NULL DEFAULT 0,
    `FeaturedProduct` TINYINT(1) NOT NULL DEFAULT 0,
    `IsBestSeller` TINYINT(1) NOT NULL DEFAULT 0,
    `SizesJson` LONGTEXT NOT NULL,
    `ColorsJson` LONGTEXT NOT NULL,
    `DetailsJson` LONGTEXT NOT NULL,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `coupons` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Code` VARCHAR(50) NOT NULL,
    `Type` VARCHAR(20) NOT NULL DEFAULT 'percent',
    `Value` DECIMAL(10,2) NOT NULL DEFAULT 0,
    `MinOrderAmount` DECIMAL(10,2) NOT NULL DEFAULT 0,
    `MaxUses` INT NOT NULL DEFAULT 0,
    `MaxUsesPerUser` INT NOT NULL DEFAULT 1,
    `UsedCount` INT NOT NULL DEFAULT 0,
    `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
    `StartAt` DATETIME(6) NULL,
    `ExpiryDate` DATETIME(6) NULL,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `orders` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `UserId` INT NULL,
    `CouponId` INT NULL,
    `TotalAmount` DECIMAL(10,2) NOT NULL DEFAULT 0,
    `Status` VARCHAR(50) NOT NULL DEFAULT 'Pending',
    `OrderDate` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `Name` VARCHAR(100) NOT NULL DEFAULT '',
    `Phone` VARCHAR(30) NOT NULL DEFAULT '',
    `Email` VARCHAR(191) NOT NULL DEFAULT '',
    `Address` VARCHAR(500) NOT NULL DEFAULT '',
    `City` VARCHAR(100) NOT NULL DEFAULT '',
    `Pincode` VARCHAR(20) NOT NULL DEFAULT '',
    `Payment` VARCHAR(50) NOT NULL DEFAULT 'COD',
    `OrderNumber` VARCHAR(100) NOT NULL DEFAULT '',
    `TrackingNumber` VARCHAR(100) NOT NULL DEFAULT '',
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `ItemsJson` LONGTEXT NOT NULL,
    `StatusHistoryJson` LONGTEXT NOT NULL,
    `CouponCode` VARCHAR(80) NOT NULL DEFAULT '',
    `DiscountAmount` DECIMAL(10,2) NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `orderitems` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `OrderId` INT NOT NULL,
    `ProductId` INT NOT NULL,
    `ItemKey` VARCHAR(120) NOT NULL,
    `Title` VARCHAR(180) NOT NULL,
    `Image` VARCHAR(500) NOT NULL DEFAULT '',
    `Price` DECIMAL(10,2) NOT NULL DEFAULT 0,
    `Mrp` DECIMAL(10,2) NOT NULL DEFAULT 0,
    `Category` VARCHAR(100) NOT NULL DEFAULT '',
    `Size` VARCHAR(80) NOT NULL DEFAULT '',
    `Color` VARCHAR(80) NOT NULL DEFAULT '',
    `Quantity` INT NOT NULL DEFAULT 1,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `addresses` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `UserId` INT NOT NULL,
    `FullName` VARCHAR(100) NOT NULL DEFAULT '',
    `Phone` VARCHAR(30) NOT NULL DEFAULT '',
    `Street` VARCHAR(300) NOT NULL DEFAULT '',
    `City` VARCHAR(100) NOT NULL DEFAULT '',
    `State` VARCHAR(100) NOT NULL DEFAULT '',
    `ZipCode` VARCHAR(20) NOT NULL DEFAULT '',
    `Country` VARCHAR(100) NOT NULL DEFAULT 'India',
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `Line2` VARCHAR(255) NOT NULL DEFAULT '',
    `Landmark` VARCHAR(255) NOT NULL DEFAULT '',
    `AddressType` VARCHAR(30) NOT NULL DEFAULT 'Home',
    `IsDefault` TINYINT(1) NOT NULL DEFAULT 0,
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `wishlistitems` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `UserId` INT NOT NULL,
    `ProductId` INT NOT NULL,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `couponusagerecords` (
    `CouponId` INT NOT NULL,
    `UserId` INT NOT NULL,
    `Count` INT NOT NULL DEFAULT 0,
    PRIMARY KEY (`CouponId`, `UserId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `returnrequests` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `OrderId` INT NOT NULL,
    `UserId` INT NOT NULL,
    `ItemProductId` INT NULL,
    `ItemProductCode` VARCHAR(120) NOT NULL DEFAULT '',
    `ItemTitle` VARCHAR(180) NOT NULL DEFAULT '',
    `Quantity` INT NOT NULL DEFAULT 1,
    `RefundAmount` DECIMAL(10,2) NOT NULL DEFAULT 0,
    `Reason` LONGTEXT NOT NULL,
    `Status` VARCHAR(50) NOT NULL DEFAULT 'Pending',
    `CustomerRemark` LONGTEXT NOT NULL,
    `AdminRemark` LONGTEXT NOT NULL,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `supportrequests` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `UserId` INT NULL,
    `OrderId` INT NULL,
    `OrderReference` VARCHAR(120) NOT NULL DEFAULT '',
    `Subject` VARCHAR(150) NOT NULL DEFAULT 'General Support',
    `Message` LONGTEXT NOT NULL,
    `Status` VARCHAR(50) NOT NULL DEFAULT 'Open',
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `Name` VARCHAR(100) NOT NULL DEFAULT '',
    `Contact` VARCHAR(100) NOT NULL DEFAULT '',
    `Email` VARCHAR(191) NOT NULL DEFAULT '',
    `Phone` VARCHAR(30) NOT NULL DEFAULT '',
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `stockalertsubscriptions` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `UserId` INT NULL,
    `ProductId` INT NOT NULL,
    `Email` VARCHAR(191) NOT NULL DEFAULT '',
    `WhatsAppNumber` VARCHAR(30) NOT NULL DEFAULT '',
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `NotifiedAt` DATETIME(6) NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `auditlogentries` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT,
    `UserId` INT NULL,
    `Role` VARCHAR(40) NOT NULL DEFAULT '',
    `Action` VARCHAR(120) NOT NULL,
    `Module` VARCHAR(120) NOT NULL,
    `EntityId` VARCHAR(120) NOT NULL DEFAULT '',
    `TimestampUtc` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `IpAddress` VARCHAR(64) NOT NULL DEFAULT '',
    `Status` VARCHAR(20) NOT NULL DEFAULT 'Success',
    `MetadataJson` LONGTEXT NOT NULL,
    `RequestPath` VARCHAR(255) NOT NULL DEFAULT '',
    `HttpMethod` VARCHAR(10) NOT NULL DEFAULT '',
    `PreviousHash` VARCHAR(128) NOT NULL DEFAULT '',
    `EntryHash` VARCHAR(128) NOT NULL DEFAULT '',
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");
        }

        private static void AddMissingColumns(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL AddColumnIfMissing('products', 'SizesJson', 'ALTER TABLE `products` ADD COLUMN `SizesJson` LONGTEXT NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('products', 'ColorsJson', 'ALTER TABLE `products` ADD COLUMN `ColorsJson` LONGTEXT NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('products', 'DetailsJson', 'ALTER TABLE `products` ADD COLUMN `DetailsJson` LONGTEXT NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('products', 'CreatedAt', 'ALTER TABLE `products` ADD COLUMN `CreatedAt` DATETIME(6) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('products', 'UpdatedAt', 'ALTER TABLE `products` ADD COLUMN `UpdatedAt` DATETIME(6) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('orders', 'CouponId', 'ALTER TABLE `orders` ADD COLUMN `CouponId` INT NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('orders', 'ItemsJson', 'ALTER TABLE `orders` ADD COLUMN `ItemsJson` LONGTEXT NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('orders', 'StatusHistoryJson', 'ALTER TABLE `orders` ADD COLUMN `StatusHistoryJson` LONGTEXT NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('orders', 'CouponCode', 'ALTER TABLE `orders` ADD COLUMN `CouponCode` VARCHAR(80) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('orders', 'DiscountAmount', 'ALTER TABLE `orders` ADD COLUMN `DiscountAmount` DECIMAL(10,2) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('addresses', 'Country', 'ALTER TABLE `addresses` ADD COLUMN `Country` VARCHAR(100) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('addresses', 'Line2', 'ALTER TABLE `addresses` ADD COLUMN `Line2` VARCHAR(255) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('addresses', 'Landmark', 'ALTER TABLE `addresses` ADD COLUMN `Landmark` VARCHAR(255) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('addresses', 'AddressType', 'ALTER TABLE `addresses` ADD COLUMN `AddressType` VARCHAR(30) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('addresses', 'IsDefault', 'ALTER TABLE `addresses` ADD COLUMN `IsDefault` TINYINT(1) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('addresses', 'UpdatedAt', 'ALTER TABLE `addresses` ADD COLUMN `UpdatedAt` DATETIME(6) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('wishlistitems', 'CreatedAt', 'ALTER TABLE `wishlistitems` ADD COLUMN `CreatedAt` DATETIME(6) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('coupons', 'Type', 'ALTER TABLE `coupons` ADD COLUMN `Type` VARCHAR(20) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('coupons', 'Value', 'ALTER TABLE `coupons` ADD COLUMN `Value` DECIMAL(10,2) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('coupons', 'MaxUsesPerUser', 'ALTER TABLE `coupons` ADD COLUMN `MaxUsesPerUser` INT NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('coupons', 'StartAt', 'ALTER TABLE `coupons` ADD COLUMN `StartAt` DATETIME(6) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('supportrequests', 'OrderReference', 'ALTER TABLE `supportrequests` ADD COLUMN `OrderReference` VARCHAR(120) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('supportrequests', 'UpdatedAt', 'ALTER TABLE `supportrequests` ADD COLUMN `UpdatedAt` DATETIME(6) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('returnrequests', 'ItemProductCode', 'ALTER TABLE `returnrequests` ADD COLUMN `ItemProductCode` VARCHAR(120) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('returnrequests', 'UpdatedAt', 'ALTER TABLE `returnrequests` ADD COLUMN `UpdatedAt` DATETIME(6) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('stockalertsubscriptions', 'UserId', 'ALTER TABLE `stockalertsubscriptions` ADD COLUMN `UserId` INT NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('stockalertsubscriptions', 'WhatsAppNumber', 'ALTER TABLE `stockalertsubscriptions` ADD COLUMN `WhatsAppNumber` VARCHAR(30) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'Role', 'ALTER TABLE `auditlogentries` ADD COLUMN `Role` VARCHAR(40) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'Module', 'ALTER TABLE `auditlogentries` ADD COLUMN `Module` VARCHAR(120) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'EntityId', 'ALTER TABLE `auditlogentries` ADD COLUMN `EntityId` VARCHAR(120) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'TimestampUtc', 'ALTER TABLE `auditlogentries` ADD COLUMN `TimestampUtc` DATETIME(6) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'IpAddress', 'ALTER TABLE `auditlogentries` ADD COLUMN `IpAddress` VARCHAR(64) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'Status', 'ALTER TABLE `auditlogentries` ADD COLUMN `Status` VARCHAR(20) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'MetadataJson', 'ALTER TABLE `auditlogentries` ADD COLUMN `MetadataJson` LONGTEXT NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'RequestPath', 'ALTER TABLE `auditlogentries` ADD COLUMN `RequestPath` VARCHAR(255) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'HttpMethod', 'ALTER TABLE `auditlogentries` ADD COLUMN `HttpMethod` VARCHAR(10) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'PreviousHash', 'ALTER TABLE `auditlogentries` ADD COLUMN `PreviousHash` VARCHAR(128) NULL');");
            migrationBuilder.Sql("CALL AddColumnIfMissing('auditlogentries', 'EntryHash', 'ALTER TABLE `auditlogentries` ADD COLUMN `EntryHash` VARCHAR(128) NULL');");
        }

        private static void NormalizeLegacyData(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `users` SET `Email` = LOWER(TRIM(IFNULL(`Email`, ''))), `Name` = IFNULL(`Name`, ''), `Password` = IFNULL(`Password`, ''), `Phone` = IFNULL(`Phone`, ''), `City` = IFNULL(`City`, ''), `Role` = IFNULL(NULLIF(TRIM(`Role`), ''), 'Customer'), `CreatedAt` = IFNULL(`CreatedAt`, UTC_TIMESTAMP(6));");
            migrationBuilder.Sql("UPDATE `users` SET `Email` = CONCAT('legacy-user-', `Id`, '@local.invalid') WHERE `Email` = '';");
            migrationBuilder.Sql("DELETE u1 FROM `users` u1 JOIN `users` u2 ON u1.`Email` = u2.`Email` AND u1.`Id` > u2.`Id`;");

            migrationBuilder.Sql("UPDATE `products` SET `Name` = IFNULL(NULLIF(TRIM(`Name`), ''), CONCAT('Product ', `Id`)), `Category` = IFNULL(`Category`, ''), `Brand` = IFNULL(`Brand`, ''), `ImageUrl` = IFNULL(`ImageUrl`, ''), `Description` = IFNULL(`Description`, ''), `Size` = IFNULL(`Size`, ''), `Color` = IFNULL(`Color`, ''), `Price` = IFNULL(`Price`, 0), `OriginalPrice` = IFNULL(`OriginalPrice`, IFNULL(`Mrp`, IFNULL(`Price`, 0))), `Mrp` = IFNULL(`Mrp`, IFNULL(`OriginalPrice`, IFNULL(`Price`, 0))), `Stock` = IFNULL(`Stock`, 0), `FeaturedProduct` = IFNULL(`FeaturedProduct`, 0), `IsBestSeller` = IFNULL(`IsBestSeller`, 0), `CreatedAt` = IFNULL(`CreatedAt`, UTC_TIMESTAMP(6)), `UpdatedAt` = IFNULL(`UpdatedAt`, UTC_TIMESTAMP(6));");
            migrationBuilder.Sql("UPDATE `products` SET `SizesJson` = JSON_ARRAY(TRIM(`Size`)) WHERE (`SizesJson` IS NULL OR TRIM(`SizesJson`) = '' OR JSON_VALID(`SizesJson`) = 0) AND TRIM(IFNULL(`Size`, '')) <> '';");
            migrationBuilder.Sql("UPDATE `products` SET `ColorsJson` = JSON_ARRAY(TRIM(`Color`)) WHERE (`ColorsJson` IS NULL OR TRIM(`ColorsJson`) = '' OR JSON_VALID(`ColorsJson`) = 0) AND TRIM(IFNULL(`Color`, '')) <> '';");
            migrationBuilder.Sql("UPDATE `products` SET `SizesJson` = '[]' WHERE `SizesJson` IS NULL OR TRIM(`SizesJson`) = '' OR JSON_VALID(`SizesJson`) = 0;");
            migrationBuilder.Sql("UPDATE `products` SET `ColorsJson` = '[]' WHERE `ColorsJson` IS NULL OR TRIM(`ColorsJson`) = '' OR JSON_VALID(`ColorsJson`) = 0;");
            migrationBuilder.Sql("UPDATE `products` SET `DetailsJson` = '{}' WHERE `DetailsJson` IS NULL OR TRIM(`DetailsJson`) = '' OR JSON_VALID(`DetailsJson`) = 0;");

            migrationBuilder.Sql("UPDATE `coupons` SET `Code` = UPPER(TRIM(IFNULL(`Code`, ''))), `Type` = IFNULL(NULLIF(TRIM(`Type`), ''), 'percent'), `Value` = IFNULL(NULLIF(`Value`, 0), IFNULL(`DiscountPercent`, 0)), `MinOrderAmount` = IFNULL(`MinOrderAmount`, 0), `MaxUses` = IFNULL(`MaxUses`, 0), `MaxUsesPerUser` = GREATEST(1, IFNULL(`MaxUsesPerUser`, 1)), `UsedCount` = IFNULL(`UsedCount`, 0), `IsActive` = IFNULL(`IsActive`, 1), `CreatedAt` = IFNULL(`CreatedAt`, UTC_TIMESTAMP(6)), `UpdatedAt` = IFNULL(`UpdatedAt`, UTC_TIMESTAMP(6));");
            migrationBuilder.Sql("UPDATE `coupons` SET `Code` = CONCAT('LEGACY-', `Id`) WHERE `Code` = '';");
            migrationBuilder.Sql("DELETE c1 FROM `coupons` c1 JOIN `coupons` c2 ON c1.`Code` = c2.`Code` AND c1.`Id` > c2.`Id`;");

            migrationBuilder.Sql("UPDATE `orders` SET `UserId` = NULL WHERE `UserId` = 0 OR `UserId` NOT IN (SELECT `Id` FROM `users`);");
            migrationBuilder.Sql("UPDATE `orders` o LEFT JOIN `coupons` c ON c.`Code` = UPPER(TRIM(IFNULL(o.`CouponCode`, ''))) SET o.`CouponId` = c.`Id` WHERE o.`CouponId` IS NULL AND c.`Id` IS NOT NULL;");
            migrationBuilder.Sql("UPDATE `orders` SET `Name` = IFNULL(`Name`, ''), `Phone` = IFNULL(`Phone`, ''), `Email` = LOWER(TRIM(IFNULL(`Email`, ''))), `Address` = IFNULL(`Address`, ''), `City` = IFNULL(`City`, ''), `Pincode` = IFNULL(`Pincode`, ''), `Payment` = IFNULL(NULLIF(TRIM(`Payment`), ''), 'COD'), `Status` = IFNULL(NULLIF(TRIM(`Status`), ''), 'Pending'), `OrderNumber` = IFNULL(NULLIF(TRIM(`OrderNumber`), ''), CONCAT('YF-LEGACY-', LPAD(`Id`, 6, '0'))), `TrackingNumber` = IFNULL(`TrackingNumber`, ''), `TotalAmount` = IFNULL(`TotalAmount`, 0), `OrderDate` = IFNULL(`OrderDate`, UTC_TIMESTAMP(6)), `UpdatedAt` = IFNULL(`UpdatedAt`, UTC_TIMESTAMP(6)), `ItemsJson` = IF(JSON_VALID(IFNULL(`ItemsJson`, '')) = 1, `ItemsJson`, '[]'), `StatusHistoryJson` = IF(JSON_VALID(IFNULL(`StatusHistoryJson`, '')) = 1, `StatusHistoryJson`, '[]'), `CouponCode` = UPPER(TRIM(IFNULL(`CouponCode`, ''))), `DiscountAmount` = IFNULL(`DiscountAmount`, 0);");

            migrationBuilder.Sql("DELETE FROM `addresses` WHERE `UserId` IS NULL OR `UserId` NOT IN (SELECT `Id` FROM `users`);");
            migrationBuilder.Sql("UPDATE `addresses` SET `FullName` = IFNULL(`FullName`, ''), `Phone` = IFNULL(`Phone`, ''), `Street` = IFNULL(`Street`, ''), `City` = IFNULL(`City`, ''), `State` = IFNULL(`State`, ''), `ZipCode` = IFNULL(`ZipCode`, ''), `Country` = IFNULL(NULLIF(TRIM(`Country`), ''), 'India'), `Line2` = IFNULL(`Line2`, ''), `Landmark` = IFNULL(`Landmark`, ''), `AddressType` = IFNULL(NULLIF(TRIM(`AddressType`), ''), 'Home'), `IsDefault` = IFNULL(`IsDefault`, 0), `CreatedAt` = IFNULL(`CreatedAt`, UTC_TIMESTAMP(6)), `UpdatedAt` = IFNULL(`UpdatedAt`, UTC_TIMESTAMP(6));");

            migrationBuilder.Sql("DELETE FROM `wishlistitems` WHERE `UserId` IS NULL OR `ProductId` IS NULL OR `UserId` NOT IN (SELECT `Id` FROM `users`) OR `ProductId` NOT IN (SELECT `Id` FROM `products`);");
            migrationBuilder.Sql("DELETE w1 FROM `wishlistitems` w1 JOIN `wishlistitems` w2 ON w1.`UserId` = w2.`UserId` AND w1.`ProductId` = w2.`ProductId` AND w1.`Id` > w2.`Id`;");
            migrationBuilder.Sql("UPDATE `wishlistitems` SET `CreatedAt` = IFNULL(`CreatedAt`, UTC_TIMESTAMP(6));");

            migrationBuilder.Sql("DELETE FROM `couponusagerecords` WHERE `CouponId` NOT IN (SELECT `Id` FROM `coupons`) OR `UserId` NOT IN (SELECT `Id` FROM `users`);");
            migrationBuilder.Sql("UPDATE `couponusagerecords` SET `Count` = GREATEST(0, IFNULL(`Count`, 0));");

            migrationBuilder.Sql("UPDATE `returnrequests` SET `ItemProductCode` = IFNULL(NULLIF(`ItemProductCode`, ''), CAST(`ItemProductId` AS CHAR));");
            migrationBuilder.Sql("UPDATE `returnrequests` SET `ItemProductId` = NULL WHERE `ItemProductId` IS NOT NULL AND `ItemProductId` NOT IN (SELECT `Id` FROM `products`);");
            migrationBuilder.Sql("DELETE FROM `returnrequests` WHERE `OrderId` IS NULL OR `UserId` IS NULL OR `OrderId` NOT IN (SELECT `Id` FROM `orders`) OR `UserId` NOT IN (SELECT `Id` FROM `users`);");
            migrationBuilder.Sql("UPDATE `returnrequests` SET `ItemProductCode` = IFNULL(`ItemProductCode`, ''), `ItemTitle` = IFNULL(`ItemTitle`, ''), `Quantity` = GREATEST(1, IFNULL(`Quantity`, 1)), `RefundAmount` = IFNULL(`RefundAmount`, 0), `Reason` = IFNULL(`Reason`, ''), `Status` = IFNULL(NULLIF(TRIM(`Status`), ''), 'Pending'), `CustomerRemark` = IFNULL(`CustomerRemark`, ''), `AdminRemark` = IFNULL(`AdminRemark`, ''), `CreatedAt` = IFNULL(`CreatedAt`, UTC_TIMESTAMP(6)), `UpdatedAt` = IFNULL(`UpdatedAt`, UTC_TIMESTAMP(6));");

            migrationBuilder.Sql("UPDATE `supportrequests` SET `OrderReference` = IFNULL(NULLIF(`OrderReference`, ''), CAST(`OrderId` AS CHAR));");
            migrationBuilder.Sql("UPDATE `supportrequests` SET `UserId` = NULL WHERE `UserId` = 0 OR `UserId` NOT IN (SELECT `Id` FROM `users`);");
            migrationBuilder.Sql("UPDATE `supportrequests` SET `OrderId` = NULL WHERE `OrderId` IS NOT NULL AND `OrderId` NOT IN (SELECT `Id` FROM `orders`);");
            migrationBuilder.Sql("UPDATE `supportrequests` SET `Name` = IFNULL(`Name`, ''), `Contact` = IFNULL(`Contact`, ''), `Subject` = IFNULL(NULLIF(TRIM(`Subject`), ''), 'General Support'), `OrderReference` = IFNULL(`OrderReference`, ''), `Message` = IFNULL(`Message`, ''), `Email` = LOWER(TRIM(IFNULL(`Email`, ''))), `Phone` = IFNULL(`Phone`, ''), `Status` = IFNULL(NULLIF(TRIM(`Status`), ''), 'Open'), `CreatedAt` = IFNULL(`CreatedAt`, UTC_TIMESTAMP(6)), `UpdatedAt` = IFNULL(`UpdatedAt`, UTC_TIMESTAMP(6));");

            migrationBuilder.Sql("DELETE FROM `stockalertsubscriptions` WHERE `ProductId` IS NULL OR `ProductId` NOT IN (SELECT `Id` FROM `products`);");
            migrationBuilder.Sql("UPDATE `stockalertsubscriptions` SET `UserId` = NULL WHERE `UserId` = 0 OR `UserId` NOT IN (SELECT `Id` FROM `users`);");
            migrationBuilder.Sql("UPDATE `stockalertsubscriptions` SET `Email` = LOWER(TRIM(IFNULL(`Email`, ''))), `WhatsAppNumber` = IFNULL(`WhatsAppNumber`, ''), `CreatedAt` = IFNULL(`CreatedAt`, UTC_TIMESTAMP(6));");

            migrationBuilder.Sql("UPDATE `auditlogentries` SET `UserId` = NULL WHERE `UserId` = 0 OR `UserId` NOT IN (SELECT `Id` FROM `users`);");
            migrationBuilder.Sql("UPDATE `auditlogentries` SET `Action` = IFNULL(NULLIF(TRIM(`Action`), ''), 'LegacyAction'), `Role` = IFNULL(`Role`, ''), `Module` = IFNULL(NULLIF(TRIM(`Module`), ''), 'Legacy'), `EntityId` = IFNULL(`EntityId`, ''), `TimestampUtc` = IFNULL(`TimestampUtc`, IFNULL(`CreatedAt`, UTC_TIMESTAMP(6))), `IpAddress` = IFNULL(`IpAddress`, ''), `Status` = IFNULL(NULLIF(TRIM(`Status`), ''), 'Success'), `MetadataJson` = IF(JSON_VALID(IFNULL(`MetadataJson`, '')) = 1, `MetadataJson`, '{}'), `RequestPath` = IFNULL(`RequestPath`, ''), `HttpMethod` = IFNULL(`HttpMethod`, ''), `PreviousHash` = IFNULL(`PreviousHash`, ''), `EntryHash` = IFNULL(`EntryHash`, '');");
        }

        private static void BackfillOrderLineItems(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO `orderitems` (`OrderId`, `ProductId`, `ItemKey`, `Title`, `Image`, `Price`, `Mrp`, `Category`, `Size`, `Color`, `Quantity`, `CreatedAt`)
SELECT
    o.`Id`,
    p.`Id`,
    COALESCE(NULLIF(j.`ItemKey`, ''), CONCAT('legacy-', j.`Ordinal`)),
    COALESCE(NULLIF(j.`Title`, ''), p.`Name`),
    COALESCE(j.`Image`, ''),
    IFNULL(j.`Price`, 0),
    IFNULL(j.`Mrp`, IFNULL(j.`Price`, 0)),
    COALESCE(j.`Category`, ''),
    COALESCE(j.`Size`, ''),
    COALESCE(j.`Color`, ''),
    GREATEST(1, IFNULL(j.`Quantity`, 1)),
    o.`OrderDate`
FROM `orders` o
JOIN JSON_TABLE(
    o.`ItemsJson`,
    '$[*]' COLUMNS (
        `Ordinal` FOR ORDINALITY,
        `ItemKey` VARCHAR(120) PATH '$.Key' NULL ON EMPTY,
        `ProductCode` VARCHAR(120) PATH '$.ProductId' NULL ON EMPTY,
        `Title` VARCHAR(180) PATH '$.Title' NULL ON EMPTY,
        `Image` VARCHAR(500) PATH '$.Image' NULL ON EMPTY,
        `Price` DECIMAL(10,2) PATH '$.Price' NULL ON EMPTY,
        `Mrp` DECIMAL(10,2) PATH '$.Mrp' NULL ON EMPTY,
        `Category` VARCHAR(100) PATH '$.Category' NULL ON EMPTY,
        `Size` VARCHAR(80) PATH '$.Size' NULL ON EMPTY,
        `Color` VARCHAR(80) PATH '$.Color' NULL ON EMPTY,
        `Quantity` INT PATH '$.Qty' NULL ON EMPTY
    )
) j
JOIN `products` p ON p.`Id` = CAST(j.`ProductCode` AS UNSIGNED)
LEFT JOIN `orderitems` existing
    ON existing.`OrderId` = o.`Id`
   AND existing.`ItemKey` = COALESCE(NULLIF(j.`ItemKey`, ''), CONCAT('legacy-', j.`Ordinal`))
WHERE JSON_VALID(o.`ItemsJson`) = 1
  AND existing.`Id` IS NULL;");
        }

        private static void HardenColumns(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `users` MODIFY `Name` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `Email` VARCHAR(191) NOT NULL, MODIFY `Password` VARCHAR(255) NOT NULL, MODIFY `Phone` VARCHAR(30) NOT NULL DEFAULT '', MODIFY `City` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `Role` VARCHAR(50) NOT NULL DEFAULT 'Customer', MODIFY `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);");
            migrationBuilder.Sql("ALTER TABLE `products` MODIFY `Name` VARCHAR(150) NOT NULL, MODIFY `Price` DECIMAL(10,2) NOT NULL DEFAULT 0, MODIFY `OriginalPrice` DECIMAL(10,2) NOT NULL DEFAULT 0, MODIFY `Mrp` DECIMAL(10,2) NOT NULL DEFAULT 0, MODIFY `Category` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `Brand` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `ImageUrl` VARCHAR(500) NOT NULL DEFAULT '', MODIFY `Description` LONGTEXT NOT NULL, MODIFY `Size` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `Color` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `Stock` INT NOT NULL DEFAULT 0, MODIFY `FeaturedProduct` TINYINT(1) NOT NULL DEFAULT 0, MODIFY `IsBestSeller` TINYINT(1) NOT NULL DEFAULT 0, MODIFY `SizesJson` LONGTEXT NOT NULL, MODIFY `ColorsJson` LONGTEXT NOT NULL, MODIFY `DetailsJson` LONGTEXT NOT NULL, MODIFY `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), MODIFY `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);");
            migrationBuilder.Sql("ALTER TABLE `coupons` MODIFY `Code` VARCHAR(50) NOT NULL, MODIFY `Type` VARCHAR(20) NOT NULL DEFAULT 'percent', MODIFY `Value` DECIMAL(10,2) NOT NULL DEFAULT 0, MODIFY `MinOrderAmount` DECIMAL(10,2) NOT NULL DEFAULT 0, MODIFY `MaxUses` INT NOT NULL DEFAULT 0, MODIFY `MaxUsesPerUser` INT NOT NULL DEFAULT 1, MODIFY `UsedCount` INT NOT NULL DEFAULT 0, MODIFY `IsActive` TINYINT(1) NOT NULL DEFAULT 1, MODIFY `StartAt` DATETIME(6) NULL, MODIFY `ExpiryDate` DATETIME(6) NULL, MODIFY `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), MODIFY `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);");
            migrationBuilder.Sql("ALTER TABLE `orders` MODIFY `UserId` INT NULL, MODIFY `CouponId` INT NULL, MODIFY `TotalAmount` DECIMAL(10,2) NOT NULL DEFAULT 0, MODIFY `Status` VARCHAR(50) NOT NULL DEFAULT 'Pending', MODIFY `OrderDate` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), MODIFY `Name` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `Phone` VARCHAR(30) NOT NULL DEFAULT '', MODIFY `Email` VARCHAR(191) NOT NULL DEFAULT '', MODIFY `Address` VARCHAR(500) NOT NULL DEFAULT '', MODIFY `City` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `Pincode` VARCHAR(20) NOT NULL DEFAULT '', MODIFY `Payment` VARCHAR(50) NOT NULL DEFAULT 'COD', MODIFY `OrderNumber` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `TrackingNumber` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), MODIFY `ItemsJson` LONGTEXT NOT NULL, MODIFY `StatusHistoryJson` LONGTEXT NOT NULL, MODIFY `CouponCode` VARCHAR(80) NOT NULL DEFAULT '', MODIFY `DiscountAmount` DECIMAL(10,2) NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `addresses` MODIFY `UserId` INT NOT NULL, MODIFY `FullName` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `Phone` VARCHAR(30) NOT NULL DEFAULT '', MODIFY `Street` VARCHAR(300) NOT NULL DEFAULT '', MODIFY `City` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `State` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `ZipCode` VARCHAR(20) NOT NULL DEFAULT '', MODIFY `Country` VARCHAR(100) NOT NULL DEFAULT 'India', MODIFY `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), MODIFY `Line2` VARCHAR(255) NOT NULL DEFAULT '', MODIFY `Landmark` VARCHAR(255) NOT NULL DEFAULT '', MODIFY `AddressType` VARCHAR(30) NOT NULL DEFAULT 'Home', MODIFY `IsDefault` TINYINT(1) NOT NULL DEFAULT 0, MODIFY `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);");
            migrationBuilder.Sql("ALTER TABLE `wishlistitems` MODIFY `UserId` INT NOT NULL, MODIFY `ProductId` INT NOT NULL, MODIFY `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);");
            migrationBuilder.Sql("ALTER TABLE `returnrequests` MODIFY `OrderId` INT NOT NULL, MODIFY `UserId` INT NOT NULL, MODIFY `ItemProductId` INT NULL, MODIFY `ItemProductCode` VARCHAR(120) NOT NULL DEFAULT '', MODIFY `ItemTitle` VARCHAR(180) NOT NULL DEFAULT '', MODIFY `Quantity` INT NOT NULL DEFAULT 1, MODIFY `RefundAmount` DECIMAL(10,2) NOT NULL DEFAULT 0, MODIFY `Reason` LONGTEXT NOT NULL, MODIFY `Status` VARCHAR(50) NOT NULL DEFAULT 'Pending', MODIFY `CustomerRemark` LONGTEXT NOT NULL, MODIFY `AdminRemark` LONGTEXT NOT NULL, MODIFY `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), MODIFY `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);");
            migrationBuilder.Sql("ALTER TABLE `supportrequests` MODIFY `UserId` INT NULL, MODIFY `OrderId` INT NULL, MODIFY `OrderReference` VARCHAR(120) NOT NULL DEFAULT '', MODIFY `Subject` VARCHAR(150) NOT NULL DEFAULT 'General Support', MODIFY `Message` LONGTEXT NOT NULL, MODIFY `Status` VARCHAR(50) NOT NULL DEFAULT 'Open', MODIFY `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), MODIFY `Name` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `Contact` VARCHAR(100) NOT NULL DEFAULT '', MODIFY `Email` VARCHAR(191) NOT NULL DEFAULT '', MODIFY `Phone` VARCHAR(30) NOT NULL DEFAULT '', MODIFY `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);");
            migrationBuilder.Sql("ALTER TABLE `stockalertsubscriptions` MODIFY `UserId` INT NULL, MODIFY `ProductId` INT NOT NULL, MODIFY `Email` VARCHAR(191) NOT NULL DEFAULT '', MODIFY `WhatsAppNumber` VARCHAR(30) NOT NULL DEFAULT '', MODIFY `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), MODIFY `NotifiedAt` DATETIME(6) NULL;");
            migrationBuilder.Sql("ALTER TABLE `auditlogentries` MODIFY `Id` BIGINT NOT NULL AUTO_INCREMENT, MODIFY `UserId` INT NULL, MODIFY `Role` VARCHAR(40) NOT NULL DEFAULT '', MODIFY `Action` VARCHAR(120) NOT NULL, MODIFY `Module` VARCHAR(120) NOT NULL, MODIFY `EntityId` VARCHAR(120) NOT NULL DEFAULT '', MODIFY `TimestampUtc` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), MODIFY `IpAddress` VARCHAR(64) NOT NULL DEFAULT '', MODIFY `Status` VARCHAR(20) NOT NULL DEFAULT 'Success', MODIFY `MetadataJson` LONGTEXT NOT NULL, MODIFY `RequestPath` VARCHAR(255) NOT NULL DEFAULT '', MODIFY `HttpMethod` VARCHAR(10) NOT NULL DEFAULT '', MODIFY `PreviousHash` VARCHAR(128) NOT NULL DEFAULT '', MODIFY `EntryHash` VARCHAR(128) NOT NULL DEFAULT '';");
        }

        private static void RemoveDriftColumns(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL DropColumnIfExists('coupons', 'DiscountPercent');");
            migrationBuilder.Sql("CALL DropColumnIfExists('auditlogentries', 'Description');");
            migrationBuilder.Sql("CALL DropColumnIfExists('auditlogentries', 'CreatedAt');");
        }

        private static void AddIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL AddIndexIfMissing('users', 'IX_users_Email', 'CREATE UNIQUE INDEX `IX_users_Email` ON `users` (`Email`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('users', 'IX_users_Role', 'CREATE INDEX `IX_users_Role` ON `users` (`Role`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('products', 'IX_products_Category', 'CREATE INDEX `IX_products_Category` ON `products` (`Category`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('products', 'IX_products_Stock', 'CREATE INDEX `IX_products_Stock` ON `products` (`Stock`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('products', 'IX_products_FeaturedProduct', 'CREATE INDEX `IX_products_FeaturedProduct` ON `products` (`FeaturedProduct`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('products', 'IX_products_IsBestSeller', 'CREATE INDEX `IX_products_IsBestSeller` ON `products` (`IsBestSeller`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('coupons', 'IX_coupons_Code', 'CREATE UNIQUE INDEX `IX_coupons_Code` ON `coupons` (`Code`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('coupons', 'IX_coupons_IsActive_StartAt_ExpiryDate', 'CREATE INDEX `IX_coupons_IsActive_StartAt_ExpiryDate` ON `coupons` (`IsActive`, `StartAt`, `ExpiryDate`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('orders', 'IX_orders_UserId', 'CREATE INDEX `IX_orders_UserId` ON `orders` (`UserId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('orders', 'IX_orders_CouponId', 'CREATE INDEX `IX_orders_CouponId` ON `orders` (`CouponId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('orders', 'IX_orders_Status', 'CREATE INDEX `IX_orders_Status` ON `orders` (`Status`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('orders', 'IX_orders_OrderDate', 'CREATE INDEX `IX_orders_OrderDate` ON `orders` (`OrderDate`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('orders', 'IX_orders_TrackingNumber', 'CREATE INDEX `IX_orders_TrackingNumber` ON `orders` (`TrackingNumber`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('orders', 'IX_orders_OrderNumber', 'CREATE UNIQUE INDEX `IX_orders_OrderNumber` ON `orders` (`OrderNumber`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('orderitems', 'IX_orderitems_ProductId', 'CREATE INDEX `IX_orderitems_ProductId` ON `orderitems` (`ProductId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('orderitems', 'IX_orderitems_OrderId_ItemKey', 'CREATE UNIQUE INDEX `IX_orderitems_OrderId_ItemKey` ON `orderitems` (`OrderId`, `ItemKey`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('addresses', 'IX_addresses_UserId', 'CREATE INDEX `IX_addresses_UserId` ON `addresses` (`UserId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('addresses', 'IX_addresses_UserId_IsDefault', 'CREATE INDEX `IX_addresses_UserId_IsDefault` ON `addresses` (`UserId`, `IsDefault`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('wishlistitems', 'IX_wishlistitems_UserId_ProductId', 'CREATE UNIQUE INDEX `IX_wishlistitems_UserId_ProductId` ON `wishlistitems` (`UserId`, `ProductId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('wishlistitems', 'IX_wishlistitems_ProductId', 'CREATE INDEX `IX_wishlistitems_ProductId` ON `wishlistitems` (`ProductId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('couponusagerecords', 'IX_couponusagerecords_UserId', 'CREATE INDEX `IX_couponusagerecords_UserId` ON `couponusagerecords` (`UserId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('returnrequests', 'IX_returnrequests_OrderId', 'CREATE INDEX `IX_returnrequests_OrderId` ON `returnrequests` (`OrderId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('returnrequests', 'IX_returnrequests_UserId', 'CREATE INDEX `IX_returnrequests_UserId` ON `returnrequests` (`UserId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('returnrequests', 'IX_returnrequests_ItemProductId', 'CREATE INDEX `IX_returnrequests_ItemProductId` ON `returnrequests` (`ItemProductId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('returnrequests', 'IX_returnrequests_OrderId_ItemProductCode', 'CREATE INDEX `IX_returnrequests_OrderId_ItemProductCode` ON `returnrequests` (`OrderId`, `ItemProductCode`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('returnrequests', 'IX_returnrequests_Status', 'CREATE INDEX `IX_returnrequests_Status` ON `returnrequests` (`Status`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('supportrequests', 'IX_supportrequests_UserId', 'CREATE INDEX `IX_supportrequests_UserId` ON `supportrequests` (`UserId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('supportrequests', 'IX_supportrequests_OrderId', 'CREATE INDEX `IX_supportrequests_OrderId` ON `supportrequests` (`OrderId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('supportrequests', 'IX_supportrequests_Status', 'CREATE INDEX `IX_supportrequests_Status` ON `supportrequests` (`Status`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('supportrequests', 'IX_supportrequests_CreatedAt', 'CREATE INDEX `IX_supportrequests_CreatedAt` ON `supportrequests` (`CreatedAt`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('stockalertsubscriptions', 'IX_stockalertsubscriptions_UserId', 'CREATE INDEX `IX_stockalertsubscriptions_UserId` ON `stockalertsubscriptions` (`UserId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('stockalertsubscriptions', 'IX_stockalertsubscriptions_ProductId', 'CREATE INDEX `IX_stockalertsubscriptions_ProductId` ON `stockalertsubscriptions` (`ProductId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('stockalertsubscriptions', 'IX_stockalertsubscriptions_Product_Email_WhatsApp_Notified', 'CREATE INDEX `IX_stockalertsubscriptions_Product_Email_WhatsApp_Notified` ON `stockalertsubscriptions` (`ProductId`, `Email`, `WhatsAppNumber`, `NotifiedAt`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('auditlogentries', 'IX_auditlogentries_TimestampUtc', 'CREATE INDEX `IX_auditlogentries_TimestampUtc` ON `auditlogentries` (`TimestampUtc`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('auditlogentries', 'IX_auditlogentries_Module_Action', 'CREATE INDEX `IX_auditlogentries_Module_Action` ON `auditlogentries` (`Module`, `Action`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('auditlogentries', 'IX_auditlogentries_UserId', 'CREATE INDEX `IX_auditlogentries_UserId` ON `auditlogentries` (`UserId`)');");
            migrationBuilder.Sql("CALL AddIndexIfMissing('auditlogentries', 'IX_auditlogentries_Status', 'CREATE INDEX `IX_auditlogentries_Status` ON `auditlogentries` (`Status`)');");
        }

        private static void AddForeignKeys(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('orders', 'FK_orders_users_UserId', 'ALTER TABLE `orders` ADD CONSTRAINT `FK_orders_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE SET NULL');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('orders', 'FK_orders_coupons_CouponId', 'ALTER TABLE `orders` ADD CONSTRAINT `FK_orders_coupons_CouponId` FOREIGN KEY (`CouponId`) REFERENCES `coupons` (`Id`) ON DELETE SET NULL');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('orderitems', 'FK_orderitems_orders_OrderId', 'ALTER TABLE `orderitems` ADD CONSTRAINT `FK_orderitems_orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `orders` (`Id`) ON DELETE CASCADE');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('orderitems', 'FK_orderitems_products_ProductId', 'ALTER TABLE `orderitems` ADD CONSTRAINT `FK_orderitems_products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE RESTRICT');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('addresses', 'FK_addresses_users_UserId', 'ALTER TABLE `addresses` ADD CONSTRAINT `FK_addresses_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('wishlistitems', 'FK_wishlistitems_users_UserId', 'ALTER TABLE `wishlistitems` ADD CONSTRAINT `FK_wishlistitems_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('wishlistitems', 'FK_wishlistitems_products_ProductId', 'ALTER TABLE `wishlistitems` ADD CONSTRAINT `FK_wishlistitems_products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE CASCADE');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('couponusagerecords', 'FK_couponusagerecords_coupons_CouponId', 'ALTER TABLE `couponusagerecords` ADD CONSTRAINT `FK_couponusagerecords_coupons_CouponId` FOREIGN KEY (`CouponId`) REFERENCES `coupons` (`Id`) ON DELETE CASCADE');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('couponusagerecords', 'FK_couponusagerecords_users_UserId', 'ALTER TABLE `couponusagerecords` ADD CONSTRAINT `FK_couponusagerecords_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('returnrequests', 'FK_returnrequests_orders_OrderId', 'ALTER TABLE `returnrequests` ADD CONSTRAINT `FK_returnrequests_orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `orders` (`Id`) ON DELETE CASCADE');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('returnrequests', 'FK_returnrequests_users_UserId', 'ALTER TABLE `returnrequests` ADD CONSTRAINT `FK_returnrequests_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('returnrequests', 'FK_returnrequests_products_ItemProductId', 'ALTER TABLE `returnrequests` ADD CONSTRAINT `FK_returnrequests_products_ItemProductId` FOREIGN KEY (`ItemProductId`) REFERENCES `products` (`Id`) ON DELETE SET NULL');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('supportrequests', 'FK_supportrequests_users_UserId', 'ALTER TABLE `supportrequests` ADD CONSTRAINT `FK_supportrequests_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE SET NULL');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('supportrequests', 'FK_supportrequests_orders_OrderId', 'ALTER TABLE `supportrequests` ADD CONSTRAINT `FK_supportrequests_orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `orders` (`Id`) ON DELETE SET NULL');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('stockalertsubscriptions', 'FK_stockalertsubscriptions_users_UserId', 'ALTER TABLE `stockalertsubscriptions` ADD CONSTRAINT `FK_stockalertsubscriptions_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE SET NULL');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('stockalertsubscriptions', 'FK_stockalertsubscriptions_products_ProductId', 'ALTER TABLE `stockalertsubscriptions` ADD CONSTRAINT `FK_stockalertsubscriptions_products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE CASCADE');");
            migrationBuilder.Sql("CALL AddForeignKeyIfMissing('auditlogentries', 'FK_auditlogentries_users_UserId', 'ALTER TABLE `auditlogentries` ADD CONSTRAINT `FK_auditlogentries_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE SET NULL');");
        }

        private static void DropForeignKeys(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('auditlogentries', 'FK_auditlogentries_users_UserId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('stockalertsubscriptions', 'FK_stockalertsubscriptions_products_ProductId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('stockalertsubscriptions', 'FK_stockalertsubscriptions_users_UserId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('supportrequests', 'FK_supportrequests_orders_OrderId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('supportrequests', 'FK_supportrequests_users_UserId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('returnrequests', 'FK_returnrequests_products_ItemProductId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('returnrequests', 'FK_returnrequests_users_UserId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('returnrequests', 'FK_returnrequests_orders_OrderId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('couponusagerecords', 'FK_couponusagerecords_users_UserId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('couponusagerecords', 'FK_couponusagerecords_coupons_CouponId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('wishlistitems', 'FK_wishlistitems_products_ProductId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('wishlistitems', 'FK_wishlistitems_users_UserId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('addresses', 'FK_addresses_users_UserId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('orderitems', 'FK_orderitems_products_ProductId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('orderitems', 'FK_orderitems_orders_OrderId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('orders', 'FK_orders_coupons_CouponId');");
            migrationBuilder.Sql("CALL DropForeignKeyIfExists('orders', 'FK_orders_users_UserId');");
        }
    }
}
