using System;
using System.Collections.Generic;
using Microsoft.AspNet.SignalR.Hubs;
using TableDependency.EventArgs;
using TableDependency.Enums;
using System.Configuration;
using System.Data.SqlClient;
using TableDependency.SqlClient;
using TableDependency.Mappers;
using Microsoft.AspNet.SignalR;

namespace Stocks
{
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

            var connectionString = ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString;
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
}