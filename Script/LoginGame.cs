using Godot;                                         // อิมพอร์ต API ของ Godot (Control, LineEdit, Label, Button, ResourceLoader, etc.)
using System;                                        // ใช้ฟีเจอร์พื้นฐานของ .NET (DateTime, String, ฯลฯ)
using System.Collections.Generic;                    // ใช้คอลเลกชันมาตรฐาน เช่น List<T>

public partial class LoginGame : Control             // คลาส LoginGame เป็นคอนโทรล (หน้าล็อกอิน)
{
	// ตั้งผ่าน Inspector ได้ (เผื่อ path ในฉากเปลี่ยน)
	[Export] private NodePath NameInputPath;         // path ของ LineEdit ช่องชื่อผู้เล่น
	[Export] private NodePath PasswordInputPath;     // path ของ LineEdit ช่องรหัสผ่าน
	[Export] private NodePath ErrorLabelPath;        // path ของ Label แสดงข้อความผิดพลาด
	[Export] private NodePath SubmitButtonPath;      // path ของ Button ปุ่มยืนยัน/ไปต่อ

	private LineEdit _nameInput;                     // ตัวแปรอ้างอิงช่องกรอกชื่อ
	private LineEdit _passwordInput;                 // ตัวแปรอ้างอิงช่องกรอกรหัสผ่าน
	private Label _errorLabel;                       // ตัวแปรอ้างอิงป้ายข้อความ error
	private Button _submitButton;                    // ตัวแปรอ้างอิงปุ่มยืนยัน

	// อักขระพิเศษที่อนุญาตในรหัสผ่าน
	private const string AllowedSpecials = @"!@#$%^&*()-_=+[]{};:'"",.<>/?\|`~"; // ชุดตัวพิเศษที่อนุญาต (verbatim string)

	public override void _Ready()                    // เรียกเมื่อโหนดพร้อมใช้งาน
	{
		_nameInput     = GetNodeOrNull<LineEdit>(NameInputPath)                 // ลองดึงตาม path จาก Inspector
					  ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/NameInput"); // ถ้าไม่ได้ ใช้ path สำรองในซีน

		_passwordInput = GetNodeOrNull<LineEdit>(PasswordInputPath)             // ดึงช่องรหัสผ่าน
					  ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/PasswordInput"); // path สำรอง

		_errorLabel    = GetNodeOrNull<Label>(ErrorLabelPath)                   // ดึง Label สำหรับ error
					  ?? GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");       // path สำรอง

		_submitButton  = GetNodeOrNull<Button>(SubmitButtonPath)                // ดึงปุ่ม Submit
					  ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/SubmitButton");    // path สำรอง

		if (_nameInput == null)     GD.PushError("NameInput not found.");       // แจ้ง error ถ้าไม่พบ node
		if (_passwordInput == null) GD.PushError("PasswordInput not found.");
		if (_errorLabel == null)    GD.PushError("ErrorLabel not found.");
		if (_submitButton == null)  GD.PushError("SubmitButton not found.");

		if (_errorLabel != null) _errorLabel.Visible = false;                   // ซ่อน error ตอนเริ่ม
		if (_submitButton != null) _submitButton.Pressed += OnSubmit;           // ผูกอีเวนต์กดปุ่ม → OnSubmit()

		if (_passwordInput != null) _passwordInput.Secret = true;               // ตั้งให้ช่องรหัสซ่อนตัวอักษร (●●●)
	}

