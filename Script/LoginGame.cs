using Godot;
using System;
using System.Collections.Generic;

public partial class LoginGame : Control
{
	private LineEdit _nameInput;// กล่องข้อความที่ผู้ใช้กรอกชื่อ
	private Label _errorLabel;// ข้อความแสดง error
	private Button _submitButton;//ปุ่มสำหรับกดยืนยัน

	public override void _Ready()
	{
		_nameInput = GetNode<LineEdit>("CenterContainer/VBoxContainer/NameInput");
		_errorLabel = GetNode<Label>("CenterContainer/VBoxContainer/ErrorLabel");
		_submitButton = GetNode<Button>("CenterContainer/VBoxContainer/SubmitButton");

		_errorLabel.Visible = false;//ซ่อน _errorLabel ตอนเริ่มต้น (Visible = false)
		_submitButton.Pressed += OnSubmit;//เชื่อมปุ่ม _submitButton.Pressed ให้เรียก OnSubmit()
	}

	private void OnSubmit()
	{
		string name = _nameInput.Text.Trim();

		// 1) อย่างน้อย 1 ตัวอักษร ถ้าชื่อว่าง แสดง error และหยุดทำงาน (return)
		if (name.Length == 0)
		{
			ShowError("กรุณากรอกชื่ออย่างน้อย 1 ตัวอักษร");
			return;
		}

		// 2) ตรวจหาอักขระพิเศษ 
		//ใช้ฟังก์ชัน GetInvalidChars() ตรวจหาตัวอักษรที่ไม่อนุญาต ถ้ามีแสดง error พร้อมระบุว่ามีตัวไหนบ้าง
		string badChars = GetInvalidChars(name);
		if (!string.IsNullOrEmpty(badChars))
		{
			//$ ใช้สำหรับ String Interpolation (การฝังค่าตัวแปรลงไปใน string)
			ShowError($"ไม่สามารถใช้ชื่อนี้ได้ เพราะมีตัวอักษรพิเศษ: {badChars}");
			return;
		}

		// 3) ตรวจชื่อซ้ำในไฟล์ JSON
		var saver = GetNode<PlayerLogin>("/root/PlayerLogin"); // Autoload
		if (!saver.SavePlayer(name))
		{
			ShowError("ชื่อนี้ถูกใช้ไปแล้ว กรุณาเลือกชื่อใหม่");
			return;
		}

		// ผ่านทั้งหมด
		HideError();
		GD.Print($"Login success with username: {name}");


		// ไปหน้าเกมถัดไป
		GetTree().ChangeSceneToFile("res://SceneStartandHigh/StartGame.tscn");
	}

	private string GetInvalidChars(string s)
	{
		 var list = new List<char>();
	foreach (char c in s)
	{
		// อนุญาต: A–Z, a–z, 0–9, อักษรไทย
		//(c >= 0x0E00 && c <= 0x0E7F) คือ รหัส Unicode ของอักษรไทย
		if (!(char.IsLetterOrDigit(c) || (c >= 0x0E00 && c <= 0x0E7F)))
		{
			if (!list.Contains(c))
				list.Add(c);
		}
	}
	return string.Join(", ", list);
	}
	
	//แสดงข้อความ error เป็นสีแดง
	private void ShowError(string msg)
	{
		_errorLabel.Text = msg;
		_errorLabel.Visible = true;
		_errorLabel.Modulate = new Color(1, 0, 0); // สีแดง
	}
	
	//ซ่อนข้อความ error
	private void HideError()
	{
		_errorLabel.Text = "";
		_errorLabel.Visible = false;
	}
}
