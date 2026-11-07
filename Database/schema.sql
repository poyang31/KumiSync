IF OBJECT_ID(N'dbo.SyncData', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SyncData
    (
        Id varchar(36) COLLATE Latin1_General_100_BIN2 NOT NULL
            CONSTRAINT PK_SyncData PRIMARY KEY,
        Content nvarchar(max) NOT NULL,
        CreatedAt datetime2(3) NOT NULL,
        UpdatedAt datetime2(3) NOT NULL,
        Version int NULL CONSTRAINT DF_SyncData_Version DEFAULT (0)
    );
END;
