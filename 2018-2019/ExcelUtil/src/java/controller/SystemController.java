package com.controller;

import com.util.ExcelEdit;
import com.util.ExcelToHtml;
import net.sf.json.JSONObject;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseBody;
import javax.servlet.http.HttpServletRequest;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * @author JFMACS
 */
@Controller
@RequestMapping(value = "/Admin")
public class SystemController {

    @RequestMapping("/index")
    public String index() {
        return "index";
    }

    @RequestMapping("/getExcel")
    public
    @ResponseBody
    Map<String, Object>
    getExcel(HttpServletRequest request) {
        Map<String, Object> map = new HashMap<String, Object>();
        String html = ExcelToHtml.readExcelToHtml("C:\\Users\\JFMACS\\Desktop\\test.xlsx", true);
        map.put("success", true);
        map.put("excel", html);
        return map;
    }

    @RequestMapping("/editValue")
    @ResponseBody
    Map<String, Object>
    editValue(HttpServletRequest request) {
        Map<String, Object> resultMap = new HashMap<String, Object>();
        String value = request.getParameter("value");
        // 后台处理
        JSONObject jsonObject = JSONObject.fromObject(value);
        Map map = (Map)jsonObject;
        boolean result = ExcelEdit.editValues("C:\\Users\\JFMACS\\Desktop\\test.xlsx", map);
        resultMap.put("success", result);
        return resultMap;
    }
}
