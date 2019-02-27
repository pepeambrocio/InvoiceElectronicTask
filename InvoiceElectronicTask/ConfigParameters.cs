
namespace InvoiceElectronicTask
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Configuration;

    public class ConfigParameters
    {
        public ConfigParameters()
        {
            ProcessType = Int32.Parse(ConfigurationManager.AppSettings["ProcessType"].ToString());
            IdPeriod = Int32.Parse(ConfigurationManager.AppSettings["IdPeriod"].ToString());
            CountryCode = ConfigurationManager.AppSettings["CountryCode"].ToString();
        }
        //public string strQuery { get; set; }
        //public System.Data.SqlClient.SqlDataAdapter data { get; set; }
        //public DataSet dsPKs { get; set; }
        //public DataRow fila { get; set; }        
        //public DataRow filaItem { get; set; }
        //public DataColumn col { get; set; }
        public string Item { get; set; }
        public string sMsg { get; set; }        
        public int ProcessType { get; set; }
        public int IdPeriod { get; set; }
        public string CountryCode { get; set; }
        public string CarrierCode { get; set; }
        public short ExeType { get; set; }
        public string PathFileY4 { get; set; }
        public string PathFileQ6 { get; set; }

        public override string ToString() => $"ProcessType : {ProcessType} , IdPeriod: {IdPeriod}, CountryCode: {CountryCode}, CarrierCode {CarrierCode}";
    }
}
