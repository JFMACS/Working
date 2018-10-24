# ExcelUtil
### 使用POI对excel文件进行内容编辑、html转换等操作，兼容.xls与.xlsx文件
#### 作者：JFMACS  
  
  
### 编辑表格中的值
#### 支持将（filePath）表格内的 匹配值 转换成 指定值，再将文件输出到（editPath），并将保持原文件的其他内容、属性
```java
// 调用
ExcelEdit.replaceValues(filePath, editPath)
// 核心代码
String value = cell.getStringCellValue();
if("replace".equals(value)){
	cell.setCellValue("replaced");
}
```
  
### 在线编辑excel中的值
#### 将（filePath）表格转换成（htmlPath）网页，将单元格转换成可编辑的textarea，前台编辑结束再将修改后的值以map的形式传到后台进行修改
```java
// 调用
ExcelToHtml.readExcelToHtml(filePath, htmlPath)
ExcelEdit.editValues(String sourceFilePath, Map map)
```
