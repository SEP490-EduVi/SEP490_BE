-- =====================================================================
-- Script: Tạo bảng WalletTransactions cho hệ thống thanh toán EduVi
-- Chạy trên database EduVi (SQL Server)
-- =====================================================================

-- 1. Tạo bảng WalletTransactions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WalletTransactions')
BEGIN
    CREATE TABLE [dbo].[WalletTransactions] (
        [TransactionID]     INT             IDENTITY(1, 1)  NOT NULL,
        [WalletID]          INT             NULL,
        [OrderCode]         BIGINT          NOT NULL,
        [TransactionType]   VARCHAR(50)     NULL,
        [Amount]            DECIMAL(18, 2)  NULL,
        [BalanceBefore]     DECIMAL(18, 2)  NULL,
        [BalanceAfter]      DECIMAL(18, 2)  NULL,
        [Status]            INT             NULL    CONSTRAINT [DF_WalletTransactions_Status] DEFAULT (0),
        [Description]       NVARCHAR(500)   NULL,
        [PlanID]            INT             NULL,
        [CreatedAt]         DATETIME        NULL    CONSTRAINT [DF_WalletTransactions_CreatedAt] DEFAULT (GETDATE()),
        [UpdatedAt]         DATETIME        NULL,

        -- Primary Key
        CONSTRAINT [PK__WalletTransactions__TransactionId] PRIMARY KEY CLUSTERED ([TransactionID] ASC),

        -- Unique constraint trên OrderCode (idempotency - chống cộng tiền 2 lần)
        CONSTRAINT [UQ__WalletTransactions__OrderCode] UNIQUE NONCLUSTERED ([OrderCode] ASC),

        -- Foreign Key tới Wallets
        CONSTRAINT [FK__WalletTrans__WalletID] FOREIGN KEY ([WalletID])
            REFERENCES [dbo].[Wallets] ([WalletID]),

        -- Foreign Key tới SubscriptionPlans
        CONSTRAINT [FK__WalletTrans__PlanID] FOREIGN KEY ([PlanID])
            REFERENCES [dbo].[SubscriptionPlans] ([PlanID])
    );

    PRINT N'✓ Bảng WalletTransactions đã được tạo thành công.';
END
ELSE
BEGIN
    PRINT N'⚠ Bảng WalletTransactions đã tồn tại, bỏ qua.';
END
GO

-- 2. Thêm cột PaymentMethod vào bảng Orders (nếu chưa có)
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'PaymentMethod'
)
BEGIN
    ALTER TABLE [dbo].[Orders]
    ADD [PaymentMethod] VARCHAR(50) NULL;

    PRINT N'✓ Đã thêm cột PaymentMethod vào bảng Orders.';
END
ELSE
BEGIN
    PRINT N'⚠ Cột PaymentMethod đã tồn tại trong Orders, bỏ qua.';
END
GO
