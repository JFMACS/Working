package com.util;

import org.apache.poi.ss.usermodel.*;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.util.*;

/**
 * @author JFMACS
 */
public class ExcelEdit {

    public static void replaceValues(String sourceFilePath, String targetFilePath) {
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
                            if(cell != null) {
                                if(cell.getCellTypeEnum() != CellType.STRING){
                                    continue;
                                }
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

    public static boolean editValues(String sourceFilePath, Map map){
        boolean result = true;
        try {
            InputStream is = new FileInputStream(new File(sourceFilePath));
            Workbook wb = WorkbookFactory.create(is);
            for(int i = 0; i < wb.getNumberOfSheets(); i++){
                Sheet sheet = wb.getSheetAt(i);
                int lastCellNum = 0;
                int lastRowNum = sheet.getLastRowNum();
                for (int rowNum = 0; rowNum <= lastRowNum; rowNum++) {
                    Row row = sheet.getRow(rowNum);
                    if(row != null){
                        if(lastCellNum < row.getLastCellNum()){
                            lastCellNum = row.getLastCellNum();
                        }
                    }
                }
                for(int j = 0; j <= lastRowNum; j++){
                    Row row = sheet.getRow(j);
                    if(row == null) {
                        row = sheet.createRow(j);
                    }
                    for(int k = 0; k < lastCellNum; k++) {
                        Cell cell =  row.getCell(k);
                        String id = "S" + i + "R" + j + "C" + k;
                        if(map.containsKey(id)){
                            if(cell == null){
                                cell = row.createCell(k);
                            }
                            cell.setCellValue(map.get(id).toString());
                        }
                    }
                }
            }
            is.close();
            FileOutputStream fileOut = new FileOutputStream(sourceFilePath);
            wb.write(fileOut);
            fileOut.close();
        } catch (Exception e) {
            result = false;
            e.printStackTrace();
        }
        return result;
    }
}