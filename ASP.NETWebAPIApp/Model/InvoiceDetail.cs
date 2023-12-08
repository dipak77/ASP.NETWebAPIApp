namespace ASP.NETWebAPIApp.Model
{
    public class InvoiceDetail
    {
        public Int64 InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public Int64 FwdInvoiceRefNo { get; set; }
        public DateTime StockReceiptDate { get; set; }
        public Int32 UOM {  get; set; }
        public Int32 QtyCases { get; set;}
        public Int32 QtyUnits { get; set; }
        public float Rate { get; set; }
        public float Amount { get; set; }
        public float TaxableAmount { get; set; }
        

    }
}
