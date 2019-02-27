using log4net;
using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InvoiceElectronicTask
{
    public class Program
    {
        // Logger instance named "MyApp".
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static ConfigParameters parameters { get; set; }
        static void Main(string[] args)
        {

            //var myTask = SimpleAsyncForEach();            
            if (args != null)
            {
                string ProcessType = string.Empty,
                    IdPeriod       = string.Empty,
                    CountryCode    = string.Empty,                    
                    ExeType        = string.Empty,
                    pathFileY4 = string.Empty,
                    pathFileQ6 = string.Empty;


                ProcessType = args[0].ToString();
                IdPeriod    = args[1].ToString();
                CountryCode = args[2].ToString();                
                ExeType     = args[3].ToString();
                pathFileY4  = args[4].ToString();
                pathFileQ6 = args[5].ToString();


                //Obtener las compañias
                List<string> carriers = new List<string>();
                carriers = GetCompanies();

                foreach (var carrierCode in carriers)
                {
                    ExeTypeTransaction exeTypeTransaction = (ExeTypeTransaction)int.Parse(ExeType);
                    Console.WriteLine($"ProcessType: {ProcessType} - IdPeriod: {IdPeriod} - CountryCode: {CountryCode} - CarrierCode: {carrierCode} - ExeType : {ExeType}");

                    switch (exeTypeTransaction)
                    {
                        case ExeTypeTransaction.DAILYINBATCH:
                            AsyncDbExecute exe = new AsyncDbExecute(int.Parse(ProcessType), int.Parse(IdPeriod), 
                                CountryCode, carrierCode, short.Parse(ExeType),
                                pathFileY4, pathFileQ6);
                            exe.RunInvoiceTaskProcess();
                            break;
                        case ExeTypeTransaction.DAILYINBYFILE:
                            AsyncDbExecutebyFile exeByFile = new AsyncDbExecutebyFile(int.Parse(ProcessType), 
                                int.Parse(IdPeriod), CountryCode, 
                                carrierCode, short.Parse(ExeType),
                                pathFileY4, pathFileQ6);
                            exeByFile.RunInvoiceTaskProcess();
                            break;
                        default: break;
                    }
                }

                

                
            }
            else
                Logger.Warn("No existen parámetro para ejecutar la aplicación");


            //RunNormal();


        }


        /// <summary>
        /// Simples the asynchronous for each.
        /// TEST
        /// </summary>
        /// <returns></returns>
        public static async Task SimpleAsyncForEach()
        {
            var enumerable = new AsyncEnumerable<int>(
                async yield =>
                {
                    for (int i = 0; i < 5000; i++)
                        await yield.ReturnAsync(i);
                });

            int counter = 0;
            await enumerable.ForEachAsync(
                number =>
                {
                    Console.WriteLine($"Print: {number}");
                    counter++;
                });
        }

        /// <summary>
        /// Gets the connection string
        /// </summary>
        /// <returns></returns>
        static private string GetConnectionString()
        {
            // To avoid storing the connection string in your code, 
            // you can retrieve it from a configuration file, using the 
            // System.Configuration.ConfigurationSettings.AppSettings property 
            return "Data Source=10.10.50.131;Initial Catalog=BookingsElectronicInvoice;Persist Security Info=True;User ID=user_EB;Password=user_EB$;MultipleActiveResultSets=True;";                
        }

        public static void RunNormal()
        {

            //AsyncDbExecute exe = new AsyncDbExecute();
            //exe.RunInvoiceTask();



            parameters = new ConfigParameters();
            //------------> CONSTRUYE EL NOMBRE DEL ARCHIVO
            DateTime dDateTime;
            string _Date;
            string _Time;
            string FilePath = string.Empty; //= ConfigurationManager.AppSettings["FileToSendPath"].ToString();
            DateTime strModifiedDate = Convert.ToDateTime(ConfigurationManager.AppSettings["ModifiedDate"].ToString());
            string strCarrierCode = ConfigurationManager.AppSettings["CarrierCode"].ToString();

            dDateTime = DateTime.Now.Date;
            _Date = dDateTime.ToString("ddMMyy");
            _Time = dDateTime.ToString("hhmm");

            Logger.Info($"Iniciando Proceso de Facturación, ProcessType:  {parameters.ToString()}");

            switch (strCarrierCode)
            {
                case "Y4":
                    FilePath = ConfigurationManager.AppSettings["FileToSendPath"].ToString();
                    break;
                case "Q6":
                    FilePath = ConfigurationManager.AppSettings["FileToSendPathQ6"].ToString();
                    break;
                default: break;
            }

            FilePath = FilePath.Replace("ddMMyy", _Date);
            FilePath = FilePath.Replace("hhmm", _Time);

            StreamWriter oSW = new StreamWriter(FilePath);

            //Dts.Variables("FinalFilePath").Value = FilePath;

            SqlConnection cnx = new SqlConnection(GetConnectionString());

            SqlCommand command = new SqlCommand();
            SqlDataAdapter adapter;

            cnx.Open();
            command.Connection = cnx;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "dbo.spBuildFile_GetPNRs";
            command.Parameters.Add(new SqlParameter("@ProcessType", parameters.ProcessType.ToString()));
            command.Parameters.Add(new SqlParameter("@ProcessId", parameters.IdPeriod.ToString()));
            command.Parameters.Add(new SqlParameter("@CountryCode", parameters.CountryCode));
            command.Parameters.Add(new SqlParameter("@ModifiedDate", strModifiedDate));
            command.Parameters.Add(new SqlParameter("@CarrierCode", strCarrierCode));
            command.CommandTimeout = 240;

            adapter = new SqlDataAdapter(command);
            DataSet ds = new DataSet();
            adapter.Fill(ds);
            adapter.Dispose();
            int cont;
            cont = 0;

            Logger.Info($"Total Registros a procesar: {ds.Tables[0].Rows.Count}");

            foreach (DataRow fila in ds.Tables[0].Rows)
            {

                //cont++;
                //if (cont == 10)
                //    break;

                parameters.Item = fila["PK_Invoice"].ToString();

                parameters.sMsg = "";

                DataSet dsCABFAC = new DataSet();
                SqlCommand command2 = new SqlCommand();
                SqlDataAdapter adapter2;

                command2.Connection = cnx;
                command2.CommandType = CommandType.StoredProcedure;
                command2.CommandText = "dbo.spBuildFile_GetCABFAC";
                command2.Parameters.Add(new SqlParameter("@PK_Invoice", parameters.Item.ToString()));
                command2.Parameters.Add(new SqlParameter("@ProcessType", parameters.ProcessType.ToString()));
                command2.Parameters.Add(new SqlParameter("@ProcessId", parameters.IdPeriod.ToString()));
                command2.Parameters.Add(new SqlParameter("@CountryCode", parameters.CountryCode.ToString()));

                adapter2 = new SqlDataAdapter(command2);
                adapter2.Fill(dsCABFAC);
                adapter2.Dispose();

                foreach (DataRow filaItem in dsCABFAC.Tables[0].Rows)
                {
                    foreach (DataColumn col in dsCABFAC.Tables[0].Columns)
                    {
                        parameters.sMsg = (parameters.sMsg
                                + (filaItem[col.Ordinal].ToString().Trim() + "|"));
                    }

                    parameters.sMsg = parameters.sMsg.Substring(0, (parameters.sMsg.Length - 1));
                    parameters.sMsg = (parameters.sMsg + "\r\n");
                }

                DataSet dsIMPFAC = new DataSet();
                SqlCommand command3 = new SqlCommand();
                SqlDataAdapter adapter3;

                command3.Connection = cnx;
                command3.CommandType = CommandType.StoredProcedure;
                command3.CommandText = "dbo.spBuildFile_GetIMPFAC";
                command3.Parameters.Add(new SqlParameter("@PK_Invoice", parameters.Item.ToString()));
                command3.Parameters.Add(new SqlParameter("@ProcessType", parameters.ProcessType.ToString()));
                command3.Parameters.Add(new SqlParameter("@ProcessId", parameters.IdPeriod));
                command3.Parameters.Add(new SqlParameter("@CountryCode", parameters.CountryCode));

                adapter3 = new SqlDataAdapter(command3);
                adapter3.Fill(dsIMPFAC);
                adapter3.Dispose();

                foreach (DataRow filaItem in dsIMPFAC.Tables[0].Rows)
                {
                    foreach (DataColumn col in dsIMPFAC.Tables[0].Columns)
                    {
                        parameters.sMsg = (parameters.sMsg
                                + (filaItem[col.Ordinal].ToString().Trim() + "|"));
                    }

                    parameters.sMsg = parameters.sMsg.Substring(0, (parameters.sMsg.Length - 1));
                    parameters.sMsg = (parameters.sMsg + "\r\n");
                }

                // --------------------------LINFAC
                DataSet dsLINFAC = new DataSet();
                SqlCommand command4 = new SqlCommand();
                SqlDataAdapter adapter4;

                command4.Connection = cnx;
                command4.CommandType = CommandType.StoredProcedure;
                command4.CommandText = "dbo.spBuildFile_GetLINFAC";
                command4.Parameters.Add(new SqlParameter("@PK_Invoice", parameters.Item.ToString()));
                command4.Parameters.Add(new SqlParameter("@ProcessType", parameters.ProcessType.ToString()));
                command4.Parameters.Add(new SqlParameter("@ProcessId", parameters.IdPeriod));
                command4.Parameters.Add(new SqlParameter("@CountryCode", parameters.CountryCode));

                adapter4 = new SqlDataAdapter(command4);
                adapter4.Fill(dsLINFAC);
                adapter4.Dispose();
                int flgWrite;
                int flgExit;

                foreach (DataRow filaItem in dsLINFAC.Tables[0].Rows)
                {
                    flgWrite = 1;
                    flgExit = 0;

                    foreach (DataColumn col in dsLINFAC.Tables[0].Columns)
                    {
                        if ((col.ColumnName == "FlgIMPLFAC"))
                        {
                            // flgImp = 1
                            if ((filaItem[col.Ordinal].ToString() == "1"))
                            {
                                parameters.sMsg = parameters.sMsg.Substring(0, (parameters.sMsg.Length - 1));
                                parameters.sMsg = (parameters.sMsg + "\r\n");
                            }
                            else
                            {
                                flgWrite = 0;
                            }

                        }
                        else if ((col.ColumnName == "FlagTP"))
                        {
                            if ((filaItem[col.Ordinal].ToString() == "1"))
                            {
                                parameters.sMsg = parameters.sMsg.Substring(0, (parameters.sMsg.Length - 1));
                                parameters.sMsg = (parameters.sMsg + "\r\n");
                                flgWrite = 1;
                            }
                            else
                            {
                                break;
                            }

                        }
                        else if ((flgWrite == 1))
                        {
                            parameters.sMsg = (parameters.sMsg
                                        + (filaItem[col.Ordinal].ToString().Trim() + "|"));
                        }
                    }

                    parameters.sMsg = parameters.sMsg.Substring(0, (parameters.sMsg.Length - 1));
                    parameters.sMsg = (parameters.sMsg + "\r\n");
                }

                // --------------------------AERO
                DataSet dsAERO = new DataSet();
                SqlCommand command5 = new SqlCommand();
                SqlDataAdapter adapter5;

                command5.Connection = cnx;
                command5.CommandType = CommandType.StoredProcedure;
                command5.CommandText = "dbo.spBuildFile_GetAERO";
                command5.Parameters.Add(new SqlParameter("@PK_Invoice", parameters.Item.ToString()));
                command5.Parameters.Add(new SqlParameter("@ProcessType", parameters.ProcessType.ToString()));
                command5.Parameters.Add(new SqlParameter("@ProcessId", parameters.IdPeriod));
                command5.Parameters.Add(new SqlParameter("@CountryCode", parameters.CountryCode));

                adapter5 = new SqlDataAdapter(command5);
                adapter5.Fill(dsAERO);
                adapter5.Dispose();

                foreach (DataRow filaItem in dsAERO.Tables[0].Rows)
                {
                    foreach (DataColumn col in dsAERO.Tables[0].Columns)
                    {
                        parameters.sMsg = (parameters.sMsg
                                + (filaItem[col.Ordinal].ToString().Trim() + "|"));
                    }

                    parameters.sMsg = parameters.sMsg.Substring(0, (parameters.sMsg.Length - 1));
                    parameters.sMsg = (parameters.sMsg + "\r\n");
                }

                // --------------------------CARGOS
                DataSet dsCARGOS = new DataSet();
                SqlCommand command6 = new SqlCommand();
                SqlDataAdapter adapter6;

                command6.Connection = cnx;
                command6.CommandType = CommandType.StoredProcedure;
                command6.CommandText = "dbo.spBuildFile_GetCARGOS";
                command6.Parameters.Add(new SqlParameter("@PK_Invoice", parameters.Item.ToString()));
                command6.Parameters.Add(new SqlParameter("@ProcessType", parameters.ProcessType.ToString()));
                command6.Parameters.Add(new SqlParameter("@ProcessId", parameters.IdPeriod));
                command6.Parameters.Add(new SqlParameter("@CountryCode", parameters.CountryCode));

                adapter6 = new SqlDataAdapter(command6);
                adapter6.Fill(dsCARGOS);
                adapter6.Dispose();

                foreach (DataRow filaItem in dsCARGOS.Tables[0].Rows)
                {
                    foreach (DataColumn col in dsCARGOS.Tables[0].Columns)
                    {
                        parameters.sMsg = (parameters.sMsg
                                + (filaItem[col.Ordinal].ToString().Trim() + "|"));
                    }

                    parameters.sMsg = parameters.sMsg.Substring(0, (parameters.sMsg.Length - 1));
                    parameters.sMsg = (parameters.sMsg + "\r\n");
                }

                // ----> escribe en el archivo
                oSW.Write(parameters.sMsg);
                oSW.Flush();

                Logger.Info($"Factura: {parameters.Item}");
            }

            oSW.Close();

            FileInfo fi = new FileInfo(FilePath);
            StreamWriter oSWFlag = new StreamWriter($"{fi.DirectoryName}\\FLG_PNR.txt");
            oSWFlag.Close();
        }

        /// <summary>
        /// Gets the companies.
        /// </summary>
        /// <returns></returns>
        protected static List<string> GetCompanies()
        {
            try
            {
                List<string> carriers = new List<string>();

                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
                {
                    conn.Open();
                    var functionQuery = "SELECT [CarrierCode] FROM [dbo].[AirLineCode] WHERE Status = 1 ";
                    SqlCommand comm = new SqlCommand(functionQuery, conn);
                    SqlDataReader reader;

                    comm.CommandType = CommandType.Text;
                    comm.CommandTimeout = 240;

                    reader = comm.ExecuteReader();

                    while (reader.Read())
                    {
                        carriers.Add(reader[0].ToString());
                    }

                    conn.Close();
                }

                return carriers;


            }
            catch (Exception)
            {
                return new List<string>();
            }
        }
    }
}
