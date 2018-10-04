import org.apache.poi.ss.usermodel.*;
import java.io.*;
import java.util.Iterator;

/**
 * @author JFMACS
 */
class ExcelEdit {

    static void replaceValues(String sourceFilePath, String targetFilePath) {
        boolean result = true;
        try {
            InputStream is = new FileInputStream(new File(sourceFilePath));
            Workbook wb = WorkbookFactory.create(is);
            for( int i = 0; i < wb.getNumberOfSheets(); i++){
                Sheet sheet = wb.getSheetAt(i);
                Iterator rows = sheet.rowIterator();
                while(rows.hasNext()){
                    Row row = (Row)rows.next();
                    if(row!=null) {
                        int num = row.getLastCellNum();
                        for(int j = 0; j < num; j++) {
                            Cell cell =  row.getCell(j);
                            if(cell!=null) {
                                cell.setCellType(CellType.STRING);
                            }
                            if(cell == null || cell.getStringCellValue() == null) {
                                continue;
                            }
                            String value = cell.getStringCellValue();
                            if(!"".equals(value)) {
                                if("资源".equals(value)){
                                    cell.setCellValue("资源值");
                                }
                            } else {
                                cell.setCellValue("");
                            }
                        }
                    }
                }
            }
            FileOutputStream fileOut = new FileOutputStream(targetFilePath);
            wb.write(fileOut);
            fileOut.close();

        } catch (Exception e) {
            result = false;
            e.printStackTrace();
        }
    }
}