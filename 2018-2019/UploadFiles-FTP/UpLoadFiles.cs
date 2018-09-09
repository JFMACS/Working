using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace WindowsFormsApplication2.UpLoad
{
    class UpLoadFiles
    {
        //ftp参数配置
        private const string ip = "172.16.102.181";
        private const int port = 21;
        private const string username = "ftpname";
        private const string psd = "";
        private const string root_remote_dir = "/home/ftp/cxy/GIS_TEST";//所有上传的内容都相对于该目录

        private string root_remote_dir_url;//ftp上传的根位置的url地址,所有上传的内容都相对于该url地址

        //性能控制：
        private const int limit_max_count_threads = 10;//同一时间工作中的线程数量
        private const int limit_ftp_connection = limit_max_count_threads + 1;//同一时刻ftp的连接数极限

        private Semaphore sem_totalUpLoadedSize = new Semaphore(1, 1);//创建1个信号量,控制更新totalUpLoadedSize
        private Semaphore sem_count_uploaded = new Semaphore(1, 1);//创建1个信号量,控制更新count_uploaded
        private Semaphore sem_size_had_uploaded = new Semaphore(1, 1);//创建1信号量，控制更新size_had_uploaded （因为断点续传功能需要自加已上传的流量大小）
        private Semaphore sem_thread_working = new Semaphore(limit_max_count_threads, limit_max_count_threads);//控制上传线程的数量

        private long size_had_uploaded = 0;//上n次的上传过程中，已上传了多少流量。
        private long totalUpLoadedSize = 0;//总共上传的字节数(算进度用)
        private int count_uploaded = 0;//已上传的文件数
        private long totalSize = 0;//需要上传的字节总数
        private int count_file = 0;//需要上传的文件总数
        private long speed = 0;//记录上传速度

        private List<Dictionary<string, string>> LocalFilePath_RemoteFilePaths = new List<Dictionary<string, string>>();//记录所有文件的本地地址 对应的 远端地址

        private DateTime startTime = DateTime.Now;//记录上传开始时间

        private bool count_finished = false;//计数是否完成。在上传之前，先统计文件总数量、文件总流量。统计完成后，值为true;
        private bool all_finished = false;//标记是否全部传完

        private bool status_paused = false;//是否暂停上传。true暂停上传 false正常继续上传
        private DateTime status_paused_start_time;//暂停的开始时间 没有初始值
        private TimeSpan status_paused_interval = new TimeSpan(0);//暂停持续了多少时间

        //构造函数
        public UpLoadFiles()
        {
            root_remote_dir_url = string.Format("ftp://{0}:{1}/{2}", ip, port, root_remote_dir);
            if (LocalFilePath_RemoteFilePaths != null)
                LocalFilePath_RemoteFilePaths.Clear();
        }

        //建议ftp使用完成后调用 内存释放
        public void Dispose()
        {
            if (sem_totalUpLoadedSize != null)
            {
                sem_totalUpLoadedSize.Dispose();
                sem_totalUpLoadedSize.Close();
            }

            if (sem_count_uploaded != null)
            {
                sem_count_uploaded.Dispose();
                sem_count_uploaded.Close();
            }

            if (sem_size_had_uploaded != null)
            {
                sem_size_had_uploaded.Dispose();
                sem_size_had_uploaded.Close();
            }

            if (sem_thread_working != null)
            {
                sem_thread_working.Dispose();
                sem_thread_working.Close();
            }
        }

        //验证证书是否有效
        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        //======================================基层功能======================================
        //在ftp_dir_path目录下创建目录
        //参数：dir_name 将创建的文件夹名
        //参数：ftp_dir_path 将创建的文件夹的父文件夹
        private void CreateRemoteDir(string dir_name, string ftp_dir_path = "")
        {
            //因为fpt上传是不允许存在西文#符号的所以替换字样上传
            dir_name = dir_name.Replace("#", "＃");
            ftp_dir_path = ftp_dir_path.Replace("#", "＃");

            string uri = root_remote_dir_url + ftp_dir_path + "/" + dir_name;
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
            ServicePointManager.DefaultConnectionLimit = limit_ftp_connection;
            FtpWebRequest reqFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            reqFtp.Credentials = new NetworkCredential(username, psd);
            reqFtp.Method = WebRequestMethods.Ftp.MakeDirectory;
            reqFtp.EnableSsl = true;
            reqFtp.UseBinary = true;
            reqFtp.KeepAlive = false;
            reqFtp.Timeout = 3600 * 1000;
            reqFtp.ReadWriteTimeout = 3600 * 1000;

            FtpWebResponse response = (FtpWebResponse)reqFtp.GetResponse();
            response.Close();
        }

        //相对远程根节点（root_remote_dir）的位置 删除文件夹的功能（只能删除空文件夹）【暂未使用】
        //参数：ftp_dir_path 将删除的文件夹路径
        private void DeleteRemoteEmptyDir(string ftp_dir_path)
        {
            ftp_dir_path = ftp_dir_path.Replace("#", "＃");//因为fpt上传是不允许存在西文#符号的所以替换字样上传

            string uri = root_remote_dir_url + ftp_dir_path;
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
            ServicePointManager.DefaultConnectionLimit = limit_ftp_connection;
            FtpWebRequest reqFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            reqFtp.Credentials = new NetworkCredential(username, psd);
            reqFtp.Method = WebRequestMethods.Ftp.RemoveDirectory;
            reqFtp.EnableSsl = true;
            reqFtp.UseBinary = true;
            reqFtp.KeepAlive = false;
            reqFtp.Timeout = 3600 * 1000;
            reqFtp.ReadWriteTimeout = 3600 * 1000;

            FtpWebResponse response = (FtpWebResponse)reqFtp.GetResponse();
            response.Close();
        }

        private void UploadFile(string local_file_path, string ftp_file_path)
        {
            ftp_file_path = ftp_file_path.Replace("#", "＃");//因为fpt上传是不允许存在西文#符号的所以替换字样上传

            string uri = root_remote_dir_url + ftp_file_path;
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
            ServicePointManager.DefaultConnectionLimit = limit_ftp_connection;
            FileInfo fileinfo = new FileInfo(local_file_path);
            FtpWebRequest reqFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            reqFtp.Credentials = new NetworkCredential(username, psd);
            reqFtp.Method = WebRequestMethods.Ftp.UploadFile;
            reqFtp.EnableSsl = true;
            reqFtp.UseBinary = true;
            reqFtp.KeepAlive = true;
            reqFtp.Timeout = 3600 * 1000;
            reqFtp.ReadWriteTimeout = 3600 * 1000;
            reqFtp.ContentLength = fileinfo.Length;

            Stream uploadStream = reqFtp.GetRequestStream();//获取上传流

            //缓冲区
            int buffLength = 1 * 1024 * 1024;
            byte[] buff = new byte[buffLength];

            FileStream filestream = fileinfo.OpenRead();
            int contentLen = 0;
            while ((contentLen = filestream.Read(buff, 0, buffLength)) != 0)
            {
                while (true)
                {
                    if (status_paused == true)
                        Delay(1000);
                    else
                        break;
                }

                uploadStream.Write(buff, 0, contentLen);
                uploadStream.Flush();

                //更新上传的流量
                sem_totalUpLoadedSize.WaitOne();
                totalUpLoadedSize += (long)contentLen;
                sem_totalUpLoadedSize.Release();
            }
            //更新上传的文件数目
            sem_count_uploaded.WaitOne();
            count_uploaded++;
            sem_count_uploaded.Release();

            filestream.Close();
            uploadStream.Close();
        }

        //追加文件，用于断点续传。
        //参数1：本地文件 指名哪个文件要进行续传
        //参数2：ftp服务器的文件 指名要把本地文件剩余内容上传到该ftp文件
        //参数3：本地文件从哪个文件位置开始续传。比如：已上传了5个字节，那么就从本地文件下标5的位置开始上传。默认为负数时，将自动求取已上传文件的大小
        private void AppendFile(string local_file_path, string ftp_file_path, long local_off_set = -1)
        {
            ftp_file_path = ftp_file_path.Replace("#", "＃");//因为fpt上传是不允许存在西文#符号的所以替换字样上传

            string uri = root_remote_dir_url + ftp_file_path;
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
            ServicePointManager.DefaultConnectionLimit = limit_ftp_connection;
            FileInfo fileinfo = new FileInfo(local_file_path);
            FtpWebRequest reqFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            reqFtp.Credentials = new NetworkCredential(username, psd);
            reqFtp.Method = WebRequestMethods.Ftp.AppendFile;
            reqFtp.EnableSsl = true;
            reqFtp.UseBinary = true;
            reqFtp.KeepAlive = false;
            reqFtp.Timeout = 3600 * 1000;
            reqFtp.ReadWriteTimeout = 3600 * 1000;
            reqFtp.ContentLength = fileinfo.Length;

            Stream uploadStream = reqFtp.GetRequestStream();//获取上传流

            //缓冲区
            int buffLength = 1 * 1024 * 1024;
            byte[] buff = new byte[buffLength];

            local_off_set = (local_off_set < 0) ? GetFileUploadedSize(uri) : local_off_set;//已上传的文件的大小

            FileStream filestream = fileinfo.OpenRead();
            filestream.Seek(local_off_set, 0);//已上传了多少量，就把文件指针偏移到该位置。
            int contentLen = 0;
            while ((contentLen = filestream.Read(buff, 0, buffLength)) != 0)
            {
                while (true)
                {
                    if (status_paused == true)
                        Delay(1000);
                    else
                        break;
                }

                uploadStream.Write(buff, 0, contentLen);
                uploadStream.Flush();

                //更新上传的流量
                sem_totalUpLoadedSize.WaitOne();
                totalUpLoadedSize += (long)contentLen;
                sem_totalUpLoadedSize.Release();
            }
            //更新上传的文件数目
            sem_count_uploaded.WaitOne();
            count_uploaded++;
            sem_count_uploaded.Release();

            filestream.Close();
            uploadStream.Close();
        }

        //相对远程根节点（root_remote_dir）的位置 删除文件的功能【暂未使用】
        //参数：file_name 将删除的文件路径
        private void DeleteRemoteFile(string ftp_file_path)
        {
            ftp_file_path = ftp_file_path.Replace("#", "＃");//因为fpt上传是不允许存在西文#符号的所以替换字样上传

            string uri = root_remote_dir_url + ftp_file_path;
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
            ServicePointManager.DefaultConnectionLimit = limit_ftp_connection;
            FtpWebRequest reqFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            reqFtp.Credentials = new NetworkCredential(username, psd);
            reqFtp.Method = WebRequestMethods.Ftp.DeleteFile;
            reqFtp.EnableSsl = true;
            reqFtp.UseBinary = true;
            reqFtp.KeepAlive = false;
            reqFtp.Timeout = 3600 * 1000;
            reqFtp.ReadWriteTimeout = 3600 * 1000;

            FtpWebResponse response = (FtpWebResponse)reqFtp.GetResponse();
            response.Close();
        }

        // 判断ftp上的文件目录是否存在(相对远程根节点（root_remote_dir）的位置)
        // 参数：dir_name希望确认的文件夹名称 参数：ftp_dir待判断的文件夹的父文件夹。
        // 可以理解为：基于根节点下，ftp_dir目录下是否有名为dir_name的文件夹
        private bool IsRemoteDirExists(string dir_name, string ftp_dir_path = "")
        {
            //因为fpt上传是不允许存在西文#符号的所以替换字样上传
            if (ftp_dir_path == null)
            {
                ftp_dir_path = string.Empty;
            }
            dir_name = dir_name.Replace("#", "＃");
            ftp_dir_path = ftp_dir_path.Replace("#", "＃");

            bool flag = false;
            string uri = root_remote_dir_url + ftp_dir_path + "/";//必须要加“/”。不加斜杠读取时会携带父目录结构，影响下面的判断
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
            ServicePointManager.DefaultConnectionLimit = limit_ftp_connection;
            FtpWebRequest reqFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            reqFtp.Credentials = new NetworkCredential(username, psd);
            reqFtp.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFtp.UsePassive = true;
            reqFtp.EnableSsl = true;
            reqFtp.UseBinary = true;
            reqFtp.KeepAlive = false;
            reqFtp.Timeout = 3600 * 1000;
            reqFtp.ReadWriteTimeout = 3600 * 1000;

            WebResponse response = reqFtp.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream());
            string line;
            //通过对指定目录的父目录进行扫描查看是否包含该目录
            while ((line = reader.ReadLine()) != null)
            {
                if (dir_name.Equals(line))
                {
                    flag = true;
                    break;
                }
            }
            reader.Close();
            response.Close();
            return flag;
        }

        //判断ftp端文件夹是否为空文件夹【暂未使用】
        //参数：dir_name 待判断的文件夹
        private bool IsRemoteDirEmpty(string ftp_dir_path)
        {
            ftp_dir_path = ftp_dir_path.Replace("#", "＃");//因为fpt上传是不允许存在西文#符号的所以替换字样上传

            bool flag = false;
            string uri = root_remote_dir_url + ftp_dir_path + "/";
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
            ServicePointManager.DefaultConnectionLimit = limit_ftp_connection;
            FtpWebRequest reqFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(uri));
            reqFtp.Credentials = new NetworkCredential(username, psd);
            reqFtp.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFtp.UsePassive = true;
            reqFtp.EnableSsl = true;
            reqFtp.UseBinary = true;
            reqFtp.KeepAlive = false;
            reqFtp.Timeout = 3600 * 1000;
            reqFtp.ReadWriteTimeout = 3600 * 1000;
            WebResponse response = reqFtp.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream());
            string line = reader.ReadLine();
            if (line == null)
                flag = true;

            reader.Close();
            response.Close();
            return flag;
        }

        //检查远端文件是否存在
        private bool IsRemoteFileExists(string ftp_file_path)
        {
            ftp_file_path = ftp_file_path.Replace("#", "＃");//因为fpt上传是不允许存在西文#符号的所以替换字样上传

            bool flag = false;
            string FileName = ftp_file_path.Substring(ftp_file_path.LastIndexOf('/') + 1);
            string RemoteDir = ftp_file_path.Substring(0, ftp_file_path.Length - FileName.Length);//不能-1 一定末尾要有/ 不加斜杠读取时会携带父目录结构，影响下面的判断
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
            ServicePointManager.DefaultConnectionLimit = limit_ftp_connection;
            FtpWebRequest reqFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(RemoteDir));
            reqFtp.Credentials = new NetworkCredential(username, psd);
            reqFtp.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFtp.UsePassive = true;
            reqFtp.EnableSsl = true;
            reqFtp.UseBinary = true;
            reqFtp.KeepAlive = false;
            reqFtp.Timeout = 3600 * 1000;
            reqFtp.ReadWriteTimeout = 3600 * 1000;
            WebResponse response = reqFtp.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream());
            string line;
            //通过对指定文件的目录进行扫描查看是否包含该文件
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Equals(FileName))
                {
                    flag = true;
                    break;
                }
            }
            reader.Close();
            response.Close();
            return flag;
        }

        //获取远端文件大小。前提：该文件存在
        private long GetRemoteFileSize(string ftp_file_path)
        {
            ftp_file_path = ftp_file_path.Replace("#", "＃");//因为fpt上传是不允许存在西文#符号的所以替换字样上传
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
            ServicePointManager.DefaultConnectionLimit = limit_ftp_connection;
            FtpWebRequest reqFtp = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftp_file_path));
            reqFtp.Credentials = new NetworkCredential(username, psd);
            reqFtp.Method = WebRequestMethods.Ftp.GetFileSize;
            reqFtp.EnableSsl = true;
            reqFtp.UseBinary = true;
            reqFtp.KeepAlive = false;
            reqFtp.Timeout = 3600 * 1000;
            reqFtp.ReadWriteTimeout = 3600 * 1000;

            FtpWebResponse response = (FtpWebResponse)reqFtp.GetResponse();
            response.Close();
            return response.ContentLength;
        }
        //======================================基层功能End======================================

        //======================================功能辅助层======================================
        public static void Delay(Int32 DateTimes)
        {
            DateTime curr = DateTime.Now;
            while (curr.AddMilliseconds(DateTimes) > DateTime.Now)
            {
                Application.DoEvents();
            }
            return;
        }

        //判断文件或文件夹是否有访问权限 
        private static bool CanBeVisited(string obj_path)
        {
            if (File.Exists(obj_path))
            {
                //判断 文件是否有权限访问
                try
                {
                    (new FileInfo(obj_path)).OpenRead();
                    return true;//能够执行上述的语句，则说明文件可以读。文件肯定有权限访问
                }
                catch
                {
                    return false;
                }
            }
            else if (Directory.Exists(obj_path))
            {
                //判断 文件夹是否有权限访问
                try
                {
                    DirectoryInfo di = new DirectoryInfo(obj_path);
                    di.GetFiles();
                    di.GetDirectories();
                    return true;//能够扩展出子文件夹 和 子文件，则说明文件夹可以读。文件夹肯定有权限访问
                }
                catch
                {
                    return false;
                }
            }
            else
                return false;//探测不到，直接视为不可访问
        }

        //获取文件的大小
        private static long GetFileSize(string filepath)
        {
            FileInfo fileInfo = new FileInfo(filepath);
            return fileInfo.Length;
        }

        //获取文件夹的大小(单位Byte)
        private static long GetDirSize(string dirpath)
        {
            DirectoryInfo objInfo = new DirectoryInfo(dirpath);
            FileInfo[] files_info = objInfo.GetFiles();
            DirectoryInfo[] dirs_info = objInfo.GetDirectories();

            long sum = 0;
            foreach (FileInfo fi in files_info)
            {
                if (!CanBeVisited(fi.FullName))//去除无访问权限的文件
                    continue;

                sum += fi.Length;
            }
            foreach (DirectoryInfo di in dirs_info)
            {
                if (!CanBeVisited(di.FullName))//去除无访问权限的文件夹
                    continue;

                string sun_dir_parth = di.FullName;
                long size = GetDirSize(sun_dir_parth);
                sum += size;
            }
            return sum;
        }

        //获取文件夹一共有多少文件
        private static int GetCountOfFileInDir(string dirpath)
        {
            DirectoryInfo objInfo = new DirectoryInfo(dirpath);
            FileInfo[] files_info = objInfo.GetFiles();
            DirectoryInfo[] dirs_info = objInfo.GetDirectories();

            int sum = files_info.Length;
            foreach (DirectoryInfo di in dirs_info)
            {
                if (!CanBeVisited(di.FullName))//去除无访问权限的文件夹
                    continue;

                string sun_dir_parth = di.FullName;
                int count = GetCountOfFileInDir(sun_dir_parth);
                sum += count;
            }
            return sum;
        }
        //======================================功能辅助层End======================================

        //======================================中间层======================================
        //上传文件夹到远程ftp
        //参数1：本地文件夹绝对路径
        //参数2：相对远程根节点（root_remote_dir）下的文件位置 如：/abc/新建文件夹（远程ftp服务器必须已存在）
        //备注：该函数是把指定的本地文件夹，上传到ftp服务器下某个已存在的文件夹之下。
        private void UploadDirLogical(string local_dir_path, string remote_dir_path)
        {
            string[] files = Directory.GetFiles(local_dir_path);
            for (int i = 0; i < files.Length; i++)
            {
                if (!CanBeVisited(files[i]))//去除无访问权限的文件
                    continue;

                string fileName = files[i].Substring(files[i].LastIndexOf('\\') + 1);
                this.UploadFileLogical(files[i], remote_dir_path + "/" + fileName);
            }

            string[] dirs = Directory.GetDirectories(local_dir_path);
            for (int i = 0; i < dirs.Length; i++)
            {
                if (!CanBeVisited(dirs[i]))//去除无访问权限的文件夹
                    continue;

                string dirName = "/" + dirs[i].Substring(dirs[i].LastIndexOf('\\') + 1);
                if (!this.IsRemoteDirExists(dirName.Substring(1), remote_dir_path))
                {
                    this.CreateRemoteDir(dirName.Substring(1), remote_dir_path);
                }
                this.UploadDirLogical(dirs[i], remote_dir_path + dirName);
            }
        }

        //上传文件到远程ftp（支持断点续传 若发现服务器有同名文件且文件大小小于本地文件，则为断点续传。）
        //参数1：本地文件绝对路径 参数2：相对远程根节点（root_remote_dir）下的文件位置 如：/abc/1.txt
        private void UploadFileLogical(string local_file_path, string remote_file_path)
        {
            string uri = root_remote_dir_url + remote_file_path;
            FileInfo fileinfo = new FileInfo(local_file_path);
            long LocalFileSize = fileinfo.Length;//本地文件的大小
            long FileUploadedSize = GetFileUploadedSize(uri);//已上传的文件的大小

            if (FileUploadedSize == LocalFileSize)
            {
                //说明已经上传了这个文件，不需要重新上传
                //更新 之前已上传总量自加
                sem_size_had_uploaded.WaitOne();
                size_had_uploaded += FileUploadedSize;
                sem_size_had_uploaded.Release();

                //更新当前已上传流量总量
                sem_totalUpLoadedSize.WaitOne();
                totalUpLoadedSize += FileUploadedSize;
                sem_totalUpLoadedSize.Release();

                //更新 上传的文件数目
                sem_count_uploaded.WaitOne();
                count_uploaded++;
                sem_count_uploaded.Release();

                return;
            }
            else if (FileUploadedSize != 0 && FileUploadedSize < LocalFileSize)
            {
                //说明需要断点续传
                //更新 之前已上传总量自加
                sem_size_had_uploaded.WaitOne();
                size_had_uploaded += FileUploadedSize;
                sem_size_had_uploaded.Release();

                //更新当前已上传流量总量
                sem_totalUpLoadedSize.WaitOne();
                totalUpLoadedSize += FileUploadedSize;
                sem_totalUpLoadedSize.Release();

                AppendFile(local_file_path, remote_file_path, FileUploadedSize);
            }
            else if (LocalFileSize < FileUploadedSize)
            {
                //ftp是按照目录结构进行上传的.若 本地文件大小 < ftp服务器上的文件大小，则说明本地文件被修改了。要重新上传，视为更新
                UploadFile(local_file_path, remote_file_path);
            }
            else
                UploadFile(local_file_path, remote_file_path);//单纯性文件上传
        }

        //在远程ftp根目录下创建文件夹及其中嵌套的所有文件夹
        //参数1：本地文件夹绝对路径
        //参数2：相对远程根节点（root_remote_dir）下的文件位置 如：/Dir1/Dir2
        public void CreateAllDir(string local_dir_path, string remote_dir_path)
        {
            string[] parts_remote_dir_path = remote_dir_path.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
            //1.求父子目录
            string self_ftp_dir_path = "/" + parts_remote_dir_path[parts_remote_dir_path.Length - 1];
            string parent_ftp_dir_path = parts_remote_dir_path.Length <= 1 ? null : remote_dir_path.Substring(0, remote_dir_path.LastIndexOf('/'));
            //2.创建父文件夹
            if (parent_ftp_dir_path != null)
            {
                //判断ftp上是否存在该父级目录。而要知道是否存在，需先知在ftp的哪个目录下进行判断。答：parent_ftp_dir_name的前一级目录
                string ftp_dir_standard = parent_ftp_dir_path.Substring(0, parent_ftp_dir_path.LastIndexOf('/'));//在该远程ftp目录判定.ftp_dir_standard定义为ftp远程基准目录
                string parent_ftp_dir_name = parent_ftp_dir_path.Substring(parent_ftp_dir_path.LastIndexOf('/') + 1);
                if (false == IsRemoteDirExists(parent_ftp_dir_name, ftp_dir_standard))
                    CreateRemoteDir(parent_ftp_dir_name, ftp_dir_standard);
            }
            //3.创建子文件夹
            string self_ftp_dir_name = self_ftp_dir_path.Remove(0, 1);//文件夹的名称是不带开头的/的。
            if (!IsRemoteDirExists(self_ftp_dir_name, parent_ftp_dir_path))
                CreateRemoteDir(self_ftp_dir_name, parent_ftp_dir_path);
            //4.本地目录继续展开，循环递归调用
            string[] local_exp_dirs = Directory.GetDirectories(local_dir_path);
            foreach (string dir in local_exp_dirs)
            {
                if (!CanBeVisited(dir))//去除无访问权限的文件夹
                    continue;

                string ftp_dir = "/" + dir.Substring(dir.LastIndexOf('\\') + 1);
                this.CreateAllDir(dir, remote_dir_path + ftp_dir);
            }
        }

        //获取输入路径下所有 本地绝对路径 与 ftp路径 对应的关系
        private List<Dictionary<string, string>> GetRelationshipOfLocalPathAndFtpPath(string[] path)
        {
            for (int i = 0; i < path.Length; i++)
            {
                if (File.Exists(path[i]))
                {
                    string dir;
                    if (Directory.GetParent(path[i]).ToString().Equals(Directory.GetDirectoryRoot(path[i])))
                    {
                        dir = "/disk_partion_" + Directory.GetDirectoryRoot(path[i]);
                        dir = dir.Replace(":\\", "");
                    }
                    else
                    {
                        dir = Directory.GetParent(path[i]).ToString();
                        dir = "/" + dir.Substring(dir.LastIndexOf('\\') + 1);
                    }
                    GetFileRemotePath(path[i], dir);
                }
                else if (Directory.Exists(path[i]))
                {
                    string dirDir;
                    if (Directory.GetParent(path[i]) == null)
                    {
                        dirDir = "/disk_partion_" + Directory.GetDirectoryRoot(path[i]);
                        dirDir = dirDir.Replace(":\\", "");
                    }
                    else if (Directory.GetParent(path[i]).ToString() == Directory.GetDirectoryRoot(path[i]))
                    {
                        dirDir = "/disk_partion_" + Directory.GetDirectoryRoot(path[i]);
                        dirDir = dirDir.Replace(":\\", "");
                    }
                    else
                    {
                        dirDir = Directory.GetParent(path[i]).ToString();
                        dirDir = "/" + dirDir.Substring(dirDir.LastIndexOf('\\') + 1);
                    }
                    GetFileInDirRemotePath(path[i], dirDir);
                }
            }
            return LocalFilePath_RemoteFilePaths;
        }

        //获取本地文件绝对路径 对应 远程相对根节点路径（为其他函数提供支持）
        private void GetFileRemotePath(string local_file_path, string ftp_dir_path)
        {
            string fileName = local_file_path.Substring(local_file_path.LastIndexOf('\\') + 1);
            Dictionary<string, string> LocalFilePath_RemoteFilePath = new Dictionary<string, string>();
            LocalFilePath_RemoteFilePath.Add("local_path", local_file_path);
            LocalFilePath_RemoteFilePath.Add("ftp_file_path_cmpt", ftp_dir_path + "/" + fileName);
            LocalFilePath_RemoteFilePaths.Add(LocalFilePath_RemoteFilePath);
        }

        //获取本地文件夹下所有文件绝对路径 对应 远程相对根节点路径（为其他函数提供支持）
        private void GetFileInDirRemotePath(string local_file_path, string ftp_dir_path)
        {
            string dirName = local_file_path.Substring(local_file_path.LastIndexOf('\\') + 1);
            string[] files = Directory.GetFiles(local_file_path);
            for (int i = 0; i < files.Length; i++)
            {
                if (!CanBeVisited(files[i]))//去除无访问权限的文件
                    continue;

                GetFileRemotePath(files[i], ftp_dir_path + "/" + dirName);
            }

            string[] dirs = Directory.GetDirectories(local_file_path);
            for (int i = 0; i < dirs.Length; i++)
            {
                if (!CanBeVisited(dirs[i]))//去除无访问权限的文件夹
                    continue;

                GetFileInDirRemotePath(dirs[i], ftp_dir_path + "/" + dirName);
            }
        }

        //获取ftp服务器上文件的大小。如果文件不存在返回0
        private long GetFileUploadedSize(string ftp_file_path)
        {
            if (IsRemoteFileExists(ftp_file_path))
                return GetRemoteFileSize(ftp_file_path);
            else
                return 0;
        }

        //获取每个 本地路径 对应 ftp相对路径。
        //返回值：List中每个Dictionary包含2个字段parent_node 和 son_node
        //其中son_node还是一个List<Dictionary> 描述了在parent_node下有哪些子文件夹
        //每个Dictionary包含2个字段node 和 base_on。node描述子远程文件夹路径 base_on描述node需要依据哪个本地文件夹创建所有node下的子文件夹 
        private List<Dictionary<string, object>> GetFtpDirPathCmptByLocalPaths(string[] path)
        {
            List<Dictionary<string, object>> FtpDirPathCmptByLocalPath = new List<Dictionary<string, object>>();
            Dictionary<int, string> LocalRoots = new Dictionary<int, string>();//记录输入路径中为盘符的路径
            Dictionary<int, string> Roots = new Dictionary<int, string>(); //记录根节点
            Dictionary<int, string> RemotePath = new Dictionary<int, string>(); //记录对应path的远程节点
            int count_remote_root = 0;

            //该循环记录所有根节点于Roots字典中
            //该循环记录所有文件夹远端地址于RemoPath字典中
            for (int i = 0; i < path.Length; i++)
            {
                if (Directory.Exists(path[i]))
                {
                    string dirPath = path[i];
                    string dirName = "/" + dirPath.Substring(dirPath.LastIndexOf('\\') + 1);
                    string dirDir;
                    if (Directory.GetParent(path[i]) == null)//本地路径为盘符时
                    {
                        dirDir = "/disk_partion_" + Directory.GetDirectoryRoot(path[i]);
                        dirDir = dirDir.Replace(":\\", "");
                        if (!LocalRoots.ContainsValue(dirDir))
                        {
                            LocalRoots.Add(i, dirDir);
                        }
                        continue;
                    }
                    else if (Directory.GetParent(path[i]).ToString() == Directory.GetDirectoryRoot(path[i]))
                    {
                        dirDir = "/disk_partion_" + Directory.GetDirectoryRoot(path[i]);
                        dirDir = dirDir.Replace(":\\", "");
                    }
                    else
                    {
                        dirPath = dirPath.Substring(0, dirPath.Length - dirName.Length);
                        dirDir = "/" + dirPath.Substring(dirPath.LastIndexOf('\\') + 1);
                    }

                    if (!Roots.ContainsValue(dirDir) && !LocalRoots.ContainsValue(dirDir))//去重并保存根节点
                    {
                        Roots.Add(count_remote_root, dirDir);
                        count_remote_root++;
                    }

                    if (!RemotePath.ContainsValue(dirDir + dirName))//去重并保存文件夹结构
                    {
                        RemotePath.Add(i, dirDir + dirName);
                    }
                }
                else if (File.Exists(path[i]))
                {
                    string filePath = path[i];
                    string fileName = "/" + filePath.Substring(filePath.LastIndexOf('\\') + 1);
                    string fileDir;
                    if (Directory.GetParent(path[i]).ToString() == Directory.GetDirectoryRoot(path[i]))
                    {
                        fileDir = "/disk_partion_" + Directory.GetDirectoryRoot(path[i]);
                        fileDir = fileDir.Replace(":\\", "");
                    }
                    else
                    {
                        filePath = filePath.Substring(0, filePath.Length - fileName.Length);
                        fileDir = "/" + filePath.Substring(filePath.LastIndexOf('\\') + 1);
                    }
                    //对于文件只保存其根节点
                    if (!Roots.ContainsValue(fileDir))
                    {
                        Roots.Add(count_remote_root, fileDir);
                        count_remote_root++;
                    }
                }
            }

            for (int i = 0; i < count_remote_root; i++)
            {
                string root;
                Dictionary<string, Object> ObjectUnderRoot = new Dictionary<string, object>();//每个ObjectUnderRoot代表一个远程根节点
                List<Dictionary<string, string>> ListObject = new List<Dictionary<string, string>>();
                //1.将字典Roots中的值取出，并创建对应的字典ObjectUnderRoot与List
                Roots.TryGetValue(i, out root);
                ObjectUnderRoot.Add("parent_node", root);

                //2.对应root值在RemotePath中查找文件夹，对每个结果创建字典保存文件夹与其本地地址，再将字典保存到List
                //查找对应root的文件夹结构
                for (int j = 0; j < path.Length; j++)
                {
                    if (RemotePath.ContainsKey(j))
                    {
                        string RemoteRoot;
                        RemotePath.TryGetValue(j, out RemoteRoot);
                        if (RemoteRoot.StartsWith(root))
                        {
                            Dictionary<string, string> Object = new Dictionary<string, string>();
                            RemoteRoot = RemoteRoot.Substring(root.Length);
                            Object.Add("node", RemoteRoot);
                            Object.Add("base_on", path[j]);
                            ListObject.Add(Object);
                            RemotePath.Remove(j);
                        }
                    }

                }
                //3.将List添加到字典ObjectUnderRoot
                ObjectUnderRoot.Add("son_node", ListObject);
                //4.将字典保存到返回的List中
                FtpDirPathCmptByLocalPath.Add(ObjectUnderRoot);
            }

            //将输入的盘符加入到返回的List中，其中parent_node为""，表示在GIS_TEST下
            if (LocalRoots.Count != 0)
            {
                Dictionary<string, Object> ObjectUnderRemoteRoot = new Dictionary<string, object>();
                List<Dictionary<string, string>> ListObject = new List<Dictionary<string, string>>();
                ObjectUnderRemoteRoot.Add("parent_node", string.Empty);
                for (int i = 0; i < path.Length; i++)
                {
                    if (LocalRoots.ContainsKey(i))
                    {
                        string LocalRoot;
                        LocalRoots.TryGetValue(i, out LocalRoot);
                        Dictionary<string, string> Object = new Dictionary<string, string>();
                        Object.Add("node", LocalRoot);
                        Object.Add("base_on", path[i]);
                        ListObject.Add(Object);
                    }
                }
                ObjectUnderRemoteRoot.Add("son_node", ListObject);
                FtpDirPathCmptByLocalPath.Add(ObjectUnderRemoteRoot);
            }

            return FtpDirPathCmptByLocalPath;
        }

        //======================================中间层End======================================

        //======================================线程实现层======================================
        //参数1：parent_node ftp父节点
        //参数2：son_nodes 父节点下的子节点的集合。字典中 node是子节点名称 base_on表示node需要依据本地哪个路径进行创建所有的子节点
        private void TH_CreateFtpDirStruct(string parent_node, List<Dictionary<string, string>> son_nodes)
        {
            if (!string.Empty.Equals(parent_node))
            {
                if (IsRemoteDirExists(parent_node.Substring(1)) == false)
                    CreateRemoteDir(parent_node.Substring(1));
            }

            foreach (Dictionary<string, string> son_node in son_nodes)
            {
                string node = null, base_on = null;
                son_node.TryGetValue("node", out node);//子节点
                son_node.TryGetValue("base_on", out base_on);//子节点依据的本地路径

                if (false == IsRemoteDirExists(node.Substring(1), parent_node))
                    CreateRemoteDir(node.Substring(1), parent_node);

                CreateAllDir(base_on, parent_node + node);
            }

            sem_thread_working.Release();
        }

        //参数1：local_file_path 本地文件地址
        //参数2：远程ftp地址。指名要把本地文件上传到远程服务器什么位置。
        private void TH_UPloadFile(string local_file_path, string ftp_file_path_cmpt)
        {
            UploadFileLogical(local_file_path, ftp_file_path_cmpt);
            sem_thread_working.Release();
        }
        //======================================线程实现层End======================================

        //======================================接口层【上传相关】======================================
        //参数：在数组中，每个string表示一个本地地址。每个地址有可能是文件或文件夹。其中，文件夹还可能存在子集文件夹
        //备注：建议先新建对象，然后在线程内使用如下函数。线程外可以调用其他接口显示状态信息（如：获取速度）。
        //为了保证稳定性，建议创建1个实体对象只用于一次批量传输。尽量避免一个对象反复多次调用该函数。如有这方面的需求，建议创建多个对象从而多次使用该函数。
        public void UploadObject(string[] path)
        {
            Console.WriteLine("正在统计上传信息...");
            //统计总文件数与总字节数
            for (int i = 0; i < path.Length; i++)
            {
                if (File.Exists(path[i]))
                {
                    count_file++;
                    totalSize += GetFileSize(path[i]);
                }
                else if (Directory.Exists(path[i]))
                {
                    count_file += GetCountOfFileInDir(path[i]);
                    totalSize += GetDirSize(path[i]);
                }
            }
            count_finished = true;

            Console.WriteLine("正在创建ftp拓扑几何加速节点...");
            //步骤1：先创建所有的文件夹结构
            List<Dictionary<string, object>> ftpDirs = GetFtpDirPathCmptByLocalPaths(path);//所有待创建的文件夹
            List<Thread> ths_create_ftp_dir = new List<Thread>();
            //先构建线程的运作 但不启动
            for (int index = 0; index < ftpDirs.Count; index++)
            {
                Dictionary<string, object> ftpDir = ftpDirs[index];
                object obj_parent_node = null, obj_son_nodes = null;
                ftpDir.TryGetValue("parent_node", out obj_parent_node);
                ftpDir.TryGetValue("son_node", out obj_son_nodes);

                //数据类型转换
                string parent_node = (string)obj_parent_node;
                List<Dictionary<string, string>> son_nodes = (List<Dictionary<string, string>>)obj_son_nodes;

                ThreadStart start = delegate { TH_CreateFtpDirStruct(parent_node, son_nodes); };
                Thread th = new Thread(start);
                ths_create_ftp_dir.Add(th);
            }
            //启动每个线程
            foreach (Thread th in ths_create_ftp_dir)
            {
                sem_thread_working.WaitOne();
                th.Start();
            }
            //等待每个线程完成创建文件夹
            foreach (Thread th in ths_create_ftp_dir)
                th.Join();

            GC.Collect();
            Console.WriteLine("加速节点创建完成...");

            //步骤2：上传文件
            List<Dictionary<string, string>> ftpFiles = GetRelationshipOfLocalPathAndFtpPath(path);
            List<Thread> ths_upload_file = new List<Thread>();
            //先构建线程的运作 但不启动
            for (int index = 0; index < ftpFiles.Count; index++)
            {
                //解析
                string local_path = null, ftp_file_path_cmpt = null;
                Dictionary<string, string> ftpFile = ftpFiles[index];
                ftpFile.TryGetValue("local_path", out local_path);
                ftpFile.TryGetValue("ftp_file_path_cmpt", out ftp_file_path_cmpt);

                //把参数放到线程函数中
                ThreadStart start = delegate { TH_UPloadFile(local_path, ftp_file_path_cmpt); };
                Thread th = new Thread(start);
                ths_upload_file.Add(th);
            }

            Console.WriteLine("正在加速上传...");
            startTime = DateTime.Now;//开始计算

            //启动每个线程
            foreach (Thread th in ths_upload_file)
            {
                sem_thread_working.WaitOne();
                th.Start();
            }

            //等待每个线程完成上传文件
            foreach (Thread th in ths_upload_file)
                th.Join();

            GC.Collect();
            all_finished = true;
        }

        //获取当下实时的速度 每秒上传多少字节
        //参数speed_quickly为true时，输出秒传速度（即：若文件已上传，则把文件大小视为本次秒传的效果。视本次上传为秒传，速度叫秒传速度）
        //反之，输出实际流量速度。（即：单位时间内，从网卡实际走了多少流量）
        public long GetSpeedOfUpload(bool speed_quickly = true)
        {
            if (status_paused == true)
            {
                speed = 0;
                return 0;//暂停上传 速度为0
            }

            DateTime curTime = DateTime.Now;//记录当前时间
            TimeSpan timespan = curTime.Subtract(startTime);
            if ((long)timespan.TotalSeconds != 0)
            {
                if (speed_quickly)
                    speed = totalUpLoadedSize / (long)(timespan.TotalSeconds - status_paused_interval.TotalSeconds);//已上传的量 除以 （开始上传时间 到 现在的计时秒数 扣除暂停的持续时间）
                else
                    speed = (totalUpLoadedSize - size_had_uploaded) / (long)(timespan.TotalSeconds - status_paused_interval.TotalSeconds);
            }
            return speed;
        }

        //获取剩余时间 单位：秒
        public long GetLeftTime()
        {
            if (speed == 0)
            {
                return -99999;
            }
            return (totalSize - totalUpLoadedSize) / speed;
        }

        //获取进度 范围[0,1]
        public double GetProgressOfUploaded()
        {
            if (all_finished == true)
                return 1.0;
            else if (count_finished == false)
                return 0.0;//还没统计完，进度为0
            else
                return 1.0 * totalUpLoadedSize / totalSize;
        }

        //获取已经上传文件数
        public int GetCountOfUploadedFiles()
        {
            return count_uploaded;
        }

        //获取 本次上传的文件 总数量。
        //返回值：正常返回正整数，表示总数量；当返回负数时，表示正常统计中尚不知有多少文件数量。
        public int GetCountOfTotalFiles()
        {
            if (count_finished == true)
                return count_file;//统计完成，才正常返回总数量
            else
                return -99999;//还没统计完成
        }

        //获取 本次上传的文件 总流量
        //返回值：正常返回正整数，表示总流量；当返回负数时，表示正常统计中尚不知总流量。
        public long GetTotalSize()
        {
            if (count_finished == true)
                return totalSize;//统计完成，才正常返回总流量
            else
                return -99999;//还没统计完成
        }

        //设置是否暂停上传。true暂停 false继续
        //注意：要么不使用这个函数。如果要用，先得有暂停（true），才能有继续上传（false）的动作行为
        public void PauseUploading(bool pause)
        {
            if (pause == true)
                this.status_paused_start_time = DateTime.Now;
            else
            {
                DateTime status_paused_end_time = DateTime.Now;
                TimeSpan ts = status_paused_end_time.Subtract(status_paused_start_time);
                status_paused_interval.Add(ts);
            }
            this.status_paused = pause;
        }
        //======================================接口层End======================================
    }
}
