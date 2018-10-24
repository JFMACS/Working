package com.util;

import org.apache.poi.hssf.usermodel.*;
import org.apache.poi.hssf.util.HSSFColor;
import org.apache.poi.ss.usermodel.*;
import org.apache.poi.ss.util.CellRangeAddress;
import org.apache.poi.xssf.usermodel.XSSFCellStyle;
import org.apache.poi.xssf.usermodel.XSSFColor;
import org.apache.poi.xssf.usermodel.XSSFFont;
import org.apache.poi.xssf.usermodel.XSSFWorkbook;

import java.io.*;
import java.text.DecimalFormat;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.HashMap;
import java.util.Map;

/**
 * @author JFMACS
 */
public class ExcelToHtml{

    /**
     * @param filePath excel源文件文件的路径
     * @param isEdit 是否支持在页面修改excel内容
     */
    public static String readExcelToHtml(String filePath, boolean isEdit){

        InputStream is = null;
        String htmlExcel = null;
        try {
            File sourceFile = new File(filePath);
            is = new FileInputStream(sourceFile);
            Workbook wb = WorkbookFactory.create(is);
            if (wb instanceof XSSFWorkbook) {
                // 03版excel处理方法
                XSSFWorkbook xWb = (XSSFWorkbook) wb;
                htmlExcel = ExcelToHtml.getExcelInfo(xWb, isEdit);
            }
            else if (wb instanceof HSSFWorkbook) {
                // 07及10版以后的excel处理方法
                HSSFWorkbook hWb = (HSSFWorkbook) wb;
                htmlExcel = ExcelToHtml.getExcelInfo(hWb, isEdit);
            }
        } catch (Exception e) {
            e.printStackTrace();
        } finally {
            try {
                assert is != null;
                is.close();
            } catch (IOException e) {
                e.printStackTrace();
            }
        }
        return htmlExcel;
    }

    private static String getExcelInfo(Workbook wb, boolean isEdit){

        StringBuffer sb = new StringBuffer();
        for(int sheetNum = 0; sheetNum < wb.getNumberOfSheets(); sheetNum++){
            Sheet sheet = wb.getSheetAt(sheetNum);
            int lastRowNum = sheet.getLastRowNum();
            int lastCellNum = 0;
            Map[] map = getRowSpanColSpanMap(sheet);
            if( sheetNum != 0){
                sb.append("<br /><br />");
            }
            sb.append("<table class='table table-bordered' style='border-spacing: inherit;border-collapse: separate;' width='100%'><caption style='font-size: 24px;font-weight: bold;'>").append(sheet.getSheetName()).append("</caption>");
            Row row;
            Cell cell;
            for (int rowNum = 0; rowNum <= lastRowNum; rowNum++) {
                row = sheet.getRow(rowNum);
                if(row != null){
                    if(lastCellNum < row.getLastCellNum()){
                        lastCellNum = row.getLastCellNum();
                    }
                }
            }
            for (int rowNum = 0; rowNum <= lastRowNum; rowNum++) {
                row = sheet.getRow(rowNum);
                if (row == null) {
                    sb.append("<tr style='height:19px'>");
                    for(int colNum = 0; colNum < lastCellNum; colNum++){
                        if (isEdit){
                            sb.append("<td style='border: solid #d0d7e5 1px;'><textarea ")
                                    .append("id='S" + sheetNum + "R" + rowNum + "C" + colNum + "' ")
                                    .append("style='background-color: transparent;border: 0px; height:100%; width:100%; outline: none;resize:none;overflow:hidden;' ")
                                    .append("></textarea></td>");
                        } else {
                            sb.append("<td></td>");
                        }

                    }
                    sb.append("</tr>");
                    continue;
                }
                sb.append("<tr style='height:19px'>");
                for (int colNum = 0; colNum < lastCellNum; colNum++) {
                    cell = row.getCell(colNum);
                    if (cell == null) {
                        // 特殊情况 空白的单元格会返回null
                        if (isEdit){
                            sb.append("<td style='border: solid #d0d7e5 1px;'><textarea ")
                                    .append("id='S" + sheetNum + "R" + rowNum + "C" + colNum + "' ")
                                    .append("style='background-color: transparent;border: 0px; height:100%; width:100%; outline: none; resize:none;overflow:hidden;' ")
                                    .append("></textarea></td>");
                        } else {
                            sb.append("<td></td>");
                        }
                        continue;
                    }
                    // 处理合并单元格排版
                    if (map[0].containsKey(rowNum + "," + colNum)) {
                        String pointString = (String) map[0].get(rowNum + "," + colNum);
                        map[0].remove(rowNum + "," + colNum);
                        int bottomRow = Integer.valueOf(pointString.split(",")[0]);
                        int bottomCol = Integer.valueOf(pointString.split(",")[1]);
                        int rowSpan = bottomRow - rowNum + 1;
                        int colSpan = bottomCol - colNum + 1;
                        sb.append("<td rowspan= '").append(rowSpan).append("' colspan= '").append(colSpan).append("' ");
                    } else if (map[1].containsKey(rowNum + "," + colNum)) {
                        map[1].remove(rowNum + "," + colNum);
                        continue;
                    } else {
                        sb.append("<td ");
                    }
                    // 处理单元格样式
                    boolean underLine = dealExcelStyle(wb, sheet, cell, sb);
                    // 处理单元格值
                    sb.append(">");
                    String stringValue = getCellValue(cell);
                    if (stringValue == null || "".equals(stringValue.trim())) {
                        stringValue = "";
                    } else {
                        // 将ascii码为160的空格转换为html下的空格（ ）
                        stringValue = stringValue.replace(String.valueOf((char) 160)," ");
                    }
                    if (isEdit) {
                        sb.append("<textarea ")
                                .append("style='background-color: transparent;border: 0px;height:100%;width:100%;text-align: inherit;font: inherit;color: inherit;outline: none;resize:none;overflow:hidden;' ")
                                .append("id='S" + sheetNum + "R" + rowNum + "C" + colNum + "' ")
                                .append(">" + stringValue + "</textarea>");

                    } else {
                        stringValue = stringValue.replace("\n", "<br>");
                        sb.append(stringValue + "</td>");
                    }
                }
                sb.append("</tr>");
            }

            sb.append("</table>");
        }
        return sb.toString();
    }

