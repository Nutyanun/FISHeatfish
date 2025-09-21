using Godot;
using System;
using System.Collections.Generic;

public partial class LoginGame : Control
{
	private LineEdit _nameInput;
	private Label _errorLabel;
	private Button _submitButton;

	public override void _Ready()
	{
		_nameInput = GetNode<LineEdit>("CenterContainer/VBoxContainer/NameInput");
		_errorLabel = GetNode<Label>("CenterContainer/VBoxContainer/ErrorLabel");
		_submitButton = GetNode<Button>("CenterContainer/VBoxContainer/SubmitButton");

		_errorLabel.Visible = false;

		_submitButton.Pressed += OnSubmit;
	}

	private void OnSubmit()
	{
		string name = _nameInput.Text.Trim();

		// 1) อย่างน้อย 1 ตัวอักษร
		if (name.Length == 0)
		{
			ShowError("กรุณากรอกชื่ออย่างน้อย 1 ตัวอักษร");
			return;
		}

		// 2) ตรวจหาอักขระพิเศษ
		string badChars = GetInvalidChars(name);
		if (!string.IsNullOrEmpty(badChars))
		{
			//$ ใช้สำหรับ String Interpolation (การฝังค่าตัวแปรลงไปใน string)
			ShowError($"ไม่สามารถใช้ชื่อนี้ได้ เพราะมีตัวอักษรพิเศษ: {badChars}");
			return;
		}

		// 3) ตรวจชื่อซ้ำ
		var registry = GetNode<UserRegistry>("/root/UserRegistry");
		if (registry != null && registry.Exists(name))
		{
			ShowError("ไม่สามารถใช้ชื่อนี้ได้ เพราะถูกใช้ไปแล้ว");
			return;
		}

		//  ผ่านทั้งหมด
		HideError();
		registry?.Add(name);
		//$ ใช้สำหรับ String Interpolation (การฝังค่าตัวแปรลงไปใน string)
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
		if (!(char.IsLetterOrDigit(c) || (c >= 0x0E00 && c <= 0x0E7F)))
		{
			if (!list.Contains(c))
				list.Add(c);
		}
	}
	return string.Join(", ", list);
	}

	private void ShowError(string msg)
	{
		_errorLabel.Text = msg;
		_errorLabel.Visible = true;
		_errorLabel.Modulate = new Color(1, 0, 0); // สีแดง
	}

	private void HideError()
	{
		_errorLabel.Text = "";
		_errorLabel.Visible = false;
	}
}
