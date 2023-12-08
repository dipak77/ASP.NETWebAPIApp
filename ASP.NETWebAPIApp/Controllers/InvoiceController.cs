using ASP.NETWebAPIApp.Model;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop.Infrastructure;
using Microsoft.SqlServer.Server;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Reflection;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ASP.NETWebAPIApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {


        private readonly ILogger<InvoiceController> _logger;
        private IWebHostEnvironment _environment;
        private IConfiguration _configuration;
        protected Stream stream { get; set; }
        public InvoiceController(ILogger<InvoiceController> logger, IWebHostEnvironment environment, IConfiguration configuration)
        {
            _logger = logger;
            _environment = environment;
            _configuration = configuration;
        }

        [HttpPost("upload", Name = "upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadFile(IFormFile postedFile, CancellationToken cancellationToken)
        {
            try
            {

                if (postedFile != null)
                {
                    //Create a Folder.
                    string path = Path.Combine(_environment.ContentRootPath, "Upload");
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    //Save the uploaded Excel file.
                    string fileName = Path.GetFileName(postedFile.FileName);
                    string filePath = Path.Combine(path, fileName);
                    using (var fileStream = new FileStream(Path.Combine(path, postedFile.FileName), FileMode.Create))
                    {
                        await postedFile.CopyToAsync(fileStream);
                    }
                    string conString = _configuration.GetConnectionString("ExcelConString");
                    DataTable dt = new DataTable();
                    var InvoiceList = new List<InvoiceDetail>();
                    var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture);
                    config.MissingFieldFound = null;// at the end of the copy method, we are at the end of both the input and output stream and need to reset the one we want to work with.
                    var reader = new StreamReader(filePath);
                    using (var csv = new CsvReader(reader,config))
                    {
                        using (var dr3 = new CsvDataReader(csv)) // error happens here when "file.Data" is used as the stream: "Synchronous reads are not supported"
                        {
                            var dt3 = new DataTable();
                            dt.Columns.Add("InvoiceNo", typeof(Int64));
                            dt.Columns.Add("InvoiceDate", typeof(DateTime));
                           // dt.Columns.Add("FwdInvoiceRefNo", typeof(Int64));
                            dt.Columns.Add("StockReceiptDate", typeof(DateTime));
                            dt.Columns.Add("UOM", typeof(Int32));
                            dt.Columns.Add("QtyCases", typeof(Int32));
                            dt.Columns.Add("QtyUnits", typeof(Int32));
                            dt.Columns.Add("Rate", typeof(float));
                            dt.Columns.Add("Amount", typeof(float));
                            dt.Columns.Add("TaxableAmount", typeof(float));

                            dt.Load(dr3);
                        }
                    }
                    Stopwatch _stopWatch;
                    _stopWatch = new Stopwatch();
                    _stopWatch.Start();
                    //Insert the Data read from the Excel file to Database Table.
                    conString = _configuration.GetConnectionString("DBConnection");
                    using (SqlConnection con = new SqlConnection(conString))
                    {
                        using (SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(con))
                        {
                            //Set the database table name.
                            sqlBulkCopy.DestinationTableName = "dbo.InvoiceDetailTbl";

                            //[OPTIONAL]: Map the Excel columns with that of the database table.
                            sqlBulkCopy.ColumnMappings.Add("InvoiceNo", "InvoiceNo");
                            sqlBulkCopy.ColumnMappings.Add("InvoiceDate", "InvoiceDate");
                            //sqlBulkCopy.ColumnMappings.Add("FwdInvoiceRefNo", "FwdInvoiceRefNo");
                            sqlBulkCopy.ColumnMappings.Add("StockReceiptDate", "StockReceiptDate");
                            sqlBulkCopy.ColumnMappings.Add("UOM", "UOM");
                            sqlBulkCopy.ColumnMappings.Add("QtyCases", "QtyCases");
                            sqlBulkCopy.ColumnMappings.Add("QtyUnits", "QtyUnits");
                            sqlBulkCopy.ColumnMappings.Add("Rate", "Rate");
                            sqlBulkCopy.ColumnMappings.Add("Amount", "Amount");
                            sqlBulkCopy.ColumnMappings.Add("TaxableAmount", "TaxableAmount");

                            con.Open();
                            await sqlBulkCopy.WriteToServerAsync(dt);
                            con.Close();
                        }
                    }
                    _stopWatch.Stop();
                    TimeSpan ts = _stopWatch.Elapsed;

                    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                                                       ts.Hours, ts.Minutes, ts.Seconds,
                                                       ts.Milliseconds / 10);
                    Console.WriteLine(elapsedTime, "RunTime");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.InnerException,"error");
               return BadRequest(ex.Message);
            }
            return Ok();

        }
        private sealed class InvoiceDetailMap : ClassMap<InvoiceDetail>
        {
            public InvoiceDetailMap()
            {
                AutoMap(CultureInfo.InvariantCulture);
                Map(m => m.InvoiceNo);
                Map(m => m.InvoiceDate);
                Map(m => m.FwdInvoiceRefNo);
                Map(m => m.StockReceiptDate);
                Map(m => m.UOM);
                Map(m => m.QtyCases);
                Map(m => m.QtyUnits);
                Map(m => m.Rate);
                Map(m => m.Amount);
                Map(m => m.TaxableAmount);
            }
        }
        public static DataTable ToDataTable<T>(List<T> items)
        {
            DataTable dataTable = new DataTable(typeof(T).Name);

            //Get all the properties
            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in Props)
            {
                //Setting column names as Property names
                dataTable.Columns.Add(prop.Name);
            }
            foreach (T item in items)
            {
                var values = new object[Props.Length];
                for (int i = 0; i < Props.Length; i++)
                {
                    //inserting property values to datatable rows
                    values[i] = Props[i].GetValue(item, null);
                }
                dataTable.Rows.Add(values);
            }
            //put a breakpoint here and check datatable
            return dataTable;
        }

    }
}
