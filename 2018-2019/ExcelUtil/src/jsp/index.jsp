<%--
  Created by IntelliJ IDEA.
  User: JFMACS
  Date: 2018/10/5
  Time: 0:42
  To change this template use File | Settings | File Templates.
--%>
<%@ page language="java" contentType="text/html; charset=utf-8" pageEncoding="utf-8"%>
<%
    String path = request.getContextPath();
    String basePath = request.getScheme() + "://"
            + request.getServerName() + ":" + request.getServerPort()
            + path + "/";
%>
<!DOCTYPE html>
<html>

<head>
    <meta http-equiv='Content-Type' content='text/html; charset=utf-8'>
    <title>Title</title>
    <link rel='stylesheet' href='<%=basePath%>css/bootstrap.min.css'>
    <style>
        .table-bordered tbody tr td{
            vertical-align: middle;
        }
        .test{
            border: none;
        }
        .scrollbar{
            width: 30px;
            height: 10px;
            margin: 0 auto;
        }
        .test-1::-webkit-scrollbar {/*滚动条整体样式*/
            width: 10px;     /*高宽分别对应横竖滚动条的尺寸*/
            height: 1px;
        }
        .test-1::-webkit-scrollbar-thumb {/*滚动条里面小方块*/
            border-radius: 10px;
            -webkit-box-shadow: inset 0 0 5px rgba(0,0,0,0.2);
            background: #535353;
        }
        .test-1::-webkit-scrollbar-track {/*滚动条里面轨道*/
            -webkit-box-shadow: inset 0 0 5px rgba(0,0,0,0.2);
            border-radius: 10px;
            background: white;
        }
    </style>
</head>
<body style="background-color: linen;">
<div style="width: 90%;margin: auto;height: 80px;">
    <button type="button" onclick="save()" class="btn btn-danger" style="float: right; margin-top: 35px; height: 30px; width: 75px; font-family: 等线, 黑体; border-radius: 5px;">保存</button>
    <button type="button" class="btn btn-success" data-toggle="modal" data-target="#myModal" style="float: right; margin-top: 35px; margin-right: 20px; height: 30px; width: 75px; font-family: 等线, 黑体; border-radius: 5px;">编辑</button>
</div>
<div id="content" class="test test-1" style="width: 90%; background-color: white; overflow-y: scroll; margin: auto; height: 850px; box-shadow: 0 0.5rem 1.25rem 0 rgba(0,0,0,0.28); z-index: 200; border-radius: 10px; ">
    <div class="scrollbar"></div>
</div>
<!-- 模态框（Modal） -->
<div class="modal fade" id="myModal" tabindex="-1" role="dialog" aria-labelledby="myModalLabel" aria-hidden="true">
    <div class="modal-dialog">
        <div class="modal-content">
            <div class="modal-header">
                <button type="button" class="close" data-dismiss="modal" aria-hidden="true">&times;</button>
                <h4 class="modal-title" id="myModalLabel">编辑内容</h4>
            </div>
            <div class="input-group" style="width: 70%; margin-left: 15%; margin-top: 3%; margin-bottom: 3%;">
                <span class="input-group-addon" id="basic-addon1">add</span>
                <input type="text" id="addContent" class="form-control" placeholder="Username" aria-describedby="basic-addon1">
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-default" data-dismiss="modal">取消</button>
                <button type="button" class="btn btn-primary" onclick="addContent()">确认</button>
            </div>
        </div><!-- /.modal-content -->
    </div><!-- /.modal -->
</div>
<script src='<%=basePath%>js/jquery.min.js'></script>
<script src='<%=basePath%>js/bootstrap.min.js'></script>
<script>

    let set = new Set();
    var selectedCell = "";
    var selectedStart = "";
    var selectedEnd = "";

    $(function () {
        $.ajax({
            url: '<%=basePath %>Admin/getExcel',
            type: 'POST',
            data: {
                "type":1
            },
            dataType: "json",
            async: false,
            success: function (data) {
                if(data.success)
                {
                    $("#content").append(data.excel);
                }
            },
            error: function () {

            }
        });
        
        $.each($("textarea"), function(i, n){
            if(n.innerHTML == ""){
                $(n).parent().css('width','30px')
            }
            $(n).css("height", n.scrollHeight + "px");
        })
        // 保存编辑过的单元格id
        $("textarea").change(function () {
            set.add(this.id);
        })
        // 临时变量：当前编辑的单元格id
        $("textarea").focus(function () {
            selectedCell = this.id;
        })
        // 临时变量：保存光标位置
        $("textarea").blur(function () {
            selectedStart = this.selectionStart;
            selectedEnd =  this.selectionEnd;
        })
    })

    function addContent(){
        var value = $("#" + selectedCell).val();
        $("#" + selectedCell).val(value.substring(0, selectedStart) + $("#addContent").val() + value.substring(selectedEnd, value.length));
        $("#addContent").val("");
        $("#myModal").modal('hide');
    }
    // 将编辑后的单元格id及其值保存为json
    function save(){
        var value = "";
        var key = "";
        var map = {};
        var str = "";
        for (let cell of set) {
            key = cell;
            value = $("#" + key).val();
            map[key] = value;
        }
        // 主要：map转json字符串放入data
        str = JSON.stringify(map);
        $.ajax({
            url: '<%=basePath %>Admin/editValue',
            type: 'POST',
            data: {
                "value":str,
            },
            dataType: "json",
            async: true,
            success: function (data) {
                if(data.success)
                {
                    window.location.reload();
                } else {
                    console.log(data.message);
                }
            },
            error: function () {

            }
        });
    }
</script>
</body>
</html>
