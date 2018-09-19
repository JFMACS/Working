public void getReportExcel() {
	try {
		ReportCondition condition = new ReportCondition();
		List<Report> reportList = databaseService.getReportList(condition);
		// 创建“test.xls”excel文件
		WritableWorkbook workBook = Workbook.createWorkbook(new File("test.xls"));
		// 在excel中创建“test”sheet
		WritableSheet writeSheet = workBook.createSheet("test", 0);
		if(workBook != null){
			writeSheet.addCell(new Label(0, 0, "报表名称"));
			writeSheet.addCell(new Label(1, 0, "备注"));
			writeSheet.addCell(new Label(2, 0, "状态"));
			writeSheet.addCell(new Label(3, 0, "创建日期"));
			writeSheet.addCell(new Label(4, 0, "修改时间"));
			writeSheet.addCell(new Label(5, 0, "部门"));
			
			for( int i = 0; i < reportList.size(); i++){
				Report report = reportList.get(i);
				writeSheet.addCell(new Label(0, i + 1, report.getReportName()));
				writeSheet.addCell(new Label(1, i + 1, report.getRemark()));
				writeSheet.addCell(new Label(2, i + 1, ""+report.getState()));
				writeSheet.addCell(new Label(3, i + 1, report.getCreatedDate().toString()));
				writeSheet.addCell(new Label(4, i + 1, report.getLastModified().toString()));
				writeSheet.addCell(new Label(5, i + 1, systemService.getDeptNameByReportId(report.getId())));
			}
			workBook.write();
			workBook.close();
		}
	} catch (Exception e) {
		e.printStackTrace();
	}
}