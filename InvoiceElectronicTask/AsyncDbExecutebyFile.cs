
namespace InvoiceElectronicTask
{    
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
    public class AsyncDbExecutebyFile
    {
        /// <summary>
        /// The logger
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// Gets or sets the parameters.
        /// </summary>
        /// <value>
        /// The parameters.
        /// </value>
        public static ConfigParameters parameters { get; set; }
        /// <summary>
        /// Gets or sets the LST error invoice.
        /// </summary>
        /// <value>
        /// The LST error invoice.
        /// </value>
        public Queue<string> Lst_Error_Invoice { get; set; }
        public List<string> Lst_Done_Invoice { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncDbExecute"/> class.
        /// </summary>
        /// <param name="processType">Type of the process.</param>
        /// <param name="idPeriod">The identifier period.</param>
        /// <param name="countryCode">The country code.</param>
        /// <param name="carrierCode">The carrier code.</param>
        public AsyncDbExecutebyFile(int processType, int idPeriod,
            string countryCode, string carrierCode, short ExeType,
            string pathFileY4, string pathFileQ6)
        {
            parameters = new ConfigParameters();

            parameters.ProcessType = processType;
            parameters.IdPeriod = idPeriod;
            parameters.CountryCode = countryCode;
            parameters.CarrierCode = carrierCode;
            parameters.ExeType = ExeType;
            parameters.PathFileY4 = pathFileY4;
            parameters.PathFileQ6 = pathFileQ6;
        }

        /// <summary>
        /// Runs the invoice task process.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void RunInvoiceTaskProcess()
        {
            //parameters = new ConfigParameters();
            Lst_Error_Invoice = new Queue<string>();
            //Lst_Done_Invoice = new List<string>();
            //------------> CONSTRUYE EL NOMBRE DEL ARCHIVO
            DateTime dDateTime;
            string _Date;
            string _Time;
            string FilePath = string.Empty;
            DateTime strModifiedDate = Convert.ToDateTime(ConfigurationManager.AppSettings["ModifiedDate"].ToString());
            //string strCarrierCode = ConfigurationManager.AppSettings["CarrierCode"].ToString();

            dDateTime = DateTime.Now;
            _Date = dDateTime.ToString("ddMMyy");
            _Time = dDateTime.ToString("hhmm");

            //Contador de archivos
            int countFile = 0;

            Logger.Info($"Iniciando Proceso de Facturación, ProcessType:  {parameters.ToString()}");

            switch (parameters.CarrierCode)
            {
                case "Y4":
                    FilePath = Path.Combine(parameters.PathFileY4,
                        ConfigurationManager.AppSettings["FileToSendPathByFile"].ToString());
                    break;
                case "Q6":
                    FilePath = Path.Combine(parameters.PathFileQ6, 
                        ConfigurationManager.AppSettings["FileToSendPathQ6ByFile"].ToString());
                    break;
                default: break;
            }

            FilePath = FilePath.Replace("ddMMyy", _Date);
            FilePath = FilePath.Replace("hhmm", _Time);

            SqlConnection cnx = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);

            //Paso 1
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
            command.Parameters.Add(new SqlParameter("@CarrierCode", parameters.CarrierCode));
            command.CommandTimeout = 240;

            adapter = new SqlDataAdapter(command);
            DataSet ds = new DataSet();
            adapter.Fill(ds);
            adapter.Dispose();

            //Paso 2
            SqlCommand command2 = new SqlCommand();
            SqlDataAdapter adapter2;

            command2.Connection = cnx;
            command2.CommandType = CommandType.Text;
            command2.CommandText = $@"select PK_Invoice from dbo.BookingsToSent Where ProcessId={parameters.IdPeriod.ToString()} 
                                      and ProcessType = { parameters.ProcessType.ToString()} and CarrierCode = '{parameters.CarrierCode}'";
            command2.CommandTimeout = 240;

            adapter2 = new SqlDataAdapter(command2);
            DataSet ds2 = new DataSet();
            adapter2.Fill(ds2);
            var datalist = ds2.Tables[0].AsEnumerable().Select(n => n.Field<string>(0)).ToList();
            adapter.Dispose();

            //var enumerable = datalist.AsEnumerable<string>().ToAsyncEnumerable();

            Logger.Info($"Total Registros a procesar: {ds2.Tables[0].Rows.Count}");

            string PK_Invoice = string.Empty;
            int count = 0;


            List<StructureInvoiceFileContent> list = new List<StructureInvoiceFileContent>();
            try
            {
                //object sync = new Object();               
                Parallel.ForEach(datalist, (string invoiceItem, ParallelLoopState loopState) =>
                {
                    // here you can add or remove items from the fileInfo list           
                    count++;
                    var message = GeneratFilesbyInvoice(invoiceItem);
                    if (message.Result.Value != null)
                    {
                        try
                        {
                            list.Add(new 
                                StructureInvoiceFileContent {
                                PK_Invoice = invoiceItem,
                                InvoiceContent = message.Result.Value,
                                ReservationCode = message.Result.Key});

                            Logger.Info($"Factura: {invoiceItem}, #Proceso: {count.ToString()} - Trace {Thread.CurrentThread.ManagedThreadId}");
                        }
                        catch (Exception)
                        {
                            Logger.Warn($"List Item duplicate {invoiceItem}");
                        }
                    }
                });

                //Varificar si no se proceso algún registro.
                var listPending = datalist.Except(list.Select(x => x.PK_Invoice).ToList());

                foreach (var item in listPending)
                {
                    Lst_Error_Invoice.Enqueue(item.Trim());
                    Logger.Info($"Agregando a proceso en cola: {item}");
                }


                
                foreach (var item in list.GroupBy(x => x.PK_Invoice).Select(g => g.First()).ToList())
                {
                    var fileName = FilePath;
                    fileName = FilePath.Replace("PNR", item.ReservationCode);
                    fileName = fileName.Replace("##", countFile++.ToString());

                    FileStream fs = new FileStream(fileName, FileMode.Create);

                    //Guarda en archivo los registro procesados en la tarea.
                    using (StreamWriter oSW = new StreamWriter(fs, Encoding.UTF8, 160012))
                    {
                        oSW.AutoFlush = true;
                        oSW.Write(item.InvoiceContent);
                    }

                    if (fs != null)
                        fs.Dispose();
                }
                Logger.Info($"Registros procesador en Tarea: {list.Count()}");
                
            }
            catch (Exception ex)
            {
                Logger.Fatal($"FATAL ERROR: {ex.StackTrace}-{ex.Message} - {ex?.InnerException}");
                throw new Exception(ex.Message, ex);
            }
            finally
            {
                

                //Guarda en archivo los registro procesados en cola.
                ExecuteQueue(FilePath, countFile);

                //Actualizar periodos
                UpdateInvoicePeriods();

                //ClearBEITables
                //Se comento 15/06/2018
                /*ClearBEITables();*/

                //Llamada a Navitaire sp

                //Crear archivo bandera
                //FileInfo fi = new FileInfo(FilePath);
                //StreamWriter oSWFlag = new StreamWriter($"{fi.DirectoryName}\\FLG_PNR.txt");
                //oSWFlag.Close();
            }

        }

