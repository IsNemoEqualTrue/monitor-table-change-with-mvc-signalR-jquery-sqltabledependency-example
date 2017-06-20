# Detect record table change with MVC, SignalR, jQuery and SqlTableDependency
[SqlTableDependency](https://www.google.com)
SqlTableDependency is a high-level C# component to used to audit, monitor and receive notifications on SQL Server's record table change.

For all Insert/Update/Delete operation on monitored table, TableDependency receive events containing values for the record inserted, changed or deleted, as well as the DML operation executed on the table, eliminating the need of an additional SELECT to update application’s data cache.

Let's assume a SQL Server database table containing stocks value modified constantly:

CREATE TABLE [dbo].[Stocks](
    [Code] [nvarchar](50) NULL,
    [Name] [nvarchar](50) NULL,
    [Price] [decimal](18, 0) NULL
) 


We are going to map those table columns with the following model:

public class Stock
{
    public decimal Price { get; set; }
    public string Symbol { get; set; }
    public string Name { get; set; }        
}


Next, we install the NuGet package:

PM> Install-Package SqlTableDependency


Next step is to create a custom hub class, used from the SignalR infrastructure:
