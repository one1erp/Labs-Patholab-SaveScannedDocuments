using System;
using System.Collections.Generic;
using System.IO;

using System.Text;

namespace SaveScannedDocuments
{


    public static class Logger
    {
        public static void WriteLogFile(string path, Exception exception)
        {
            try
            {
                using (FileStream file = new FileStream(path, FileMode.Append, FileAccess.Write))
                {
                    var streamWriter = new StreamWriter(file);
                    streamWriter.WriteLine(DateTime.Now);
                    streamWriter.WriteLine("Error ");
                    streamWriter.WriteLine("Message");
                    streamWriter.WriteLine(exception.Message);
                    streamWriter.WriteLine("InnerException");
                    streamWriter.WriteLine(exception.InnerException);
                    streamWriter.WriteLine("StackTrace");
                    streamWriter.WriteLine(exception.StackTrace);
                    if (exception.InnerException != null)
                    {
                        streamWriter.WriteLine("InnerException.Message");
                        streamWriter.WriteLine(exception.InnerException.Message);
                    }
                    streamWriter.WriteLine();
                    streamWriter.WriteLine("///////////////////////////////////////////");
                    streamWriter.WriteLine();
                    streamWriter.Close();
                }
            }
            catch
            {
            }


        }


        public static void WriteLogFile(string path, string errorMessage)
        {
            try
            {
                using (FileStream file = new FileStream(path, FileMode.Append, FileAccess.Write))
                {
                    var streamWriter = new StreamWriter(file);        
                    streamWriter.WriteLine(errorMessage);
                    streamWriter.Close();
                }
            }
            catch
            {
            }


        }
    }
}
