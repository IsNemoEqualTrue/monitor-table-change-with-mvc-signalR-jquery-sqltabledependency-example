/// <reference path="../Scripts/jquery-1.10.2.js" />
/// <reference path="../Scripts/jquery.signalR-2.1.1.js" />

// Crockford's supplant method (poor man's templating)
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
    var rowTemplate = '<tr data-symbol="{Symbol}"><td>{Symbol}</td><td>{Name}</td><td>{Price}</td></tr>';

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
            $stockTableBody.find('tr[data-symbol=' + stock.Symbol + ']').effect("highlight", { color: "yellow" }, 2000);
        }
    });

    // Start the connection
    $.connection.hub.start().then(init);
});