	// -------------------- MAIN FLOW --------------------
	private void OnSubmit()                                                     // เรียกเมื่อผู้ใช้กดปุ่ม Submit
	{
		if (_nameInput == null || _passwordInput == null || _errorLabel == null) // ป้องกันกรณี node ไม่พร้อม
		{
			GD.PushError("UI nodes missing, cannot submit.");                   // log แล้วหยุด
			return;
		}

		// 1) ตรวจชื่อ
		string name = (_nameInput.Text ?? "").Trim();                           // ดึงข้อความชื่อ (กัน null) และตัดช่องว่างหัวท้าย
		if (name.Length == 0)                                                   // ถ้าไม่กรอก
		{
			ShowError("กรุณากรอกชื่ออย่างน้อย 1 ตัวอักษร");                  // แจ้งผู้ใช้
			return;                                                             // จบ flow
		}
		string badName = GetInvalidNameChars(name);                             // ตรวจหาตัวอักษรที่ไม่อนุญาตในชื่อ
		if (!string.IsNullOrEmpty(badName))                                     // ถ้าพบ
		{
			ShowError($"ไม่สามารถใช้ชื่อนี้ได้ เพราะมีตัวอักษรพิเศษ: {badName}"); // โชว์ตัวที่ผิด
			return;
		}

		// 2) ตรวจรหัสผ่าน
		string password = (_passwordInput.Text ?? "").Trim();                   // ดึงรหัสผ่าน (กัน null) และ Trim
		string pwdErr = ValidatePassword(password);                              // ตรวจรูปแบบ/ความยาว/ชุดตัวอักษร
		if (pwdErr != null)                                                     // ถ้าไม่ผ่าน
		{
			ShowError(pwdErr);                                                  // แสดงข้อความผิดพลาด
			return;
		}

		// 3) อ้างอิงระบบผู้เล่น (Autoload: PlayerLogin)
		var saver = PlayerLogin.Instance                                        // ใช้ซิงเกิลตันถ้ามี
				 ?? GetNodeOrNull<PlayerLogin>("/root/PlayerLogin");           // หรือดึงจาก Autoload path
		if (saver == null)                                                      // ถ้าไม่เจอระบบ
		{
			ShowError("ระบบ PlayerLogin (Autoload) ไม่ได้เปิดใช้งาน");       // บอกผู้ใช้
			GD.PushError("Missing /root/PlayerLogin. Add Autoload.");          // log สำหรับ dev
			return;
		}

		// 4) ลองล็อกอินผู้ใช้เดิมก่อน
		if (saver.LoginExisting(name, password))                                // ถ้าพบผู้ใช้เดิมและรหัสถูก
		{
			HideError();                                                        // ซ่อน error
			GD.Print($"✅ Login OK: {saver.CurrentUser?.PlayerName}");          // log ดีบัก
			GoNext();                                                           // ไปซีนถัดไป
			return;                                                             // จบ flow
		}

		// 5) ถ้าไม่พบ ก็สมัครใหม่
		if (!saver.SavePlayer(name, password))                                  // สมัครใหม่ (ถ้า false = ชื่อซ้ำ/กติกาไม่ผ่าน)
		{
			ShowError("ชื่อนี้ถูกใช้แล้ว กรุณาลองชื่ออื่น");                  // แจ้งผู้ใช้
			return;
		}

		// SavePlayer() ของคุณจะตั้ง CurrentUser ให้แล้ว
		// กันพลาด: ถ้ายังไม่มี CurrentUser ให้ตั้งเพิ่ม
		if (saver.CurrentUser == null)                                          // safety-net สำหรับโค้ด SavePlayer ภายใน
		{
			saver.CurrentUser = new PlayerLogin.SaveData                        // ตั้งผู้ใช้ปัจจุบันด้วยตัวเอง
			{
				PlayerName = name,                                              // ชื่อผู้เล่น
				Password   = password,                                          // รหัสผ่าน (หมายเหตุ: โปรดพิจารณาเก็บแบบแฮชในโปรดักชัน)
				CreatedAt  = DateTime.UtcNow.ToString("o"),                     // เวลา UTC รูปแบบ ISO 8601
			};
		}

		HideError();                                                            // เคลียร์ error
		GD.Print($"🎉 Registered & Login: {saver.CurrentUser?.PlayerName}");    // log ดีบัก
		GoNext();                                                               // ไปซีนถัดไป
	}

