
namespace InvoiceElectronicTask
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public class Reference
    {
        public static string GetEnumDescription(Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());

            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])fi.GetCustomAttributes(
                typeof(DescriptionAttribute),
                false);

            if (attributes != null &&
                attributes.Length > 0)
                return attributes[0].Description;
            else
                return value.ToString();
        }
    }
    public enum ProcesStep{
        STEP_FIRST = 0,
        STEP_SECOND = 1
    }

    public enum ExeTypeTransaction
    {
        [Description("PROCESO DIARIO MASIVO")]
        DAILYINBATCH = 1,
        [Description("PROCESO DIARIO POR ARCHIVO")]
        DAILYINBYFILE = 3
    }
}
