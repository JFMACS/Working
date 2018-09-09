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
### 基本逻辑
#### 第一步 在FTP上创建所有文件夹
**1. 函数 CreateAllDir(local_dir_path,ftp_dir_path_cmpt)**  
  参数 1：本地文件夹绝对路径  
  参数 2：远程文件夹的相对路径  
  效果：ftp 服务器在相对 root 下，创建 A 文件夹，以及其子文件夹。
  注意：要判断文件夹是否已存在，不存在才创建  
  例：  
  local_dir_path：XXX\Desktop\A 文件夹  
  ftp_dir_path_cmpt：/Desktop/A  
  效果：ftp 的 root 目录下，先创建 Desktop 文件夹，然后再 Desktop 中创建了 A 文件夹以及 A 文件夹中的子文件夹  
  具体实现步骤：  
  设 2 层目录，前一层叫父层，后层叫当前层  
  <1>判断父层（/Desktop 文件夹）存在吗？—— 不存在，创建父层  
  <2>判断当前层（/A 文件夹）存在吗？—— 不存在，创建当前层。  
  <3>本地文件夹地址（XXX\Desktop\A 文件夹） —— 目录展开，得到子目录 1 层。设每个子目录叫：de，每个 de 做第四步：  
  <4>得到 de 的绝对路径 ABS;根据的 de 名称构建远程相对路径 FS；  
  调用 CreateAllDir 把 ABS 当做参数 1; FS 当做参数 2。  
  
**2. 函数 GetFtpDirPathCmptByLocalPaths(string[] path)**  
  参数：每个 string 都是一个路径。每个路径可能是文件，也可能是文件夹  
  输出：每个 本地路径 对应 ftp 相对路径。  
  返回类型:List<Dic>  
  Key1:parent_node Value1:XXXXX -->Str  
  Key2:sun_node Value2:XXXXX -->List<Dic>,Dic{node,base_on}  
  例：  
  path 数组内容是：  
  XXXX\Desktop\A 文件夹  
  XXXX\Desktop\A 文件夹\B 文件夹（当然，这是不可能的。但也考虑在内。）  
  XXXX\Desktop\test1.txt  
  XXXX\Desktop\test2.txt  
  C:\test2.txt  
	
**（1）. 函数执行，先得到 List<Dic> , Dic{local_dir_path,ftp_dir_path_cmpt}:**  
  XXXX\Desktop\A 文件夹 --->  /Desktop/A 文件夹  
  XXXX\Desktop\A 文件夹\B 文件夹--->  /A 文件夹/B 文件夹  
  XXXX\Desktop\C 文件夹 --->  /Desktop/C 文件夹  
  XXXX\Desktop\test1.txt         --->  /Desktop  
  XXXX\Desktop\test2.txt         --->  /Desktop  
  C:\test2.txt --->  /disk_partion_c  
	
**（2）. 再得到 3 个 Dic**  
第 1 个字典 Dictionary1:  
  Key Value  
  parent_node    “/Desktop”  
  sun_node       List<Dictionary>  
  List<Dictionary>有 2 个,分别叫 a,b：  
  Dictionary_a  
  Key Value  
  node   “/A 文件夹”  
  base_on “XXXX\Desktop\A 文件夹”  
  Dictionary_b  
  Key Value  
  node   “/C 文件夹”  
  base_on “XXXX\Desktop\C 文件夹”  
第 2 个字典 Dictionary2:  
  Key Value  
  parent_node    “/A 文件夹”  
  sun_node       List<Dictionary>  
  此时 List<Dictionary>，只有 1 个元素，取名叫 a 字典  
  Dictionary_a  
  Key Value  
  node   “/B 文件夹”  
  base_on “XXXX\Desktop\A 文件夹\B 文件夹”  
  （理解：base_on 意思是基于什么来创建子文件夹）  
第 3 个字典 Dictionary3:  
  Key Value      
  parent_node “disk_partion_c”  
  sun_node null 或 元素个数为 0  
  最后把字典 1 和字典 2 合成 List  
	
**（3）. 开几个线程？**  
  3.1 函数 GetFtpDirPathCmptByLocalPaths 返回 List<Dic>了有多少个？设定有x个；  
  3.2 设定最大线程数 max_count_thread_upload(线程锁)  
  3.3 x < max_count_thread_upload ? 开 x 个：开 max_count_thread_upload 个  
								 
**（4）. 每个线程怎么工作？**  
  <a> 逐个从 List 取出 Dic ， 创 建 线 程 ， 每 个 线 程 负 责 一 个  Dic{parent_node,sun_node}  
  <b> Dic 取 parent_node，并调用 CreateRemoteDir 创建 1 层  
  <c> Dic 取 sun_node 得到 List<Dictionary> sun_nodes  
  <d> sun_nodes 取 node 依据 base_on 调用 CreateAllDir 创建所有文件夹  

#### 第二步 上传文件
**1. 函数 C GetFileRelationshipOfLocalPathAndFtpPathCmpt：**  
  输入：string[] path  
  输出：每个 本地文件绝对路径 对应 远程相对根节点路径。类型：List<Dic>  
  Key1:local_path Value1:XXXXX  
  Key2:ftp_file_path_cmpt Value2:XXXX  
  如：XXX\Desktop\test.txt  -->  /Desktop/test.txt  
	
**2. 确定开多少个线程？**  
  函数 C 得到的返回值 localpath - remotepath,有多少个？假设有 x 个，结果放在了 ftpFiles 中  
  2.1 设定最大线程数 max_count_thread_upload  
  2.2 线程锁有 1 个，用于读取 ftpFiles 资源。每个线程读取 1 个进行上传。传完再读 1 个。  
  
#### 注：不论文件夹还是文件，ftp 服务器是不允许存在#符号，故而统一换成了＃符号  
