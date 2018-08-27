	@RequestMapping(value = "/getVerify.php")
	public void getVerify(HttpServletRequest request, HttpServletResponse response) {
		response.setContentType("image/jpeg");// 设置相应类型,告诉浏览器输出的内容为图片
		response.setHeader("Pragma", "No-cache");// 设置响应头信息，告诉浏览器不要缓存此内容
		response.setHeader("Cache-Control", "no-cache");
		response.setDateHeader("Expire", 0);
		RandomValidateCode randomValidateCode = new RandomValidateCode();
		try {
			randomValidateCode.getRandcode(request, response);// 输出验证码图片方法
		} catch (Exception e) {
			e.printStackTrace();
		}
	}

	@RequestMapping("/userLogin")
	@ResponseBody
	public UpdateSystemUserResponse userLogin(HttpServletRequest request) {
		UpdateSystemUserResponse response = new UpdateSystemUserResponse();
		try {
			MessageDigest md5 = MessageDigest.getInstance("MD5");
			BASE64Encoder base64en = new BASE64Encoder();
			String userName = request.getParameter("userName");
			String userPassword = request.getParameter("userPassword");
			SystemUser systemUser = systemService.getSystemUserByUserName(userName);
			//获取输入验证码
			String validate = request.getParameter("validate");
			validate = validate.toUpperCase();

			if (systemUser != null
					&& base64en.encode(md5.digest(userPassword.getBytes("utf-8"))).equals(systemUser.getUserPassword())
					&& systemUser.getIsDeleted() == 0) {
				//验证验证码是否正确
				String random = (String) request.getSession().getAttribute("RANDOMVALIDATECODEKEY");
				if (!validate.equals(random)) {
					response.setMessage("验证码输入有误");
					response.setSuccess(false);
					return response;
				}

				systemUser.setLastModified(new Date());
				systemService.updateSystemUser(systemUser);
				request.getSession().setAttribute("loginUserName", userName);
				request.getSession().setAttribute("loginUserId", systemUser.getId());
				request.getSession().setAttribute("systemUser", systemUser);
				response.setSuccess(true);
			} else {
				response.setMessage("用户名或密码有误");
				response.setSuccess(false);
			}
		} catch (Exception e) {
			response.setSuccess(false);
			e.printStackTrace();
		}
		return response;
	}