        /// <summary>
        /// Fills the data set asynchronous.
        /// </summary>
        /// <param name="asyncConnectionString">The asynchronous connection string.</param>
        /// <param name="PK_Invoice">The pk invoice.</param>
        /// <returns></returns>
        private Task<DataSet> FillDataSetAsync(string asyncConnectionString, string PK_Invoice)
        {
            //System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            //watch.Start();
            //watch.Stop();
            //var timespam = watch.Elapsed;

            return Task<DataSet>.Factory.StartNew(() =>
            {
                var dataset = new DataSet();
                using (SqlConnection conn = new SqlConnection(asyncConnectionString))
                {
                    //conn.Open();
                    var functionQuery = "spBuildFile_GetInvoice_App";
                    SqlCommand comm = new SqlCommand(functionQuery, conn);

                    comm.Parameters.AddWithValue("PK_Invoice", PK_Invoice);
                    comm.Parameters.AddWithValue("ProcessType", parameters.ProcessType.ToString());
                    comm.Parameters.AddWithValue("ProcessId", parameters.IdPeriod.ToString());
                    comm.Parameters.AddWithValue("CountryCode", parameters.CountryCode.ToString());

                    comm.CommandType = CommandType.StoredProcedure;
                    comm.CommandTimeout = 240;

                    SqlDataAdapter da = new SqlDataAdapter();
                    da.SelectCommand = comm;

                    da.Fill(dataset);
                }
                return dataset;
            });
        }

