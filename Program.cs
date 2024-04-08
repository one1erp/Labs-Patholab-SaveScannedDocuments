using System;
using System.Linq;
using System.IO;
using Oracle.ManagedDataAccess.Client;


namespace SaveScannedDocuments
{



    class Program
    {
      
        static string LOGPATH = null;
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
                

                var oraCon = new OracleConnection(cs);
                oraCon.Open();
                Logger.WriteLogFile(LOGPATH, "Oracle Connected");

                //            
                foreach (var s in pdfFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(s);

                        var validName = fileName.Replace("_", "/");


                        string getSdgSql = "Select sdg_id from lims_sys.sdg where   Substr(sdg.name,1,1) in ('B','C','P' )  and name='" + validName + "'";
                        var cmd = new OracleCommand(getSdgSql, oraCon);
                        var sdgId = cmd.ExecuteScalar();
                        Logger.WriteLogFile(LOGPATH, getSdgSql);

                        if (sdgId != null)
                        {
                            string newDestination = MoveFile(destination, s);
                            if (newDestination != null)
                            {



                                //Update by sdg id 
                                string sql = "update  sdg_user  set u_atfilenm ='" + newDestination +
                                             "' where sdg_id = '" +
                                             sdgId + "'";
                                cmd.CommandText = sql;
                                Logger.WriteLogFile(LOGPATH, sql);

                                var res = cmd.ExecuteNonQuery();
                                if (res == 1) //Success
                                {
                                    Logger.WriteLogFile(LOGPATH, "Document for " + validName + "  Updated in DB");
                                }
                            }
                            else
                            {
                                Logger.WriteLogFile(LOGPATH, s + " doesn't moving , path will not be update in DB");

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
                    newDest = Path.Combine(destFolder, Path.GetFileName(s));
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
