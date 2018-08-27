# UploadFile-FTP
### 桌面应用使用FTP进行文件上传，支持文件及文件夹上传、断点续传、进度计算、速度计算、多线程上传等功能
#### 作者：JFMACS

### 输入
```c#
void UploadObject(string[] path) //本地文件的绝对地址
```
### 输出
```c#
long GetSpeedOfUpload() //获取文件上传的速度
```
```c#
long GetLeftTime() //获取文件上传剩余的时间
```
```c#
double GetProgressOfUploaded() //获取进度
```
