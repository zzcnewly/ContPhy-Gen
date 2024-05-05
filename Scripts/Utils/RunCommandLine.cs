using System;
using System.Diagnostics;
using System.IO;

namespace PRUtils{
    class RunProgram
    {
        static public string Main(string cmd)
        {   
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return  RunCommand("cmd.exe", "/C " + cmd);
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                return  RunCommand("/bin/bash", "-c \"" + cmd + "\"");
            }
            else {
                return  RunCommand("/bin/bash", "-c \"" + cmd + "\"");
            }
        }

        static public string RunCommand(string fileName, string arguments)
        {   
            Process process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.ErrorDataReceived += new DataReceivedEventHandler(Output);

            process.Start();

            process.BeginErrorReadLine();// start async reading 
            process.WaitForExit();
            process.Close();
            process.Dispose();
            return "";
        }

        static private void Output(object sendProcess, DataReceivedEventArgs output)
        {
            if (!string.IsNullOrEmpty(output.Data))
            {
                UnityEngine.Debug.Log(output.Data);
            }
        }


        public static void DeleteFolder(string folderPath)
        {
            try
            {
                DeleteFolderRecursively(folderPath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log("An error occurred while deleting the folder: " + ex.Message);
            }
        }

        static void DeleteFolderRecursively(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"The folder '{folderPath}' does not exist.");
            }
            while (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }
        }
    }
}