    private static Map[] getRowSpanColSpanMap(Sheet sheet) {

        Map<String, String> map0 = new HashMap<String, String>();
        Map<String, String> map1 = new HashMap<String, String>();
        int mergedNum = sheet.getNumMergedRegions();
        CellRangeAddress range;
        for (int i = 0; i < mergedNum; i++) {
            range = sheet.getMergedRegion(i);
            int topRow = range.getFirstRow();
            int topCol = range.getFirstColumn();
            int bottomRow = range.getLastRow();
            int bottomCol = range.getLastColumn();
            map0.put(topRow + "," + topCol, bottomRow + "," + bottomCol);
            int tempRow = topRow;
            while (tempRow <= bottomRow) {
                int tempCol = topCol;
                while (tempCol <= bottomCol) {
                    map1.put(tempRow + "," + tempCol, "");
                    tempCol++;
                }
                tempRow++;
            }
            map1.remove(topRow + "," + topCol);
        }
        return new Map[]{ map0, map1 };
    }


    /**
     * 获取表格单元格Cell内容
     * @param cell 单元格
     */
    private static String getCellValue(Cell cell) {

        String result;
        switch (cell.getCellTypeEnum()) {
            // 数字类型
            case NUMERIC:
                // 处理日期格式、时间格式
                if (HSSFDateUtil.isCellDateFormatted(cell)) {
                    SimpleDateFormat sdf;
                    if (cell.getCellStyle().getDataFormat() == HSSFDataFormat.getBuiltinFormat("h:mm")) {
                        sdf = new SimpleDateFormat("HH:mm");
                    } else {
                        // 日期
                        sdf = new SimpleDateFormat("yyyy-MM-dd");
                    }
                    Date date = cell.getDateCellValue();
                    result = sdf.format(date);
                } else if (cell.getCellStyle().getDataFormat() == 58) {
                    // 处理自定义日期格式：m月d日(通过判断单元格的格式id解决，id的值是58)
                    SimpleDateFormat sdf = new SimpleDateFormat("yyyy-MM-dd");
                    double value = cell.getNumericCellValue();
                    Date date = DateUtil.getJavaDate(value);
                    result = sdf.format(date);
                } else {
                    double value = cell.getNumericCellValue();
                    CellStyle style = cell.getCellStyle();
                    DecimalFormat format = new DecimalFormat();
                    String temp = style.getDataFormatString();
                    // 单元格设置成常规
                    if ("General".equals(temp)) {
                        format.applyPattern("#");
                    }
                    result = format.format(value);
                }
                break;
            // String类型
            case STRING:
                result = cell.getRichStringCellValue().toString();
                break;
            default:
                result = "";
                break;
        }
        return result;
    }

