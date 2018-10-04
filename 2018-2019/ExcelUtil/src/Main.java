/**
 * @author JFMACS
 */
public class Main {
    public static void main(String[] args) {

        String filePath =  "C:\\Users\\JFMACS\\Desktop\\测试.xlsx";
        String editPath = "C:\\Users\\JFMACS\\Desktop\\测试2.xlsx";
        ExcelEdit.replaceValues(filePath, editPath);

        String htmlPath = "C:\\Users\\JFMACS\\Desktop\\测试2.html";
        ExcelToHtml.readExcelToHtml(filePath, htmlPath);
    }
}
