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
  
### 将excel转换为html
#### 支持将（filePath）表格转换成（htmlPath）网页，可保留表格名称、表格排版、表格样式、文字样式、居中、部分边框
```java
// 调用
ExcelToHtml.readExcelToHtml(filePath, htmlPath)
```
注：若将转换结果以字符串形式传到页面，需注意“:” “"” “\n”等的转换
