package com.util;

import java.io.*;
import com.aspose.cells.*; 

public class ExcelToPDF {
	
	public static boolean getLicense() {
        boolean result = false;
        try {
            InputStream is = Excel2PDF.class.getClassLoader().getResourceAsStream(SavePathUtil.ASPOSE_LICENSE_PATH + "license.xml"); 
            License aposeLic = new License();
            aposeLic.setLicense(is);
            result = true;
        } catch (Exception e) {               
            e.printStackTrace();
        }
        return result;
    }
	
	public static boolean convert(String Address,String saveAddr) {
		if (!getLicense()) {
            return false;
        }
        try {
        	System.out.println(Address);
            File pdfFile = new File(saveAddr);
            Workbook wb = new Workbook(Address);
            PdfSaveOptions pdfSaveOptions=new PdfSaveOptions();
            pdfSaveOptions.setCalculateFormula(true);
            pdfSaveOptions.setAllColumnsInOnePagePerSheet(true);
            FileOutputStream fileOS = new FileOutputStream(pdfFile);
            wb.save(fileOS, pdfSaveOptions);  
            fileOS.close();  
            return true;
        } catch (Exception e) {
            e.printStackTrace();
            return false;
        }
	}
	
}
