import org.apache.poi.xssf.streaming.SXSSFRow;
import org.apache.poi.xssf.streaming.SXSSFSheet;
import org.apache.poi.xssf.streaming.SXSSFWorkbook;
import org.apache.poi.xssf.usermodel.XSSFWorkbook;

import java.io.BufferedOutputStream;
import java.io.FileOutputStream;
import java.io.IOException;

/**
 * Solution class
 *
 * @author JFMACS
 * @date 2018/08/10
 */
public class Solution {

    // 3.8版本以上的poi大数据量的excel文件导出
    public static void main(String[] args) {
        long startTime = System.currentTimeMillis();
        String filePath = "D:\\test.xlsx";
        SXSSFWorkbook sxssfWorkbook;
        BufferedOutputStream outputStream = null;
        XSSFWorkbook workbook;
        try {
            workbook = new XSSFWorkbook();
            workbook.createSheet("测试Sheet");
            // 这样表示SXSSFWorkbook只会保留100条数据在内存中，其它的数据都会写到磁盘里，这样的话占用的内存就会很少
            sxssfWorkbook = new SXSSFWorkbook(workbook,100);
            // 获取第一个Sheet页
            SXSSFSheet sheet = sxssfWorkbook.getSheetAt(0);
            for (int i = 0; i < 50; i++) {
                for (int z = 0; z < 10000; z++) {
                    SXSSFRow row = sheet.createRow(i * 10000 + z);
                    for (int j = 0; j < 10; j++) {
                        row.createCell(j).setCellValue("你好："+j);
                    }
                }
            }
            outputStream = new BufferedOutputStream(new FileOutputStream(filePath));
            sxssfWorkbook.write(outputStream);
            outputStream.flush();
            // 释放workbook所占用的所有windows资源
            sxssfWorkbook.dispose();
        } catch (IOException e) {
            e.printStackTrace();
        } finally {
            if (outputStream != null) {
                try {
                    outputStream.close();
                } catch (IOException e) {
                    e.printStackTrace();
                }
            }
        }
        long endTime = System.currentTimeMillis();
        System.out.println(endTime-startTime);
    }
}
