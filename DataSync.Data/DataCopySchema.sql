-- Data Copy Module Schema
-- Run this script on the DataSyncDb database

-- Configuration table for data copy operations
CREATE TABLE DataCopy_Configurations (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ConfigName NVARCHAR(100) NOT NULL,
    
    -- Source Configuration
    SourceDbServerIP NVARCHAR(50) NOT NULL,
    SourceDbName NVARCHAR(100) NOT NULL,
    SourceTableName NVARCHAR(100) NOT NULL,
    SourceCustomQuery NVARCHAR(MAX) NULL, -- Optional WHERE clause (e.g., "WHERE Status = 'Active'")
    
    -- Destination Configuration
    DestDbServerIP NVARCHAR(50) NOT NULL,
    DestDbName NVARCHAR(100) NOT NULL,
    DestTableName NVARCHAR(100) NOT NULL,
    
    -- Copy Options
    TruncateBeforeCopy BIT DEFAULT 0,
    BatchSize INT DEFAULT 1000,
    
    -- Scheduling
    IsScheduled BIT DEFAULT 0,
    ScheduleCron NVARCHAR(50) NULL, -- Cron expression (e.g., "0 0 * * *" for daily at midnight)
    
    Enabled BIT DEFAULT 1,
    CreatedDate DATETIME DEFAULT GETDATE(),
    ModifiedDate DATETIME DEFAULT GETDATE(),
    
    CONSTRAINT UQ_DataCopy_ConfigName UNIQUE(ConfigName)
);

-- Job execution tracking
CREATE TABLE DataCopy_Jobs (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ConfigId INT NOT NULL,
    
    -- Job Status
    Status NVARCHAR(20) NOT NULL, -- Pending, Running, Completed, Failed, Cancelled
    TriggerType NVARCHAR(20) NOT NULL, -- Manual, Scheduled
    
    -- Progress Tracking
    TotalRecords INT NULL,
    ProcessedRecords INT DEFAULT 0,
    FailedRecords INT DEFAULT 0,
    ProgressPercentage DECIMAL(5,2) DEFAULT 0,
    
    -- Timing
    StartTime DATETIME NULL,
    EndTime DATETIME NULL,
    Duration INT NULL, -- Seconds
    
    -- Error Handling
    ErrorMessage NVARCHAR(MAX) NULL,
    
    CreatedDate DATETIME DEFAULT GETDATE(),
    
    CONSTRAINT FK_DataCopy_Jobs_Config FOREIGN KEY (ConfigId) 
        REFERENCES DataCopy_Configurations(Id) ON DELETE CASCADE
);

-- Detailed job logs
CREATE TABLE DataCopy_JobLogs (
    Id INT PRIMARY KEY IDENTITY(1,1),
    JobId INT NOT NULL,
    LogLevel NVARCHAR(20) NOT NULL, -- Info, Warning, Error
    Message NVARCHAR(MAX) NOT NULL,
    LogTime DATETIME DEFAULT GETDATE(),
    
    CONSTRAINT FK_DataCopy_JobLogs_Job FOREIGN KEY (JobId) 
        REFERENCES DataCopy_Jobs(Id) ON DELETE CASCADE
);

-- Indexes for performance
CREATE INDEX IX_DataCopy_Jobs_ConfigId ON DataCopy_Jobs(ConfigId);
CREATE INDEX IX_DataCopy_Jobs_Status ON DataCopy_Jobs(Status);
CREATE INDEX IX_DataCopy_Jobs_CreatedDate ON DataCopy_Jobs(CreatedDate DESC);
CREATE INDEX IX_DataCopy_JobLogs_JobId ON DataCopy_JobLogs(JobId);

-- Sample data for testing
INSERT INTO DataCopy_Configurations (ConfigName, SourceDbServerIP, SourceDbName, SourceTableName, DestDbServerIP, DestDbName, DestTableName, BatchSize, Enabled)
VALUES 
    ('Sample Copy - Orders', '192.168.1.17', 'SalesDB', 'Orders', '192.168.1.18', 'ArchiveDB', 'Orders_Archive', 1000, 1),
    ('Sample Copy - Customers', '192.168.1.17', 'SalesDB', 'Customers', '192.168.1.17', 'SalesDB', 'Customers_Backup', 500, 0);

GO
