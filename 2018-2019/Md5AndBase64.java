package test;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.security.MessageDigest;
import sun.misc.BASE64Encoder;

public class main {
	public static void main(String[] args) {
		try {
			BufferedReader br = new BufferedReader(new InputStreamReader(System.in));
			System.out.println("请输入密码");
			String code = br.readLine();
			MessageDigest md5 = MessageDigest.getInstance("MD5");
			BASE64Encoder base64en = new BASE64Encoder();
			System.out.println("加密后的密文：" + base64en.encode(md5.digest(code.getBytes("utf-8"))));
		} catch (Exception e) {
			e.printStackTrace();
		}
	}
}