    /**
     * 处理表格样式
     * @param wb 工作簿
     * @param sheet 表格
     * @param sb 字符流
     */
    private static boolean dealExcelStyle(Workbook wb,Sheet sheet,Cell cell,StringBuffer sb){
        boolean underLine = false;
        CellStyle cellStyle = cell.getCellStyle();
        if (cellStyle != null) {
            // 单元格内容的水平对齐方式
            HorizontalAlignment alignment = cellStyle.getAlignmentEnum();
            sb.append("align='").append(convertAlignToHtml(alignment)).append("' ");
            // 单元格中内容的垂直排列方式
            VerticalAlignment verticalAlignment = cellStyle.getVerticalAlignmentEnum();
            sb.append("valign='").append(convertVerticalAlignToHtml(verticalAlignment)).append("' ");

            if (wb instanceof XSSFWorkbook) {
                XSSFFont xf = ((XSSFCellStyle) cellStyle).getFont();
                sb.append("style='");
                // 文本大小
                sb.append("font-size: ").append(xf.getFontHeight() / 2).append("%;");
                // 文本字体
                sb.append("font-family: ").append(xf.getFontName()).append(";");
                // 文本加粗
                if (xf.getBold()) {
                    sb.append("font-weight: bold;");
                }
                // 文本斜体
                if (xf.getItalic()) {
                    sb.append("font-style: italic;");
                }
                // 文本下划线
                if (xf.getUnderline() != 0) {
                    sb.append("text-decoration-line: underline;");
                    underLine = true;
                }
                // 表格宽度
                int columnWidth = (int) sheet.getColumnWidthInPixels(cell.getColumnIndex());
                sb.append("width:").append(columnWidth).append("px;");
                // 表格排版样式
                String align = convertAlignToHtml(alignment);
                sb.append("text-align:").append(align).append(";");
                // 字体颜色
                XSSFColor xc = xf.getXSSFColor();
                if (xc != null) {
                    sb.append("color:#").append(xc.getARGBHex().substring(2)).append(";");
                }
                // 背景颜色
                XSSFColor bgColor = (XSSFColor) cellStyle.getFillForegroundColorColor();
                if (bgColor != null) {
                    sb.append("background-color:#").append(bgColor.getARGBHex().substring(2)).append(";");
                }
                // 表格边框
                /*
                sb.append(getBorderStyle(0, cellStyle.getBorderTopEnum().getCode(), ((XSSFCellStyle) cellStyle).getTopBorderXSSFColor()));
                sb.append(getBorderStyle(1, cellStyle.getBorderRightEnum().getCode(), ((XSSFCellStyle) cellStyle).getRightBorderXSSFColor()));
                sb.append(getBorderStyle(2, cellStyle.getBorderBottomEnum().getCode(), ((XSSFCellStyle) cellStyle).getBottomBorderXSSFColor()));
                sb.append(getBorderStyle(3, cellStyle.getBorderLeftEnum().getCode(), ((XSSFCellStyle) cellStyle).getLeftBorderXSSFColor()));
                */

            }else if(wb instanceof HSSFWorkbook){

                HSSFFont hf = ((HSSFCellStyle) cellStyle).getFont(wb);
                short fontColor = hf.getColor();
                sb.append("style='");
                // 类HSSFPalette用于求的颜色的国际标准形式
                HSSFPalette palette = ((HSSFWorkbook) wb).getCustomPalette();
                HSSFColor hc = palette.getColor(fontColor);
                // 字体大小
                sb.append("font-size: ").append(hf.getFontHeight() / 2).append("%;");
                // 文本字体
                sb.append("font-family: ").append(hf.getFontName()).append(";");
                // 文本加粗
                if (hf.getBold()) {
                    sb.append("font-weight: bold;");
                }
                // 文本斜体
                if (hf.getItalic()) {
                    sb.append("font-style: italic;");
                }
                // 文本下划线
                if (hf.getUnderline() != 0) {
                    sb.append("text-decoration-line: underline;");
                    underLine = true;
                }
                // 表格宽度
                int columnWidth = (int) sheet.getColumnWidthInPixels(cell.getColumnIndex());
                sb.append("width:").append(columnWidth).append("px;");
                // 表头排版样式
                String align = convertAlignToHtml(alignment);
                sb.append("text-align:").append(align).append(";");
                // 字体颜色
                String fontColorStr = convertToStandardColor(hc);
                if (fontColorStr != null && !"".equals(fontColorStr.trim())) {
                    sb.append("color:").append(fontColorStr).append(";");
                }
                // 背景颜色
                short bgColor = cellStyle.getFillForegroundColor();
                hc = palette.getColor(bgColor);
                String bgColorStr = convertToStandardColor(hc);
                if (bgColorStr != null && !"".equals(bgColorStr.trim())) {
                    sb.append("background-color:").append(bgColorStr).append(";");
                }
                // 表格边框
                /*
                sb.append( getBorderStyle(palette,0, cellStyle.getBorderTopEnum().getCode(),cellStyle.getTopBorderColor()));
                sb.append( getBorderStyle(palette,1, cellStyle.getBorderRightEnum().getCode(),cellStyle.getRightBorderColor()));
                sb.append( getBorderStyle(palette,3, cellStyle.getBorderLeftEnum().getCode(),cellStyle.getLeftBorderColor()));
                sb.append( getBorderStyle(palette,2, cellStyle.getBorderBottomEnum().getCode(),cellStyle.getBottomBorderColor()));
                */
            }

            sb.append("' ");
        }
        return underLine;
    }

