/*
 III. сохранение настроек сервера:

Зайти с помощью radmin на выбранный сервер соответвующего ЕДДС

1. скопировать файлы *.ini из каталога C:\ITK_OS\ItkServer в \ITK_OS на \\Zebra в соответствующий каталог для сервера ЕДДС
2* для серверов 192.168.16.11, 192.168.16.13, 192.168.16.14, 192.168.16.15
скопировать файлы *.ini из C:\OsAdapter_v2 в \OsAdapter_v2 на \\Zebra в соответствующий каталог для сервера ЕДДС

Hjlbyf-Vfnm2021
166itkos
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using NLog.Fluent;

namespace ServersBackUpCollector
{
    class Program
    {
        private static Logger log = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            log.Debug("Start");
            [DllImport("kernel32.dll")]
            static extern IntPtr GetConsoleWindow();

            [DllImport("user32.dll")]
            static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            int showProcess = 0;
            const int SW_HIDE = 0;
            const int SW_SHOW = 5;
            var handle = GetConsoleWindow();


            string localPath = AppDomain.CurrentDomain.BaseDirectory;

            if (!File.Exists(localPath + @"\conf.ini"))
            {
                log.Error("Не найден файл конфигурации: " + localPath + @"\conf.ini");
                return;
            }

            INIManager config = new INIManager(localPath + @"\conf.ini");
            if (!bool.TryParse(config.GetPrivateString("General", "ShowProcess"), out bool isShowProcess))
            {
                log.Error("Не удалось прочитать в конфиге параметр ShowProcess");
                isShowProcess = false;
            }
            if (isShowProcess)
                showProcess = SW_SHOW;
            //ShowWindow(handle, SW_HIDE);
            ShowWindow(handle, showProcess);

            if (!int.TryParse(config.GetPrivateString("General", "ServerCount"), out int serverCount))
            {
                log.Error("Не удалось прочитать в конфиге параметр ServerCount");
                return;
            }
            if (serverCount <= 0)
            {
                log.Error("Параметр ServerCount меньше или равен нулю");
                return;
            }

            List<IPAddress> serverIpList = new List<IPAddress>();

            for (int i = 1; i < serverCount+1; i++)
            {
                string address = config.GetPrivateString("ServerAddressList", "s" + i.ToString());
                if (IPAddress.TryParse(address, out IPAddress parsedAddress))
                {
                    serverIpList.Add(parsedAddress);
                }
                else
                {
                    log.Error("Не удалось распознать как IP адрес параметр: s"+i+"="+ address);
                }
            }

            if (serverIpList.Count == 0)
            {
                log.Error("Список ip адресов серверов пустой. Проверьте конфиг");
                return;
            }

            string allBackUpDirPath = config.GetPrivateString("General", "AllBackUpDirPath");
            if (string.IsNullOrEmpty(allBackUpDirPath))
            {
                log.Error("Не указан путь для сохранения бекапов. Проверьте конфиг. параметр AllBackUpDirPath");
                return;
            }

            try
            {
                //удаляем старый бекап бекапов
                if (Directory.Exists(allBackUpDirPath + "_bakup") && Directory.Exists(allBackUpDirPath))
                    Directory.Delete(allBackUpDirPath + "_bakup", true);
                //бекапим текущие бекапы
                if (Directory.Exists(allBackUpDirPath))
                    Directory.Move(allBackUpDirPath, allBackUpDirPath + "_bakup");
                //Закачиваем новые
                Directory.CreateDirectory(allBackUpDirPath);

                foreach (var address in serverIpList)
                {
                    string sourceBackupFolder = Directory
                        .GetDirectories("\\\\" + address + "\\SharedFiles\\", "АРМ*_BackUp").FirstOrDefault();

                    if (!string.IsNullOrEmpty(sourceBackupFolder))
                    {
                        string[] iniList = Directory.GetFiles("C:\\ITK_OS\\ItkServer", "*.ini");
                        string serverBackupFolder = sourceBackupFolder + "\\Сервер_" + address.ToString();

                        foreach (string f in iniList)
                        {
                            string fName = f.Substring("C:\\ITK_OS\\ItkServer".Length + 1);
                            File.Copy(f, Path.Combine(f, serverBackupFolder + "\\" + fName), true);
                        }

                        if (address.ToString().Equals("192.168.16.11") || address.ToString().Equals("192.168.16.13") ||
                            address.ToString().Equals("192.168.16.14") || address.ToString().Equals("192.168.16.15"))
                        {
                            string OsAdapterPath = @"\\" + address.ToString() + "\\C$\\OsAdapter_v2";
                            
                            if (Directory.Exists(OsAdapterPath))
                            {
                                string[] iniList2 = Directory.GetFiles(OsAdapterPath, "*.ini");
                                foreach (string f in iniList2)
                                {
                                    string fName = f.Substring(OsAdapterPath.Length + 1);
                                    log.Debug("fName=" + fName);
                                    try
                                    {
                                        File.Copy(f, Path.Combine(f, serverBackupFolder + "\\OsAdapter_v2\\" + fName),
                                            true);
                                    }
                                    catch (Exception e)
                                    {
                                        log.Error("Ошибка при копировании настроек OsAdapter_v2:" + e.ToString());
                                    }
                                }
                            }
                        }

                        //todo тут надо переделать
                        //todo сделать расширяемость через конфиги
                        //todo добавить работу по расписанию
                        //

                        CopyFolder(sourceBackupFolder,
                            allBackUpDirPath + "\\" + sourceBackupFolder.Substring(15 + address.ToString().Length));
                    }
                    else
                    {
                        log.Error("Не найден каталог с бекапами на сервере " + address);
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Ошибка: " + e.ToString());
            }



            static void CopyFolder(string sourceFolder, string destFolder)
            {
                if (!Directory.Exists(destFolder))
                    Directory.CreateDirectory(destFolder);
                string[] files = Directory.GetFiles(sourceFolder);
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    string dest = Path.Combine(destFolder, name);
                    File.Copy(file, dest, true);
                }

                string[] folders = Directory.GetDirectories(sourceFolder);
                foreach (string folder in folders)
                {
                    string name = Path.GetFileName(folder);
                    string dest = Path.Combine(destFolder, name);
                    CopyFolder(folder, dest);
                }
            }
        }
    }

    public class INIManager
    {
        public INIManager(string aPath)
        {
            path = aPath;
        }

        public string GetPrivateString(string aSection, string aKey)
        {
            StringBuilder buffer = new StringBuilder(SIZE);
            GetPrivateString(aSection, aKey, null, buffer, SIZE, path);
            return buffer.ToString();
        }

        //Пишет значение в INI-файл (по указанным секции и ключу) 
        public void WritePrivateString(string aSection, string aKey, string aValue)
        {
            //Записать значение в INI-файл
            WritePrivateString(aSection, aKey, aValue, path);
        }

        //Возвращает или устанавливает путь к INI файлу
        public string Path
        {
            get { return path; }
            set { path = value; }
        }

        //Поля класса
        private const int SIZE = 1024; //Максимальный размер (для чтения значения из файла)
        private string path = null; //Для хранения пути к INI-файлу

        //Импорт функции GetPrivateProfileString (для чтения значений) из библиотеки kernel32.dll
        [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileString")]
        private static extern int GetPrivateString(string section, string key, string def, StringBuilder buffer,
            int size, string path);

        //Импорт функции WritePrivateProfileString (для записи значений) из библиотеки kernel32.dll
        [DllImport("kernel32.dll", EntryPoint = "WritePrivateProfileString")]
        private static extern int WritePrivateString(string section, string key, string str, string path);
    }
}
