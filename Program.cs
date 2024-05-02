using System;
using System.Linq;
using System.IO;
using Oracle.ManagedDataAccess.Client;


namespace SaveScannedDocuments
{



    class Program
    {
      
        static string LOGPATH = null;
        private static OracleConnection oraCon;

        static void Main(string[] args)
        {

            try
            {
                if (string.IsNullOrEmpty(args[0]))
                {
                    return;
                }

                //Get parameters from ini file
                IniFile ini = new IniFile(args[0]);

                //Load ini Values
                var cs = ini.GetString("CONNECTIONS", "ORACLE", "");
                var source = ini.GetString("FOLDERS", "Source", "");
                var destination = ini.GetString("FOLDERS", "Destination", "");
                LOGPATH = ini.GetString("FOLDERS", "LOGFILE", "");
                LOGPATH = LOGPATH + "UpdateScannedDocument" + DateTime.Now.ToString("yyyyMMdd") + ".txt";

                //Write to log
                Logger.WriteLogFile(LOGPATH, "//////////////////////////////" + Environment.NewLine +
                                             "Start program on " + DateTime.Now + Environment.NewLine + "Load ini Values" + Environment.NewLine +
                                             "Connection String is " + cs + Environment.NewLine +
                                             "destination is " + destination + Environment.NewLine +
                                             "source is " + source);





                string[] pdfFiles;
                if (Directory.Exists(source))
                {

                    //Get pdf files
                    pdfFiles = Directory.GetFiles(source, "*.pdf");
                    string msgf = pdfFiles.Length + " documents found";
                    Logger.WriteLogFile(LOGPATH, msgf);

                    if (pdfFiles.Count() < 1)
                    {
                        Logger.WriteLogFile(LOGPATH, "Files not found " + Environment.NewLine + " Exit program");
                        return;
                    }
                }
                else
                {
                    Logger.WriteLogFile(LOGPATH, source + " doesn't exists" + Environment.NewLine + " Exit program");
                    return;

                }




                var debug =
                    "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=192.168.0.239)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=limsprod)));User Id=lims_sys;Password=lims_sys;";
                

                oraCon = new OracleConnection(cs);
                oraCon.Open();
                Logger.WriteLogFile(LOGPATH, "Oracle Connected");

                //            
                foreach (var path in pdfFiles)
                {
                    try
                    {

                        var fileName = Path.GetFileNameWithoutExtension(path);
                        var validName = fileName.Replace("_", "/");
                        string getSdgSql = "Select sdg_id from lims_sys.sdg where Substr(sdg.name,1,1) in ('B','C','P' )  and name='" + validName + "'";
                        var cmd = new OracleCommand(getSdgSql, oraCon);
                        var sdgId = cmd.ExecuteScalar();

                        Logger.WriteLogFile(LOGPATH, getSdgSql);

                        if (sdgId != null)
                        {
                            
                            string newDestination = MoveFile(destination, path);
                            if (newDestination != null)
                            {
                          
                                var success = SaveFileDest(sdgId.ToString(), path);
                                string successMsg = success ? "Proccess done successfully" : "Proccess failed";
                                Logger.WriteLogFile(LOGPATH, successMsg);
                            }
                            else
                            {
                                Logger.WriteLogFile(LOGPATH, path + " doesn't moving , path will not be update in DB");
                            }
                        }
                        else
                        {
                            string msg = "SDG named " + validName + " does not exist or does not meet the conditions";
                            Logger.WriteLogFile(LOGPATH, msg);

                        }
                        if (cmd != null)
                            cmd.Dispose();


                    }
                    catch (Exception exu)
                    {

                        Logger.WriteLogFile(LOGPATH, exu);

                    }
                }


                if (oraCon != null)
                    oraCon.Close();

                Logger.WriteLogFile(LOGPATH, "Finish");


            }
            catch (Exception ex)
            {
                Logger.WriteLogFile(LOGPATH, ex);

            }
            finally
            {

            }

        }

        private static bool SaveFileDest(string sdgId, string path)
        {
            try
            {
                using (oraCon)
                {
                    OracleTransaction transaction = oraCon.BeginTransaction();

                    var fileName = Path.GetFileNameWithoutExtension(path);
                    var sdg_attachment_id = string.Empty;
                    string sql = "select lims.sq_U_SDG_ATTACHMENT.nextval from dual";
                    var cmd = new OracleCommand(sql, oraCon);
                    sdg_attachment_id = cmd.ExecuteScalar().ToString();

                    sql = $"insert into lims_sys.U_SDG_ATTACHMENT(U_SDG_ATTACHMENT_ID, name, version, VERSION_STATUS) values({sdg_attachment_id}, '{DateTime.Now}', 1, 'A')";
                    cmd = new OracleCommand(sql, oraCon);
                    cmd.CommandText = sql;
                    bool success_1 = cmd.ExecuteNonQuery() == 1;

                    sql = $"insert into lims_sys.U_SDG_ATTACHMENT_USER(U_SDG_ATTACHMENT_ID, U_SDG_ID, U_TITLE, U_PATH) values({sdg_attachment_id}, {sdgId}, 'הגיע מהממשק', '{path}')";
                    cmd = new OracleCommand(sql, oraCon);
                    cmd.CommandText = sql;
                    bool success_2 = cmd.ExecuteNonQuery() == 1;

                    if (success_1 && success_2)
                    {
                        transaction.Commit();
                        Logger.WriteLogFile(LOGPATH, "Both queries executed successfully. Transaction committed.");
                        return true;
                    }
                    else
                    {
                        transaction.Rollback();
                        Logger.WriteLogFile(LOGPATH, "An error occurred. Transaction rolled back.");
                        return false;
                    }
                        
               
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLogFile(LOGPATH, ex);
                return false;
            }
        }
        private static string MoveFile(string destination, string s)
        {

            try
            {

                string newDest = null;
                if (Directory.Exists(destination))
                {
                    var destFolder = CreateMonthlyFolder(destination);
                    if (!Directory.Exists(destFolder))
                    {
                        Directory.CreateDirectory(destFolder);
                    }
                    string fileName = Path.GetFileNameWithoutExtension(Path.GetFileName(s)); // Get the file name without extension
                    string fileExtension = Path.GetExtension(Path.GetFileName(s)); // Get the file extension
                    string newFileName = fileName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + fileExtension;
                    newDest = Path.Combine(destFolder, newFileName);
                    File.Move(s, newDest);
                    Logger.WriteLogFile(LOGPATH, s + " moving to " + newDest);


                }
                else
                {
                    Logger.WriteLogFile(LOGPATH, destination + " does not exists");
                    return null;
                }
                return newDest;

            }
            catch (Exception e)
            {

                Logger.WriteLogFile(LOGPATH, e);
                return null;
            }

        }


        public static string CreateMonthlyFolder(string baseFolder)
        {

            DateTime now = DateTime.Now;
            var yearName = now.ToString("yyyy");
            var monthName = now.ToString("MM");


            var folder = Path.Combine(baseFolder,
                                      Path.Combine(yearName, monthName));
            return folder;


        }
    }


}
