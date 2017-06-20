# Detect record table change with MVC, SignalR, jQuery and SqlTableDependency
[SqlTableDependency](https://www.google.com)
SqlTableDependency is a high-level C# component to used to audit, monitor and receive notifications on SQL Server's record table change.

For all Insert/Update/Delete operation on monitored table, TableDependency receive events containing values for the record inserted, changed or deleted, as well as the DML operation executed on the table, eliminating the need of an additional SELECT to update application’s data cache.

Let's assume a SQL Server database table containing stocks value modified constantly:
```SQL
CREATE TABLE [dbo].[Stocks](
    [Code] [nvarchar](50) NULL,
    [Name] [nvarchar](50) NULL,
    [Price] [decimal](18, 0) NULL
) 
```

We are going to map those table columns with the following model:
```C#
public class Stock
{
    public decimal Price { get; set; }
    public string Symbol { get; set; }
    public string Name { get; set; }        
}
```

Next, we install the NuGet package:

```C#
PM> Install-Package SqlTableDependency**
```

Next step is to create a custom hub class, used from the SignalR infrastructure:

```C#
[HubName("stockTicker")]
public class StockTickerHub : Hub
{
    private readonly StockTicker _stockTicker;

    public StockTickerHub() :
        this(StockTicker.Instance)
    {

    }

    public StockTickerHub(StockTicker stockTicker)
    {
        _stockTicker = stockTicker;
    }

    public IEnumerable<Stock> GetAllStocks()
    {
        return _stockTicker.GetAllStocks();
    }
}
```

We'll use the SignalR Hub API to handle server-to-client interaction. A StockTickerHub class that derives from the SignalR Hub class will handle receiving connections and method calls from clients. We can't put these functions in a Hub class, because Hub instances are transient. A Hub class instance is created for each operation on the hub, such as connections and calls from the client to the server. So the mechanism that keeps stock data, updates prices, and broadcasts the price updates that have to run in a separate class, which you'll name StockTicker:

```C#
public class StockTicker
{
    // Singleton instance
    private readonly static Lazy<StockTicker> _instance = new Lazy<StockTicker>(
        () => new StockTicker(GlobalHost.ConnectionManager.GetHubContext<StockTickerHub>().Clients));

    private static SqlTableDependency<Stock> _tableDependency;

    private StockTicker(IHubConnectionContext<dynamic> clients)
    {
        Clients = clients;

        var mapper = new ModelToTableMapper<Stock>();
        mapper.AddMapping(s => s.Symbol, "Code");

        _tableDependency = new SqlTableDependency<Stock>(
            ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString,
            "Stocks",
            mapper);

        _tableDependency.OnChanged += SqlTableDependency_Changed;
        _tableDependency.OnError += SqlTableDependency_OnError;
        _tableDependency.Start();
    }

    public static StockTicker Instance
    {
        get
        {
            return _instance.Value;
        }
    }

    private IHubConnectionContext<dynamic> Clients
    {
        get;
        set;
    }

    public IEnumerable<Stock> GetAllStocks()
    {
        var stockModel = new List<Stock>();

        var connectionString = ConfigurationManager.ConnectionStrings
				["connectionString"].ConnectionString;
        using (var sqlConnection = new SqlConnection(connectionString))
        {
            sqlConnection.Open();
            using (var sqlCommand = sqlConnection.CreateCommand())
            {
                sqlCommand.CommandText = "SELECT * FROM [Stocks]";

                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        var code = sqlDataReader.GetString(sqlDataReader.GetOrdinal("Code"));
                        var name = sqlDataReader.GetString(sqlDataReader.GetOrdinal("Name"));
                        var price = sqlDataReader.GetDecimal(sqlDataReader.GetOrdinal("Price"));

                        stockModel.Add(new Stock { Symbol = code, Name = name, Price = price });
                    }
                }
            }
        }

        return stockModel;
    }

    void SqlTableDependency_OnError(object sender, ErrorEventArgs e)
    {
        throw e.Error;
    }

    /// <summary>
    /// Broadcast New Stock Price
    /// </summary>
    void SqlTableDependency_Changed(object sender, RecordChangedEventArgs<Stock> e)
    {
        if (e.ChangeType != ChangeType.None)
        {
            BroadcastStockPrice(e.Entity);
        }
    }

    private void BroadcastStockPrice(Stock stock)
    {
        Clients.All.updateStockPrice(stock);
    }

    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _tableDependency.Stop();
            }

            disposedValue = true;
        }
    }

    ~StockTicker()
    {
        Dispose(false);
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
```

Now it's time to see the HTML page:

```XML
<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
    <title>SqlTableDependencly with SignalR</title>
</head>
<body>
    <h1>SqlTableDependencly with SignalR</h1>

    <div id="stockTable">
        <table border="1">
            <thead style="background-color:silver">
                <tr><th>Symbol</th><th>Name</th><th>Price</th></tr>
            </thead>
            <tbody>
                <tr class="loading"><td colspan="3">loading...</td></tr>
            </tbody>
        </table>
    </div>

    <script src="jquery-1.10.2.min.js"></script>
    <script src="jquery.color-2.1.2.min.js"></script>
    <script src="../Scripts/jquery.signalR-2.2.0.js"></script>
    <script src="../signalr/hubs"></script>
    <script src="SignalR.StockTicker.js"></script>
</body>
</html>
```

and how we manage data returned from SignalR in our JavaScript code:

```javascript
// Crockford's supplant method
if (!String.prototype.supplant) {
    String.prototype.supplant = function (o) {
        return this.replace(/{([^{}]*)}/g,
            function (a, b) {
                var r = o[b];
                return typeof r === 'string' || typeof r === 'number' ? r : a;
            }
        );
    };
}

$(function () {
    var ticker = $.connection.stockTicker; // the generated client-side hub proxy
    var $stockTable = $('#stockTable');
    var $stockTableBody = $stockTable.find('tbody');
    var rowTemplate = '<tr data-symbol="{Symbol}"><td>
    {Symbol}</td><td>{Name}</td><td>{Price}</td></tr>';

    function formatStock(stock) {
        return $.extend(stock, {
            Price: stock.Price.toFixed(2)
        });
    }

    function init() {
        return ticker.server.getAllStocks().done(function (stocks) {
            $stockTableBody.empty();

            $.each(stocks, function () {
                var stock = formatStock(this);
                $stockTableBody.append(rowTemplate.supplant(stock));
            });
        });
    }

    // Add client-side hub methods that the server will call
    $.extend(ticker.client, {
        updateStockPrice: function (stock) {
            var displayStock = formatStock(stock);
            $row = $(rowTemplate.supplant(displayStock)),
            $stockTableBody.find('tr[data-symbol=' + stock.Symbol + ']').replaceWith($row);
        }
    });

    // Start the connection
    $.connection.hub.start().then(init);
});
```

In the end, we do not have to forget to register the SignalR route:

```C#
[assembly: OwinStartup(typeof(Stocks.Startup))]
namespace Stocks
{
    public static class Startup
    {
        public static void Configuration(IAppBuilder app)
        {
            // For more information on how to configure your application using OWIN startup, 
            // visit http://go.microsoft.com/fwlink/?LinkID=316888

             app.MapSignalR();
        }
    }
}
```

For more info about SqlTableDependency, refere to https://github.com/christiandelbianco/monitor-table-change-with-sqltabledependency 

