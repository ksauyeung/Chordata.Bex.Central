
-- --------------------------------------------------
-- Entity Designer DDL Script for SQL Server 2005, 2008, 2012 and Azure
-- --------------------------------------------------
-- Date Created: 08/02/2017 14:15:42
-- Generated from EDMX file: D:\Projects\Chordata.Bex.Central\Data\Model.edmx
-- --------------------------------------------------

SET QUOTED_IDENTIFIER OFF;
GO
USE [Tuna];
GO
IF SCHEMA_ID(N'dbo') IS NULL EXECUTE(N'CREATE SCHEMA [dbo]');
GO

-- --------------------------------------------------
-- Dropping existing FOREIGN KEY constraints
-- --------------------------------------------------


-- --------------------------------------------------
-- Dropping existing tables
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[APIs]', 'U') IS NOT NULL
    DROP TABLE [dbo].[APIs];
GO
IF OBJECT_ID(N'[dbo].[OPs]', 'U') IS NOT NULL
    DROP TABLE [dbo].[OPs];
GO

-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'APIs'
CREATE TABLE [dbo].[APIs] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] varchar(50)  NOT NULL,
    [ApiUrl1] varchar(100)  NOT NULL,
    [ApiUrl2] varchar(100)  NULL,
    [DefaultKey] varchar(256)  NULL,
    [DefaultSecret] varchar(256)  NULL
);
GO

-- Creating table 'OPs'
CREATE TABLE [dbo].[OPs] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] varchar(100)  NOT NULL,
    [Text1] varchar(100)  NULL,
    [Text2] varchar(100)  NULL,
    [Text4] varchar(100)  NULL,
    [Text5] varchar(100)  NULL,
    [Text6] varchar(100)  NULL,
    [Text7] varchar(100)  NULL,
    [Text8] varchar(100)  NULL,
    [Text9] varchar(100)  NULL,
    [Text10] varchar(100)  NULL,
    [Num1] decimal(18,8)  NULL,
    [Num2] decimal(18,8)  NULL,
    [Num4] decimal(18,8)  NULL,
    [Num5] decimal(18,8)  NULL,
    [Num6] decimal(18,8)  NULL,
    [Num7] decimal(18,8)  NULL,
    [Num8] decimal(18,8)  NULL,
    [Num9] decimal(18,8)  NULL,
    [Num10] decimal(18,8)  NULL,
    [Date1] datetime  NULL,
    [Date2] datetime  NULL,
    [Date3] datetime  NULL,
    [Date4] datetime  NULL,
    [Order1] varchar(256)  NULL,
    [Order2] varchar(256)  NULL,
    [Order3] varchar(256)  NULL,
    [Order4] varchar(256)  NULL,
    [LastCompleted] datetime  NULL,
    [Enabled] bit  NULL
);
GO

-- --------------------------------------------------
-- Creating all PRIMARY KEY constraints
-- --------------------------------------------------

-- Creating primary key on [Id] in table 'APIs'
ALTER TABLE [dbo].[APIs]
ADD CONSTRAINT [PK_APIs]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'OPs'
ALTER TABLE [dbo].[OPs]
ADD CONSTRAINT [PK_OPs]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- --------------------------------------------------
-- Creating all FOREIGN KEY constraints
-- --------------------------------------------------

-- --------------------------------------------------
-- Script has ended
-- --------------------------------------------------