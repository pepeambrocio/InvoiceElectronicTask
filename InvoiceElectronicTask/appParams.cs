

namespace InvoiceElectronicTask
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    public class appParams
    {
        public appParams()
        {

        }
        public int ProcessType { get; set; }
        public int IdPeriod { get; set; }
        public string CountryCode { get; set; }
        public string CarrierCode { get; set; }
        public List<string> Carriers { get; set; }
        public override string ToString() => $"ProcessType : {ProcessType} , IdPeriod: {IdPeriod}, CountryCode: {CountryCode}";
    }
}