	// -------------------- Scene Transition --------------------
	private void GoNext()                                                       // ฟังก์ชันเปลี่ยนซีนเมื่อเข้าสู่ระบบสำเร็จ
	{
		HideError();                                                            // ซ่อน error เผื่อค้าง
		GD.Print($"Login success → {(_nameInput?.Text ?? "")}");                // พิมพ์ชื่อที่ล็อกอินสำเร็จ
		const string nextScene = "res://SceneStartandHigh/StartGame.tscn";      // ไฟล์ซีนถัดไป
		if (ResourceLoader.Exists(nextScene))                                   // ถ้ามีไฟล์อยู่จริง
			GetTree().ChangeSceneToFile(nextScene);                             // เปลี่ยนซีน
		else                                                                    // ถ้าไม่มีไฟล์
			ShowError("ไม่พบซีนถัดไป: " + nextScene);                         // แจ้งผู้ใช้
	}

	// -------------------- Helpers --------------------
	// อนุญาต A–Z a–z 0–9 และอักษรไทย
	private string GetInvalidNameChars(string s)                                // คืนรายชื่อตัวอักษรที่ "ไม่อนุญาต" ในชื่อ (คั่นด้วย comma)
	{
		var list = new List<char>();                                            // ลิสต์เก็บตัวที่ผิด
		foreach (char c in s)                                                   // วนทุกตัวในสตริง
		{
			if (!(char.IsLetterOrDigit(c) || (c >= 0x0E00 && c <= 0x0E7F)))     // อนุญาต a-zA-Z0-9 หรือช่วง Unicode ภาษาไทย
				if (!list.Contains(c)) list.Add(c);                             // ถ้าไม่อนุญาตและยังไม่ถูกเพิ่ม → เพิ่มเข้าไป
		}
		return string.Join(", ", list);                                         // รวมเป็นสตริงด้วย ", "
	}

	// รหัสผ่านต้อง ≥ 6 ตัว และมีเฉพาะ a-z A-Z 0-9 หรืออักขระใน AllowedSpecials
	private string ValidatePassword(string pwd)                                 // คืน null ถ้าผ่าน, ไม่งั้นคืนข้อความ error
	{
		if (string.IsNullOrEmpty(pwd) || pwd.Length < 6)                        // เช็คขั้นต่ำ 6 ตัวอักษร
			return "รหัสผ่านต้องมีอย่างน้อย 6 ตัว";                           // แจ้งผู้ใช้

		var invalids = new List<char>();                                        // ลิสต์เก็บตัวที่ไม่อนุญาต
		foreach (char c in pwd)                                                 // วนทุกตัวในรหัสผ่าน
		{
			if (char.IsLetterOrDigit(c)) continue;                              // ตัวอักษร/ตัวเลข → ผ่าน
			if (AllowedSpecials.IndexOf(c) >= 0) continue;                      // อยู่ในชุดที่อนุญาต → ผ่าน
			if (!invalids.Contains(c)) invalids.Add(c);                         // ไม่อนุญาต → เก็บ (กันซ้ำ)
		}
		if (invalids.Count > 0)                                                 // ถ้ามีตัวผิด
			return $"รหัสผ่านมีอักขระที่ไม่อนุญาต: {string.Join(", ", invalids)}"; // สร้างข้อความรวมรายการ

		return null;                                                            // ผ่านทุกเงื่อนไข
	}

	private void ShowError(string msg)                                          // แสดงข้อความผิดพลาด
	{
		_errorLabel.Text = msg;                                                 // ตั้งข้อความ
		_errorLabel.Visible = true;                                             // เปิดให้มองเห็น
		_errorLabel.Modulate = new Color(1, 0, 0);                              // ทำเป็นสีแดงให้เด่น
	}

	private void HideError()                                                    // ซ่อนข้อความผิดพลาด
	{
		_errorLabel.Text = "";                                                  // ล้างข้อความ
		_errorLabel.Visible = false;                                            // ปิดการมองเห็น
	}
}
