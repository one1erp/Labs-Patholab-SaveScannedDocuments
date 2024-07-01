using System;
using System.Linq;
using System.IO;
using Oracle.ManagedDataAccess.Client;
using System.Reflection;
using System.Configuration;
using System.Diagnostics;


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

                var cs = ConfigurationManager.AppSettings["CONNECTIONS"];
                var source = ConfigurationManager.AppSettings["Source"];
                var destination = ConfigurationManager.AppSettings["Destination"];
                LOGPATH = ConfigurationManager.AppSettings["LOGFILE"]+ "UpdateScannedDocument" + DateTime.Now.ToString("yyyyMMdd") + ".txt";


                //Write to log
                Logger.WriteLogFile(LOGPATH, "//////////////////////////////" + Environment.NewLine +
                                             "Start program on " + DateTime.Now + Environment.NewLine + "Load config Values" + Environment.NewLine +
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

                using (OracleConnection oraCon = new OracleConnection(cs))
                {
                    try
                    {
                        oraCon.Open();
                        Logger.WriteLogFile(LOGPATH, "Oracle Connected");

                        foreach (var path in pdfFiles)
                        {
                           
                                var fileName = Path.GetFileNameWithoutExtension(path);
                                var validName = fileName.Insert(fileName.Length - 2, "/");
                                string getSdgSql = "Select sdg_id from lims_sys.sdg where Substr(sdg.name,1,1) in ('B','C','P') and name='" + validName + "'";

                                using (var cmd = new OracleCommand(getSdgSql, oraCon))
                                {
                                    var sdgId = cmd.ExecuteScalar();
                                    Logger.WriteLogFile(LOGPATH, getSdgSql);

                                    if (sdgId != null)
                                    {
                                        string newDestination = MoveFile(destination, path);
                                        if (newDestination != null)
                                        {
                                            var success = SaveFileDest(sdgId.ToString(), newDestination, oraCon);
                                            string successMsg = success ? "Process done successfully" : "Process failed";
                                            Logger.WriteLogFile(LOGPATH, successMsg);
                                        }
                                        else
                                        {
                                            Logger.WriteLogFile(LOGPATH, path + " isn't moving, path will not be updated in DB");
                                        }
                                    }
                                    else
                                    {
                                        string msg = "SDG named " + validName + " does not exist or does not meet the conditions";
                                        Logger.WriteLogFile(LOGPATH, msg);
                                    }
                                }
                            }                        
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLogFile(LOGPATH, ex.ToString());
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.WriteLogFile(LOGPATH, ex);
            }

        }

        private static bool SaveFileDest(string sdgId, string path, OracleConnection oraCon)
        {
            try
            {
                using (oraCon)
                {
                    OracleTransaction transaction = oraCon.BeginTransaction();

                    var sdg_attachment_id = string.Empty;
                    string sql = "select lims.sq_U_SDG_ATTACHMENT.nextval from dual";
                    var cmd = new OracleCommand(sql, oraCon);
                    sdg_attachment_id = cmd.ExecuteScalar().ToString();

                    //Create a new record with the received file data in U_SDG_ATTACHMENT table.
                    sql = $"insert into lims_sys.U_SDG_ATTACHMENT(U_SDG_ATTACHMENT_ID, name, version, VERSION_STATUS) values({sdg_attachment_id}, '{DateTime.Now}', 1, 'A')";
                    cmd = new OracleCommand(sql, oraCon);
                    cmd.CommandText = sql;
                    bool success_1 = cmd.ExecuteNonQuery() == 1;

                    //Create a new suitable record in U_SDG_ATTACHMENT_USER table.
                    sql = $"insert into lims_sys.U_SDG_ATTACHMENT_USER(U_SDG_ATTACHMENT_ID, U_SDG_ID, U_TITLE, U_PATH) values({sdg_attachment_id}, {sdgId}, 'הגיע מהממשק', '{path}')";
                    cmd = new OracleCommand(sql, oraCon);
                    cmd.CommandText = sql;
                    bool success_2 = cmd.ExecuteNonQuery() == 1;

                    //only if the two record created, commit and approve the changes.
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


        private static string MoveFile(string destination, string source)
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

                    //change file's name in order to allow several documents for the same SDG.
                    string fileName = Path.GetFileNameWithoutExtension(Path.GetFileName(source)); // Get the file name without extension
                    string fileExtension = Path.GetExtension(Path.GetFileName(source)); // Get the file extension
                    string newFileName = fileName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + fileExtension;


                    newDest = Path.Combine(destFolder, newFileName);
                    File.Move(source, newDest);
                    Logger.WriteLogFile(LOGPATH, source + " moving to " + newDest);


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