    /**
     * 单元格内容的水平对齐方式
     * @param alignment excel单元格水平对齐方式
     */
    private static String convertAlignToHtml(HorizontalAlignment alignment) {
        String align = "center";
        switch (alignment) {
            case LEFT:
            case GENERAL:
                align = "left";
                break;
            case CENTER:
                break;
            case RIGHT:
                align = "right";
                break;
            default:
                break;
        }
        return align;
    }

    /**
     * 单元格中内容的垂直排列方式
     * @param verticalAlignment excel单元格水平垂直方式
     */
    private static String convertVerticalAlignToHtml(VerticalAlignment verticalAlignment) {
        String vAlign = "middle";
        switch (verticalAlignment) {
            case BOTTOM:
                vAlign = "bottom";
                break;
            case CENTER:
                vAlign = "center";
                break;
            case TOP:
                vAlign = "top";
                break;
            default:
                break;
        }
        return vAlign;
    }

    private static String convertToStandardColor(HSSFColor hc) {

        StringBuilder sb = new StringBuilder("");
        if (hc != null) {
            if (HSSFColor.AUTOMATIC.index == hc.getIndex()) {
                return null;
            }
            sb.append("#");
            for (int i = 0; i < hc.getTriplet().length; i++) {
                sb.append(fillWithZero(Integer.toHexString(hc.getTriplet()[i])));
            }
        }
        return sb.toString();
    }

    private static String fillWithZero(String str) {
        if (str != null && str.length() < 2) {
            return "0" + str;
        }
        return str;
    }

    private static String[] borders = {"border-top:", "border-right:", "border-bottom:", "border-left:"};
    private static String[] borderStyles = {"solid ", "solid ", "solid ", "dashed ", "dotted ", "solid ", "double ", "solid ", "solid ", "solid", "solid", "solid", "solid", "solid"};

    private static String getBorderStyle(HSSFPalette palette, int b, short s, short t){
        if(s == 0){
            return borders[b]+borderStyles[s]+"#d0d7e5 1px;";
        }
        String borderColorStr = convertToStandardColor( palette.getColor(t));
        borderColorStr = borderColorStr == null || borderColorStr.length() < 1 ? "#000000" : borderColorStr;
        return borders[b] + borderStyles[s] + borderColorStr + " 1px;";
    }

    private static String getBorderStyle(int b, short s, XSSFColor xc){
        if(s == 0){
            return borders[b] + borderStyles[s] + "#d0d7e5 1px;";
        }
        if(xc != null) {
            String borderColorStr = xc.getARGBHex();
            borderColorStr = borderColorStr == null || borderColorStr.length() < 1 ? "#000000" : borderColorStr.substring(2);
            return borders[b] + borderStyles[s] + borderColorStr + " 1px;";
        }
        return "";
    }
    /**
     * @param content 生成的excel表格标签
     * @param htmlPath 生成的html文件地址
     */
    private static void writeFile(String content, String htmlPath){
        File file = new File(htmlPath);
        StringBuilder sb = new StringBuilder();
        try {
            // 创建文件
            final boolean newFile = file.createNewFile();

            sb.append("<!DOCTYPE html><html><head><meta http-equiv='Content-Type' content='text/html; charset=utf-8'><title>Html Test</title>")
                    .append("\n<link rel='stylesheet' href='css/bootstrap.min.css'>\n</head><body>");
            sb.append("<div>");
            sb.append(content);
            sb.append("</div>");
            sb.append("\n<script src='js/jquery.min.js'></script>\n" +
                      "\n<script src='js/bootstrap.min.js'></script>\n");
            sb.append("</body></html>");

            PrintStream printStream = new PrintStream(new FileOutputStream(file));
            // 将字符串写入文件
            printStream.println(sb.toString());

        } catch (IOException e) {
            e.printStackTrace();
        }

    }
}