        /// <summary>
        /// Generats the filesby invoice.
        /// </summary>
        /// <param name="PK_Invoice">The pk invoice.</param>
        /// <returns></returns>
        private async Task<KeyValuePair<string,string>> GeneratFilesbyInvoice(string PK_Invoice)
        {
            StringBuilder sbFile = new StringBuilder();
            string carrierCode = string.Empty;
            try
            {
                await FillDataSetAsync(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString, PK_Invoice).ContinueWith(task =>
                {
                    DataSet ds1 = task.Result;
                    carrierCode = ds1.Tables[0].Rows[0]["PNR"].ToString();
                    for (int table = 0; table <= ds1.Tables.Count - 1; table++)
                    {
                        foreach (DataRow row in ds1.Tables[table].Rows)
                        {
                            if (table == 2)
                            {
                                var rowAsString1 = string.Join("|", row.ItemArray.Take(11).ToArray()) + "\r\n";
                                sbFile.Append(rowAsString1);
                                if (row["FlgIMPLFAC"].ToString() == "1")
                                {
                                    var rowAsString2 = string.Join("|", row.ItemArray.Select(x => x.ToString().Trim()).Skip(12).Take(9)) + "\r\n";
                                    sbFile.Append(rowAsString2);
                                }
                                else if (row["FlagTP"].ToString() == "1")
                                {
                                    var rowAsString3 = string.Join("|", row.ItemArray.Select(x => x.ToString().Trim()).Skip(22).Take(14)) + "\r\n";
                                    sbFile.Append(rowAsString3);
                                }
                            }
                            else
                            {
                                var rowAsString = string.Join("|", row.ItemArray) + "\r\n";
                                sbFile.Append(rowAsString);
                            }
                        }
                    }
                });
                return new KeyValuePair<string, string>(carrierCode, sbFile.ToString());

                //var ds1 = await FillDataSetAsync(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString, PK_Invoice);
                //var ds1 = FillDataSet(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString, PK_Invoice);                
            }
            catch (Exception ex)
            {
                if (ex is SqlException)
                {
                    Lst_Error_Invoice.Enqueue(PK_Invoice.Trim());
                }
                Logger.Info($"Error en Factura: {PK_Invoice}, Error: {ex.Message}");

                return new KeyValuePair<string, string>(carrierCode, string.Empty);
            }

        }

        /// <summary>
        /// Executes the queue.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        private void ExecuteQueue(string filePath, int countFile)
        {
            Logger.Info("Verificando si existen procesos en cola...");
            
            while (Lst_Error_Invoice.Count > 0)
            {
                try
                {
                    var item = Lst_Error_Invoice.Peek();
                    var message = GeneratFilesbyInvoice(item);
                    var fileName = filePath;
                    
                    if (message.Result.Value != null)
                    {
                        fileName = filePath.Replace("PNR", message?.Result.Key);
                        fileName = filePath.Replace("##", countFile++.ToString());
                        FileStream fs = new FileStream(fileName, FileMode.Create);

                        using (StreamWriter oSW = new StreamWriter(fs, Encoding.UTF8, 160012))
                        {
                            oSW.AutoFlush = true;
                            oSW.Write(message?.Result);
                            oSW.Flush();
                        }

                        if (fs != null)
                            fs.Dispose();
                    }
                    

                    Logger.Info($"Factura: {item}, #Proceso: en cola.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error en proceso en cola {ex.Message}");
                }
                finally
                {
                    Lst_Error_Invoice.Dequeue();
                }
            }

        }

        /// <summary>
        /// Updates the invoice periods.
        /// </summary>
        /// <returns></returns>
        protected bool UpdateInvoicePeriods()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
                {
                    var functionQuery = "[PNR].[spBuildFile_SetUpdatePeriods]";
                    SqlCommand comm = new SqlCommand(functionQuery, conn);

                    comm.Parameters.AddWithValue("ProcessType", parameters.ProcessType.ToString());
                    comm.Parameters.AddWithValue("ProcessId", parameters.IdPeriod.ToString());
                    comm.Parameters.AddWithValue("CountryCode", parameters.CountryCode.ToString());
                    comm.Parameters.AddWithValue("FinalFilePath", string.Empty);  //Ruta donde se deposito el archivo

                    comm.CommandType = CommandType.StoredProcedure;
                    comm.CommandTimeout = 240;
                    conn.Open();

                    comm.ExecuteNonQuery();
                }

                return true;

            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Clears the bei tables.
        /// </summary>
        /// <returns></returns>
        protected bool ClearBEITables()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
                {
                    var functionQuery = "dbo.spClearBEITables";
                    SqlCommand comm = new SqlCommand(functionQuery, conn);                   

                    comm.CommandType = CommandType.StoredProcedure;
                    comm.CommandTimeout = 240;
                    conn.Open();

                    comm.ExecuteNonQuery();                   
                }

                return true;

            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
