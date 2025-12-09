-- Add checkpoint and recovery fields to DataCopy_Jobs table
ALTER TABLE DataCopy_Jobs
ADD LastSuccessfulOffset INT NOT NULL DEFAULT 0,
    CanResume BIT NOT NULL DEFAULT 0,
    RetryCount INT NOT NULL DEFAULT 0;

-- Add retry configuration fields to DataCopy_Configurations table
ALTER TABLE DataCopy_Configurations
ADD MaxRetryAttempts INT NOT NULL DEFAULT 3,
    UseTransaction BIT NOT NULL DEFAULT 0,
    RetryFailedRecords BIT NOT NULL DEFAULT 1;

-- Create DataCopy_FailedRecords table for dead letter queue
CREATE TABLE DataCopy_FailedRecords (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    JobId INT NOT NULL,
    RecordData NVARCHAR(MAX) NOT NULL,
    ErrorMessage NVARCHAR(MAX),
    RetryCount INT NOT NULL DEFAULT 0,
    FailedDate DATETIME NOT NULL DEFAULT GETDATE(),
    LastRetryDate DATETIME NULL,
    Resolved BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_DataCopy_FailedRecords_Job FOREIGN KEY (JobId) REFERENCES DataCopy_Jobs(Id) ON DELETE CASCADE
);

-- Create index on JobId for faster lookups
CREATE INDEX IX_DataCopy_FailedRecords_JobId ON DataCopy_FailedRecords(JobId);
CREATE INDEX IX_DataCopy_FailedRecords_Resolved ON DataCopy_FailedRecords(Resolved);
